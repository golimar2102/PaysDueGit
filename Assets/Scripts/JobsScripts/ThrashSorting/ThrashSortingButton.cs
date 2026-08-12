using UnityEngine;
using System.Collections;
using UnityEngine.Localization;

public class TrashSortingButton : MonoBehaviour
{
    public enum ButtonType
    {
        StartMinigame = 1,
        Hammer = 2,
        Divider = 3,
        StopMinigame = 4
    }

    [Header("Тип кнопки")]
    [Tooltip("За какую функцию отвечает эта кнопка (1 - Старт, 2 - Молот, 3 - Перегородка, 4 - Стоп)")]
    public ButtonType buttonType = ButtonType.StartMinigame;

    [Header("Ссылка на контроллер миниигры")]
    [Tooltip("Если не назначен, запрашивается из родителя или со сцены")]
    public TrashSortingController controller;

    [Header("Локализация и подсказка")]
    [Tooltip("Строка локализации для действия кнопки")]
    public LocalizedString localizedPrompt;
    [Tooltip("Пользовательский текст подсказки (если локализация не используется)")]
    public string customPromptText;

    [Header("Анимация вдавливания кнопки")]
    [Tooltip("Модель кнопки для анимации вдавливания (если пустая, используется текущий Transform)")]
    public Transform buttonMeshTransform;
    [Tooltip("Смещение вдавливания в локальных координатах (например, 0, -0.04, 0 или 0, 0, -0.04)")]
    public Vector3 pressOffset = new Vector3(0f, -0.04f, 0f);
    [Tooltip("Скорость вдавливания")]
    public float pressSpeed = 25f;
    [Tooltip("Скорость возврата обратно")]
    public float returnSpeed = 15f;
    [Tooltip("Аудиоисточник для щелчка кнопки")]
    public AudioSource buttonAudio;
    [Tooltip("Звуковой клип щелчка кнопки")]
    public AudioClip buttonClickSound;

    [Header("Подсветка")]
    public Outline outline;

    private Vector3 initialLocalPos;
    private bool isPressing = false;
    private Coroutine pressCoroutine;

    void Awake()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<TrashSortingController>() ?? FindFirstObjectByType<TrashSortingController>();
        }

        if (buttonMeshTransform == null)
        {
            buttonMeshTransform = transform;
        }
        initialLocalPos = buttonMeshTransform.localPosition;

        if (outline == null)
        {
            outline = GetComponentInChildren<Outline>(true);
        }

        if (outline != null)
        {
            outline.enabled = false;
        }

        // Авто-добавление коллайдера если отсутствует
        if (GetComponent<Collider>() == null && GetComponentInChildren<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    /// <summary>
    /// Проверяет, можно ли взаимодействовать с кнопкой в данный момент.
    /// Кнопка старта активна ТОЛЬКО когда миниигра выключена.
    /// Остальные кнопки активны ТОЛЬКО когда миниигра включена.
    /// </summary>
    public bool CanInteract()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<TrashSortingController>() ?? FindFirstObjectByType<TrashSortingController>();
        }

        if (controller == null) return false;

        if (buttonType == ButtonType.StartMinigame)
        {
            return !controller.IsMinigameActive;
        }
        else
        {
            return controller.IsMinigameActive;
        }
    }

    public void SetHighlight(bool active)
    {
        if (outline != null)
        {
            outline.enabled = active;
        }
    }

    public string GetPromptText(KeyCode interactKey)
    {
        string prompt = "";

        if (localizedPrompt != null && !localizedPrompt.IsEmpty)
        {
            prompt = localizedPrompt.GetLocalizedString();
        }
        else if (!string.IsNullOrEmpty(customPromptText))
        {
            prompt = customPromptText;
        }
        else
        {
            switch (buttonType)
            {
                case ButtonType.StartMinigame:
                    prompt = "Запустить конвеер";
                    break;
                case ButtonType.Hammer:
                    prompt = "Молот";
                    break;
                case ButtonType.Divider:
                    prompt = "Перегородка";
                    break;
                case ButtonType.StopMinigame:
                    prompt = "Выключить конвеер";
                    break;
            }
        }

        return $"<color=#FFD700>[{interactKey}]</color> {prompt}";
    }

    public void Interact(PlayerInteract player)
    {
        if (!CanInteract()) return;

        // Анимация вдавливания
        AnimatePress();

        switch (buttonType)
        {
            case ButtonType.StartMinigame:
                controller.StartMinigame(player);
                break;
            case ButtonType.Hammer:
                controller.TriggerHammer();
                break;
            case ButtonType.Divider:
                controller.ToggleDivider();
                break;
            case ButtonType.StopMinigame:
                controller.StopMinigame();
                break;
        }
    }

    private void AnimatePress()
    {
        if (buttonAudio != null)
        {
            if (buttonClickSound != null) buttonAudio.PlayOneShot(buttonClickSound);
            else buttonAudio.Play();
        }

        if (pressCoroutine != null)
        {
            StopCoroutine(pressCoroutine);
        }
        pressCoroutine = StartCoroutine(ButtonPressRoutine());
    }

    private IEnumerator ButtonPressRoutine()
    {
        isPressing = true;
        Vector3 targetPos = initialLocalPos + pressOffset;

        // Вдавливание вниз
        while (Vector3.Distance(buttonMeshTransform.localPosition, targetPos) > 0.001f)
        {
            buttonMeshTransform.localPosition = Vector3.MoveTowards(
                buttonMeshTransform.localPosition, 
                targetPos, 
                pressSpeed * Time.deltaTime
            );
            yield return null;
        }
        buttonMeshTransform.localPosition = targetPos;

        // Возврат обратно
        while (Vector3.Distance(buttonMeshTransform.localPosition, initialLocalPos) > 0.001f)
        {
            buttonMeshTransform.localPosition = Vector3.MoveTowards(
                buttonMeshTransform.localPosition, 
                initialLocalPos, 
                returnSpeed * Time.deltaTime
            );
            yield return null;
        }
        buttonMeshTransform.localPosition = initialLocalPos;
        isPressing = false;
    }
}
