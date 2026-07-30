using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public partial class PlayerInteract : MonoBehaviour
{
    void Update()
    {
        if (TVChairController.activeChair != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (GeneratorDoorController.activeGeneratorDoor != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            
            HandleGeneratorZoomedInteraction();
            return;
        }

        if (DoughRollingController.activeRollingBoard != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (WorkbenchController.activeWorkbench != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (VendingMachineController.activeVendingMachine != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (LocationTransitionController.activeTransition != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (PeepholeController.activePeephole != null)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            RemoveHighlight();
            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
        {
            RemoveHighlight();
            if (interactText != null) interactText.gameObject.SetActive(false);
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        bool foundSomething = false;
        string currentPrompt = "";

        if (PerformRaycast(ray, interactDistance, out hit))
        {
            // Пересчитываем компоненты только при смене объекта
            if (hit.collider != lastHitCollider)
            {
                lastHitCollider    = hit.collider;
                cachedItem         = hit.collider.GetComponentInParent<PickUpItem>();
                cachedDoor         = hit.collider.GetComponentInParent<DoorController>();
                cachedPlant        = hit.collider.GetComponentInParent<PlantedPlant>();
                cachedClock        = hit.collider.GetComponentInParent<WorldClock>();
                cachedToggleDevice = hit.collider.GetComponentInParent<WorldToggleDevice>();
                cachedTeleportDoor = hit.collider.GetComponentInParent<TeleportDoor>();
                cachedWindow       = hit.collider.GetComponentInParent<WindowController>();
                cachedNpc          = hit.collider.GetComponentInParent<NPCDialogue>();
                cachedCorpse       = hit.collider.GetComponentInParent<NPCCorpse>();
                cachedMeatGrinder  = hit.collider.GetComponentInParent<MeatGrinderController>();
                cachedGeneratorDoor = hit.collider.GetComponentInParent<GeneratorDoorController>();
                cachedLiquidSource  = hit.collider.GetComponentInParent<LiquidSource>();
                cachedItemSource    = hit.collider.GetComponentInParent<ItemSource>();
                cachedStorage       = hit.collider.GetComponentInParent<StorageContainer>();
                cachedTVChair       = hit.collider.GetComponentInParent<TVChairController>();
                cachedDoughRolling  = hit.collider.GetComponentInParent<DoughRollingController>();
                cachedWorkbench     = hit.collider.GetComponentInParent<WorkbenchController>();
                cachedVendingMachine = hit.collider.GetComponentInParent<VendingMachineController>();
                cachedLocationTransition = hit.collider.GetComponentInParent<LocationTransitionController>();
                cachedPeephole     = hit.collider.GetComponentInParent<PeepholeController>();
                cachedWaterCoolerPipe = hit.collider.GetComponentInParent<WaterCoolerPipe>();
                cachedWaterCoolerTap  = hit.collider.GetComponentInParent<WaterCoolerTap>();
                cachedSinkTap         = hit.collider.GetComponentInParent<SinkTapController>();
                cachedButcheringTable = hit.collider.GetComponentInParent<ButcheringTableController>();
                cachedIndustrialMeatGrinder = hit.collider.GetComponentInParent<IndustrialMeatGrinder>();
                cachedEnemyAI         = hit.collider.GetComponentInParent<EnemyAI>();
                
                if (cachedItem != null && !cachedItem.isPlaced)
                {
                    cachedMeatGrinder = null;
                    cachedToggleDevice = null;
                    cachedItemSource = null;
                }
            }

            if (cachedTVChair != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    string sitPrompt = (actions != null && actions.sitAction != null && !actions.sitAction.IsEmpty) 
                        ? actions.sitAction.GetLocalizedString() 
                        : "Сесть";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {sitPrompt}\n";
                    foundSomething = true;

                    if (currentLookTVChair != cachedTVChair)
                    {
                        RemoveHighlight();
                        currentLookTVChair = cachedTVChair;
                        currentLookTVChair.SetHighlight(true);
                    }

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        cachedTVChair.SitDown(this);
                        RemoveHighlight();
                    }
                }
            }
            else if (cachedClock != null)
            {
                string timeStr = (actions != null && actions.timePrefix != null && !actions.timePrefix.IsEmpty) ? actions.timePrefix.GetLocalizedString() : "Время:";
                currentPrompt += $"{timeStr} {DayNightCycle.Instance.GetFormattedTime()}\n";
                foundSomething = true;
            }
            else if (cachedStorage != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    string prompt = "";
                    if (cachedStorage.isOpened)
                    {
                        prompt = (actions != null && actions.exit != null && !actions.exit.IsEmpty) 
                            ? actions.exit.GetLocalizedString() 
                            : "Закрыть";
                    }
                    else
                    {
                        prompt = (cachedStorage.localizedOpenPrompt != null && !cachedStorage.localizedOpenPrompt.IsEmpty) 
                            ? cachedStorage.localizedOpenPrompt.GetLocalizedString() 
                            : "Открыть";
                    }

                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                    foundSomething = true;

                    if (currentLookStorage != cachedStorage)
                    {
                        RemoveHighlight();
                        currentLookStorage = cachedStorage;
                        currentLookStorage.SetHighlight(true);
                    }

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        if (cachedStorage.isOpened)
                        {
                            cachedStorage.Close();
                        }
                        else
                        {
                            cachedStorage.Open();
                        }
                        animator?.SetTrigger(InteractHash);
                    }
                }
            }
            else if (cachedDoor != null)
            {
                if (cachedDoor.isLocked)
                {
                    string lockedStr = (actions != null && actions.locked != null && !actions.locked.IsEmpty) ? actions.locked.GetLocalizedString() : "Заперто";
                    string unlockStr = (actions != null && actions.unlockAction != null && !actions.unlockAction.IsEmpty) ? actions.unlockAction.GetLocalizedString() : "Снять замок";

                    currentPrompt += $"<color=#FF4444>[{cachedInteractKey}]</color> {lockedStr}\n";
                    currentPrompt += $"<color=#FFD700>[{cachedToggleKey}]</color> {unlockStr}\n";

                    foundSomething = true;

                    if (Input.GetKeyDown(cachedInteractKey)) { cachedDoor.TryOpenDoor(transform.position); animator?.SetTrigger(InteractHash); }
                    if (Input.GetKeyDown(cachedToggleKey)) { cachedDoor.InteractWithLock(); animator?.SetTrigger(InteractHash); }
                }
                else
                {
                    string doorStr = (actions != null && actions.doorAction != null && !actions.doorAction.IsEmpty) ? actions.doorAction.GetLocalizedString() : "Открыть/Закрыть";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {doorStr}\n";

                    bool hasPadlock = false;
                    if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                    {
                        int index = InventoryManager.Instance.selectedSlotIndex;
                        if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                        {
                            InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[index];
                            if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.currentItemID == cachedDoor.padlockItemID)
                            {
                                hasPadlock = true;
                            }
                        }
                    }

                    if (hasPadlock)
                    {
                        string lockStr = (actions != null && actions.lockAction != null && !actions.lockAction.IsEmpty) ? actions.lockAction.GetLocalizedString() : "Повесить замок";
                        currentPrompt += $"<color=#FFD700>[{cachedToggleKey}]</color> {lockStr}\n";
                    }

                    foundSomething = true;

                    if (Input.GetKeyDown(cachedInteractKey)) { cachedDoor.TryOpenDoor(transform.position); animator?.SetTrigger(InteractHash); }
                    if (Input.GetKeyDown(cachedToggleKey) && hasPadlock) { cachedDoor.InteractWithLock(); animator?.SetTrigger(InteractHash); }
                }
            }
            else if (cachedWindow != null)
            {
                if (cachedWindow.isLocked)
                {
                    string lockedStr = (actions != null && actions.locked != null && !actions.locked.IsEmpty) ? actions.locked.GetLocalizedString() : "Заперто";
                    currentPrompt += $"<color=#FF4444>[!]</color> {lockedStr}\n";
                    foundSomething = true;

                    if (Input.GetKeyDown(cachedInteractKey)) { cachedWindow.ToggleWindow(); animator?.SetTrigger(InteractHash); }
                }
                else
                {
                    string windowStr = (actions != null && actions.doorAction != null && !actions.doorAction.IsEmpty) ? actions.doorAction.GetLocalizedString() : "Открыть/Закрыть";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {windowStr}\n";
                    foundSomething = true;

                    if (Input.GetKeyDown(cachedInteractKey)) { cachedWindow.ToggleWindow(); animator?.SetTrigger(InteractHash); }
                }
            }
            else if (cachedEnemyAI != null && cachedEnemyAI.currentState == EnemyAI.NPCState.Enslaved)
            {
                InventorySlot activeSlot = null;
                if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                {
                    int index = InventoryManager.Instance.selectedSlotIndex;
                    if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                    {
                        activeSlot = InventoryManager.Instance.hotbarSlots[index];
                    }
                }

                bool canFeed = false;
                if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null)
                {
                    InventoryItemData itemData = activeSlot.itemData;
                    if (itemData.category == ItemCategory.Food || 
                        (itemData.isConsumable && itemData.currentLiquidType == LiquidType.Biomass && itemData.currentAmount > 0))
                    {
                        canFeed = true;
                    }
                }

                string genderStr = cachedEnemyAI.gender == EnemyAI.NPCGender.Male ? "Мужчина" : "Женщина";
                if (canFeed)
                {
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> Покормить ({genderStr}, {cachedEnemyAI.age} лет, Сытость: {cachedEnemyAI.hunger:F0}%)\n";
                }
                
                if (EnemyAI.carriedSlave == null && NPCCorpse.carriedCorpse == null)
                {
                    currentPrompt += $"<color=#FFD700>[{cachedToggleKey}]</color> Взять раба на руки\n";
                }

                foundSomething = true;

                if (canFeed && Input.GetKeyDown(cachedInteractKey))
                {
                    bool fed = cachedEnemyAI.TryFeed(activeSlot.itemData);
                    if (fed)
                    {
                        animator?.SetTrigger(InteractHash);
                        if (activeSlot.itemData.category == ItemCategory.Food)
                        {
                            activeSlot.itemData.amount--;
                            if (activeSlot.itemData.amount <= 0)
                            {
                                activeSlot.ClearSlot();
                            }
                            else
                            {
                                activeSlot.UpdateSlotUI();
                            }
                        }
                        else if (activeSlot.itemData.isConsumable && activeSlot.itemData.currentLiquidType == LiquidType.Biomass)
                        {
                            activeSlot.itemData.currentAmount = Mathf.Max(0, activeSlot.itemData.currentAmount - 20);
                            if (activeSlot.itemData.currentAmount == 0)
                            {
                                activeSlot.itemData.currentLiquidType = LiquidType.None;
                            }
                            activeSlot.UpdateSlotUI();
                        }
                    }
                }

                if (EnemyAI.carriedSlave == null && NPCCorpse.carriedCorpse == null && Input.GetKeyDown(cachedToggleKey))
                {
                    cachedEnemyAI.PickUpSlave(this.gameObject);
                    animator?.SetTrigger(InteractHash);
                }
            }
            else if (cachedNpc != null && cachedNpc.enabled)
            {
                string talkStr = (actions != null && actions.talkAction != null && !actions.talkAction.IsEmpty) ? actions.talkAction.GetLocalizedString() : "Говорить";
                currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {talkStr} ({cachedNpc.npcName})\n";
                foundSomething = true;

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    if (DialogueManager.Instance != null)
                    {
                        DialogueManager.Instance.StartDialogue(cachedNpc, playerCamera);
                        RemoveHighlight();
                    }
                }
            }
            else if (cachedCorpse != null)
            {
                if (cachedCorpse.currentTable != null)
                {
                    // Труп на столе - взаимодействуем через стол разделки, но наведясь на труп
                    ButcheringTableController table = cachedCorpse.currentTable;
                    
                    if (currentLookCorpse != cachedCorpse)
                    {
                        RemoveHighlight();
                        currentLookCorpse = cachedCorpse;
                        currentLookCorpse.SetHighlight(true);
                    }

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

                    string prompt = "";
                    if (table.CanPickUpCorpse())
                    {
                        string pickupStr = (table.locPromptPickUp != null && !table.locPromptPickUp.IsEmpty) ? table.locPromptPickUp.GetLocalizedString() : (isEn ? "Take body back" : "Забрать тело");
                        prompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {pickupStr}\n";
                    }
                    if (table.CanStartButchering())
                    {
                        string butcherStr = (table.locPromptButcher != null && !table.locPromptButcher.IsEmpty) ? table.locPromptButcher.GetLocalizedString() : (isEn ? "Butcher body" : "Начать разделку");
                        prompt += $"<color=#FFD700>[{cachedToggleKey}]</color> {butcherStr}\n";
                    }

                    if (!string.IsNullOrEmpty(prompt))
                    {
                        currentPrompt += prompt;
                        foundSomething = true;
                    }

                    if (Input.GetKeyDown(cachedInteractKey) && table.CanPickUpCorpse())
                    {
                        RemoveHighlight();
                        table.PickUpCorpse(this.gameObject);
                        animator?.SetTrigger(InteractHash);
                    }
                    else if (Input.GetKeyDown(cachedToggleKey) && table.CanStartButchering())
                    {
                        table.EnterButcheringMode(playerCamera);
                        RemoveHighlight();
                    }
                }
                else if (cachedCorpse.currentGrinder != null)
                {
                    // Труп на мясорубке - взаимодействуем через мясорубку, но наведясь на труп
                    IndustrialMeatGrinder grinder = cachedCorpse.currentGrinder;
                    
                    if (currentLookIndustrialMeatGrinder != grinder)
                    {
                        RemoveHighlight();
                        currentLookIndustrialMeatGrinder = grinder;
                        currentLookIndustrialMeatGrinder.SetHighlight(true);
                    }

                    bool carryingCorpse = NPCCorpse.carriedCorpse != null;
                    string prompt = grinder.GetInteractPrompt(carryingCorpse, cachedInteractKey, cachedToggleKey, 1);
                    
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        currentPrompt += prompt + "\n";
                        foundSomething = true;
                    }

                    if (Input.GetKeyDown(cachedInteractKey) && grinder.state == IndustrialMeatGrinder.GrinderState.Idle)
                    {
                        if (grinder.IsPowerWorking() && (!grinder.requireContainerToStart || grinder.IsContainerPlaced()))
                        {
                            if (!grinder.isGrinderOn)
                            {
                                RemoveHighlight();
                                grinder.ToggleGrinderState();
                                animator?.SetTrigger(InteractHash);
                            }
                        }
                    }
                    else if (Input.GetKeyDown(cachedToggleKey) && grinder.CanPickUpCorpse())
                    {
                        RemoveHighlight();
                        grinder.PickUpCorpse(this.gameObject);
                        animator?.SetTrigger(InteractHash);
                    }
                }
                else
                {
                    // Обычный труп на полу
                    if (currentLookCorpse != cachedCorpse)
                    {
                        RemoveHighlight();
                        currentLookCorpse = cachedCorpse;
                        currentLookCorpse.SetHighlight(true);
                    }

                    string pickupStr = cachedCorpse.promptText;
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {pickupStr}\n";
                    foundSomething = true;

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        RemoveHighlight();
                        cachedCorpse.PickUp(this.gameObject);
                        animator?.SetTrigger(InteractHash);
                    }
                }
            }
            else if (cachedButcheringTable != null)
            {
                // Если на столе есть труп, мы не взаимодействуем со столом напрямую (только через труп)
                if (cachedButcheringTable.hasCorpse)
                {
                    RemoveHighlight();
                }
                else
                {
                    if (currentLookButcheringTable != cachedButcheringTable)
                    {
                        RemoveHighlight();
                        currentLookButcheringTable = cachedButcheringTable;
                        currentLookButcheringTable.SetHighlight(true);
                    }

                    bool carryingCorpse = NPCCorpse.carriedCorpse != null;
                    string prompt = cachedButcheringTable.GetInteractPrompt(carryingCorpse, cachedInteractKey, cachedToggleKey);
                    
                    if (!string.IsNullOrEmpty(prompt))
                    {
                        currentPrompt += prompt + "\n";
                        foundSomething = true;
                    }

                    if (Input.GetKeyDown(cachedInteractKey) && carryingCorpse)
                    {
                        RemoveHighlight();
                        cachedButcheringTable.PlaceCorpse(NPCCorpse.carriedCorpse);
                        animator?.SetTrigger(InteractHash);
                    }
                }
            }
            else if (cachedIndustrialMeatGrinder != null)
            {
                int lookArea = cachedIndustrialMeatGrinder.GetLookArea(lastHitCollider);

                if (currentLookIndustrialMeatGrinder != cachedIndustrialMeatGrinder)
                {
                    RemoveHighlight();
                    currentLookIndustrialMeatGrinder = cachedIndustrialMeatGrinder;
                    currentLookIndustrialMeatGrinder.SetHighlight(true);
                }

                bool carryingCorpse = NPCCorpse.carriedCorpse != null;
                string prompt = cachedIndustrialMeatGrinder.GetInteractPrompt(carryingCorpse, cachedInteractKey, cachedToggleKey, lookArea);
                
                if (!string.IsNullOrEmpty(prompt))
                {
                    currentPrompt += prompt + "\n";
                    foundSomething = true;
                }

                if (lookArea == 1) // Conveyor Belt
                {
                    if (!cachedIndustrialMeatGrinder.hasCorpse)
                    {
                        if (carryingCorpse && Input.GetKeyDown(cachedInteractKey))
                        {
                            RemoveHighlight();
                            cachedIndustrialMeatGrinder.PlaceCorpse(NPCCorpse.carriedCorpse);
                            animator?.SetTrigger(InteractHash);
                        }
                    }
                    else
                    {
                        if (Input.GetKeyDown(cachedToggleKey) && cachedIndustrialMeatGrinder.CanPickUpCorpse())
                        {
                            RemoveHighlight();
                            cachedIndustrialMeatGrinder.PickUpCorpse(this.gameObject);
                            animator?.SetTrigger(InteractHash);
                        }
                    }
                }
                else if (lookArea == 2) // Button 1 (ON Button)
                {
                    if (Input.GetKeyDown(cachedInteractKey) && cachedIndustrialMeatGrinder.state == IndustrialMeatGrinder.GrinderState.Idle)
                    {
                        if (cachedIndustrialMeatGrinder.IsPowerWorking() && (!cachedIndustrialMeatGrinder.requireContainerToStart || cachedIndustrialMeatGrinder.IsContainerPlaced()))
                        {
                            if (!cachedIndustrialMeatGrinder.isGrinderOn)
                            {
                                RemoveHighlight();
                                cachedIndustrialMeatGrinder.ToggleGrinderState();
                                animator?.SetTrigger(InteractHash);
                            }
                        }
                    }
                }
                else if (lookArea == 3) // Button 2 (OFF Button)
                {
                    if (Input.GetKeyDown(cachedInteractKey) && cachedIndustrialMeatGrinder.state == IndustrialMeatGrinder.GrinderState.Idle)
                    {
                        if (cachedIndustrialMeatGrinder.IsPowerWorking())
                        {
                            if (cachedIndustrialMeatGrinder.isGrinderOn)
                            {
                                RemoveHighlight();
                                cachedIndustrialMeatGrinder.ToggleGrinderState();
                                animator?.SetTrigger(InteractHash);
                            }
                        }
                    }
                }
            }
            else if (cachedMeatGrinder != null)
            {
                string mgPrompt = (cachedMeatGrinder.interactPrompt != null && !cachedMeatGrinder.interactPrompt.IsEmpty) ? cachedMeatGrinder.interactPrompt.GetLocalizedString() : "Использовать мясорубку";
                currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {mgPrompt}\n";
                
                if (cachedItem != null && cachedItem.isPlacedOnSnapPoint)
                {
                    string pickupStr = (actions != null && actions.pickUpAction != null && !actions.pickUpAction.IsEmpty) ? actions.pickUpAction.GetLocalizedString() : "Взять:";
                    currentPrompt += $"<color=#FFD700>[{cachedToggleKey}]</color> {pickupStr} {cachedItem.itemName}\n";
                }

                foundSomething = true;

                if (currentLookMeatGrinder != cachedMeatGrinder)
                {
                    RemoveHighlight();
                    currentLookMeatGrinder = cachedMeatGrinder;
                    currentLookMeatGrinder.SetHighlight(true);
                }

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedMeatGrinder.EnterMeatGrinderMode(playerCamera);
                    RemoveHighlight();
                }

                if (cachedItem != null && cachedItem.isPlacedOnSnapPoint && Input.GetKeyDown(cachedToggleKey))
                {
                    RemoveHighlight();
                    cachedItem.PickUp();
                    animator?.SetTrigger(InteractHash);
                }
            }
            else if (cachedPlant != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    bool isHarvestable = cachedPlant.IsHarvestable();
                    string stageName = cachedPlant.GetStageName();

                    if (isHarvestable)
                    {
                        string harvestStr = (actions != null && actions.harvestAction != null && !actions.harvestAction.IsEmpty) ? actions.harvestAction.GetLocalizedString() : "Собрать урожай";
                        currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {harvestStr}: {stageName}\n";
                        foundSomething = true;

                        if (Input.GetKeyDown(cachedInteractKey))
                        {
                            cachedPlant.HarvestByHand();
                            animator?.SetTrigger(InteractHash);
                        }
                    }
                    else
                    {
                        currentPrompt += $"{stageName}\n";
                        foundSomething = true;
                    }
                }
            }
            else if (cachedTeleportDoor != null)
            {
                foundSomething = true;

                if (currentHoveredDoor != cachedTeleportDoor)
                {
                    if (currentHoveredDoor != null) currentHoveredDoor.SetHover(false);
                    currentHoveredDoor = cachedTeleportDoor;
                    currentHoveredDoor.SetHover(true);
                }

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedTeleportDoor.DoTeleport(playerCamera.transform.root.gameObject);
                }
            }
            else if (cachedGeneratorDoor != null)
            {
                string doorPrompt = (actions != null && actions.doorAction != null && !actions.doorAction.IsEmpty) ? actions.doorAction.GetLocalizedString() : "Открыть";
                currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {doorPrompt}\n";
                foundSomething = true;

                if (currentLookGeneratorDoor != cachedGeneratorDoor)
                {
                    RemoveHighlight();
                    currentLookGeneratorDoor = cachedGeneratorDoor;
                    currentLookGeneratorDoor.SetHighlight(true);
                }

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedGeneratorDoor.Interact(playerCamera);
                    RemoveHighlight();
                }
            }
            else if (cachedDoughRolling != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    string prompt = (cachedDoughRolling.interactPrompt != null && !cachedDoughRolling.interactPrompt.IsEmpty) 
                        ? cachedDoughRolling.interactPrompt.GetLocalizedString() 
                        : "Раскатать тесто";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                    foundSomething = true;

                    if (currentLookDoughRolling != cachedDoughRolling)
                    {
                        RemoveHighlight();
                        currentLookDoughRolling = cachedDoughRolling;
                        currentLookDoughRolling.SetHighlight(true);
                    }

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        cachedDoughRolling.EnterDoughRollingMode(playerCamera);
                        RemoveHighlight();
                    }
                }
            }
            else if (cachedWorkbench != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    string prompt = (cachedWorkbench.interactPrompt != null && !cachedWorkbench.interactPrompt.IsEmpty) 
                        ? cachedWorkbench.interactPrompt.GetLocalizedString() 
                        : "Использовать верстак";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                    foundSomething = true;

                    if (currentLookWorkbench != cachedWorkbench)
                    {
                        RemoveHighlight();
                        currentLookWorkbench = cachedWorkbench;
                        currentLookWorkbench.SetHighlight(true);
                    }

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        cachedWorkbench.EnterWorkbenchMode(playerCamera);
                        RemoveHighlight();
                    }
                }
            }
            else if (cachedVendingMachine != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    string prompt = (cachedVendingMachine.interactPrompt != null && !cachedVendingMachine.interactPrompt.IsEmpty) 
                        ? cachedVendingMachine.interactPrompt.GetLocalizedString() 
                        : "Использовать торговый автомат";
                    currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                    foundSomething = true;

                    if (currentLookVendingMachine != cachedVendingMachine)
                    {
                        RemoveHighlight();
                        currentLookVendingMachine = cachedVendingMachine;
                        currentLookVendingMachine.SetHighlight(true);
                    }

                    if (Input.GetKeyDown(cachedInteractKey))
                    {
                        cachedVendingMachine.EnterVendingMachineMode(playerCamera);
                        RemoveHighlight();
                    }
                }
            }
            else if (cachedLocationTransition != null)
            {
                string prompt = (cachedLocationTransition.interactPrompt != null && !cachedLocationTransition.interactPrompt.IsEmpty) 
                    ? cachedLocationTransition.interactPrompt.GetLocalizedString() 
                    : "Войти";
                currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                foundSomething = true;

                if (currentLookLocationTransition != cachedLocationTransition)
                {
                    RemoveHighlight();
                    currentLookLocationTransition = cachedLocationTransition;
                    currentLookLocationTransition.SetHighlight(true);
                }

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedLocationTransition.StartTransition(playerCamera);
                    RemoveHighlight();
                }
            }
            else if (cachedPeephole != null)
            {
                string prompt = (cachedPeephole.interactPrompt != null && !cachedPeephole.interactPrompt.IsEmpty) 
                    ? cachedPeephole.interactPrompt.GetLocalizedString() 
                    : "Посмотреть в глазок";
                currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> {prompt}\n";
                foundSomething = true;

                if (currentLookPeephole != cachedPeephole)
                {
                    RemoveHighlight();
                    currentLookPeephole = cachedPeephole;
                    currentLookPeephole.SetHighlight(true);
                }

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedPeephole.StartTransition(playerCamera);
                    RemoveHighlight();
                }
            }
            else if (cachedWaterCoolerPipe != null)
            {
                foundSomething = true;

                if (currentLookWaterCoolerPipe != cachedWaterCoolerPipe)
                {
                    RemoveHighlight();
                    currentLookWaterCoolerPipe = cachedWaterCoolerPipe;
                    currentLookWaterCoolerPipe.SetHighlight(true);
                }

                // Получаем активный слот инвентаря
                InventorySlot activeSlot = null;
                if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                {
                    int index = InventoryManager.Instance.selectedSlotIndex;
                    if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                    {
                        activeSlot = InventoryManager.Instance.hotbarSlots[index];
                    }
                }

                if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null && 
                    activeSlot.itemData.isConsumable && 
                    (activeSlot.itemData.consumableType == ConsumableType.LiquidContainer || activeSlot.itemData.consumableType == ConsumableType.LampOil))
                {
                    InventoryItemData containerData = activeSlot.itemData;
                    bool isAllowedType = cachedWaterCoolerPipe.controller.allowedLiquids.Contains(containerData.currentLiquidType);
                    
                    if (!isAllowedType || containerData.currentAmount <= 0)
                    {
                        string liquidName = GetLocalizedLiquidName(containerData.currentLiquidType);
                        string msg = (cachedWaterCoolerPipe.locIncompatibleLiquid != null && !cachedWaterCoolerPipe.locIncompatibleLiquid.IsEmpty)
                            ? cachedWaterCoolerPipe.locIncompatibleLiquid.GetLocalizedString(new { liquid = liquidName })
                            : GetDefaultPrompt("IncompatibleLiquid");
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerPipe.StopPouring();
                    }
                    else if (cachedWaterCoolerPipe.controller.currentWater > 0f && 
                             cachedWaterCoolerPipe.controller.currentLiquidType != LiquidType.None && 
                             containerData.currentLiquidType != cachedWaterCoolerPipe.controller.currentLiquidType)
                    {
                        string liquidName = GetLocalizedLiquidName(cachedWaterCoolerPipe.controller.currentLiquidType);
                        string msg = (cachedWaterCoolerPipe.locDifferentLiquid != null && !cachedWaterCoolerPipe.locDifferentLiquid.IsEmpty)
                            ? cachedWaterCoolerPipe.locDifferentLiquid.GetLocalizedString(new { liquid = liquidName })
                            : GetDefaultPrompt("DifferentLiquid", liquidName);
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerPipe.StopPouring();
                    }
                    else if (cachedWaterCoolerPipe.IsFull())
                    {
                        string msg = (cachedWaterCoolerPipe.locFull != null && !cachedWaterCoolerPipe.locFull.IsEmpty)
                            ? cachedWaterCoolerPipe.locFull.GetLocalizedString()
                            : GetDefaultPrompt("Full");
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerPipe.StopPouring();
                    }
                    else
                    {
                        string liquidName = GetLocalizedLiquidName(containerData.currentLiquidType);
                        string prompt = (cachedWaterCoolerPipe.pourPrompt != null && !cachedWaterCoolerPipe.pourPrompt.IsEmpty) 
                            ? cachedWaterCoolerPipe.pourPrompt.GetLocalizedString(new { liquid = liquidName, key = cachedInteractKey.ToString() })
                            : GetDefaultPrompt("PourDefault", liquidName, cachedInteractKey.ToString());
                        currentPrompt += $"{prompt}\n";

                        if (Input.GetKey(cachedInteractKey))
                        {
                            cachedWaterCoolerPipe.PourWater(activeSlot);
                        }
                        else
                        {
                            cachedWaterCoolerPipe.StopPouring();
                        }
                    }
                }
                else
                {
                    string msg = (cachedWaterCoolerPipe.locNeedContainer != null && !cachedWaterCoolerPipe.locNeedContainer.IsEmpty)
                        ? cachedWaterCoolerPipe.locNeedContainer.GetLocalizedString()
                        : GetDefaultPrompt("NeedContainer");
                    currentPrompt += $"{msg}\n";
                    cachedWaterCoolerPipe.StopPouring();
                }
            }
            else if (cachedWaterCoolerTap != null)
            {
                foundSomething = true;

                if (currentLookWaterCoolerTap != cachedWaterCoolerTap)
                {
                    RemoveHighlight();
                    currentLookWaterCoolerTap = cachedWaterCoolerTap;
                    currentLookWaterCoolerTap.SetHighlight(true);
                }

                // Получаем активный слот инвентаря
                InventorySlot activeSlot = null;
                if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                {
                    int index = InventoryManager.Instance.selectedSlotIndex;
                    if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                    {
                        activeSlot = InventoryManager.Instance.hotbarSlots[index];
                    }
                }

                bool hasContainer = activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null && 
                    activeSlot.itemData.isConsumable && 
                    (activeSlot.itemData.consumableType == ConsumableType.LiquidContainer || activeSlot.itemData.consumableType == ConsumableType.LampOil);

                bool isCoolerEmpty = cachedWaterCoolerTap.IsEmpty();

                // 1. Логика набора воды (E)
                if (hasContainer)
                {
                    InventoryItemData containerData = activeSlot.itemData;
                    if (containerData.currentAmount >= containerData.maxAmount)
                    {
                        string msg = (cachedWaterCoolerTap.locContainerFull != null && !cachedWaterCoolerTap.locContainerFull.IsEmpty)
                            ? cachedWaterCoolerTap.locContainerFull.GetLocalizedString()
                            : GetDefaultPrompt("ContainerFull");
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerTap.StopFilling();
                    }
                    else if (containerData.currentLiquidType != LiquidType.None && containerData.currentLiquidType != cachedWaterCoolerTap.controller.currentLiquidType)
                    {
                        string liquidName = GetLocalizedLiquidName(containerData.currentLiquidType);
                        string msg = (cachedWaterCoolerTap.locContainerDifferent != null && !cachedWaterCoolerTap.locContainerDifferent.IsEmpty)
                            ? cachedWaterCoolerTap.locContainerDifferent.GetLocalizedString(new { liquid = liquidName })
                            : GetDefaultPrompt("ContainerDifferent", liquidName);
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerTap.StopFilling();
                    }
                    else if (isCoolerEmpty)
                    {
                        string msg = (cachedWaterCoolerTap.locEmpty != null && !cachedWaterCoolerTap.locEmpty.IsEmpty)
                            ? cachedWaterCoolerTap.locEmpty.GetLocalizedString()
                            : GetDefaultPrompt("Empty");
                        currentPrompt += $"{msg}\n";
                        cachedWaterCoolerTap.StopFilling();
                    }
                    else
                    {
                        string liquidName = GetLocalizedLiquidName(cachedWaterCoolerTap.controller.currentLiquidType);
                        string fillStr = (cachedWaterCoolerTap.fillPrompt != null && !cachedWaterCoolerTap.fillPrompt.IsEmpty) 
                            ? cachedWaterCoolerTap.fillPrompt.GetLocalizedString(new { liquid = liquidName, key = cachedInteractKey.ToString() })
                            : GetDefaultPrompt("FillDefault", liquidName, cachedInteractKey.ToString());
                        currentPrompt += $"{fillStr}\n";

                        if (Input.GetKey(cachedInteractKey))
                        {
                            cachedWaterCoolerTap.FillContainer(activeSlot);
                        }
                        else
                        {
                            cachedWaterCoolerTap.StopFilling();
                        }
                    }
                }
                else
                {
                    string msg = (cachedWaterCoolerTap.locNeedEmptyContainer != null && !cachedWaterCoolerTap.locNeedEmptyContainer.IsEmpty)
                        ? cachedWaterCoolerTap.locNeedEmptyContainer.GetLocalizedString()
                        : GetDefaultPrompt("NeedEmptyContainer");
                    currentPrompt += $"{msg}\n";
                    cachedWaterCoolerTap.StopFilling();
                }

                // 2. Логика питья (F)
                if (isCoolerEmpty)
                {
                    if (!hasContainer)
                    {
                        string msg = (cachedWaterCoolerTap.locEmpty != null && !cachedWaterCoolerTap.locEmpty.IsEmpty)
                            ? cachedWaterCoolerTap.locEmpty.GetLocalizedString()
                            : GetDefaultPrompt("Empty");
                        currentPrompt += $"{msg}\n";
                    }
                    cachedWaterCoolerTap.StopDrinking();
                }
                else if (!cachedWaterCoolerTap.drinkableLiquids.Contains(cachedWaterCoolerTap.controller.currentLiquidType))
                {
                    cachedWaterCoolerTap.StopDrinking();
                }
                else if (PlayerStats.Instance != null && PlayerStats.Instance.currentThirst >= PlayerStats.Instance.maxThirst)
                {
                    string msg = (cachedWaterCoolerTap.locNotThirsty != null && !cachedWaterCoolerTap.locNotThirsty.IsEmpty)
                        ? cachedWaterCoolerTap.locNotThirsty.GetLocalizedString()
                        : GetDefaultPrompt("NotThirsty");
                    currentPrompt += $"{msg}\n";
                    cachedWaterCoolerTap.StopDrinking();
                }
                else
                {
                    string liquidName = GetLocalizedLiquidName(cachedWaterCoolerTap.controller.currentLiquidType);
                    string drinkStr = (cachedWaterCoolerTap.drinkPrompt != null && !cachedWaterCoolerTap.drinkPrompt.IsEmpty) 
                        ? cachedWaterCoolerTap.drinkPrompt.GetLocalizedString(new { liquid = liquidName, key = cachedToggleKey.ToString() })
                        : GetDefaultPrompt("DrinkDefault", liquidName, cachedToggleKey.ToString());
                    currentPrompt += $"{drinkStr}\n";

                    if (Input.GetKey(cachedToggleKey))
                    {
                        cachedWaterCoolerTap.DrinkWater();
                    }
                    else
                    {
                        cachedWaterCoolerTap.StopDrinking();
                    }
                }
            }
            else if (cachedSinkTap != null)
            {
                foundSomething = true;

                if (currentLookSinkTap != cachedSinkTap)
                {
                    RemoveHighlight();
                    currentLookSinkTap = cachedSinkTap;
                    currentLookSinkTap.SetHighlight(true);
                }

                // 1. Включение/выключение крана (E)
                string prompt = cachedSinkTap.GetInteractPrompt(cachedInteractKey.ToString());
                currentPrompt += $"{prompt}\n";

                if (Input.GetKeyDown(cachedInteractKey))
                {
                    cachedSinkTap.ToggleTap();
                    animator?.SetTrigger(InteractHash);
                }

                // 2. Набор воды из раковины (F)
                InventorySlot activeSlot = null;
                if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                {
                    int index = InventoryManager.Instance.selectedSlotIndex;
                    if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                    {
                        activeSlot = InventoryManager.Instance.hotbarSlots[index];
                    }
                }

                bool hasContainer = activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null && 
                    activeSlot.itemData.isConsumable && 
                    (activeSlot.itemData.consumableType == ConsumableType.LiquidContainer || activeSlot.itemData.consumableType == ConsumableType.LampOil);

                if (hasContainer && cachedSinkTap.currentSinkWater > 0.01f && cachedSinkTap.tankController != null && cachedSinkTap.tankController.currentLiquidType != LiquidType.None)
                {
                    InventoryItemData data = activeSlot.itemData;
                    if (data.currentAmount >= data.maxAmount)
                    {
                        string msg = GetDefaultPrompt("ContainerFull");
                        currentPrompt += $"{msg}\n";
                    }
                    else if (data.currentLiquidType != LiquidType.None && data.currentLiquidType != cachedSinkTap.tankController.currentLiquidType)
                    {
                        string liquidName = GetLocalizedLiquidName(data.currentLiquidType);
                        string msg = GetDefaultPrompt("ContainerDifferent", liquidName);
                        currentPrompt += $"{msg}\n";
                    }
                    else
                    {
                        string scoopPromptText = cachedSinkTap.GetScoopPrompt(cachedToggleKey.ToString());
                        currentPrompt += $"{scoopPromptText}\n";

                        if (Input.GetKeyDown(cachedToggleKey))
                        {
                            cachedSinkTap.ScoopWater(activeSlot);
                            animator?.SetTrigger(InteractHash);
                        }
                    }
                }
            }
            else if (cachedLiquidSource != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    foundSomething = true;

                    if (currentLookLiquidSource != cachedLiquidSource)
                    {
                        RemoveHighlight();
                        currentLookLiquidSource = cachedLiquidSource;
                        currentLookLiquidSource.SetHighlight(true);
                    }

                    // Check active slot container eligibility
                    InventorySlot activeSlot = null;
                    if (InventoryManager.Instance != null && InventoryManager.Instance.hotbarSlots != null)
                    {
                        int index = InventoryManager.Instance.selectedSlotIndex;
                        if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                        {
                            activeSlot = InventoryManager.Instance.hotbarSlots[index];
                        }
                    }

                    string srcName = (cachedLiquidSource.sourceName != null && !cachedLiquidSource.sourceName.IsEmpty) 
                        ? cachedLiquidSource.sourceName.GetLocalizedString() 
                        : GetLocalizedLiquidName(cachedLiquidSource.liquidType);

                    if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null && 
                        activeSlot.itemData.isConsumable && 
                        (activeSlot.itemData.consumableType == ConsumableType.LampOil || activeSlot.itemData.consumableType == ConsumableType.LiquidContainer))
                    {
                        // Player has a container
                        InventoryItemData containerData = activeSlot.itemData;
                        if (containerData.currentAmount >= containerData.maxAmount)
                        {
                            currentPrompt += "Сосуд полон\n";
                        }
                        else if (containerData.currentLiquidType != LiquidType.None && containerData.currentLiquidType != cachedLiquidSource.liquidType)
                        {
                            string liquidNameRu = GetLocalizedLiquidName(containerData.currentLiquidType);
                            currentPrompt += $"Сосуд содержит другую жидкость ({liquidNameRu})\n";
                        }
                        else
                        {
                            currentPrompt += $"<color=#FFD700>[{cachedInteractKey}]</color> Набрать {srcName}\n";

                            if (Input.GetKeyDown(cachedInteractKey))
                            {
                                FillContainer(activeSlot, cachedLiquidSource);
                                animator?.SetTrigger(InteractHash);
                            }
                        }
                    }
                    else
                    {
                        // Player does not have a container
                        currentPrompt += $"Нужен пустой сосуд для {srcName}\n";
                    }
                }
            }
            else if (cachedItemSource != null)
            {
                if (NPCCorpse.carriedCorpse != null)
                {
                    RemoveHighlight();
                }
                else
                {
                    foundSomething = true;

                    if (currentLookItemSource != cachedItemSource)
                    {
                        RemoveHighlight();
                        currentLookItemSource = cachedItemSource;
                        currentLookItemSource.SetHighlight(true);
                    }

                    string actionName = cachedItemSource.GetActionName();
                    string srcName = cachedItemSource.GetSourceName();

                    if (!cachedItemSource.isInfinite && cachedItemSource.remainingUses <= 0)
                    {
                        string emptyMsg = cachedItemSource.GetEmptySourceMessage();
                        currentPrompt += $"{srcName}\n<color=#FF4444>{emptyMsg}</color>\n";
                    }
                    else
                    {
                        bool canFit = cachedItemSource.CanDispense();

                        if (!canFit)
                        {
                            string fullMsg = cachedItemSource.GetInventoryFullMessage();
                            currentPrompt += $"{srcName}\n<color=#FF4444>{fullMsg}</color>\n";
                        }
                        else
                        {
                            currentPrompt += $"{srcName}\n<color=#FFD700>[{cachedInteractKey}]</color> {actionName}\n";

                            if (Input.GetKeyDown(cachedInteractKey))
                            {
                                if (cachedItemSource.TryDispense())
                                {
                                    animator?.SetTrigger(InteractHash);
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (currentHoveredDoor != null)
                {
                    currentHoveredDoor.SetHover(false);
                    currentHoveredDoor = null;
                }

                if (cachedItem != null)
                {
                    if (NPCCorpse.carriedCorpse != null)
                    {
                        RemoveHighlight();
                    }
                    else
                    {
                        BoilingPot pot = cachedItem.GetComponent<BoilingPot>();
                        if (pot == null) pot = cachedItem.GetComponentInChildren<BoilingPot>();

                        FryingPan pan = cachedItem.GetComponent<FryingPan>();
                        if (pan == null) pan = cachedItem.GetComponentInChildren<FryingPan>();
                        
                        bool canPutDumplings = false;
                        string dumplingMeatType = "";
                        bool blockPickup = false;
                        bool canPlateDumplings = false;
                        bool canBowlDumplings = false;

                        if (pot != null && InventoryManager.Instance != null)
                        {
                            // 1. Проверяем, можно ли засыпать сырые пельмени
                            int index = InventoryManager.Instance.selectedSlotIndex;
                            if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                            {
                                InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[index];
                                if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null)
                                {
                                    canPutDumplings = pot.CanAcceptDumplings(activeSlot.itemData.itemID, out dumplingMeatType);
                                    
                                    // Проверяем, держит ли игрок тарелку для сбора готовых пельменей
                                    if (pot.AreDumplingsCooked() && activeSlot.itemData.itemID == pot.plateItemID)
                                    {
                                        canPlateDumplings = true;
                                    }
                                }
                            }

                            // Если в кастрюле есть любые пельмени, блокируем обычный подбор кастрюли
                            if (pot.HasAnyDumplings())
                            {
                                blockPickup = true;
                            }
                        }
                        else if (pan != null && InventoryManager.Instance != null)
                        {
                            // 1. Проверяем, можно ли засыпать сырые пельмени в сковороду
                            int index = InventoryManager.Instance.selectedSlotIndex;
                            if (index >= 0 && index < InventoryManager.Instance.hotbarSlots.Length)
                            {
                                InventorySlot activeSlot = InventoryManager.Instance.hotbarSlots[index];
                                if (activeSlot != null && !activeSlot.IsEmpty() && activeSlot.itemData != null)
                                {
                                    canPutDumplings = pan.CanAcceptDumplings(activeSlot.itemData.itemID, out dumplingMeatType);
                                    
                                    // Проверяем, держит ли игрок миску для сбора готовых пельменей
                                    if (pan.AreDumplingsCooked() && activeSlot.itemData.itemID == pan.bowlItemID)
                                    {
                                        canBowlDumplings = true;
                                    }
                                }
                            }

                            // Если в сковороде есть любые пельмени, блокируем обычный подбор
                            if (pan.HasAnyDumplings())
                            {
                                blockPickup = true;
                            }
                        }

                        // Действие по ЛКМ: Засыпать пельмени
                        if (canPutDumplings)
                        {
                            currentPrompt += "<color=#FFD700>[ЛКМ]</color> Положить пельмени\n";
                            foundSomething = true;

                            if (Input.GetKeyDown(KeyCode.Mouse0))
                            {
                                if (pot != null)
                                {
                                    pot.AddDumplings(dumplingMeatType);
                                }
                                else if (pan != null)
                                {
                                    pan.AddDumplings(dumplingMeatType);
                                }
                                InventoryManager.Instance.ConsumeItemInActiveSlot();
                                animator?.SetTrigger(InteractHash);
                            }
                        }

                        // Подсветка предмета
                        if (currentLookItem != cachedItem)
                        {
                            RemoveHighlight();
                            currentLookItem = cachedItem;
                            currentLookItem.SetHighlight(true);
                        }

                        // Действие по клавише взаимодействия (E)
                        KeyCode pickupKey = cachedItem.isPlacedOnSnapPoint ? cachedToggleKey : cachedInteractKey;

                        if (blockPickup)
                        {
                            foundSomething = true;
                            if (pot != null)
                            {
                                if (pot.AreDumplingsCooked())
                                {
                                    if (canPlateDumplings)
                                    {
                                        currentPrompt += $"<color=#FFD700>[{pickupKey}]</color> Наложить пельмени\n";
                                        if (Input.GetKeyDown(pickupKey))
                                        {
                                            pot.PlateDumplings();
                                            InventoryManager.Instance.ConsumeItemInActiveSlot(); // Тратим тарелку из рук
                                            animator?.SetTrigger(InteractHash);
                                        }
                                    }
                                    else
                                    {
                                        currentPrompt += $"<color=#A0A0A0>[{pickupKey}] Нужна тарелка</color>\n";
                                    }
                                }
                                else
                                {
                                    currentPrompt += "<color=#A0A0A0>Пельмени варятся...</color>\n";
                                }
                            }
                            else if (pan != null)
                            {
                                if (pan.AreDumplingsCooked())
                                {
                                    if (canBowlDumplings)
                                    {
                                        currentPrompt += $"<color=#FFD700>[{pickupKey}]</color> Забрать в миску\n";
                                        if (Input.GetKeyDown(pickupKey))
                                        {
                                            pan.PlateDumplings();
                                            InventoryManager.Instance.ConsumeItemInActiveSlot(); // Тратим миску из рук
                                            animator?.SetTrigger(InteractHash);
                                        }
                                    }
                                    else
                                    {
                                        currentPrompt += $"<color=#A0A0A0>[{pickupKey}] Нужна миска</color>\n";
                                    }
                                }
                                else
                                {
                                    currentPrompt += "<color=#A0A0A0>Пельмени жарятся...</color>\n";
                                }
                            }
                        }
                        else
                        {
                            // Обычный подбор предмета (когда он пустой)
                            string pickupStr = (actions != null && actions.pickUpAction != null && !actions.pickUpAction.IsEmpty) ? actions.pickUpAction.GetLocalizedString() : "Взять:";
                            currentPrompt += $"<color=#FFD700>[{pickupKey}]</color> {pickupStr} {cachedItem.itemName}\n";
                            foundSomething = true;

                            if (Input.GetKeyDown(pickupKey))
                            {
                                RemoveHighlight();
                                cachedItem.PickUp();
                                animator?.SetTrigger(InteractHash);
                            }
                        }
                    }
                }
                else if (cachedToggleDevice != null && cachedToggleDevice.isSwitch)
                {
                    if (currentLookToggleDevice != cachedToggleDevice)
                    {
                        RemoveHighlight();
                        currentLookToggleDevice = cachedToggleDevice;
                        currentLookToggleDevice.SetHighlight(true);
                    }
                }
                else
                {
                    RemoveHighlight();
                }

                if (cachedToggleDevice != null)
                {
                    KeyCode activeKey = cachedToggleDevice.isSwitch ? cachedInteractKey : cachedToggleKey;
                    
                    string toggleStr = (actions != null && actions.toggle != null && !actions.toggle.IsEmpty) ? actions.toggle.GetLocalizedString() : cachedToggleDevice.promptText;
                    currentPrompt += $"<color=#FFD700>[{activeKey}]</color> {toggleStr}\n";
                    foundSomething = true;

                    if (Input.GetKeyDown(activeKey))
                    {
                        cachedToggleDevice.Toggle();
                        animator?.SetTrigger(InteractHash);
                    }
                }
            }
        }
        else
        {
            // Нет хита — сбрасываем кэш
            lastHitCollider = null;
            RemoveHighlight();

            if (currentHoveredDoor != null)
            {
                currentHoveredDoor.SetHover(false);
                currentHoveredDoor = null;
            }
        }

        if (foundSomething)
        {
            ShowText(currentPrompt);
        }
        else if (interactText != null)
        {
            interactText.gameObject.SetActive(false);
        }
    }
}
