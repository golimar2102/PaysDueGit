using UnityEngine;

public class StoveKnob : WorldToggleDevice
{
    [Header("Звуки плиты")]
    [Tooltip("Звук прокрута ручки (проигрывается в момент поворота)")]
    public AudioSource rotateSound;
    [Tooltip("Звук зажигания газа (проигрывается при включении)")]
    public AudioSource ignitionSound;
    [Tooltip("Звук затухания газа (проигрывается при выключении)")]
    public AudioSource extinguishSound;

    [Header("Звук горения газа (Loop)")]
    [Tooltip("Звук, который будет циклически воспроизводиться, пока конфорка включена")]
    public AudioSource gasLoopSound;

    [Header("Настройки вращения (-Y)")]
    [Tooltip("Трансформ ручки для плавного поворота. Если не задан, возьмет значение из Switch Transform.")]
    public Transform knobTransform;
    [Tooltip("Угол поворота по оси Y во включенном состоянии (обычно отрицательный, например -90)")]
    public float onYRotation = -90f;
    [Tooltip("Угол поворота по оси Y в выключенном состоянии (обычно 0)")]
    public float offYRotation = 0f;
    [Tooltip("Скорость плавного поворота ручки")]
    public float rotationSpeed = 8f;

    [Header("Точка размещения (Опционально)")]
    [Tooltip("Точка, куда ставится посуда для этой горелки")]
    public PlacementPoint burnerPlacement;

    private float targetYRotation;
    private float originalX;
    private float originalZ;

    protected override void Start()
    {
        // Принудительно устанавливаем тип переключателя, чтобы взаимодействие шло через Key_Interact (E)
        isSwitch = true;

        // Если подсказка не настроена, задаем дефолтное значение
        if (string.IsNullOrEmpty(promptText) || promptText == "Вкл / Выкл")
        {
            promptText = "Повернуть ручку";
        }

        // Защита от конфликта: если Switch Transform задан, переносим его в knobTransform
        // и очищаем switchTransform, чтобы базовый класс WorldToggleDevice не делал резких snap-поворотов
        if (switchTransform != null)
        {
            if (knobTransform == null)
            {
                knobTransform = switchTransform;
            }
            switchTransform = null;
        }

        if (knobTransform != null)
        {
            originalX = knobTransform.localEulerAngles.x;
            originalZ = knobTransform.localEulerAngles.z;
            targetYRotation = isOn ? onYRotation : offYRotation;
            
            // Устанавливаем начальное вращение
            knobTransform.localEulerAngles = new Vector3(originalX, targetYRotation, originalZ);
        }

        base.Start();
    }

    protected virtual void Update()
    {
        // Вызываем базовый Update для проверки питания генератора
        base.Update();

        // Плавно поворачиваем ручку по оси Y
        if (knobTransform != null)
        {
            float currentY = knobTransform.localEulerAngles.y;
            float newY = Mathf.LerpAngle(currentY, targetYRotation, Time.deltaTime * rotationSpeed);
            knobTransform.localEulerAngles = new Vector3(originalX, newY, originalZ);
        }
    }

    public override void Toggle()
    {
        // Звук прокрута проигрывается в момент совершения взаимодействия
        if (rotateSound != null) rotateSound.Play();

        bool oldState = isOn;
        base.Toggle();

        if (isOn != oldState)
        {
            if (isOn)
            {
                if (ignitionSound != null) ignitionSound.Play();
            }
            else
            {
                if (extinguishSound != null) extinguishSound.Play();
            }
        }
    }

    public override void SetState(bool state)
    {
        bool oldState = isOn;
        if (state != oldState)
        {
            if (rotateSound != null) rotateSound.Play();
            if (state)
            {
                if (ignitionSound != null) ignitionSound.Play();
            }
            else
            {
                if (extinguishSound != null) extinguishSound.Play();
            }
        }
        base.SetState(state);
    }

    protected override void UpdateDeviceState()
    {
        base.UpdateDeviceState();
        targetYRotation = isOn ? onYRotation : offYRotation;
        UpdateGasLoopSound();
    }

    private void UpdateGasLoopSound()
    {
        if (gasLoopSound == null) return;

        bool shouldBeActive = isOn;
        if (requiresPower && !IsPowerWorking())
        {
            shouldBeActive = false;
        }

        if (shouldBeActive)
        {
            if (!gasLoopSound.isPlaying)
            {
                gasLoopSound.loop = true;
                gasLoopSound.Play();
            }
        }
        else
        {
            if (gasLoopSound.isPlaying)
            {
                gasLoopSound.Stop();
            }
        }
    }
}
