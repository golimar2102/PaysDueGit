using UnityEngine;
using UnityEngine.Localization;
using System.Collections.Generic;

[System.Serializable]
public struct DispenseItemConfig
{
    [Tooltip("ID предмета из All Items Database в InventoryManager")]
    public int itemID;
    [Tooltip("Количество предметов в одной выдаче")]
    public int amount;
}

[RequireComponent(typeof(Collider))]
public class ItemSource : MonoBehaviour
{
    [Header("Настройки предметов")]
    [Tooltip("Список предметов, которые выдает этот источник")]
    public DispenseItemConfig[] itemsToGive;

    [Header("Настройки источника")]
    [Tooltip("Бесконечный ли источник предметов? (как water source)")]
    public bool isInfinite = true;

    [Tooltip("Оставшееся количество выдач/использований (если не бесконечный)")]
    public int remainingUses = 5;

    [Tooltip("Деактивировать ли объект (gameObject.SetActive(false)) когда заряды закончатся?")]
    public bool deactivateWhenEmpty = false;

    [Header("Локализация подсказок")]
    [Tooltip("Название действия (например, 'Взять' или 'Обыскать')")]
    public LocalizedString actionName;

    [Tooltip("Название источника (например, 'Ящик с инструментами')")]
    public LocalizedString sourceName;

    [Tooltip("Сообщение, если инвентарь полон")]
    public LocalizedString inventoryFullMessage;

    [Tooltip("Сообщение, если источник пуст")]
    public LocalizedString emptySourceMessage;

    [Header("Звуки")]
    [Tooltip("Звук при успешном получении предметов")]
    public AudioSource dispenseSound;
    [Tooltip("Звук при неудачной попытке (например, если инвентарь полон)")]
    public AudioSource errorSound;

    private Outline outline;

    void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
    }

    public void SetHighlight(bool highlight)
    {
        if (outline != null) outline.enabled = highlight;
    }

    /// <summary>
    /// Проверяет, можно ли выдать все предметы в инвентарь.
    /// </summary>
    public bool CanDispense()
    {
        if (!isInfinite && remainingUses <= 0) return false;
        if (itemsToGive == null || itemsToGive.Length == 0) return false;

        List<DispenseItemConfig> itemList = new List<DispenseItemConfig>(itemsToGive);
        return InventoryManager.Instance != null && InventoryManager.Instance.CanFitItems(itemList);
    }

    /// <summary>
    /// Пытается выдать предметы в инвентарь игрока.
    /// Возвращает true в случае успеха.
    /// </summary>
    public bool TryDispense()
    {
        if (!isInfinite && remainingUses <= 0) return false;
        if (itemsToGive == null || itemsToGive.Length == 0) return false;

        if (InventoryManager.Instance == null) return false;

        List<DispenseItemConfig> itemList = new List<DispenseItemConfig>(itemsToGive);
        if (!InventoryManager.Instance.CanFitItems(itemList))
        {
            if (errorSound != null) errorSound.Play();
            return false;
        }

        foreach (var config in itemsToGive)
        {
            GameObject prefab = InventoryManager.Instance.GetPrefabByID(config.itemID);
            if (prefab == null)
            {
                Debug.LogWarning($"[ItemSource] Префаб для ID {config.itemID} не найден в базе InventoryManager!");
                continue;
            }

            PickUpItem pickup = prefab.GetComponent<PickUpItem>();
            if (pickup == null)
            {
                pickup = prefab.GetComponentInChildren<PickUpItem>();
            }

            if (pickup != null)
            {
                InventoryItemData data = new InventoryItemData(pickup);
                data.amount = config.amount;

                // Добавляем предмет (мы уже гарантировали, что он влезет)
                InventoryManager.Instance.AddItemWithLeftover(data);
            }
        }

        if (!isInfinite)
        {
            remainingUses--;
            if (remainingUses <= 0 && deactivateWhenEmpty)
            {
                gameObject.SetActive(false);
            }
        }

        if (dispenseSound != null)
        {
            dispenseSound.Play();
        }

        return true;
    }

    public string GetActionName()
    {
        if (actionName != null && !actionName.IsEmpty) return actionName.GetLocalizedString();
        return "Взять";
    }

    public string GetSourceName()
    {
        if (sourceName != null && !sourceName.IsEmpty) return sourceName.GetLocalizedString();
        return gameObject.name;
    }

    public string GetInventoryFullMessage()
    {
        if (inventoryFullMessage != null && !inventoryFullMessage.IsEmpty) return inventoryFullMessage.GetLocalizedString();
        return "Инвентарь полон!";
    }

    public string GetEmptySourceMessage()
    {
        if (emptySourceMessage != null && !emptySourceMessage.IsEmpty) return emptySourceMessage.GetLocalizedString();
        return "Пусто";
    }
}
