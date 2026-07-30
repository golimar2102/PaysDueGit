using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ShadowOptimizerZone : MonoBehaviour
{
    public enum ThresholdAxis { X, Y, Z }
    public enum OperationMode { DirectionalExit, InsideZone }

    [Header("Settings")]
    [Tooltip("Режим работы зоны:\nDirectionalExit - переключение по стороне выхода.\nInsideZone - выключение теней, пока игрок внутри.")]
    public OperationMode operationMode = OperationMode.DirectionalExit;

    [Tooltip("Список источников света, тени которых будут переключаться.")]
    public List<Light> lightsToOptimize = new List<Light>();
    
    [Tooltip("Тег объекта, реагирующего на триггер (обычно игрок).")]
    public string targetTag = "Player";

    [Tooltip("Ось локальных координат коллайдера, которая будет служить 'границей'.")]
    public ThresholdAxis thresholdAxis = ThresholdAxis.Z;

    [Tooltip("Если true: при переходе в 'положительную' сторону выбранной оси тени отключаются. При возвращении (в 'отрицательную') - включаются.")]
    public bool disableShadowsOnPositiveSide = true;

    [Tooltip("Дополнительный поворот осей (в градусах). Позволяет повернуть 'плоскость срабатывания' не вращая сам коллайдер.")]
    public Vector3 axisRotationOffset = Vector3.zero;

    // Сохраняем изначальный тип теней для каждого источника света (Soft или Hard)
    private Dictionary<Light, LightShadows> originalShadowTypes = new Dictionary<Light, LightShadows>();

    private void Awake()
    {
        // Запоминаем изначальные настройки теней у добавленных источников
        foreach (Light light in lightsToOptimize)
        {
            if (light != null)
            {
                originalShadowTypes[light] = light.shadows;
            }
        }
    }

    private void Start()
    {
        // Переводим объект в слой Ignore Raycast, чтобы он не перекрывал лучи взаимодействия игрока (PlayerInteract)
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        // Проверяем, является ли коллайдер триггером
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[ShadowOptimizerZone] Коллайдер на объекте {gameObject.name} не является триггером! Устанавливаю isTrigger = true.");
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (operationMode == OperationMode.InsideZone && other.CompareTag(targetTag))
        {
            // Выключаем тени, пока игрок внутри
            ToggleShadows(false);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(targetTag))
        {
            if (operationMode == OperationMode.InsideZone)
            {
                // Включаем тени обратно при выходе
                ToggleShadows(true);
            }
            else if (operationMode == OperationMode.DirectionalExit)
            {
                // Определяем, с какой стороны от коллайдера оказался игрок после выхода
                Vector3 directionToPlayer = other.transform.position - transform.position;
                
                // Определяем локальный вектор выбранной оси
                Vector3 localThreshold = Vector3.forward; // По умолчанию Z
                switch (thresholdAxis)
                {
                    case ThresholdAxis.X:
                        localThreshold = Vector3.right; // Красная стрелка
                        break;
                    case ThresholdAxis.Y:
                        localThreshold = Vector3.up; // Зеленая стрелка
                        break;
                    case ThresholdAxis.Z:
                        localThreshold = Vector3.forward; // Синяя стрелка
                        break;
                }

                // Применяем дополнительный поворот координат (если задан)
                if (axisRotationOffset != Vector3.zero)
                {
                    localThreshold = Quaternion.Euler(axisRotationOffset) * localThreshold;
                }

                // Переводим вектор в мировые координаты с учетом поворота самого объекта
                Vector3 worldThresholdVector = transform.TransformDirection(localThreshold);

                // Если dotProduct > 0, игрок вышел в положительную сторону по выбранной оси
                float dotProduct = Vector3.Dot(worldThresholdVector, directionToPlayer);
                bool isPositiveSide = dotProduct > 0;
                
                // Определяем, нужно ли включить тени
                bool turnOn = disableShadowsOnPositiveSide ? !isPositiveSide : isPositiveSide;
                
                ToggleShadows(turnOn);
            }
        }
    }

    private void ToggleShadows(bool turnOn)
    {
        foreach (Light light in lightsToOptimize)
        {
            if (light != null)
            {
                if (turnOn)
                {
                    // Возвращаем исходный тип теней (Soft/Hard)
                    if (originalShadowTypes.TryGetValue(light, out LightShadows originalShadow))
                    {
                        light.shadows = originalShadow;
                    }
                }
                else
                {
                    // Полностью отключаем тени для оптимизации
                    light.shadows = LightShadows.None;
                }
            }
        }
    }
}
