using UnityEngine;
using System.Collections.Generic;

public class ItemPlacementManager : MonoBehaviour
{
    [Header("Настройки луча")]
    public Camera playerCamera;
    [Tooltip("Как далеко можно ставить предметы")]
    public float placeDistance = 6f; 
    [Tooltip("Слои, на которые можно ставить (ОБЯЗАТЕЛЬНО ВЫБЕРИ DEFAULT или GROUND!)")]
    public LayerMask surfaceLayers;
    [Tooltip("Слегка приподнять предмет над землей при установке")]
    public float heightOffset = 0.05f;

    [Header("Кнопки (Из настроек)")]
    public string placeKeyPref = "Key_Place";
    public KeyCode defaultPlaceKey = KeyCode.V;
    public string shootKeyPref = "Key_Shoot";
    public KeyCode defaultShootKey = KeyCode.Mouse0;
    public string aimKeyPref = "Key_Aim";
    public KeyCode defaultAimKey = KeyCode.Mouse1;

    [Header("База предметов")]
    [Tooltip("Перетащи сюда ПРЕФАБЫ всех предметов из проекта, которые можно ставить (Лампы, свечи и т.д.)")]
    public List<GameObject> placeablePrefabs;

    [Header("Материалы Голограммы")]
    public Material validPreviewMaterial;
    public Material invalidPreviewMaterial;
    [Tooltip("Материал для доступных точек (например, синий)")]
    public Material distantSnapPreviewMaterial;

    private GameObject currentPreview;
    private GameObject currentPrefabRef;
    private Renderer[] previewRenderers;

    private int lastItemID = -1;

    private bool lastPreviewState = false;
    private bool forceUpdateColor = true;

    // Кэшированные клавиши
    private KeyCode cachedPlaceKey;
    private KeyCode cachedShootKey;
    private KeyCode cachedAimKey;

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        RefreshKeyBindings();

        // Авто-генерация зеленых превьюшек для PlacementPoint
        PlacementPoint[] points = FindObjectsByType<PlacementPoint>(FindObjectsSortMode.None);
        foreach (var p in points)
        {
            Material previewMat = distantSnapPreviewMaterial != null ? distantSnapPreviewMaterial : validPreviewMaterial;
            p.InitializeHighlights(previewMat);
        }
    }

    /// <summary>Вызвать из SettingsManager после изменения привязок клавиш.</summary>
    public void RefreshKeyBindings()
    {
        cachedPlaceKey = (KeyCode)PlayerPrefs.GetInt(placeKeyPref, (int)defaultPlaceKey);
        cachedShootKey = (KeyCode)PlayerPrefs.GetInt(shootKeyPref, (int)defaultShootKey);
        cachedAimKey = (KeyCode)PlayerPrefs.GetInt(aimKeyPref, (int)defaultAimKey);
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            ResetPreview();
            return;
        }

        int itemInHandID = GetCurrentItemID();
        if (itemInHandID == -1)
        {
            ResetPreview();
            return;
        }

        bool requiresSnap = false;
        if (itemInHandID != lastItemID)
        {
            GameObject prefabRef = FindPrefabByID(itemInHandID);
            if (prefabRef != null)
            {
                PickUpItem p = prefabRef.GetComponent<PickUpItem>();
                if (p == null) p = prefabRef.GetComponentInChildren<PickUpItem>();
                if (p != null) requiresSnap = p.requiresSnapPoint;
            }
        }
        else
        {
            if (currentPrefabRef != null)
            {
                PickUpItem p = currentPrefabRef.GetComponent<PickUpItem>();
                if (p == null) p = currentPrefabRef.GetComponentInChildren<PickUpItem>();
                if (p != null) requiresSnap = p.requiresSnapPoint;
            }
        }

        bool isHoldingKey = requiresSnap ? Input.GetKey(cachedAimKey) : Input.GetKey(cachedPlaceKey);

        if (isHoldingKey)
        {
            HandlePlacementMode(cachedShootKey, requiresSnap, itemInHandID);
        }
        else
        {
            ResetPreview();
        }
    }

    private void HandlePlacementMode(KeyCode shootKey, bool requiresSnap, int itemInHandID)
    {

        if (itemInHandID == -1)
        {
            ResetPreview();
            return;
        }

        if (itemInHandID != lastItemID)
        {
            lastItemID = itemInHandID;
            currentPrefabRef = FindPrefabByID(itemInHandID);
            RebuildPreview();
        }

        if (currentPrefabRef == null || currentPreview == null)
        {
            ResetPreview();
            return;
        }

        bool foundValidHit = false;
        RaycastHit finalHit = new RaycastHit();
        PlacementPoint targetSnapPoint = null;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (requiresSnap)
        {
            HighlightValidPlacementPoints(lastItemID);

            // Обязательно разрешаем пересекать триггеры, так как PlacementPoint обычно IsTrigger
            RaycastHit[] hits = Physics.RaycastAll(ray, placeDistance, Physics.AllLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                PlacementPoint pp = h.collider.GetComponent<PlacementPoint>();
                if (pp == null) pp = h.collider.GetComponentInParent<PlacementPoint>();

                if (pp != null && pp.AcceptsItem(lastItemID) && !pp.IsOccupied())
                {
                    targetSnapPoint = pp;
                    finalHit = h;
                    foundValidHit = true;
                    // Отключаем синюю голограмму точки, так как поверх нее будет рисоваться зеленая голограмма предмета из рук
                    targetSnapPoint.SetHighlight(false);
                    break;
                }
            }
        }
        else
        {
            RaycastHit[] hits = Physics.RaycastAll(ray, placeDistance, surfaceLayers);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider.CompareTag("Player") || h.transform.root.CompareTag("Player")) continue;
                if (h.collider.isTrigger) continue;

                finalHit = h;
                foundValidHit = true;
                break;
            }
        }

        if (foundValidHit)
        {
            currentPreview.SetActive(true);
            
            if (requiresSnap && targetSnapPoint != null)
            {
                GameObject preset = targetSnapPoint.GetPresetObject(lastItemID);
                if (preset != null)
                {
                    currentPreview.transform.position = preset.transform.position;
                    currentPreview.transform.rotation = preset.transform.rotation;
                    currentPreview.transform.localScale = preset.transform.localScale;
                }
                else
                {
                    Vector3 offset = Vector3.zero;
                    if (currentPrefabRef != null)
                    {
                        PickUpItem pickup = currentPrefabRef.GetComponent<PickUpItem>();
                        if (pickup == null) pickup = currentPrefabRef.GetComponentInChildren<PickUpItem>();
                        if (pickup != null)
                        {
                            offset = targetSnapPoint.transform.rotation * pickup.placementOffset;
                        }
                    }
                    currentPreview.transform.position = targetSnapPoint.transform.position + offset;
                    currentPreview.transform.rotation = targetSnapPoint.transform.rotation;
                }
            }
            else
            {
                Vector3 playerForward = playerCamera.transform.forward;
                playerForward.y = 0f;
                Quaternion yRotation = playerForward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(playerForward) : Quaternion.identity;
                currentPreview.transform.rotation = yRotation * currentPrefabRef.transform.rotation;

                currentPreview.transform.position = finalHit.point;

                float bottomOffset = 0f;
                Transform bottomPoint = GetChildByName(currentPreview.transform, "BottomPoint");

                if (bottomPoint != null)
                {
                    bottomOffset = currentPreview.transform.position.y - bottomPoint.position.y;
                }
                else
                {
                    Collider[] previewCols = currentPreview.GetComponentsInChildren<Collider>();
                    
                    if (previewCols.Length > 0)
                    {
                        float lowestPoint = float.MaxValue;
                        foreach (Collider col in previewCols)
                        {
                            if (col.isTrigger) continue; 
                            
                            if (col.bounds.min.y < lowestPoint)
                            {
                                lowestPoint = col.bounds.min.y;
                            }
                        }
                        
                        if (lowestPoint != float.MaxValue)
                        {
                            bottomOffset = currentPreview.transform.position.y - lowestPoint;
                        }
                    }
                }
                
                currentPreview.transform.position = finalHit.point + (Vector3.up * (bottomOffset + heightOffset));
            }

            bool canPlace = true; 
            
            if (canPlace != lastPreviewState || forceUpdateColor)
            {
                SetPreviewColor(canPlace);
                lastPreviewState = canPlace;
                forceUpdateColor = false;
            }

            if (Input.GetKeyDown(shootKey) && canPlace)
            {
                PlaceItem(currentPreview.transform.position, currentPreview.transform.rotation, targetSnapPoint);
            }
        }
        else
        {
            currentPreview.SetActive(false); 
        }
    }

    private void PlaceItem(Vector3 position, Quaternion rotation, PlacementPoint snapPoint = null)
    {
        // 1. Берем данные из рук перед тем, как предмет удалится из инвентаря
        int index = InventoryManager.Instance.selectedSlotIndex;
        InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[index];
        InventoryItemData dataToRestore = null;

        if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null)
        {
            dataToRestore = activeSlot.itemData;
        }

        GameObject spawnedItem = null;

        GameObject presetToEnable = null;
        if (snapPoint != null)
        {
            presetToEnable = snapPoint.GetPresetObject(lastItemID);
        }

        // Проверяем, является ли пресет реальным подбираемым предметом или просто пустышкой-заглушкой
        PickUpItem presetPickup = null;
        if (presetToEnable != null)
        {
            presetPickup = presetToEnable.GetComponent<PickUpItem>();
            if (presetPickup == null) presetPickup = presetToEnable.GetComponentInChildren<PickUpItem>();
        }

        // Если это точка привязки с готовым РЕАЛЬНЫМ пресетом на сцене — просто включаем его
        if (presetToEnable != null && presetPickup != null)
        {
            spawnedItem = presetToEnable;
            spawnedItem.SetActive(true);
            
            presetPickup.isFloating = false;
            presetPickup.isPlacedOnSnapPoint = true;
            presetPickup.isPlaced = true;
            presetPickup.isPickedUp = false;
            presetPickup.destroyOnPickUp = false; // Пресеты не удаляются, а скрываются
            if (dataToRestore != null) presetPickup.RestoreData(dataToRestore);
        }
        else
        {
            // 2. Иначе спавним реальный префаб предмета из инвентаря
            Vector3 spawnPos = position;
            Quaternion spawnRot = rotation;
            Vector3 spawnScale = currentPrefabRef.transform.localScale;

            // Если есть пустышка-пресет, копируем ее координаты и масштаб, и скрываем ее
            if (presetToEnable != null)
            {
                spawnPos = presetToEnable.transform.position;
                spawnRot = presetToEnable.transform.rotation;
                spawnScale = presetToEnable.transform.localScale;
                presetToEnable.SetActive(false);
            }

            spawnedItem = Instantiate(currentPrefabRef, spawnPos, spawnRot);
            spawnedItem.transform.localScale = spawnScale;
            
            // 3. Восстанавливаем данные и настраиваем физику
            PickUpItem pickupComponent = spawnedItem.GetComponent<PickUpItem>();
            if (pickupComponent == null) pickupComponent = spawnedItem.GetComponentInChildren<PickUpItem>();
            
            if (pickupComponent != null)
            {
                pickupComponent.isFloating = false; 
                pickupComponent.isPlacedOnSnapPoint = (snapPoint != null);
                pickupComponent.isPlaced = true;
                pickupComponent.isPickedUp = false;
                if (dataToRestore != null) pickupComponent.RestoreData(dataToRestore);
            }

            Rigidbody[] rbs = spawnedItem.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rbs) rb.isKinematic = true;

            MonoBehaviour[] scripts = spawnedItem.GetComponentsInChildren<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script.GetType().Name == "LanternController") script.enabled = false;
                if (script.GetType().Name == "LanternCenterOfMass") script.enabled = false;
            }
            
            WorldToggleDevice toggleDevice = spawnedItem.GetComponent<WorldToggleDevice>();
            if (toggleDevice != null)
            {
                toggleDevice.SetState(true);
            }
            else
            {
                Light[] lights = spawnedItem.GetComponentsInChildren<Light>(true);
                foreach (Light l in lights) l.enabled = true;
                
                ParticleSystem[] particles = spawnedItem.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem p in particles) p.Play();
            }
        }

        Debug.Log($"Предмет установлен! ID: {lastItemID}");

        RemoveCurrentItem();
        ResetPreview();
    }

    private void RebuildPreview()
    {
        if (currentPreview != null) Destroy(currentPreview);
        if (currentPrefabRef == null) return;

        currentPreview = Instantiate(currentPrefabRef);
        
        // --- БРОНЕБОЙНОЕ ИСПРАВЛЕНИЕ: ПРАВИЛЬНЫЙ ПОРЯДОК УДАЛЕНИЯ ---
        // 1. Сначала удаляем ConsumableItem, так как от него зависит PickUpItem
        ConsumableItem[] consumables = currentPreview.GetComponentsInChildren<ConsumableItem>(true);
        foreach (var c in consumables)
        {
            if (c != null) DestroyImmediate(c);
        }

        // 2. Затем удаляем все остальные скрипты, кроме PickUpItem
        MonoBehaviour[] scripts = currentPreview.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour m in scripts) 
        {
            if (m != null && !(m is PickUpItem)) DestroyImmediate(m);
        }

        // 3. Теперь удаляем сам PickUpItem
        PickUpItem[] pickups = currentPreview.GetComponentsInChildren<PickUpItem>(true);
        foreach (var p in pickups)
        {
            if (p != null) DestroyImmediate(p);
        }

        // 4. Затем удаляем физику (Rigidbodies)
        Rigidbody[] rbs = currentPreview.GetComponentsInChildren<Rigidbody>(true);
        foreach (Rigidbody rb in rbs) 
        {
            if (rb != null) DestroyImmediate(rb);
        }

        // 5. И только в самом конце безопасно удаляем коллайдеры
        Collider[] cols = currentPreview.GetComponentsInChildren<Collider>(true);
        foreach (Collider c in cols) 
        {
            if (c != null) DestroyImmediate(c);
        }
        // -------------------------------------------------------------

        previewRenderers = currentPreview.GetComponentsInChildren<Renderer>();
        currentPreview.SetActive(false);
        
        forceUpdateColor = true; 
    }

    private void SetPreviewColor(bool isValid)
    {
        Material mat = isValid ? validPreviewMaterial : invalidPreviewMaterial;
        if (mat == null || previewRenderers == null) return;

        foreach (Renderer r in previewRenderers)
        {
            Material[] newMats = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                newMats[i] = mat;
            }
            r.sharedMaterials = newMats;
        }
    }

    private void ResetPreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
        lastItemID = -1;
        HideAllPlacementPoints();
    }

    private void HighlightValidPlacementPoints(int itemID)
    {
        PlacementPoint[] points = FindObjectsByType<PlacementPoint>(FindObjectsSortMode.None);
        foreach (var p in points)
        {
            p.SetHighlight(p.AcceptsItem(itemID) && !p.IsOccupied(), itemID);
        }
    }

    private void HideAllPlacementPoints()
    {
        PlacementPoint[] points = FindObjectsByType<PlacementPoint>(FindObjectsSortMode.None);
        foreach (var p in points)
        {
            p.SetHighlight(false);
        }
    }

    private int GetCurrentItemID()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
        {
            int index = InventoryManager.Instance.selectedSlotIndex;
            if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
            {
                if (!InventoryManager.Instance.hotbarSlots[index].IsEmpty())
                {
                    return InventoryManager.Instance.hotbarSlots[index].currentItemID;
                }
            }
        }
        return -1;
    }

    private GameObject FindPrefabByID(int id)
    {
        foreach (GameObject prefab in placeablePrefabs)
        {
            if (prefab == null) continue;

            // Используем GetComponentInChildren для страховки
            PickUpItem p = prefab.GetComponent<PickUpItem>();
            if (p == null) p = prefab.GetComponentInChildren<PickUpItem>();

            if (p != null && p.itemID == id) 
            {
                return prefab;
            }
        }
        return null;
    }

    private void RemoveCurrentItem()
    {
        if (InventoryManager.Instance != null)
        {
            // Теперь менеджер сам знает, отнять 1 штуку из стака или удалить слот
            InventoryManager.Instance.ConsumeItemInActiveSlot();
        }
    }

    private Transform GetChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == name) return child;
        }
        return null;
    }
}