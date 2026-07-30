using UnityEngine;

/// <summary>
/// Универсальный скрипт расходуемого предмета в руках.
/// Заменяет UsableItemInHands и UsableItem — размещай на любом объекте предмета.
/// </summary>
public class UsableItemInHands : MonoBehaviour
{
    [Header("Эффект: Здоровье")]
    public bool restoreHealth = false;
    [Tooltip("Сколько ХП восстановит (отрицательное значение = яд)")]
    public float healthAmount = 10f;

    [Header("Эффект: Еда")]
    public bool restoreFood = false;
    [Tooltip("Сколько сытости восстановит")]
    public float foodAmount = 50f;

    [Header("Эффект: Патроны")]
    public bool giveAmmo = false;
    [Tooltip("Сколько патронов добавит")]
    public int ammoAmount = 2;

    [Header("Управление")]
    [Tooltip("Ключ в PlayerPrefs для клавиши использования")]
    public string useKeyPref = "Key_Use";
    [Tooltip("Клавиша по умолчанию, если настройка не найдена")]
    public KeyCode defaultUseKey = KeyCode.Mouse0;

    [Header("Эффекты")]
    [Tooltip("Звук использования")]
    public AudioSource useSound;

    // Кэшированные ссылки
    private PlayerStats cachedStats;
    private WeaponController cachedWeapon;
    private KeyCode cachedUseKey;

    void Start()
    {
        cachedStats  = FindFirstObjectByType<PlayerStats>();
        cachedWeapon = FindFirstObjectByType<WeaponController>(FindObjectsInactive.Include);
        RefreshKeyBinding();
    }

    /// <summary>Вызывать из SettingsManager после изменения привязок клавиш.</summary>
    public void RefreshKeyBinding()
    {
        cachedUseKey = (KeyCode)PlayerPrefs.GetInt(useKeyPref, (int)defaultUseKey);
    }

    void Update()
    {
        if (Input.GetKeyDown(cachedUseKey))
        {
            if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;
            UseItem();
        }
    }

    void UseItem()
    {
        if (restoreHealth && cachedStats != null)
        {
            if (healthAmount > 0)
                cachedStats.Heal(healthAmount);
            else
                cachedStats.TakeDamage(Mathf.Abs(healthAmount));
        }

        if (restoreFood && cachedStats != null)
            cachedStats.Feed(foodAmount);

        if (giveAmmo)
        {
            if (cachedWeapon != null)
                cachedWeapon.AddAmmo(ammoAmount);
            else
                Debug.LogWarning("[UsableItem] Оружие не найдено!");
        }

        if (useSound != null) useSound.Play();

        // Правильное потребление: уменьшает стак или очищает слот
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.ConsumeItemInActiveSlot();
    }
}