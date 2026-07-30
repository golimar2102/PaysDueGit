using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Localization;

public class MeatGrinderController : MonoBehaviour
{
    [Header("Настройки камеры")]
    public Transform cameraTargetPos; 
    public float cameraMoveSpeed = 4f;

    public enum MeatGrinderTurnMode { ButtonPress, MouseDrag }

    [Header("Настройки ручки")]
    public MeatGrinderTurnMode turnMode = MeatGrinderTurnMode.ButtonPress;
    public Transform handleMesh; 
    public float turnAnglePerClick = 90f; 
    public int turnsRequiredForProcess = 4; 
    public float handleTurnSpeed = 10f;
    
    [Tooltip("Сколько градусов нужно прокрутить мышью для получения фарша (360 = 1 оборот)")]
    public float dragDegreesRequired = 1440f; 
    
    public float mouseDragSensitivity = 10f;
    public float handleFallbackSpeed = 5f;

    [System.Serializable]
    public class MeatRecipe
    {
        [Tooltip("ID предмета на входе (например, сырая свинина)")]
        public int inputItemID;
        [Tooltip("Префаб, который появится на выходе (например, фарш из свинины)")]
        public GameObject outputPrefab;
        [Tooltip("Количество предметов на выходе")]
        public int outputAmount = 1;
    }

    [Header("Рецепты переработки")]
    public Transform spawnPoint; 
    public MeatRecipe[] recipes;

    [Header("Смещение инвентаря")]
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Смещение Хотбара")]
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 0f);
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Перетащите сюда элементы UI инвентаря, которые нужно скрыть (например, окно экипировки)")]
    public GameObject[] objectsToHideDuringGrinding;

    [Header("UI Мясорубки")]
    public GameObject meatGrinderUIPanel; 
    public InventorySlot inputSlot; 
    public Image progressBar; 
    
    [Header("Эффекты")]
    public ParticleSystem grindParticles;
    
    [Tooltip("Текст 'Крутить'")]
    public TextMeshProUGUI promptText; 
    public LocalizedString turnPromptLoc;
    public LocalizedString dragPromptLoc;

    [Tooltip("Текст 'Выход'")]
    public TextMeshProUGUI exitPromptText;
    public LocalizedString exitPromptLoc;

    [Header("Текст взаимодействия (PlayerInteract)")]
    public LocalizedString interactPrompt;

    [HideInInspector]
    public bool isUsing = false;
    
    private int currentTurnCount = 0;
    private bool isTurning = false;
    private float accumulatedAngle = 0f;
    private bool isDraggingHandle = false;
    
    private Quaternion startDragRot;
    private Quaternion idleRestRotation;
    private Coroutine fallbackCoroutine;

    // Сохранение состояния камеры
    private Transform mainCamera;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraLocked = false;

    private Coroutine cameraCoroutine;
    private KeyCode interactKey = KeyCode.E;
    private KeyCode fireKey = KeyCode.Mouse0;
    private Outline outline;

    // Сохранение состояния инвентаря
    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;
    
    private RectTransform hotbarRect;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    void Start()
    {
        if (handleMesh != null)
        {
            idleRestRotation = handleMesh.localRotation;
        }

        if (meatGrinderUIPanel != null)
        {
            meatGrinderUIPanel.SetActive(false);
        }
        
        if (inputSlot != null && recipes != null)
        {
            inputSlot.isSpecialSlot = true;
            inputSlot.acceptedItemIDs = new int[recipes.Length];
            for(int i = 0; i < recipes.Length; i++)
            {
                inputSlot.acceptedItemIDs[i] = recipes[i].inputItemID;
            }
        }

        RefreshKeyBindings();
    }

    void OnEnable()
    {
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
    }

    void OnDisable()
    {
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
    }

    private void RefreshKeyBindings()
    {
        interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
        fireKey = (KeyCode)PlayerPrefs.GetInt("Key_Fire", (int)KeyCode.Mouse0);
        
        UpdatePromptTexts();
    }

    private void UpdatePromptTexts()
    {
        KeyCode inventoryKey = (KeyCode)PlayerPrefs.GetInt("Key_Inventory", (int)KeyCode.Tab);
        
        if (promptText != null) 
        {
            if (turnMode == MeatGrinderTurnMode.ButtonPress)
            {
                string turnStr = (turnPromptLoc != null && !turnPromptLoc.IsEmpty) ? turnPromptLoc.GetLocalizedString() : "Крутить";
                promptText.text = $"{turnStr}: [{interactKey}]";
            }
            else
            {
                string dragStr = (dragPromptLoc != null && !dragPromptLoc.IsEmpty) ? dragPromptLoc.GetLocalizedString() : "Тянуть";
                promptText.text = $"{dragStr}: [ЛКМ]";
            }
        }

        if (exitPromptText != null)
        {
            string exitStr = (exitPromptLoc != null && !exitPromptLoc.IsEmpty) ? exitPromptLoc.GetLocalizedString() : "Выход";
            exitPromptText.text = $"{exitStr}: [{inventoryKey}] / [Esc]";
        }
    }

    private void SetParticlesActive(bool active)
    {
        if (grindParticles == null) return;
        
        if (active && !grindParticles.isPlaying)
        {
            grindParticles.Play();
        }
        else if (!active && grindParticles.isPlaying)
        {
            grindParticles.Stop();
        }
    }

    void Update()
    {
        if (isUsing)
        {
            // Выход из режима, если игрок закрыл инвентарь (например, нажал Tab)
            if (InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                ExitMeatGrinderMode();
                return;
            }

            // Логика прокрутки
            if (turnMode == MeatGrinderTurnMode.ButtonPress)
            {
                if (Input.GetKeyDown(interactKey) && !isTurning)
                {
                    StartCoroutine(TurnHandleRoutine());
                    if (inputSlot == null || inputSlot.isEmpty)
                    {
                        if (inputSlot != null) inputSlot.OnPointerClick(null);
                    }
                }
            }
            else if (turnMode == MeatGrinderTurnMode.MouseDrag)
            {
                if (Input.GetKeyDown(fireKey))
                {
                    if (InventorySlot.hoveredSlot != null)
                    {
                        // Кликнули по слоту инвентаря (например, берем предмет) - не блокируем курсор
                    }
                    else
                    {
                        Cursor.lockState = CursorLockMode.Locked;
                        Cursor.visible = false;
                        isDraggingHandle = true;
                        
                        if (fallbackCoroutine != null) StopCoroutine(fallbackCoroutine);
                        startDragRot = handleMesh.localRotation;
                        accumulatedAngle = 0f;
                        if (progressBar != null) progressBar.fillAmount = 0f;
                        
                        if (inputSlot == null || inputSlot.isEmpty)
                        {
                            if (inputSlot != null) inputSlot.OnPointerClick(null);
                        }
                    }
                }
                else if (Input.GetKeyUp(fireKey))
                {
                    if (isDraggingHandle)
                    {
                        Cursor.lockState = CursorLockMode.None;
                        Cursor.visible = true;
                        isDraggingHandle = false;
                        SetParticlesActive(false);
                        
                        bool hasMeat = inputSlot != null && !inputSlot.isEmpty;
                        
                        if (hasMeat)
                        {
                            float totalRequired = dragDegreesRequired;
                            if (accumulatedAngle > 0f && accumulatedAngle < totalRequired)
                            {
                                fallbackCoroutine = StartCoroutine(HandleFallbackRoutine(startDragRot));
                            }
                        }
                        else
                        {
                            // Пустая мясорубка: ручка падает под силой тяжести в начальное положение
                            fallbackCoroutine = StartCoroutine(HandleFallbackRoutine(idleRestRotation));
                        }
                    }
                }

                if (isDraggingHandle && Input.GetKey(fireKey))
                {
                    float mouseDelta = Input.GetAxis("Mouse X") + Input.GetAxis("Mouse Y");
                    float deltaAngle = mouseDelta * mouseDragSensitivity;
                    
                    if (deltaAngle > 0)
                    {
                        handleMesh.Rotate(0, deltaAngle, 0, Space.Self);
                        
                        bool hasMeat = inputSlot != null && !inputSlot.isEmpty;
                        
                        if (hasMeat)
                        {
                            SetParticlesActive(true);
                            accumulatedAngle += deltaAngle;
                            
                            float totalRequired = dragDegreesRequired;
                            
                            if (progressBar != null) 
                            {
                                float ratio = accumulatedAngle / totalRequired;
                                progressBar.fillAmount = ratio;
                            }
                            
                            if (accumulatedAngle >= totalRequired)
                            {
                                accumulatedAngle -= totalRequired;
                                ProcessMeat();
                                
                                startDragRot = handleMesh.localRotation;
                                if (progressBar != null) progressBar.fillAmount = 0f;
                            }
                        }
                        else
                        {
                            SetParticlesActive(false);
                            // Если прогресс остался от предыдущего куска, сбрасываем
                            accumulatedAngle = 0f;
                            if (progressBar != null) progressBar.fillAmount = 0f;
                        }
                    }
                    else
                    {
                        SetParticlesActive(false);
                    }
                }
            }
        }
    }

    void LateUpdate()
    {
        if (isCameraLocked && mainCamera != null && cameraTargetPos != null)
        {
            mainCamera.position = cameraTargetPos.position;
            mainCamera.rotation = cameraTargetPos.rotation;
        }
    }

    public void EnterMeatGrinderMode(Camera playerCam)
    {
        if (isUsing) return;
        isUsing = true;

        if (progressBar != null) progressBar.fillAmount = 0f;
        accumulatedAngle = 0f;
        currentTurnCount = 0;
        SetParticlesActive(false);

        UpdatePromptTexts();

        mainCamera = playerCam.transform;

        originalCameraParent = mainCamera.parent;
        originalCameraLocalPos = mainCamera.localPosition;
        originalCameraLocalRot = mainCamera.localRotation;

        if (meatGrinderUIPanel != null) meatGrinderUIPanel.SetActive(true);

        // Открываем инвентарь, если он закрыт
        if (InventoryManager.Instance != null)
        {
            if (!InventoryManager.Instance.isOpen)
            {
                InventoryManager.Instance.ToggleInventory(); 
            }

            InventoryManager.Instance.HaltHotbarAnimation();

            // Манипуляция UI инвентаря
            if (InventoryManager.Instance.inventoryUI != null)
            {
                inventoryRect = InventoryManager.Instance.inventoryUI.GetComponent<RectTransform>();
                if (inventoryRect != null)
                {
                    originalInventoryPos = inventoryRect.anchoredPosition;
                    originalInventoryScale = inventoryRect.localScale;

                    inventoryRect.anchoredPosition += inventoryOffsetPosition;
                    inventoryRect.localScale = new Vector3(
                        originalInventoryScale.x * inventoryScaleMultiplier.x,
                        originalInventoryScale.y * inventoryScaleMultiplier.y,
                        originalInventoryScale.z * inventoryScaleMultiplier.z
                    );
                }
            }

            if (InventoryManager.Instance.hotbarPanel != null)
            {
                hotbarRect = InventoryManager.Instance.hotbarPanel;
                if (hotbarRect != null)
                {
                    hotbarRect.anchoredPosition += hotbarOffsetPosition;
                    hotbarRect.localScale = new Vector3(
                        hotbarRect.localScale.x * hotbarScaleMultiplier.x,
                        hotbarRect.localScale.y * hotbarScaleMultiplier.y,
                        hotbarRect.localScale.z * hotbarScaleMultiplier.z
                    );
                }
            }

            if (objectsToHideDuringGrinding != null)
            {
                foreach (GameObject obj in objectsToHideDuringGrinding)
                {
                    if (obj != null) obj.SetActive(false);
                }
            }
        }

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        cameraCoroutine = StartCoroutine(MoveCamera(cameraTargetPos.position, cameraTargetPos.rotation));
    }

    public void ExitMeatGrinderMode()
    {
        if (!isUsing) return;
        isUsing = false;

        if (meatGrinderUIPanel != null) meatGrinderUIPanel.SetActive(false);

        isDraggingHandle = false;
        SetParticlesActive(false);
        if (fallbackCoroutine != null) StopCoroutine(fallbackCoroutine);
        accumulatedAngle = 0f;
        if (progressBar != null) progressBar.fillAmount = 0f;

        // ВАЖНО: Сначала возвращаем UI на исходные позиции, 
        // чтобы InventoryManager при закрытии захватил правильные координаты!
        if (inventoryRect != null)
        {
            inventoryRect.anchoredPosition = originalInventoryPos;
            inventoryRect.localScale = originalInventoryScale;
        }

        // Хотбар восстанавливать вручную не нужно, так как InventoryManager.ToggleInventory 
        // сам жестко задает его позицию по центру.

        // Теперь закрываем инвентарь (сразу скрывает его, без мерцаний в центре экрана)
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
        {
            InventoryManager.Instance.ToggleInventory();
        }

        if (objectsToHideDuringGrinding != null)
        {
            foreach (GameObject obj in objectsToHideDuringGrinding)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        
        // Возвращаем камеру назад
        cameraCoroutine = StartCoroutine(MoveCameraBack());
    }

    private IEnumerator TurnHandleRoutine()
    {
        isTurning = true;
        bool hasMeat = inputSlot != null && !inputSlot.isEmpty;
        
        if (hasMeat) SetParticlesActive(true);
        float t = 0;
        Quaternion startRot = handleMesh.localRotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, turnAnglePerClick, 0); // Крутим по оси X, можно поменять если нужно

        while (t < 1f)
        {
            t += Time.deltaTime * handleTurnSpeed;
            handleMesh.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }
        handleMesh.localRotation = endRot;

        if (hasMeat)
        {
            currentTurnCount++;
            if (progressBar != null) 
            {
                float ratio = (float)currentTurnCount / turnsRequiredForProcess;
                progressBar.fillAmount = ratio;
            }

            if (currentTurnCount >= turnsRequiredForProcess)
            {
                currentTurnCount = 0;
                ProcessMeat();
                if (progressBar != null) progressBar.fillAmount = 0f;
            }
        }
        else
        {
            currentTurnCount = 0;
            if (progressBar != null) progressBar.fillAmount = 0f;
        }

        isTurning = false;
        SetParticlesActive(false);
    }

    private IEnumerator HandleFallbackRoutine(Quaternion targetRotation)
    {
        float t = 0f;
        Quaternion currentRot = handleMesh.localRotation;
        float startProgress = progressBar != null ? progressBar.fillAmount : 0f;
        
        while (t < 1f)
        {
            t += Time.deltaTime * handleFallbackSpeed;
            float smooth = Mathf.SmoothStep(0f, 1f, t);
            handleMesh.localRotation = Quaternion.Slerp(currentRot, targetRotation, smooth);
            if (progressBar != null) progressBar.fillAmount = Mathf.Lerp(startProgress, 0f, smooth);
            yield return null;
        }
        
        handleMesh.localRotation = targetRotation;
        if (progressBar != null) progressBar.fillAmount = 0f;
        accumulatedAngle = 0f;
    }

    private void ProcessMeat()
    {
        if (inputSlot == null || inputSlot.isEmpty) return;

        // Ищем рецепт
        GameObject prefabToSpawn = null;
        int countToSpawn = 1;
        if (recipes != null && inputSlot.itemData != null)
        {
            foreach (var recipe in recipes)
            {
                if (recipe.inputItemID == inputSlot.itemData.itemID)
                {
                    prefabToSpawn = recipe.outputPrefab;
                    countToSpawn = Mathf.Max(1, recipe.outputAmount);
                    break;
                }
            }
        }

        // Отнимаем 1 предмет
        if (inputSlot.itemData != null && inputSlot.itemData.amount > 1)
        {
            inputSlot.itemData.amount--;
            inputSlot.UpdateSlotUI();
        }
        else
        {
            inputSlot.ClearSlot();
        }

        // Спавним фарш
        if (prefabToSpawn != null && spawnPoint != null)
        {
            for (int i = 0; i < countToSpawn; i++)
            {
                Instantiate(prefabToSpawn, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot)
    {
        float t = 0;
        Vector3 startPos = mainCamera.position;
        Quaternion startRot = mainCamera.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCamera.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCamera.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            yield return null;
        }
        
        mainCamera.position = targetPos;
        mainCamera.rotation = targetRot;
        isCameraLocked = true;
    }

    private IEnumerator MoveCameraBack()
    {
        isCameraLocked = false;
        float t = 0;
        Vector3 startPos = mainCamera.localPosition;
        Quaternion startRot = mainCamera.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCamera.localPosition = Vector3.Lerp(startPos, originalCameraLocalPos, smoothT);
            mainCamera.localRotation = Quaternion.Slerp(startRot, originalCameraLocalRot, smoothT);
            yield return null;
        }
        
        mainCamera.localPosition = originalCameraLocalPos;
        mainCamera.localRotation = originalCameraLocalRot;
    }
}
