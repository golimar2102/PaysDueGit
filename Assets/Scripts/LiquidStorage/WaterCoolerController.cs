using UnityEngine;
using System.Collections.Generic;

public class WaterCoolerController : MonoBehaviour
{
    [Header("Настройки воды")]
    [Tooltip("Текущее количество воды в кулере")]
    public float currentWater = 0f;
    [Tooltip("Максимальное количество воды в кулере")]
    public float maxWater = 100f;
    [Tooltip("Текущий тип жидкости в кулере")]
    public LiquidType currentLiquidType = LiquidType.CleanWater;
    [Tooltip("Список жидкостей, которые разрешено вливать в кулер")]
    public List<LiquidType> allowedLiquids = new List<LiquidType> { LiquidType.CleanWater, LiquidType.DirtyWater };

    [Header("Визуальное отображение")]
    [Tooltip("3D объект жидкости, масштаб которого будет меняться")]
    public Transform waterVisualObject;
    [Tooltip("Максимальный масштаб жидкости по выбранной оси при полном объеме")]
    public float maxWaterVisualScale = 1f;
    
    public enum ScaleAxis { X, Y, Z }
    [Tooltip("Ось масштабирования жидкости")]
    public ScaleAxis waterScaleAxis = ScaleAxis.Y;

    [Header("Материалы жидкостей")]
    [Tooltip("Рендерер для отображения жидкости")]
    public Renderer liquidRenderer;
    [Tooltip("Список сопоставлений типов жидкостей и их материалов")]
    public List<LiquidVisualMapping> liquidMaterials;

    [Header("Настройки перелива")]
    [Tooltip("Скорость перелива воды в секунду")]
    public float pourRate = 20f;

    [Header("Звуки")]
    [Tooltip("Звук заливания воды в трубу (зацикленный)")]
    public AudioSource pourSound;
    [Tooltip("Звук набора/питья воды из крана (зацикленный)")]
    public AudioSource fillSound;

    [Header("Интерактивные элементы")]
    [Tooltip("Ссылка на трубу вливания воды")]
    public WaterCoolerPipe pipe;
    [Tooltip("Ссылка на кран забора воды")]
    public WaterCoolerTap tap;

    private Vector3 initialWaterScale;

    void Awake()
    {
        if (currentWater <= 0f)
        {
            currentWater = 0f;
            currentLiquidType = LiquidType.None;
        }

        if (waterVisualObject != null)
        {
            initialWaterScale = waterVisualObject.localScale;
        }
        UpdateWaterVisual();
        UpdateLiquidMaterial();
    }

    void Update()
    {
        UpdateWaterVisual();
        UpdateLiquidMaterial();
    }

    /// <summary>
    /// Масштабирует 3D модель воды в реальном времени в зависимости от объема.
    /// </summary>
    public void UpdateWaterVisual()
    {
        if (waterVisualObject == null) return;

        float fillPercentage = Mathf.Clamp01(currentWater / maxWater);
        Vector3 newScale = initialWaterScale;

        switch (waterScaleAxis)
        {
            case ScaleAxis.X:
                newScale.x = initialWaterScale.x * fillPercentage * maxWaterVisualScale;
                break;
            case ScaleAxis.Y:
                newScale.y = initialWaterScale.y * fillPercentage * maxWaterVisualScale;
                break;
            case ScaleAxis.Z:
                newScale.z = initialWaterScale.z * fillPercentage * maxWaterVisualScale;
                break;
        }

        // Если воды практически нет, скрываем объект совсем
        if (fillPercentage <= 0.005f)
        {
            newScale = Vector3.zero;
        }

        waterVisualObject.localScale = newScale;
    }

    /// <summary>
    /// Меняет материал визуального объекта в зависимости от типа жидкости в кулере.
    /// </summary>
    public void UpdateLiquidMaterial()
    {
        if (liquidRenderer == null || liquidMaterials == null) return;

        foreach (var mapping in liquidMaterials)
        {
            if (mapping.liquidType == currentLiquidType)
            {
                liquidRenderer.sharedMaterial = mapping.material;
                return;
            }
        }
    }
}
