using UnityEngine;
using System.Collections.Generic;

public class EquipmentManager : MonoBehaviour
{
    // Делаем скрипт одиночкой (Singleton), чтобы к нему было легко обращаться из инвентаря
    public static EquipmentManager Instance;

    [System.Serializable]
    public class WeaponData
    {
        [Tooltip("ID предмета (должен совпадать с ID в PickUpItem)")]
        public int itemID; 
        
        [Tooltip("Сама моделька оружия, висящая на игроке")]
        public GameObject weaponObject; 

        [Tooltip("ОПЦИОНАЛЬНО: Свой материал для модели (полезно для разных семян, банок)")]
        public Material customMaterial; // <-- НОВОЕ ПОЛЕ
    }

    [Header("Список доступного оружия")]
    public List<WeaponData> weapons = new List<WeaponData>();

    // Текущее экипированное оружие
    private GameObject currentWeapon;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // При старте прячем всё оружие, чтобы руки были пустыми
        UnequipAll();
    }

    // Этот метод будем вызывать из кругового меню
    public void EquipItem(int id)
    {
        UnequipAll(); // Сначала прячем то, что уже в руках

        // Заплатка для мини-игр: не активируем оружие, если идет миниигра разделки или раскатки
        if (ButcheringTableController.activeTable != null && ButcheringTableController.activeTable.isMinigameActive)
        {
            return;
        }
        if (DoughRollingController.activeRollingBoard != null && DoughRollingController.activeRollingBoard.isViewing)
        {
            return;
        }
        if (WorkbenchController.activeWorkbench != null && WorkbenchController.activeWorkbench.isViewing)
        {
            return;
        }

        // Ищем в списке оружие с нужным ID
        foreach (WeaponData w in weapons)
        {
            if (w.itemID == id)
            {
                if (w.weaponObject != null)
                {
                    w.weaponObject.SetActive(true); // Включаем модельку
                    currentWeapon = w.weaponObject;

                    // --- МАГИЯ СМЕНЫ МАТЕРИАЛА (ИСПРАВЛЕНО) ---
                    // Если мы указали кастомный материал в Инспекторе - применяем его КО ВСЕМ ЧАСТЯМ
                    if (w.customMaterial != null)
                    {
                        // 1. Красим все обычные детали (MeshRenderer)
                        MeshRenderer[] renderers = w.weaponObject.GetComponentsInChildren<MeshRenderer>(true);
                        foreach (MeshRenderer r in renderers)
                        {
                            r.material = w.customMaterial;
                        }

                        // 2. Красим все анимированные/скелетные детали (SkinnedMeshRenderer) - обычно Лицо и Тело находятся здесь!
                        SkinnedMeshRenderer[] skinnedRenderers = w.weaponObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                        foreach (SkinnedMeshRenderer sr in skinnedRenderers)
                        {
                            sr.material = w.customMaterial;
                        }
                    }
                    // -----------------------------

                    Debug.Log($"[EquipmentManager] Достали оружие с ID: {id}");
                }
                return; // Нашли и включили, дальше искать не нужно
            }
        }
    }

    // Спрятать всё оружие
    public void UnequipAll()
    {
        foreach (WeaponData w in weapons)
        {
            if (w.weaponObject != null)
            {
                w.weaponObject.SetActive(false);
            }
        }
        currentWeapon = null;
    }
    
    public void SetWeaponVisibility(bool isVisible)
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(isVisible);
        }
    }
}