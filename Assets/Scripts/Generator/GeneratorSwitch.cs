using UnityEngine;
using System.Collections;

public class GeneratorSwitch : MonoBehaviour
{
    [Header("Состояние")]
    [Tooltip("Текущее положение тумблера")]
    public bool isOn = false;

    [Header("Визуал")]
    [Tooltip("Рычажок тумблера, который будет поворачиваться")]
    public Transform switchHandle;
    [Tooltip("Локальные углы поворота для состояния ON")]
    public Vector3 onAngle = new Vector3(30f, 0f, 0f);
    [Tooltip("Локальные углы поворота для состояния OFF")]
    public Vector3 offAngle = new Vector3(-30f, 0f, 0f);
    
    [Tooltip("Время анимации переключения")]
    public float toggleDuration = 0.15f;

    [Header("Индикатор")]
    [Tooltip("Рендерер лампочки/светодиода тумблера")]
    public MeshRenderer indicatorRenderer;
    [Tooltip("Материал для включенного состояния (зеленый)")]
    public Material onMaterial;
    [Tooltip("Материал для выключенного состояния (красный)")]
    public Material offMaterial;

    [Header("Звуки")]
    [Tooltip("Звук щелчка при переключении")]
    public AudioSource clickSound;

    private Coroutine rotateCoroutine;

    void Start()
    {
        // Инициализируем положение при старте
        if (switchHandle != null)
        {
            switchHandle.localRotation = Quaternion.Euler(isOn ? onAngle : offAngle);
        }
        UpdateIndicator();
    }

    /// <summary>
    /// Переключает положение тумблера на противоположное.
    /// </summary>
    public void Toggle()
    {
        SetState(!isOn);
        if (clickSound != null)
        {
            clickSound.Play();
        }
    }

    /// <summary>
    /// Принудительно задает положение тумблера (например, при рандомизации).
    /// </summary>
    public void SetState(bool state)
    {
        isOn = state;
        UpdateIndicator();

        if (rotateCoroutine != null)
        {
            StopCoroutine(rotateCoroutine);
        }

        if (gameObject.activeInHierarchy && toggleDuration > 0f && switchHandle != null)
        {
            rotateCoroutine = StartCoroutine(AnimateRotation(isOn ? onAngle : offAngle));
        }
        else if (switchHandle != null)
        {
            switchHandle.localRotation = Quaternion.Euler(isOn ? onAngle : offAngle);
        }
    }

    private void UpdateIndicator()
    {
        if (indicatorRenderer != null)
        {
            indicatorRenderer.sharedMaterial = isOn ? onMaterial : offMaterial;
        }
    }

    private IEnumerator AnimateRotation(Vector3 targetEuler)
    {
        float elapsed = 0f;
        Quaternion startRot = switchHandle.localRotation;
        Quaternion targetRot = Quaternion.Euler(targetEuler);

        while (elapsed < toggleDuration)
        {
            elapsed += Time.deltaTime;
            switchHandle.localRotation = Quaternion.Slerp(startRot, targetRot, elapsed / toggleDuration);
            yield return null;
        }

        switchHandle.localRotation = targetRot;
    }
}
