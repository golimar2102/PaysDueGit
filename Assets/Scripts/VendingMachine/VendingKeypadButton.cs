using UnityEngine;
using System.Collections;

public enum KeypadButtonType
{
    Digit,
    Clear,
    Enter
}

public class VendingKeypadButton : MonoBehaviour
{
    public KeypadButtonType buttonType = KeypadButtonType.Digit;
    public string digitValue = "0";

    [Header("Интерактив и Подсветка")]
    [Tooltip("Outline компонент для подсветки кнопки при наведении мыши")]
    public Outline outline;

    [Header("Анимация Вдавливания")]
    [Tooltip("Локальное смещение при нажатии кнопки (например, (0, 0, 0.01) или Z-вдавление)")]
    public Vector3 pressOffset = new Vector3(0f, 0f, 0.01f);
    [Tooltip("Скорость вдавливания и возврата кнопки")]
    public float pressSpeed = 15f;

    private Vector3 originalLocalPos;
    private Coroutine pressCoroutine;

    void Awake()
    {
        originalLocalPos = transform.localPosition;
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;
    }

    public void SetHover(bool isHovered)
    {
        if (outline != null)
        {
            outline.enabled = isHovered;
        }
    }

    public void AnimatePress()
    {
        if (!gameObject.activeInHierarchy) return;
        if (pressCoroutine != null) StopCoroutine(pressCoroutine);
        pressCoroutine = StartCoroutine(PressRoutine());
    }

    private IEnumerator PressRoutine()
    {
        Vector3 targetPos = originalLocalPos + pressOffset;
        float t = 0f;

        // Вдавливание внутрь
        while (t < 1f)
        {
            t += Time.deltaTime * pressSpeed;
            transform.localPosition = Vector3.Lerp(originalLocalPos, targetPos, Mathf.Clamp01(t));
            yield return null;
        }

        t = 0f;
        // Возврат назад
        while (t < 1f)
        {
            t += Time.deltaTime * pressSpeed;
            transform.localPosition = Vector3.Lerp(targetPos, originalLocalPos, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        pressCoroutine = null;
    }
}
