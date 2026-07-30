using UnityEngine;
using System.Collections.Generic;

public class SeedController : MonoBehaviour
{
    [System.Serializable]
    public struct SeedPlantMapping
    {
        public int seedItemID;
        public GameObject realPlantPrefab;
        public GameObject previewPlantPrefab;
        [Tooltip("Опциональный префаб посаженной лунки для этой культуры")]
        public GameObject plantedHolePrefab;
    }

    [Header("Защита от багов (ВАЖНО!)")]
    [Tooltip("Впиши сюда ID всех твоих семян. Скрипт сработает ТОЛЬКО если в руках один из этих предметов!")]
    public List<int> validSeedIDs = new List<int>();

    [Header("Маппинг семян (для поддержки разных культур)")]
    public List<SeedPlantMapping> seedMappings = new List<SeedPlantMapping>();

    [Header("Префабы Растения (по умолчанию / Fallback)")]
    public GameObject realPlantPrefab;
    public GameObject previewPlantPrefab;

    [Header("Настройки лунок (по умолчанию / Fallback)")]
    [Tooltip("Префаб пустой лунки (для восстановления при сборе)")]
    public GameObject emptyHolePrefab;
    [Tooltip("Префаб посаженной лунки")]
    public GameObject plantedHolePrefab;

    [Header("Настройки посадки")]
    public float plantDistance = 4f;
    public float heightOffset = 0f;

    [Header("Цвета голограммы")]
    public Color validColor = new Color(0.2f, 1f, 0.2f, 0.5f);
    public Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);

    [Header("Звук")]
    public AudioSource plantSound;

    private GameObject currentPreview;
    private Renderer[] previewRenderers;

    private Camera mainCam;
    private HashSet<int> validSeedIDsSet;
    private Dictionary<int, GameObject> instantiatedPreviews = new Dictionary<int, GameObject>();
    private Dictionary<int, Renderer[]> previewRenderersMap = new Dictionary<int, Renderer[]>();
    private int lastHeldSeedID = -1;

    void Start()
    {
        mainCam = Camera.main;
        validSeedIDsSet = new HashSet<int>(validSeedIDs);
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            HidePreview();
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            HidePreview();
            return;
        }

        // --- БРОНЕБОЙНАЯ ЗАЩИТА: ПРОВЕРЯЕМ, ЧТО В РУКАХ ИМЕННО СЕМЯ ПО ID ---
        bool isHoldingSeed = false;
        int activeSeedID = -1;

        if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
        {
            int activeIndex = InventoryManager.Instance.selectedSlotIndex;
            if (activeIndex >= 0 && activeIndex < InventoryManager.Instance.hotbarSlots.Length)
            {
                InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[activeIndex];

                if (!activeSlot.IsEmpty())
                {
                    int itemID = activeSlot.currentItemID;
                    if (validSeedIDsSet != null && validSeedIDsSet.Contains(itemID))
                    {
                        isHoldingSeed = true;
                        activeSeedID = itemID;
                    }
                }
            }
        }

        if (!isHoldingSeed)
        {
            HidePreview();
            lastHeldSeedID = -1;
            return;
        }

        if (activeSeedID != lastHeldSeedID)
        {
            SwitchActiveSeed(activeSeedID);
        }

        bool isAiming = Input.GetMouseButton(1);

        if (isAiming)
        {
            HandlePreviewAndPlanting();
        }
        else
        {
            HidePreview();
        }
    }

    private void HandlePreviewAndPlanting()
    {
        if (mainCam == null) return;

        Ray ray = new Ray(mainCam.transform.position, mainCam.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, plantDistance))
        {
            bool isHole = hit.collider.CompareTag("Hole");
            bool isPlantedHole = hit.collider.CompareTag("PlantedHole");

            if (isHole || isPlantedHole)
            {
                if (currentPreview != null)
                {
                    currentPreview.SetActive(true);
                    currentPreview.transform.position = hit.collider.transform.position + (Vector3.up * heightOffset);

                    Vector3 playerForward = mainCam.transform.forward;
                    playerForward.y = 0f;
                    Quaternion baseRot = Quaternion.identity;
                    if (playerForward.sqrMagnitude > 0.001f)
                        baseRot = Quaternion.LookRotation(playerForward);

                    currentPreview.transform.rotation = baseRot * previewPlantPrefab.transform.rotation;
                }

                bool canPlant = isHole;

                if (previewRenderers != null)
                {
                    Color targetColor = canPlant ? validColor : invalidColor;
                    foreach (Renderer r in previewRenderers)
                    {
                        if (r.material.HasProperty("_BaseColor")) r.material.SetColor("_BaseColor", targetColor);
                        else if (r.material.HasProperty("_Color")) r.material.color = targetColor;
                    }
                }

                if (Input.GetMouseButtonDown(0))
                {
                    if (canPlant) PlantSeed(hit.collider.gameObject);
                    else Debug.Log("Эта грядка уже занята!");
                }
            }
            else
            {
                HidePreview();
            }
        }
        else
        {
            HidePreview();
        }
    }

    private void SwitchActiveSeed(int seedID)
    {
        HidePreview();
        lastHeldSeedID = seedID;

        GameObject previewPrefab = GetPreviewPrefabForSeed(seedID);
        if (previewPrefab == null)
        {
            currentPreview = null;
            previewRenderers = null;
            return;
        }

        if (!instantiatedPreviews.TryGetValue(seedID, out currentPreview))
        {
            currentPreview = Instantiate(previewPrefab);
            currentPreview.SetActive(false);

            Collider[] colliders = currentPreview.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders) col.enabled = false;

            Renderer[] renderers = currentPreview.GetComponentsInChildren<Renderer>();
            instantiatedPreviews[seedID] = currentPreview;
            previewRenderersMap[seedID] = renderers;
        }

        previewRenderers = previewRenderersMap[seedID];
    }

    private GameObject GetPreviewPrefabForSeed(int seedID)
    {
        if (seedMappings != null)
        {
            foreach (var mapping in seedMappings)
            {
                if (mapping.seedItemID == seedID && mapping.previewPlantPrefab != null)
                {
                    return mapping.previewPlantPrefab;
                }
            }
        }
        return previewPlantPrefab;
    }

    private GameObject GetRealPrefabForSeed(int seedID)
    {
        if (seedMappings != null)
        {
            foreach (var mapping in seedMappings)
            {
                if (mapping.seedItemID == seedID && mapping.realPlantPrefab != null)
                {
                    return mapping.realPlantPrefab;
                }
            }
        }
        return realPlantPrefab;
    }

    private GameObject GetPlantedHolePrefabForSeed(int seedID)
    {
        if (seedMappings != null)
        {
            foreach (var mapping in seedMappings)
            {
                if (mapping.seedItemID == seedID && mapping.plantedHolePrefab != null)
                {
                    return mapping.plantedHolePrefab;
                }
            }
        }
        return plantedHolePrefab;
    }

    private void PlantSeed(GameObject holeObject)
    {
        GameObject realPrefab = GetRealPrefabForSeed(lastHeldSeedID);
        GameObject plantedHolePref = GetPlantedHolePrefabForSeed(lastHeldSeedID);

        if (realPrefab != null && currentPreview != null && plantedHolePref != null)
        {
            // Спавним засаженную лунку на месте пустой
            GameObject newHole = Instantiate(plantedHolePref, holeObject.transform.position, holeObject.transform.rotation);
            newHole.tag = "PlantedHole";

            // Спавним растение и делаем его дочерним к новой лунке
            GameObject spawnedPlant = Instantiate(realPrefab, currentPreview.transform.position, currentPreview.transform.rotation);
            spawnedPlant.transform.SetParent(newHole.transform, true);

            // Передаем префаб пустой лунки для восстановления при сборе
            PlantedPlant plantScript = spawnedPlant.GetComponent<PlantedPlant>();
            if (plantScript != null)
            {
                plantScript.emptyHolePrefab = emptyHolePrefab;
            }

            // Удаляем старую пустую лунку
            Destroy(holeObject);
            Debug.Log("Растение посажено!");
        }

        if (plantSound != null) plantSound.Play();

        HidePreview();

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ConsumeItemInActiveSlot();
    }

    private void HidePreview()
    {
        if (currentPreview != null && currentPreview.activeSelf) currentPreview.SetActive(false);
    }

    void OnDisable() { HidePreview(); }
    void OnDestroy()
    {
        foreach (var preview in instantiatedPreviews.Values)
        {
            if (preview != null) Destroy(preview);
        }
        instantiatedPreviews.Clear();
        previewRenderersMap.Clear();
    }
}