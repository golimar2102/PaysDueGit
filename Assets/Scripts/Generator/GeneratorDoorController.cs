using UnityEngine;
using System.Collections;

public class GeneratorDoorController : MonoBehaviour
{
    public static GeneratorDoorController activeGeneratorDoor = null;

    [Header("Компоненты двери")]
    [Tooltip("Трансформ самой дверцы, которая будет двигаться/поворачиваться")]
    public Transform doorTransform;

    [Tooltip("Точка камеры, куда прилетает взгляд игрока (настроить руками)")]
    public Transform cameraPoint;

    [Header("Настройки вращения двери")]
    [Tooltip("Углы вращения в открытом состоянии")]
    public Vector3 openAngleOffset = new Vector3(0f, 90f, 0f);
    [Tooltip("Углы вращения в закрытом состоянии")]
    public Vector3 closeAngleOffset = Vector3.zero;

    [Header("Настройки смещения положения двери (Опционально)")]
    [Tooltip("Включить изменение позиции двери (для раздвижных дверей)")]
    public bool usePositionTransition = false;
    [Tooltip("Локальная позиция в открытом состоянии")]
    public Vector3 openPositionOffset;
    [Tooltip("Локальная позиция в закрытом состоянии")]
    public Vector3 closePositionOffset;

    [Header("Состояние двери")]
    public bool isOpened = false;
    [Tooltip("Закрывать ли дверцу автоматически при выходе из режима просмотра?")]
    public bool closeDoorOnExit = false;

    [Header("Настройки дёрганного эффекта (FNaF style)")]
    [Tooltip("Включить дёрганье и ступенчатый (покадровый) перелёт камеры. Если снято, перелёт будет плавным.")]
    public bool useJerkyTransition = false;
    [Tooltip("Длительность перелёта камеры")]
    public float cameraTransitionDuration = 0.2f;
    [Tooltip("Длительность открытия двери")]
    public float doorOpenDuration = 0.15f;
    [Tooltip("Количество кадров/шагов для ступенчатого перелёта (актуально только при включенном Jerky Transition)")]
    public int transitionStepsCount = 5;
    [Tooltip("Интенсивность тряски/дёрганья камеры при перелёте (актуально только при включенном Jerky Transition)")]
    public float cameraJitter = 0.05f;
    [Tooltip("Включить дёрганье/вибрацию самой дверцы при движении")]
    public bool useDoorJitter = true;
    [Tooltip("Интенсивность дёрганья дверцы при движении (актуально только при включенном Door Jitter)")]
    public float doorJitter = 0.03f;

    [Header("Скрытие интерфейса")]
    [Tooltip("Элементы UI, которые будут автоматически скрыты при просмотре (например, Canvas с HUD, прицел, индикаторы)")]
    public GameObject[] objectsToHide;

    [Header("Освещение генератора")]
    [Tooltip("Свет или игровой объект со светом, который включается при открытии двери и выключается при выходе")]
    public GameObject[] generatorLight;

    [Header("Настройки смещения инвентаря")]
    [Tooltip("Показывать ли инвентарь при взаимодействии с щитком?")]
    public bool showInventory = false;
    [Tooltip("Смещение инвентаря на экране")]
    public Vector2 inventoryOffsetPosition = new Vector2(-300f, 0f);
    [Tooltip("Масштаб инвентаря")]
    public Vector3 inventoryScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);
    [Tooltip("Смещение хотбара на экране")]
    public Vector2 hotbarOffsetPosition = new Vector2(-300f, 25f);
    [Tooltip("Масштаб хотбара")]
    public Vector3 hotbarScaleMultiplier = new Vector3(0.8f, 0.8f, 1f);

    [Header("Покачивание камеры (Idle Sway)")]
    [Tooltip("Включить легкое плавное покачивание камеры во время просмотра")]
    public bool useCameraSway = true;
    [Tooltip("Скорость покачивания камеры")]
    public float swaySpeed = 1.5f;
    [Tooltip("Амплитуда покачивания (смещения) камеры")]
    public float swayAmount = 0.02f;
    [Tooltip("Амплитуда покачивания (поворота) камеры")]
    public float swayRotationAmount = 0.2f;

    [Header("Звуки")]
    [Tooltip("Звук открытия дверцы")]
    public AudioSource openSound;
    [Tooltip("Звук закрытия дверцы")]
    public AudioSource closeSound;

    [Header("Подсветка")]
    [Tooltip("Компонент Outline для подсветки двери при наведении")]
    public Outline outline;

    [Header("Связанный генератор")]
    [Tooltip("Ссылка на контроллер генератора (если пусто, ищет в родителях/дочерних)")]
    public GeneratorController generator;

    // Состояние взаимодействия
    private bool isViewing = false;
    private bool isTransitioning = false;

    // Сохранение состояния камеры и игрока
    private Transform playerCameraTransform;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private bool isCameraLocked = false;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    private KeyCode interactKey = KeyCode.E;
    private float swayTime = 0f;

    private RectTransform inventoryRect;
    private Vector2 originalInventoryPos;
    private Vector3 originalInventoryScale;
    private RectTransform hotbarRect;

    void Awake()
    {
        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }
        if (outline != null)
        {
            outline.enabled = false;
        }
    }

    void Start()
    {
        if (generator == null)
        {
            generator = GetComponentInParent<GeneratorController>();
        }
        if (generator == null)
        {
            generator = GetComponentInChildren<GeneratorController>(true);
        }

        // Инициализируем начальное положение двери
        if (doorTransform != null)
        {
            doorTransform.localEulerAngles = isOpened ? openAngleOffset : closeAngleOffset;
            if (usePositionTransition)
            {
                doorTransform.localPosition = isOpened ? openPositionOffset : closePositionOffset;
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
        if (activeGeneratorDoor == this)
        {
            activeGeneratorDoor = null;
        }
    }

    private void RefreshKeyBindings()
    {
        interactKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
    }

    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }

    public void Interact(Camera playerCam)
    {
        if (isTransitioning) return;

        if (!isViewing)
        {
            StartCoroutine(EnterViewRoutine(playerCam));
        }
    }

    void Update()
    {
        if (isViewing && !isTransitioning)
        {
            // Если открыт инвентарь, и игрок закрыл его вручную (например, нажал Tab)
            if (showInventory && InventoryManager.Instance != null && !InventoryManager.Instance.isOpen)
            {
                StartCoroutine(ExitViewRoutine());
                return;
            }

            // Выход из просмотра по нажатию Esc или кнопки взаимодействия
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(interactKey))
            {
                StartCoroutine(ExitViewRoutine());
            }
        }
    }

    void LateUpdate()
    {
        if (isCameraLocked && playerCameraTransform != null && cameraPoint != null)
        {
            Vector3 targetPos = cameraPoint.position;
            Quaternion targetRot = cameraPoint.rotation;

            if (useCameraSway)
            {
                swayTime += Time.deltaTime * swaySpeed;
                
                // Рассчитываем плавное смещение по осям X и Y сдвига и Z вращения
                float offsetX = Mathf.Sin(swayTime) * swayAmount;
                float offsetY = Mathf.Cos(swayTime * 1.5f) * swayAmount;
                Vector3 swayOffset = cameraPoint.right * offsetX + cameraPoint.up * offsetY;

                float rotateZ = Mathf.Sin(swayTime * 0.8f) * swayRotationAmount;
                Quaternion swayRot = Quaternion.Euler(0f, 0f, rotateZ);

                playerCameraTransform.position = targetPos + swayOffset;
                playerCameraTransform.rotation = targetRot * swayRot;
            }
            else
            {
                playerCameraTransform.position = targetPos;
                playerCameraTransform.rotation = targetRot;
            }
        }
    }

    private IEnumerator EnterViewRoutine(Camera playerCam)
    {
        isTransitioning = true;
        isViewing = true;
        activeGeneratorDoor = this;

        // Сохраняем ссылки
        playerCameraTransform = playerCam.transform;
        originalCameraParent = playerCameraTransform.parent;
        originalCameraLocalPos = playerCameraTransform.localPosition;
        originalCameraLocalRot = playerCameraTransform.localRotation;

        // Находим и отключаем управление игроком
        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();
        
        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // 1. Скрываем оружие и предметы в руках
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(false);
        }

        // 2. Скрываем указанные элементы UI
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // Выключаем подсветку
        SetHighlight(false);

        // 3. Переносим/анимируем камеру к точке
        yield return StartCoroutine(AnimateCamera(playerCameraTransform.position, playerCameraTransform.rotation, cameraPoint.position, cameraPoint.rotation));

        isCameraLocked = true;
        swayTime = 0f;

        // Включаем свет при открытии
        if (generatorLight != null)
        {
            foreach (GameObject obj in generatorLight)
            {
                obj.SetActive(true);
            }
        }

        // Позиционируем инвентарь, если включено
        if (showInventory && InventoryManager.Instance != null)
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

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 4. Открываем дверцу
        isOpened = true;
        yield return StartCoroutine(AnimateDoor(true));

        isTransitioning = false;
    }

    private IEnumerator ExitViewRoutine()
    {
        isTransitioning = true;
        isCameraLocked = false;

        // Возвращаем канистру игроку, если она была установлена
        if (generator != null && generator.hasCanister)
        {
            InventoryItemData canisterData = generator.ExtractCanister();
            if (canisterData != null)
            {
                if (InventoryManager.Instance != null)
                {
                    bool added = InventoryManager.Instance.AddItem(canisterData);
                    if (!added)
                    {
                        InventoryManager.Instance.SpawnDroppedItem(canisterData);
                    }
                }
            }
        }

        // Выключаем свет при отдалении
        if (generatorLight != null)
        {
            foreach (GameObject obj in generatorLight)
            {
                obj.SetActive(false);
            }
        }

        // 1. Закрываем дверцу, если включена опция закрытия при выходе
        if (closeDoorOnExit)
        {
            isOpened = false;
            yield return StartCoroutine(AnimateDoor(false));
        }

        // Вычисляем целевую позицию возврата камеры
        Vector3 targetWorldPos = originalCameraParent != null ? originalCameraParent.TransformPoint(originalCameraLocalPos) : originalCameraLocalPos;
        Quaternion targetWorldRot = originalCameraParent != null ? originalCameraParent.rotation * originalCameraLocalRot : originalCameraLocalRot;

        // 2. Возвращаем камеру назад к игроку
        yield return StartCoroutine(AnimateCamera(playerCameraTransform.position, playerCameraTransform.rotation, targetWorldPos, targetWorldRot));

        // Возвращаем камеру в локальные координаты родителя
        playerCameraTransform.localPosition = originalCameraLocalPos;
        playerCameraTransform.localRotation = originalCameraLocalRot;

        // Восстанавливаем инвентарь на исходную позицию
        if (showInventory)
        {
            if (inventoryRect != null)
            {
                inventoryRect.anchoredPosition = originalInventoryPos;
                inventoryRect.localScale = originalInventoryScale;
            }

            if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
            {
                InventoryManager.Instance.ToggleInventory();
            }
        }

        // 3. Возвращаем видимость предметов в руках
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(true);
        }

        // 4. Показываем скрытые элементы UI обратно
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        // Включаем управление игроком обратно
        if (playerMovement != null)
        {
            playerMovement.enabled = true;
        }
        if (mouseLook != null)
        {
            mouseLook.enabled = true;
        }

        isViewing = false;
        activeGeneratorDoor = null;
        isTransitioning = false;
    }

    private IEnumerator AnimateCamera(Vector3 startPos, Quaternion startRot, Vector3 targetPos, Quaternion targetRot)
    {
        float elapsed = 0f;

        if (useJerkyTransition)
        {
            // Ступенчатый перелёт с тряской
            int steps = transitionStepsCount > 0 ? transitionStepsCount : 5;
            float stepDuration = cameraTransitionDuration / steps;

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;

                // Добавляем дёрганье (jitter)
                Vector3 jitterPos = Vector3.zero;
                Quaternion jitterRot = Quaternion.identity;
                if (i < steps && cameraJitter > 0f)
                {
                    jitterPos = Random.insideUnitSphere * cameraJitter;
                    jitterRot = Quaternion.Euler(
                        Random.Range(-cameraJitter * 100f, cameraJitter * 100f),
                        Random.Range(-cameraJitter * 100f, cameraJitter * 100f),
                        Random.Range(-cameraJitter * 100f, cameraJitter * 100f)
                    );
                }

                playerCameraTransform.position = Vector3.Lerp(startPos, targetPos, t) + jitterPos;
                playerCameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, t) * jitterRot;

                yield return new WaitForSeconds(stepDuration);
            }
        }
        else
        {
            // Плавный переход с использованием SmoothStep (ускорение в начале, замедление в конце)
            while (elapsed < cameraTransitionDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cameraTransitionDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                playerCameraTransform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                playerCameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
                yield return null;
            }
        }

        playerCameraTransform.position = targetPos;
        playerCameraTransform.rotation = targetRot;
    }

    private IEnumerator AnimateDoor(bool open)
    {
        if (doorTransform == null) yield break;

        if (open && openSound != null) openSound.Play();
        if (!open && closeSound != null) closeSound.Play();

        float elapsed = 0f;
        Quaternion startRot = doorTransform.localRotation;
        Quaternion targetRot = open ? Quaternion.Euler(openAngleOffset) : Quaternion.Euler(closeAngleOffset);

        Vector3 startPos = doorTransform.localPosition;
        Vector3 targetPos = open ? openPositionOffset : closePositionOffset;

        if (doorOpenDuration > 0f)
        {
            while (elapsed < doorOpenDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / doorOpenDuration);

                float tTwitchy = t;
                if (useDoorJitter && t < 0.95f && doorJitter > 0f)
                {
                    float jitterNoise = Mathf.Sin(elapsed * 80f) * doorJitter;
                    tTwitchy = Mathf.Clamp01(t + jitterNoise);
                }
                else
                {
                    tTwitchy = Mathf.SmoothStep(0f, 1f, t);
                }

                doorTransform.localRotation = Quaternion.Slerp(startRot, targetRot, tTwitchy);
                if (usePositionTransition)
                {
                    doorTransform.localPosition = Vector3.Lerp(startPos, targetPos, tTwitchy);
                }

                yield return null;
            }
        }

        doorTransform.localRotation = targetRot;
        if (usePositionTransition)
        {
            doorTransform.localPosition = targetPos;
        }
    }
}
