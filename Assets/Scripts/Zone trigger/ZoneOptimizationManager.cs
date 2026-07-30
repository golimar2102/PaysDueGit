using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер оптимизации зон локаций.
/// Слушает переключение зон из DayNightCycle (OnZoneChanged) и автоматически
/// включает/выключает родительские объекты локаций для экономии CPU и GPU ресурсов.
/// </summary>
public class ZoneOptimizationManager : MonoBehaviour
{
    [Serializable]
    public class ZoneGroup
    {
        [Tooltip("Название основной зоны из enum GameZone")]
        public GameZone zone;

        [Tooltip("Список под-зон (например, Windmill или подвал внутри Farm). При входе в под-зону основная локация НЕ отключается!")]
        public List<GameZone> subZones = new List<GameZone>();

        [Tooltip("Родительский GameObject локации, в котором находятся все объекты, свет и НПС этой зоны")]
        public GameObject zoneRootObject;

        [Tooltip("Если true — данная локация никогда не отключается (например, Ферма и Barn)")]
        public bool isAlwaysActive = false;
    }

    [Header("Настройки Локаций")]
    [Tooltip("Список всех зон игры и их соответствующих родительских объектов на сцене")]
    public List<ZoneGroup> zones = new List<ZoneGroup>();

    [Header("Отладка")]
    [Tooltip("Выводить сообщения о переключении зон в консоль")]
    public bool enableDebugLogs = true;

    private void OnEnable()
    {
        DayNightCycle.OnZoneChanged += HandleZoneChanged;
    }

    private void OnDisable()
    {
        DayNightCycle.OnZoneChanged -= HandleZoneChanged;
    }

    private void Start()
    {
        // Инициализация стартового состояния зон при запуске сцены
        if (DayNightCycle.Instance != null)
        {
            UpdateZoneStates(DayNightCycle.Instance.currentZone);
        }
    }

    private void HandleZoneChanged(GameZone newZone)
    {
        UpdateZoneStates(newZone);
    }

    /// <summary>
    /// Обновляет активность родительских объектов локаций в зависимости от текущей зоны игрока
    /// </summary>
    public void UpdateZoneStates(GameZone activeZone)
    {
        // Проверяем, является ли новая зона управляемой физической локацией или чьей-то под-зоной
        bool isManagedZone = false;
        foreach (var group in zones)
        {
            if (group == null) continue;
            if (group.zone == activeZone || (group.subZones != null && group.subZones.Contains(activeZone)))
            {
                isManagedZone = true;
                break;
            }
        }

        // Если зона является просто световой/звуковой микро-зоной и не внесена ни в чьи subZones,
        // мы НЕ отключаем текущие активные физические локации!
        if (!isManagedZone)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[ZoneOptimizationManager] Зона '{activeZone}' является внутренней микро-зоной (не привязана к объекту локации). Состояние объектов не изменено.");
            }
            EnemyAI.ProcessBackgroundSlaveryEscapes();
            return;
        }

        foreach (var zoneGroup in zones)
        {
            if (zoneGroup == null || zoneGroup.zoneRootObject == null) continue;

            // Локация должна быть активна, если:
            // 1. Игрок находится в этой главной зоне
            // 2. ИЛИ игрок находится в одной из её под-зон (например, Windmill внутри Farm)
            // 3. ИЛИ зона помечена как всегда активная (isAlwaysActive)
            bool isCurrentZone = (zoneGroup.zone == activeZone);
            bool isSubZone = (zoneGroup.subZones != null && zoneGroup.subZones.Contains(activeZone));
            bool shouldBeActive = isCurrentZone || isSubZone || zoneGroup.isAlwaysActive;

            if (zoneGroup.zoneRootObject.activeSelf != shouldBeActive)
            {
                zoneGroup.zoneRootObject.SetActive(shouldBeActive);

                if (enableDebugLogs)
                {
                    Debug.Log($"[ZoneOptimizationManager] Зона {zoneGroup.zone} {(shouldBeActive ? "<color=green>АКТИВИРОВАНА</color>" : "<color=red>ОТКЛЮЧЕНА</color>")}");
                }
            }
        }

        // Просчитываем фоновые побеги при смене зон
        EnemyAI.ProcessBackgroundSlaveryEscapes();
    }

    private float nextBackgroundCheckTime = 0f;

    private void Update()
    {
        if (Time.time >= nextBackgroundCheckTime)
        {
            nextBackgroundCheckTime = Time.time + 2.0f;
            EnemyAI.ProcessBackgroundSlaveryEscapes();
        }
    }
}
