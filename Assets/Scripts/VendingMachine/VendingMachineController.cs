using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.Localization;

[System.Serializable]
public class VendingItemSlot
{
    [Tooltip("Код товара для ввода на клавиатуре (например, '01', '02', '12')")]
    public string slotCode = "01";

    [Tooltip("Название товара")]
    public string itemName = "Предмет";

    [Tooltip("Префаб выпадающего предмета (с компонентом PickUpItem)")]
    public GameObject itemPrefab;

    [Tooltip("Цена предмета в монетах")]
    public int price = 1;

    [Tooltip("Остаток товара в торговом автомате (-1 если бесконечно)")]
    public int stockCount = 5;

    [Header("3D Модели на полке и Спираль")]
    [Tooltip("Единичный 3D объект товара на полке (для обратной совместимости)")]
    public GameObject shelfVisualObject;

    [Tooltip("Массив 3D объектов предметов на полке в ряду (от переднего к задним)")]
    public GameObject[] shelfVisualObjects;

    [Tooltip("Спираль/пружина полки (поворачивается при покупке)")]
    public Transform springSpiral;

    [Tooltip("Угол поворота спирали при покупке (в градусах, например 360)")]
    public float springRotationAngle = 360f;

    [Tooltip("Ось вращения спирали (по умолчанию Vector3(0, 0, 1))")]
    public Vector3 springRotationAxis = new Vector3(0f, 0f, 1f);

    [Tooltip("Смещение предметов вперед при шаге продвижения полки (в локальных координатах)")]
    public Vector3 pushStepOffset = new Vector3(0f, 0f, -0.1f);

    [Tooltip("Время анимации продвижения полки и вращения спирали (в секундах)")]
    public float pushAnimDuration = 0.4f;
}

public class VendingMachineController : MonoBehaviour
{
    public static VendingMachineController activeVendingMachine = null;

    [Header("Камера Игрока")]
    [Tooltip("Точка, куда прилетает главная камера игрока при подходе к автомату")]
    public Transform cameraTargetPos;
    [Tooltip("Скорость полета камеры")]
    public float cameraMoveSpeed = 4f;

    [Header("3 Экрана / Камеры Торгового Автомата")]
    [Tooltip("Камера 1: Витрина с товарами (левый экран)")]
    public Camera windowCamera;
    [Tooltip("Камера 2: Панель с кнопками и табло (правый верхний экран)")]
    public Camera keypadCamera;
    [Tooltip("Камера 3: Лоток выдачи товаров (правый нижний экран)")]
    public Camera trayCamera;

    [Header("Настройки разделения экрана (Viewport Rects)")]
    [Tooltip("Область экрана 1 (Окно с товарами): X, Y, Width, Height в пропорциях от 0 до 1")]
    public Rect windowViewportRect = new Rect(0.0f, 0.0f, 0.5f, 1.0f);
    [Tooltip("Область экрана 2 (Клавиатура): X, Y, Width, Height в пропорциях от 0 до 1")]
    public Rect keypadViewportRect = new Rect(0.5f, 0.4f, 0.5f, 0.6f);
    [Tooltip("Область экрана 3 (Лоток выдачи): X, Y, Width, Height в пропорциях от 0 до 1")]
    public Rect trayViewportRect = new Rect(0.5f, 0.0f, 0.5f, 0.4f);

    [Header("Панель Кнопок и Дисплей")]
    [Tooltip("Текстовое табло автомата для вывода введенного кода и ошибок (например, '01')")]
    public TMP_Text keypadDisplayText;
    [Tooltip("Отдельное текстовое табло для цены и баланса игрока в формате '15/45' (внесенные/требуемые)")]
    public TMP_Text priceDisplayText;
    [Tooltip("Максимальная длина кода (например, 2 цифры для '01'-'12')")]
    public int maxCodeLength = 2;
    [Tooltip("Автоматически подтверждать покупку при достижении максимальной длины кода")]
    public bool autoSubmitOnLength = true;

    [Header("Товары")]
    public VendingItemSlot[] items;
    [Tooltip("Точка спавна выпадающего предмета в лотке (Зона 3)")]
    public Transform dispenseSpawnPoint;
    [Tooltip("Импульс выталкивания предмета в лоток")]
    public Vector3 dispenseForce = new Vector3(0f, -0.2f, 0.3f);

    [Header("Скрытие интерфейса")]
    [Tooltip("Элементы UI HUD, скрываемые во время работы с автоматом")]
    public GameObject[] objectsToHide;
    [Tooltip("Имя слоя оружия для скрытия")]
    public string weaponLayerName = "Weapon";

    [Header("Подсказка и Подсветка")]
    public LocalizedString interactPrompt;
    public Outline outline;

    [Header("Звуковые эффекты")]
    public AudioSource buttonSound;
    public AudioSource dispenseSound;
    public AudioSource errorSound;
    public AudioSource coinSound;
    [Tooltip("Звук приземления предмета в лоток выдачи (Зона 3)")]
    public AudioSource trayDropSound;

    [Header("Настройки Падения Предмета в Лоток")]
    [Tooltip("Триггер лотка выдачи (Collider с IsTrigger=true в лотке Зоны 3). При касании проигрывается trayDropSound.")]
    public Collider trayDropTrigger;
    [Tooltip("Начальный толчок предмета с полки вперед перед падением вниз")]
    public Vector3 shelfDropImpulse = new Vector3(0f, 0f, -0.4f);

    [Header("Дверца Лотка Выдачи (Tray Door)")]
    [Tooltip("3D объект дверцы/шторки лотка выдачи")]
    public Transform trayDoorTransform;
    [Tooltip("Коллайдер дверцы лотка (автоматически отключается при открытии, чтобы не преграждать клики по предметам)")]
    public Collider trayDoorCollider;
    [Tooltip("Ось вращения дверцы (например, Vector3(1, 0, 0) для поворота вверх/внутрь)")]
    public Vector3 doorRotationAxis = new Vector3(1f, 0f, 0f);
    [Tooltip("Угол открытия дверцы в градусах (например, -60 или 70)")]
    public float doorOpenAngle = -60f;
    [Tooltip("Скорость плавного открытия и закрытия дверцы")]
    public float doorAnimSpeed = 6f;
    [Tooltip("Звук открытия дверцы")]
    public AudioSource doorOpenSound;
    [Tooltip("Звук закрытия дверцы")]
    public AudioSource doorCloseSound;

    // Состояние процесса
    public bool isViewing { get; private set; } = false;
    private bool isTransitioning = false;

    // Данные ввода
    private string currentEnteredCode = "";
    private Coroutine statusMessageCoroutine;
    private VendingKeypadButton currentHoveredButton = null;

    private Quaternion originalDoorLocalRot;
    private bool isTrayDoorHovered = false;

    private Transform mainCameraTransform;
    private Camera mainCameraComponent;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPos;
    private Quaternion originalCameraLocalRot;
    private int originalCullingMask;

    private PlayerMovement playerMovement;
    private MouseLook mouseLook;

    void Awake()
    {
        if (outline == null) outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;

        if (trayDoorTransform != null)
        {
            originalDoorLocalRot = trayDoorTransform.localRotation;
        }

        if (windowCamera != null) windowCamera.enabled = false;
        if (keypadCamera != null) keypadCamera.enabled = false;
        if (trayCamera != null) trayCamera.enabled = false;
    }

    void Start()
    {
        UpdateDisplay("00");
    }

    void OnDisable()
    {
        if (activeVendingMachine == this)
        {
            activeVendingMachine = null;
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    void Update()
    {
        if (!isViewing || isTransitioning) return;

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))
        {
            ExitVendingMachineMode();
            return;
        }

        UpdateButtonHover();

        UpdateTrayDoorHover();

        // Ввод с физической клавиатуры
        HandleKeyboardInput();

        if (Input.GetMouseButtonDown(0))
        {
            HandleMouseClick();
        }
    }

    private void UpdateTrayDoorHover()
    {
        if (trayDoorTransform == null) return;

        Vector2 mousePos = Input.mousePosition;
        Vector2 normalizedMousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

        bool newlyHovered = false;

        if (trayCamera != null && trayViewportRect.Contains(normalizedMousePos))
        {
            newlyHovered = true;
        }

        if (isTrayDoorHovered != newlyHovered)
        {
            isTrayDoorHovered = newlyHovered;
            if (isTrayDoorHovered)
            {
                PlaySound(doorOpenSound);
                SetDoorCollidersEnabled(false);
            }
            else
            {
                PlaySound(doorCloseSound);
                SetDoorCollidersEnabled(true);
            }
        }

        Quaternion desiredRot = isTrayDoorHovered 
            ? (originalDoorLocalRot * Quaternion.AngleAxis(doorOpenAngle, doorRotationAxis)) 
            : originalDoorLocalRot;

        trayDoorTransform.localRotation = Quaternion.Slerp(trayDoorTransform.localRotation, desiredRot, Time.deltaTime * doorAnimSpeed);
    }

    private void SetDoorCollidersEnabled(bool isEnabled)
    {
        if (trayDoorCollider != null)
        {
            trayDoorCollider.enabled = isEnabled;
        }
        else if (trayDoorTransform != null)
        {
            Collider[] doorCols = trayDoorTransform.GetComponentsInChildren<Collider>();
            foreach (Collider col in doorCols)
            {
                if (col != trayDropTrigger)
                {
                    col.enabled = isEnabled;
                }
            }
        }
    }

    private void UpdateButtonHover()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 normalizedMousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

        VendingKeypadButton newlyHovered = null;

        if (keypadCamera != null && keypadViewportRect.Contains(normalizedMousePos))
        {
            Ray ray = keypadCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                newlyHovered = hit.collider.GetComponentInParent<VendingKeypadButton>();
            }
        }

        if (currentHoveredButton != newlyHovered)
        {
            if (currentHoveredButton != null)
            {
                currentHoveredButton.SetHover(false);
            }
            currentHoveredButton = newlyHovered;
            if (currentHoveredButton != null)
            {
                currentHoveredButton.SetHover(true);
            }
        }
    }

    private void HandleKeyboardInput()
    {
        // Ввод цифр 0-9
        for (int i = 0; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i))
            {
                PressDigit(i.ToString());
                return;
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.C))
        {
            PressClear();
            return;
        }

        // Подтверждение (Enter / KeypadEnter)
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            PressEnter();
            return;
        }
    }

    private void HandleMouseClick()
    {
        Vector2 mousePos = Input.mousePosition;
        Vector2 normalizedMousePos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);

        if (keypadCamera != null && keypadViewportRect.Contains(normalizedMousePos))
        {
            Ray ray = keypadCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                VendingKeypadButton btn = hit.collider.GetComponentInParent<VendingKeypadButton>();
                if (btn != null)
                {
                    if (btn.buttonType == KeypadButtonType.Digit) PressDigit(btn.digitValue);
                    else if (btn.buttonType == KeypadButtonType.Clear) PressClear();
                    else if (btn.buttonType == KeypadButtonType.Enter) PressEnter();
                }
            }
        }
        else if (trayCamera != null && trayViewportRect.Contains(normalizedMousePos))
        {
            Ray ray = trayCamera.ScreenPointToRay(mousePos);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                PickUpItem item = hit.collider.GetComponentInParent<PickUpItem>();
                if (item != null)
                {
                    item.PickUp();
                    break;
                }
            }
        }
    }

    private void AnimateMatchingButton(KeypadButtonType type, string digit = "")
    {
        VendingKeypadButton[] allButtons = GetComponentsInChildren<VendingKeypadButton>(true);
        foreach (var btn in allButtons)
        {
            if (btn != null && btn.buttonType == type)
            {
                if (type == KeypadButtonType.Digit && btn.digitValue != digit)
                    continue;

                btn.AnimatePress();
                break;
            }
        }
    }

    public void PressDigit(string digit)
    {
        AnimateMatchingButton(KeypadButtonType.Digit, digit);
        PlaySound(buttonSound);

        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusMessageCoroutine = null;
        }

        if (currentEnteredCode.Length >= maxCodeLength)
        {
            currentEnteredCode = "";
        }

        currentEnteredCode += digit;
        UpdateDisplay(currentEnteredCode);

        if (autoSubmitOnLength && currentEnteredCode.Length >= maxCodeLength)
        {
            PressEnter();
        }
    }

    public void PressClear()
    {
        AnimateMatchingButton(KeypadButtonType.Clear);
        PlaySound(buttonSound);

        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusMessageCoroutine = null;
        }

        currentEnteredCode = "";
        UpdateDisplay("00");
    }

    public void PressEnter()
    {
        AnimateMatchingButton(KeypadButtonType.Enter);

        if (string.IsNullOrEmpty(currentEnteredCode)) return;

        VendingItemSlot slot = System.Array.Find(items, item => item.slotCode.Equals(currentEnteredCode, System.StringComparison.OrdinalIgnoreCase));

        if (slot == null)
        {
            ShowStatusMessage("ERR: NO CODE", errorSound);
            currentEnteredCode = "";
            return;
        }

        if (slot.stockCount == 0)
        {
            ShowStatusMessage("SOLD OUT", errorSound);
            currentEnteredCode = "";
            return;
        }

        int currentCoins = PlayerStats.Instance != null ? PlayerStats.Instance.coins : 0;
        if (currentCoins < slot.price)
        {
            ShowStatusMessage($"NEED {slot.price} COIN", errorSound);
            currentEnteredCode = "";
            return;
        }

        // Списываем монеты
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.RemoveCoins(slot.price);
        }
        PlaySound(coinSound);

        // Уменьшаем количество
        if (slot.stockCount > 0)
        {
            slot.stockCount--;
        }

        DispenseItem(slot);

        ShowStatusMessage("SUCCESS", dispenseSound);
        currentEnteredCode = "";
    }

    private void DispenseItem(VendingItemSlot slot)
    {
        StartCoroutine(DispenseItemRoutine(slot));
    }

    private IEnumerator DispenseItemRoutine(VendingItemSlot slot)
    {
        GameObject frontVisual = null;
        if (slot.shelfVisualObjects != null && slot.shelfVisualObjects.Length > 0)
        {
            foreach (GameObject obj in slot.shelfVisualObjects)
            {
                if (obj != null && obj.activeSelf)
                {
                    frontVisual = obj;
                    break;
                }
            }
        }
        else if (slot.shelfVisualObject != null && slot.shelfVisualObject.activeSelf)
        {
            frontVisual = slot.shelfVisualObject;
        }

        if (slot.springSpiral != null)
        {
            StartCoroutine(RotateSpringRoutine(slot.springSpiral, slot.springRotationAxis, slot.springRotationAngle, slot.pushAnimDuration));
        }

        if (slot.shelfVisualObjects != null && slot.shelfVisualObjects.Length > 0)
        {
            yield return StartCoroutine(PushShelfItemsRoutine(slot, frontVisual));
        }
        else
        {
            float delay = slot.pushAnimDuration > 0f ? slot.pushAnimDuration : 0.4f;
            yield return new WaitForSeconds(delay);
            if (frontVisual != null)
            {
                frontVisual.SetActive(false);
            }
        }

        Vector3 fallStartPos = frontVisual != null ? frontVisual.transform.position : (dispenseSpawnPoint != null ? dispenseSpawnPoint.position + Vector3.up * 1.5f : transform.position + Vector3.up * 1.5f);
        Quaternion fallStartRot = frontVisual != null ? frontVisual.transform.rotation : transform.rotation;

        GameObject fallingObj = null;
        if (slot.itemPrefab != null)
        {
            fallingObj = Instantiate(slot.itemPrefab, fallStartPos, fallStartRot);
        }
        else if (frontVisual != null)
        {
            fallingObj = Instantiate(frontVisual, fallStartPos, fallStartRot);
            fallingObj.SetActive(true);
        }

        if (fallingObj != null)
        {
            PickUpItem pickup = fallingObj.GetComponent<PickUpItem>();
            if (pickup == null) pickup = fallingObj.GetComponentInChildren<PickUpItem>();
            if (pickup != null)
            {
                pickup.isFloating = false;
            }

            Collider[] colliders = fallingObj.GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    col.isTrigger = false;
                    if (col is MeshCollider mc) mc.convex = true;
                }
            }

            Rigidbody rb = fallingObj.GetComponent<Rigidbody>();
            if (rb == null) rb = fallingObj.AddComponent<Rigidbody>();

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 impulse = frontVisual != null 
                ? frontVisual.transform.TransformDirection(shelfDropImpulse) 
                : transform.TransformDirection(shelfDropImpulse);
            rb.AddForce(impulse, ForceMode.Impulse);

            VendingFallingItem tracker = fallingObj.GetComponent<VendingFallingItem>();
            if (tracker == null) tracker = fallingObj.AddComponent<VendingFallingItem>();
            tracker.Init(this, slot);

            StartCoroutine(FallbackLandingTracker(fallingObj, slot));
        }
    }

    public void OnFallingItemTouchTray(GameObject fallingItem, VendingItemSlot slot)
    {
        if (fallingItem == null) return;

        AudioSource soundToPlay = trayDropSound != null ? trayDropSound : dispenseSound;
        if (soundToPlay != null)
        {
            soundToPlay.Play();
        }

        Transform spawnPoint = dispenseSpawnPoint != null ? dispenseSpawnPoint : fallingItem.transform;

        if (slot.itemPrefab != null)
        {
            GameObject finalItem = Instantiate(slot.itemPrefab, spawnPoint.position, spawnPoint.rotation);

            PickUpItem finalPickup = finalItem.GetComponent<PickUpItem>();
            if (finalPickup == null) finalPickup = finalItem.GetComponentInChildren<PickUpItem>();
            if (finalPickup != null)
            {
                finalPickup.isFloating = false;
            }

            Collider col = finalItem.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = false;
                if (col is MeshCollider mc) mc.convex = true;
            }

            Rigidbody rb = finalItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                rb.AddForce(spawnPoint.TransformDirection(dispenseForce), ForceMode.Impulse);
            }
        }

        // Удаляем временную падающую модель
        Destroy(fallingItem);
    }

    private IEnumerator FallbackLandingTracker(GameObject fallingItem, VendingItemSlot slot)
    {
        float targetY = dispenseSpawnPoint != null ? dispenseSpawnPoint.position.y : transform.position.y;
        float timeout = 3.0f;
        float elapsed = 0f;

        while (elapsed < timeout && fallingItem != null)
        {
            elapsed += Time.deltaTime;

            if (fallingItem.transform.position.y <= targetY + 0.15f)
            {
                OnFallingItemTouchTray(fallingItem, slot);
                yield break;
            }

            yield return null;
        }

        if (fallingItem != null)
        {
            OnFallingItemTouchTray(fallingItem, slot);
        }
    }

    private IEnumerator RotateSpringRoutine(Transform spring, Vector3 axis, float angle, float duration)
    {
        if (spring == null) yield break;

        duration = duration > 0f ? duration : 0.4f;
        Vector3 normalizedAxis = axis != Vector3.zero ? axis.normalized : Vector3.forward;

        float elapsed = 0f;
        float lastAngle = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            float currentAngle = Mathf.Lerp(0f, angle, smoothT);
            float deltaAngle = currentAngle - lastAngle;
            lastAngle = currentAngle;

            spring.Rotate(normalizedAxis, deltaAngle, Space.Self);
            yield return null;
        }

        float remainingAngle = angle - lastAngle;
        if (Mathf.Abs(remainingAngle) > 0.001f)
        {
            spring.Rotate(normalizedAxis, remainingAngle, Space.Self);
        }
    }

    private IEnumerator PushShelfItemsRoutine(VendingItemSlot slot, GameObject frontVisualToHide)
    {
        float duration = slot.pushAnimDuration > 0f ? slot.pushAnimDuration : 0.4f;
        List<Transform> itemsToMove = new List<Transform>();
        List<Vector3> startPositions = new List<Vector3>();
        List<Vector3> targetPositions = new List<Vector3>();

        if (slot.shelfVisualObjects != null)
        {
            foreach (GameObject obj in slot.shelfVisualObjects)
            {
                if (obj != null && obj.activeSelf)
                {
                    itemsToMove.Add(obj.transform);
                    startPositions.Add(obj.transform.localPosition);
                    targetPositions.Add(obj.transform.localPosition + slot.pushStepOffset);
                }
            }
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < itemsToMove.Count; i++)
            {
                if (itemsToMove[i] != null)
                {
                    itemsToMove[i].localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], smoothT);
                }
            }
            yield return null;
        }

        for (int i = 0; i < itemsToMove.Count; i++)
        {
            if (itemsToMove[i] != null)
            {
                itemsToMove[i].localPosition = targetPositions[i];
            }
        }

        if (frontVisualToHide != null)
        {
            frontVisualToHide.SetActive(false);
        }
    }

    private void ShowStatusMessage(string msg, AudioSource sound = null)
    {
        PlaySound(sound);
        if (statusMessageCoroutine != null) StopCoroutine(statusMessageCoroutine);
        statusMessageCoroutine = StartCoroutine(TemporaryStatusRoutine(msg));
    }

    private IEnumerator TemporaryStatusRoutine(string msg)
    {
        UpdateDisplay(msg);
        yield return new WaitForSeconds(1.8f);
        UpdateDisplay("00");
        statusMessageCoroutine = null;
    }

    private void UpdateDisplay(string text)
    {
        if (keypadDisplayText != null)
        {
            keypadDisplayText.text = text;
        }

        UpdatePriceDisplay();
    }

    public void UpdatePriceDisplay()
    {
        if (priceDisplayText == null) return;

        int playerCoins = PlayerStats.Instance != null ? PlayerStats.Instance.coins : 0;
        int requiredPrice = 0;

        if (!string.IsNullOrEmpty(currentEnteredCode))
        {
            VendingItemSlot slot = System.Array.Find(items, item => item.slotCode.Equals(currentEnteredCode, System.StringComparison.OrdinalIgnoreCase));
            if (slot != null)
            {
                requiredPrice = slot.price;
            }
        }

        priceDisplayText.text = $"{playerCoins}/{requiredPrice}";
    }

    private void PlaySound(AudioSource audio)
    {
        if (audio != null) audio.Play();
    }

    // =========================================================
    // =========================================================

    public void EnterVendingMachineMode(Camera playerCam)
    {
        if (isViewing || isTransitioning) return;

        isViewing = true;
        isTransitioning = true;
        activeVendingMachine = this;

        mainCameraTransform = playerCam.transform;
        mainCameraComponent = playerCam;

        originalCameraParent = mainCameraTransform.parent;
        originalCameraLocalPos = mainCameraTransform.localPosition;
        originalCameraLocalRot = mainCameraTransform.localRotation;

        playerMovement = playerCam.GetComponentInParent<PlayerMovement>();
        if (playerMovement == null) playerMovement = playerCam.GetComponentInChildren<PlayerMovement>();

        mouseLook = playerCam.GetComponent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInParent<MouseLook>();
        if (mouseLook == null) mouseLook = playerCam.GetComponentInChildren<MouseLook>();

        if (playerMovement != null) playerMovement.enabled = false;
        if (mouseLook != null) mouseLook.enabled = false;

        // Скрываем оружие
        if (!string.IsNullOrEmpty(weaponLayerName))
        {
            originalCullingMask = playerCam.cullingMask;
            int weaponLayer = LayerMask.NameToLayer(weaponLayerName);
            if (weaponLayer != -1)
            {
                playerCam.cullingMask &= ~(1 << weaponLayer);
            }
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(false);
        }

        // Скрываем элементы HUD
        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        SetHighlight(false);

        if (cameraTargetPos != null)
        {
            StartCoroutine(MoveCameraToTarget(cameraTargetPos.position, cameraTargetPos.rotation));
        }
        else
        {
            EnableSplitScreenCameras();
            isTransitioning = false;
        }

        UpdatePriceDisplay();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ExitVendingMachineMode()
    {
        if (!isViewing || isTransitioning) return;

        isTransitioning = true;

        if (currentHoveredButton != null)
        {
            currentHoveredButton.SetHover(false);
            currentHoveredButton = null;
        }

        if (isTrayDoorHovered)
        {
            isTrayDoorHovered = false;
            PlaySound(doorCloseSound);
        }

        if (trayDoorTransform != null)
        {
            trayDoorTransform.localRotation = originalDoorLocalRot;
        }

        // Отключаем 3 доп. камеры
        DisableSplitScreenCameras();

        // Включаем отображение основной камеры
        if (mainCameraComponent != null)
        {
            mainCameraComponent.enabled = true;
            if (!string.IsNullOrEmpty(weaponLayerName))
            {
                mainCameraComponent.cullingMask = originalCullingMask;
            }
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(true);
        }

        if (objectsToHide != null)
        {
            foreach (GameObject obj in objectsToHide)
            {
                if (obj != null) obj.SetActive(true);
            }
        }

        StartCoroutine(MoveCameraBack());
    }

    private void EnableSplitScreenCameras()
    {
        if (windowCamera != null)
        {
            windowCamera.rect = windowViewportRect;
            windowCamera.enabled = true;
        }

        if (keypadCamera != null)
        {
            keypadCamera.rect = keypadViewportRect;
            keypadCamera.enabled = true;
        }

        if (trayCamera != null)
        {
            trayCamera.rect = trayViewportRect;
            trayCamera.enabled = true;
        }

        if (mainCameraComponent != null && (windowCamera != null || keypadCamera != null || trayCamera != null))
        {
            mainCameraComponent.enabled = false;
        }
    }

    private void DisableSplitScreenCameras()
    {
        if (windowCamera != null) windowCamera.enabled = false;
        if (keypadCamera != null) keypadCamera.enabled = false;
        if (trayCamera != null) trayCamera.enabled = false;
    }

    private IEnumerator MoveCameraToTarget(Vector3 targetPos, Quaternion targetRot)
    {
        float t = 0;
        Vector3 startPos = mainCameraTransform.position;
        Quaternion startRot = mainCameraTransform.rotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCameraTransform.position = Vector3.Lerp(startPos, targetPos, smoothT);
            mainCameraTransform.rotation = Quaternion.Slerp(startRot, targetRot, smoothT);
            yield return null;
        }

        mainCameraTransform.position = targetPos;
        mainCameraTransform.rotation = targetRot;

        EnableSplitScreenCameras();
        isTransitioning = false;
    }

    private IEnumerator MoveCameraBack()
    {
        float t = 0;
        Vector3 startPos = mainCameraTransform.localPosition;
        Quaternion startRot = mainCameraTransform.localRotation;

        while (t < 1f)
        {
            t += Time.deltaTime * cameraMoveSpeed;
            float smoothT = Mathf.SmoothStep(0, 1, t);
            mainCameraTransform.localPosition = Vector3.Lerp(startPos, originalCameraLocalPos, smoothT);
            mainCameraTransform.localRotation = Quaternion.Slerp(startRot, originalCameraLocalRot, smoothT);
            yield return null;
        }

        mainCameraTransform.localPosition = originalCameraLocalPos;
        mainCameraTransform.localRotation = originalCameraLocalRot;

        if (playerMovement != null) playerMovement.enabled = true;
        if (mouseLook != null) mouseLook.enabled = true;

        isViewing = false;
        activeVendingMachine = null;
        isTransitioning = false;
    }
}
