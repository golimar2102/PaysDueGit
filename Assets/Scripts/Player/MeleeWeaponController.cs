using UnityEngine;

public class MeleeWeaponController : MonoBehaviour
{
    [Header("Настройки оружия")]
    [Tooltip("Название для отладки, например 'Ржавый тесак'")]
    public string weaponName = "Холодное оружие";

    [Tooltip("Базовый урон (ЛКМ)")]
    public float damage = 25f;
    [Tooltip("Множитель урона для сильной атаки (ПКМ)")]
    public float heavyDamageMultiplier = 1.5f;

    [Header("Настройки атаки")]
    [Tooltip("Задержка между ударами")]
    public float attackCooldown = 0.5f;
    private float nextAttackTime = 0f;

    [Header("Трата стамины")]
    [Tooltip("Сколько стамины тратит обычный удар")]
    public float lightAttackStamina = 15f;
    [Tooltip("Сколько стамины тратит усиленный удар")]
    public float heavyAttackStamina = 30f;

    [Header("Ссылки")]
    public Animator weaponAnimator;

    private PlayerStats playerStats;
    private WeaponDamage currentWeaponDamage;

    // Хэши параметров аниматора
    private static readonly int SpeedHash     = Animator.StringToHash("Speed");
    private static readonly int AttackLHash   = Animator.StringToHash("AttackL");
    private static readonly int AttackRHash   = Animator.StringToHash("AttackR");

    // Константы скоростей анимации (совпадают с AnimParentController)
    private const float WalkSpeed = 2.5f;
    private const float RunSpeed  = 8f;

    void Start()
    {
        if (weaponAnimator == null)
            weaponAnimator = GetComponent<Animator>();

        playerStats = FindFirstObjectByType<PlayerStats>();
        currentWeaponDamage = GetComponentInChildren<WeaponDamage>();
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen)
            return;

        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive)
            return;

        HandleAttacks();
        HandleMovementAnimation();
    }

    private void HandleAttacks()
    {
        if (Time.time < nextAttackTime)
            return;

        // ЛКМ — Обычный удар
        if (Input.GetButtonDown("Fire1"))
        {
            if (playerStats != null && !playerStats.HasStamina(lightAttackStamina))
            {
                Debug.Log("Не хватает сил для обычного удара!");
                return;
            }

            if (playerStats != null) playerStats.UseStamina(lightAttackStamina);

            weaponAnimator.SetTrigger(AttackLHash);
            nextAttackTime = Time.time + attackCooldown;
            Debug.Log($"[{weaponName}] Обычный удар!");

            if (currentWeaponDamage != null)
            {
                currentWeaponDamage.damage = damage;
                currentWeaponDamage.EnableDamage();
            }
        }
        else if (Input.GetButtonDown("Fire2"))
        {
            if (playerStats != null && !playerStats.HasStamina(heavyAttackStamina))
            {
                Debug.Log("Слишком устал для сильного удара!");
                return;
            }

            if (playerStats != null) playerStats.UseStamina(heavyAttackStamina);

            weaponAnimator.SetTrigger(AttackRHash);
            nextAttackTime = Time.time + (attackCooldown * 1.5f);

            float heavyDmg = damage * heavyDamageMultiplier;
            Debug.Log($"[{weaponName}] СИЛЬНЫЙ УДАР! Урон: {heavyDmg}");

            if (currentWeaponDamage != null)
            {
                currentWeaponDamage.damage = heavyDmg;
                currentWeaponDamage.EnableDamage();
            }
        }
    }

    private void HandleMovementAnimation()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        float currentSpeed = 0f;

        if (Mathf.Abs(x) > 0.1f || Mathf.Abs(z) > 0.1f)
        {
            currentSpeed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : WalkSpeed;
        }

        if (weaponAnimator != null)
        {
            weaponAnimator.SetFloat(SpeedHash, currentSpeed);
        }
    }
}