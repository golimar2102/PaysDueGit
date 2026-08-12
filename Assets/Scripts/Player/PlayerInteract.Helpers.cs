using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public partial class PlayerInteract : MonoBehaviour
{
    private void ShowText(string message)
    {
        if (interactText != null)
        {
            interactText.text = message;
            if (!interactText.gameObject.activeSelf) interactText.gameObject.SetActive(true);
        }
    }

    private void RemoveHighlight()
    {
        if (currentLookItem != null)
        {
            currentLookItem.SetHighlight(false);
            currentLookItem = null;
        }
        if (currentLookMeatGrinder != null)
        {
            currentLookMeatGrinder.SetHighlight(false);
            currentLookMeatGrinder = null;
        }
        if (currentLookToggleDevice != null)
        {
            currentLookToggleDevice.SetHighlight(false);
            currentLookToggleDevice = null;
        }
        if (currentLookGeneratorDoor != null)
        {
            currentLookGeneratorDoor.SetHighlight(false);
            currentLookGeneratorDoor = null;
        }
        if (currentLookLiquidSource != null)
        {
            currentLookLiquidSource.SetHighlight(false);
            currentLookLiquidSource = null;
        }
        if (currentLookItemSource != null)
        {
            currentLookItemSource.SetHighlight(false);
            currentLookItemSource = null;
        }
        if (currentLookStorage != null)
        {
            currentLookStorage.SetHighlight(false);
            currentLookStorage = null;
        }
        if (currentLookTVChair != null)
        {
            currentLookTVChair.SetHighlight(false);
            currentLookTVChair = null;
        }
        if (currentLookDoughRolling != null)
        {
            currentLookDoughRolling.SetHighlight(false);
            currentLookDoughRolling = null;
        }
        if (currentLookWorkbench != null)
        {
            currentLookWorkbench.SetHighlight(false);
            currentLookWorkbench = null;
        }
        if (currentLookVendingMachine != null)
        {
            currentLookVendingMachine.SetHighlight(false);
            currentLookVendingMachine = null;
        }
        if (currentLookLocationTransition != null)
        {
            currentLookLocationTransition.SetHighlight(false);
            currentLookLocationTransition = null;
        }
        if (currentLookPeephole != null)
        {
            currentLookPeephole.SetHighlight(false);
            currentLookPeephole = null;
        }
        if (currentLookWaterCoolerPipe != null)
        {
            currentLookWaterCoolerPipe.StopPouring();
            currentLookWaterCoolerPipe.SetHighlight(false);
            currentLookWaterCoolerPipe = null;
        }
        if (currentLookWaterCoolerTap != null)
        {
            currentLookWaterCoolerTap.StopFilling();
            currentLookWaterCoolerTap.StopDrinking();
            currentLookWaterCoolerTap.SetHighlight(false);
            currentLookWaterCoolerTap = null;
        }
        if (currentLookSinkTap != null)
        {
            currentLookSinkTap.SetHighlight(false);
            currentLookSinkTap = null;
        }
        if (currentLookCorpse != null)
        {
            currentLookCorpse.SetHighlight(false);
            currentLookCorpse = null;
        }
        if (currentLookButcheringTable != null)
        {
            currentLookButcheringTable.SetHighlight(false);
            currentLookButcheringTable = null;
        }
        if (currentLookIndustrialMeatGrinder != null)
        {
            currentLookIndustrialMeatGrinder.SetHighlight(false);
            currentLookIndustrialMeatGrinder = null;
        }
        if (currentLookTrashSortingButton != null)
        {
            currentLookTrashSortingButton.SetHighlight(false);
            currentLookTrashSortingButton = null;
        }
    }

    private void FillContainer(InventorySlot slot, LiquidSource source)
    {
        if (slot == null || slot.itemData == null || source == null) return;

        InventoryItemData data = slot.itemData;
        int maxCap = data.maxAmount;
        int current = data.currentAmount;
        int needed = maxCap - current;

        if (needed <= 0) return;

        int added = needed;
        if (!source.isInfinite)
        {
            added = Mathf.Min(needed, source.remainingAmount);
            source.remainingAmount -= added;
        }

        if (added <= 0) return;

        data.currentAmount += added;
        data.currentLiquidType = source.liquidType;

        if (string.IsNullOrEmpty(data.baseItemName))
        {
            data.baseItemName = data.itemName;
        }

        string liquidSuffix = GetLocalizedLiquidName(source.liquidType);
        data.itemName = $"{data.baseItemName} ({liquidSuffix})";

        if (InventoryManager.Instance != null)
        {
            GameObject prefab = InventoryManager.Instance.GetPrefabByID(data.itemID);
            if (prefab != null)
            {
                ConsumableItem cons = prefab.GetComponent<ConsumableItem>();
                if (cons != null)
                {
                    Sprite[] customIcons = cons.GetFillIconsForLiquid(source.liquidType);
                    data.fillIcons = (customIcons != null && customIcons.Length > 0) ? customIcons : cons.fillIcons;
                }
            }
        }

        if (data.fillIcons != null && data.fillIcons.Length > 0)
        {
            float fillPercentage = Mathf.Clamp01((float)data.currentAmount / data.maxAmount);
            int iconIndex = Mathf.RoundToInt(fillPercentage * (data.fillIcons.Length - 1));
            data.itemIcon = data.fillIcons[iconIndex];
        }

        slot.UpdateSlotUI();

        if (source.pourSound != null)
        {
            source.pourSound.Play();
        }
    }

    private void HandleGeneratorZoomedInteraction()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = playerCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (PerformRaycast(ray, 10f, out hit))
            {
                GeneratorSwitch sw = hit.collider.GetComponentInParent<GeneratorSwitch>();
                if (sw != null)
                {
                    sw.Toggle();
                    return;
                }

                // Проверяем рычаг рубильника
                GeneratorController generatorUnderCursor = hit.collider.GetComponentInParent<GeneratorController>();
                if (generatorUnderCursor != null && generatorUnderCursor.leverTransform != null)
                {
                    if (hit.collider.transform == generatorUnderCursor.leverTransform || 
                        hit.collider.transform.IsChildOf(generatorUnderCursor.leverTransform))
                    {
                        generatorUnderCursor.InteractLever();
                        return;
                    }
                }

                // Проверяем держатель предохранителя
                GeneratorFuseHolder holder = hit.collider.GetComponentInParent<GeneratorFuseHolder>();
                if (holder != null && !holder.isEmpty)
                {
                    StartCoroutine(DragFuseFrom3D(holder));
                    return;
                }

                // Проверяем трубу заправки
                GeneratorFuelPipe pipe = hit.collider.GetComponentInParent<GeneratorFuelPipe>();
                if (pipe == null && generatorUnderCursor != null && generatorUnderCursor.jerrycan3DModel != null)
                {
                    if (hit.collider.gameObject == generatorUnderCursor.jerrycan3DModel || 
                        hit.collider.transform.IsChildOf(generatorUnderCursor.jerrycan3DModel.transform))
                    {
                        pipe = generatorUnderCursor.fuelPipe;
                    }
                }

                if (pipe != null && !pipe.isEmpty)
                {
                    StartCoroutine(DragCanisterFrom3D(pipe));
                    return;
                }
            }
        }
    }

    private IEnumerator DragCanisterFrom3D(GeneratorFuelPipe pipe)
    {
        float startTime = Time.time;
        Vector3 startMousePos = Input.mousePosition;
        bool isDragging = false;

        GameObject dragGhost = null;
        UnityEngine.UI.Image ghostImage = null;

        GeneratorController generator = pipe != null ? pipe.GetComponentInParent<GeneratorController>() : null;

        while (Input.GetMouseButton(0))
        {
            if (!isDragging && Vector3.Distance(Input.mousePosition, startMousePos) > 10f)
            {
                isDragging = true;

                dragGhost = new GameObject("DragGhost3DCanister");
                ghostImage = dragGhost.AddComponent<UnityEngine.UI.Image>();
                ghostImage.raycastTarget = false;

                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    dragGhost.transform.SetParent(canvas.rootCanvas.transform, false);
                }

                if (generator != null && generator.installedCanisterData != null)
                {
                    ghostImage.sprite = generator.installedCanisterData.itemIcon;
                    ghostImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
                }
                dragGhost.SetActive(true);
            }

            if (isDragging && dragGhost != null)
            {
                Canvas canvas = dragGhost.GetComponentInParent<Canvas>();
                if (canvas != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.rootCanvas.transform as RectTransform,
                    Input.mousePosition,
                    canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera,
                    out Vector3 worldPoint))
                {
                    dragGhost.transform.position = worldPoint;
                }
            }

            yield return null;
        }

        if (dragGhost != null)
        {
            Destroy(dragGhost);
        }

        if (isDragging)
        {
            if (InventorySlot.hoveredSlot != null && !InventorySlot.hoveredSlot.isLocked)
            {
                InventorySlot targetSlot = InventorySlot.hoveredSlot;

                if (generator != null && targetSlot.AcceptsItem(generator.installedCanisterData))
                {
                    if (targetSlot.isEmpty)
                    {
                        targetSlot.AddItem(pipe.ExtractCanister());
                    }
                    else if (targetSlot.itemData != null && targetSlot.itemData.isStackable && 
                             targetSlot.itemData.itemID == generator.installedCanisterData.itemID &&
                             targetSlot.itemData.amount < targetSlot.itemData.maxStackSize)
                    {
                        targetSlot.itemData.amount++;
                        targetSlot.UpdateSlotUI();
                        pipe.ExtractCanister();
                    }
                }
            }
        }
        else
        {
            if (InventoryManager.Instance != null && generator != null && generator.installedCanisterData != null)
            {
                InventoryItemData canisterData = generator.installedCanisterData;
                if (InventoryManager.Instance.AddItem(canisterData))
                {
                    pipe.ExtractCanister();
                }
                
            }
        }
    }

    private IEnumerator DragFuseFrom3D(GeneratorFuseHolder holder)
    {
        float startTime = Time.time;
        Vector3 startMousePos = Input.mousePosition;
        bool isDragging = false;

        GameObject dragGhost = null;
        UnityEngine.UI.Image ghostImage = null;

        while (Input.GetMouseButton(0))
        {
            if (!isDragging && Vector3.Distance(Input.mousePosition, startMousePos) > 10f)
            {
                isDragging = true;

                dragGhost = new GameObject("DragGhost3D");
                ghostImage = dragGhost.AddComponent<UnityEngine.UI.Image>();
                ghostImage.raycastTarget = false;

                Canvas canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                {
                    dragGhost.transform.SetParent(canvas.rootCanvas.transform, false);
                }

                if (holder.installedFuseData != null)
                {
                    ghostImage.sprite = holder.installedFuseData.itemIcon;
                    ghostImage.rectTransform.sizeDelta = new Vector2(64f, 64f);
                }
                dragGhost.SetActive(true);
            }

            if (isDragging && dragGhost != null)
            {
                Canvas canvas = dragGhost.GetComponentInParent<Canvas>();
                if (canvas != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    canvas.rootCanvas.transform as RectTransform,
                    Input.mousePosition,
                    canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera,
                    out Vector3 worldPoint))
                {
                    dragGhost.transform.position = worldPoint;
                }
            }

            yield return null;
        }

        if (dragGhost != null)
        {
            Destroy(dragGhost);
        }

        if (isDragging)
        {
            if (InventorySlot.hoveredSlot != null && !InventorySlot.hoveredSlot.isLocked)
            {
                InventorySlot targetSlot = InventorySlot.hoveredSlot;

                if (targetSlot.AcceptsItem(holder.installedFuseData))
                {
                    if (targetSlot.isEmpty)
                    {
                        targetSlot.AddItem(holder.ExtractFuse());
                    }
                    else if (targetSlot.itemData != null && targetSlot.itemData.isStackable && 
                             targetSlot.itemData.itemID == holder.installedFuseData.itemID &&
                             targetSlot.itemData.amount < targetSlot.itemData.maxStackSize)
                    {
                        targetSlot.itemData.amount++;
                        targetSlot.UpdateSlotUI();
                        holder.ExtractFuse();
                    }
                }
            }
        }
        else
        {
            if (InventoryManager.Instance != null && holder.installedFuseData != null)
            {
                InventoryItemData fuseData = holder.installedFuseData;
                if (InventoryManager.Instance.AddItem(fuseData))
                {
                    holder.ExtractFuse();
                }
                
            }
        }
    }

    private bool PerformRaycast(Ray ray, float distance, out RaycastHit hitResult)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));

        foreach (var h in hits)
        {
            if (h.collider.gameObject.layer == 2)
            {
                continue;
            }
            if (h.collider.gameObject == gameObject || 
                h.collider.transform.IsChildOf(transform) ||
                h.collider.transform.root == transform.root)
            {
                continue;
            }
            if (h.collider.name.IndexOf("CAMERASPOT", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }
            if (h.collider.transform.parent != null && 
                h.collider.transform.parent.name.IndexOf("CAMERASPOT", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            if (h.collider.isTrigger)
            {
                bool isInteractable = h.collider.GetComponentInParent<PickUpItem>() != null ||
                                     h.collider.GetComponentInParent<DoorController>() != null ||
                                     h.collider.GetComponentInParent<PlantedPlant>() != null ||
                                     h.collider.GetComponentInParent<WorldClock>() != null ||
                                     h.collider.GetComponentInParent<WorldToggleDevice>() != null ||
                                     h.collider.GetComponentInParent<TeleportDoor>() != null ||
                                     h.collider.GetComponentInParent<WindowController>() != null ||
                                     h.collider.GetComponentInParent<NPCDialogue>() != null ||
                                     h.collider.GetComponentInParent<MeatGrinderController>() != null ||
                                     h.collider.GetComponentInParent<GeneratorDoorController>() != null ||
                                     h.collider.GetComponentInParent<DoughRollingController>() != null ||
                                     h.collider.GetComponentInParent<WorkbenchController>() != null ||
                                     h.collider.GetComponentInParent<VendingMachineController>() != null ||
                                     h.collider.GetComponentInParent<LiquidSource>() != null ||
                                     h.collider.GetComponentInParent<ItemSource>() != null ||
                                     h.collider.GetComponentInParent<StorageContainer>() != null ||
                                     h.collider.GetComponentInParent<GeneratorSwitch>() != null ||
                                     h.collider.GetComponentInParent<GeneratorFuseHolder>() != null ||
                                     h.collider.GetComponentInParent<GeneratorFuelPipe>() != null ||
                                     h.collider.GetComponentInParent<WaterCoolerPipe>() != null ||
                                     h.collider.GetComponentInParent<WaterCoolerTap>() != null ||
                                     h.collider.GetComponentInParent<SinkTapController>() != null ||
                                     h.collider.GetComponentInParent<ButcheringTableController>() != null ||
                                     (h.collider.GetComponentInParent<TrashSortingButton>() != null && h.collider.GetComponentInParent<TrashSortingButton>().CanInteract()) ||
                                     (h.collider.GetComponentInParent<GeneratorController>() != null && h.collider.GetComponentInParent<GeneratorController>().leverTransform != null && 
                                      (h.collider.transform == h.collider.GetComponentInParent<GeneratorController>().leverTransform || h.collider.transform.IsChildOf(h.collider.GetComponentInParent<GeneratorController>().leverTransform)));

                if (!isInteractable)
                {
                    continue;
                }
            }

            hitResult = h;
            return true;
        }

        hitResult = new RaycastHit();
        return false;
    }

    /// <summary>
    /// Возвращает локализованное название жидкости, ес...
    /// </summary>
    public static string GetLocalizedLiquidName(LiquidType type)
    {
        if (Instance != null && Instance.localizedLiquids != null)
        {
            foreach (var mapping in Instance.localizedLiquids)
            {
                if (mapping.liquidType == type && mapping.localizedName != null && !mapping.localizedName.IsEmpty)
                {
                    return mapping.localizedName.GetLocalizedString();
                }
            }
        }

        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                if (code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase))
                {
                    return LiquidHelper.GetLiquidNameEn(type);
                }
            }
        }
        catch {}

        return LiquidHelper.GetLiquidNameRu(type);
    }

    private static string GetDefaultPrompt(string key, string arg1 = "", string arg2 = "")
    {
        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        switch (key)
        {
            case "IncompatibleLiquid": 
                return isEn ? "Incompatible liquid" : "Неподходящая жидкость";
            case "DifferentLiquid": 
                return isEn ? $"Cooler contains a different liquid ({arg1})" : $"Кулер содержит другую жидкость ({arg1})";
            case "Full": 
                return isEn ? "Cooler is full" : "Кулер полон";
            case "NeedContainer": 
                return isEn ? "Need a container with a suitable liquid" : "Нужен сосуд с подходящей жидкостью";
            case "ContainerFull": 
                return isEn ? "Container is full" : "Сосуд полон";
            case "ContainerDifferent": 
                return isEn ? $"Container contains a different liquid ({arg1})" : $"Сосуд содержит другую жидкость ({arg1})";
            case "Empty": 
                return isEn ? "Cooler is empty" : "Кулер пуст";
            case "NeedEmptyContainer": 
                return isEn ? "Need an empty container to fill" : "Нужен пустой сосуд для набора воды";
            case "NotThirsty": 
                return isEn ? "You are not thirsty" : "Вы не хотите пить";
            case "PourDefault": 
                return isEn ? $"<color=#FFD700>[Hold {arg2}]</color> Pour {arg1}" : $"<color=#FFD700>[Зажмите {arg2}]</color> Влить {arg1}";
            case "FillDefault": 
                return isEn ? $"<color=#FFD700>[Hold {arg2}]</color> Fill {arg1}" : $"<color=#FFD700>[Зажмите {arg2}]</color> Набрать {arg1}";
            case "DrinkDefault": 
                return isEn ? $"<color=#FFD700>[Hold {arg2}]</color> Drink: {arg1}" : $"<color=#FFD700>[Зажмите {arg2}]</color> Выпить: {arg1}";
            default: return "";
        }
    }
}
