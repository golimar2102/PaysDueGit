using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Настройки мыши")]
    public float mouseSensitivity = 300f; 
    public Transform playerBody; 
    public Transform fpsCamera;

    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        // НОВОЕ: Загружаем чувствительность, которую игрок настроил в меню!
        // Если ничего не настроено, по умолчанию будет 300f.
        LoadSettings();
    }

    void OnEnable()
    {
        SettingsManager.OnSettingsSaved += LoadSettings;
    }

    void OnDisable()
    {
        SettingsManager.OnSettingsSaved -= LoadSettings;
    }

    private void LoadSettings()
    {
        mouseSensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 300f); 
    }

    void Update()
    {
        // Не даем крутить персонажем, если открыт инвентарь ИЛИ ИДЕТ ДИАЛОГ
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;
        
        // <-- НОВАЯ СТРОЧКА: Запрещаем крутить головой при разговоре
        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive) return;

        // Считываем движение мыши по горизонтали (Работает ВСЕГДА)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        
        // 1. Поворот тела влево/вправо (Даже если мы смотрим из Fixed Camera)
        if (playerBody != null)
        {
            playerBody.Rotate(Vector3.up * mouseX);
        }

        // 2. Поворот невидимой камеры вверх/вниз
        // Мы УБРАЛИ проверку на то, включена ли камера. 
        // Теперь она крутится всегда, чтобы передавать угол наклона скриптам HeadTracking и PlayerInteract!
        if (fpsCamera != null)
        {
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            // Наклоняем только саму камеру, не трогая капсулу кота
            fpsCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}