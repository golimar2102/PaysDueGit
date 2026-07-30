using UnityEngine;
using TMPro;

public class WeaponController : MonoBehaviour
{
    [Header("Настройки оружия")]
    public float fireRate = 0.8f;
    public float aimDelay = 0.2f;

    [Header("Патроны")]
    public int maxAmmo = 2;
    public int currentAmmo = 2;
    public int reserveAmmo = 10;

    [Header("Урон и Дальность (Falloff)")]
    public float baseDamage = 65f;
    public float optimalRange = 4f;
    public float maxRange = 25f;
    public float minDamagePercent = 0.2f;
    public bool isStunWeapon = false;

    [Header("Разброс дроби (Spread)")]
    public int pelletsCount = 8;
    [Tooltip("Разброс при стрельбе от бедра")]
    public float hipSpread = 0.08f;
    [Tooltip("Разброс при прицеливании (ПКМ)")]
    public float aimSpread = 0.02f;

    private float currentSpread;

    [Header("Прицел (UI)")]
    [Tooltip("Перетащи сюда картинку прицела из Canvas")]
    public RectTransform crosshairRect;
    [Tooltip("Множитель размера прицела на экране (подбери на глаз, например 2500)")]
    public float crosshairScaleMultiplier = 2500f;

    [Header("Ссылки")]
    public Animator weaponAnimator;
    public TextMeshProUGUI ammoText;
    public Camera playerCamera;

    private float nextFireTime = 0f;
    private float aimTimer = 0f;
    private bool isAiming = false;
    private LayerMask enemyLayer;

    // Кэшированные клавиши (считываются один раз в Start)
    private KeyCode cachedAimKey;
    private KeyCode cachedFireKey;
    private KeyCode cachedReloadKey;

    // Хэши параметров аниматора
    private static readonly int SpeedHash      = Animator.StringToHash("Speed");
    private static readonly int IsAimingHash   = Animator.StringToHash("IsAiming");
    private static readonly int ShootHash      = Animator.StringToHash("Shoot");
    private static readonly int ReloadOneHash  = Animator.StringToHash("ReloadOne");
    private static readonly int ReloadFullHash = Animator.StringToHash("ReloadFull");

    private const float WalkSpeed = 2.5f;
    private const float RunSpeed  = 8f;

    void Start()
    {
        if (weaponAnimator == null) weaponAnimator = GetComponent<Animator>();
        if (playerCamera == null) playerCamera = Camera.main;

        enemyLayer = LayerMask.GetMask("Enemy");
        currentSpread = hipSpread;

        // Кэшируем клавиши один раз
        RefreshKeyBindings();

        UpdateAmmoUI();
    }

    void OnEnable()
    {
        if (ammoText != null) ammoText.gameObject.SetActive(true);
        if (crosshairRect != null) crosshairRect.gameObject.SetActive(true);
        // Обновляем привязки при каждом включении оружия (игрок мог зайти в настройки)
        RefreshKeyBindings();
        UpdateAmmoUI();
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
    }

    void OnDisable()
    {
        if (ammoText != null) ammoText.gameObject.SetActive(false);
        if (crosshairRect != null) crosshairRect.gameObject.SetActive(false);
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
    }

    /// <summary>Читает PlayerPrefs один раз и кэширует клавиши.</summary>
    public void RefreshKeyBindings()
    {
        cachedAimKey    = (KeyCode)PlayerPrefs.GetInt("Key_Aim",    (int)KeyCode.Mouse1);
        cachedFireKey   = (KeyCode)PlayerPrefs.GetInt("Key_Fire",   (int)KeyCode.Mouse0);
        cachedReloadKey = (KeyCode)PlayerPrefs.GetInt("Key_Reload", (int)KeyCode.R);
    }

    void Update()
    {
        if (InventoryManager.Instance != null && InventoryManager.Instance.isOpen) return;
        if (DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive) return;

        HandleAiming();
        HandleCrosshair();
        HandleShooting();
        HandleReloading();
        HandleMovementAnimation();
    }

    private void HandleAiming()
    {
        isAiming = Input.GetKey(cachedAimKey);
        aimTimer = isAiming ? aimTimer + Time.deltaTime : 0f;
        weaponAnimator.SetBool(IsAimingHash, isAiming);
    }

    private void HandleCrosshair()
    {
        float targetSpread = isAiming ? aimSpread : hipSpread;
        currentSpread = Mathf.Lerp(currentSpread, targetSpread, Time.deltaTime * 10f);

        if (crosshairRect != null)
        {
            float size = currentSpread * crosshairScaleMultiplier;
            crosshairRect.sizeDelta = new Vector2(size, size);
        }
    }

    private void HandleShooting()
    {
        if (Input.GetKeyDown(cachedFireKey) && Time.time >= nextFireTime)
        {
            if (!isAiming)
            {
                Debug.Log("Сначала прицелься (ПКМ)!");
                return;
            }
            if (aimTimer < aimDelay)
            {
                Debug.Log("Подожди, я ещё поднимаю пушку!");
                return;
            }
            if (currentAmmo <= 0)
            {
                Debug.Log("Нет патронов!");
                return;
            }
            currentAmmo--;
            nextFireTime = Time.time + fireRate;
            weaponAnimator.SetTrigger(ShootHash);
            ShootRaycast();
            UpdateAmmoUI();
        }
    }

    private void ShootRaycast()
    {
        if (playerCamera == null) return;

        float pelletBaseDamage = baseDamage / pelletsCount;

        for (int i = 0; i < pelletsCount; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * currentSpread;
            Vector3 spreadOffset = (playerCamera.transform.right * randomCircle.x) + (playerCamera.transform.up * randomCircle.y);
            Vector3 shootDirection = playerCamera.transform.forward + spreadOffset;

            Ray spreadRay = new Ray(playerCamera.transform.position, shootDirection);
            RaycastHit hit;

            if (Physics.Raycast(spreadRay, out hit, maxRange, enemyLayer))
            {
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null)
                {
                    if (isStunWeapon)
                    {
                        if (enemy.currentState == EnemyAI.NPCState.Dead || enemy.currentState == EnemyAI.NPCState.Stunned) continue;
                    }
                    else
                    {
                        if (enemy.currentState == EnemyAI.NPCState.Dead && !enemy.CanDamageCorpse()) continue;
                    }

                    float hitDistance = hit.distance;
                    float damageMultiplier = 1f;

                    if (hitDistance > optimalRange)
                    {
                        float falloffRatio = (hitDistance - optimalRange) / (maxRange - optimalRange);
                        falloffRatio = Mathf.Clamp01(falloffRatio);
                        damageMultiplier = Mathf.Lerp(1f, minDamagePercent, falloffRatio);
                    }

                    float finalDamage = pelletBaseDamage * damageMultiplier * Random.Range(0.85f, 1.15f);
                    if (isStunWeapon)
                    {
                        enemy.TakeStunDamage(finalDamage, transform.root);
                    }
                    else
                    {
                        enemy.TakeDamage(finalDamage, transform.root);
                    }
                }
            }
        }
    }

    private void HandleReloading()
    {
        if (Input.GetKeyDown(cachedReloadKey) && currentAmmo < maxAmmo && reserveAmmo > 0)
        {
            int bulletsToReload = Mathf.Min(maxAmmo - currentAmmo, reserveAmmo);
            currentAmmo += bulletsToReload;
            reserveAmmo -= bulletsToReload;

            weaponAnimator.SetTrigger(bulletsToReload == 1 ? ReloadOneHash : ReloadFullHash);
            UpdateAmmoUI();
        }
    }

    public void AddAmmo(int amount)
    {
        reserveAmmo += amount;
        UpdateAmmoUI();
    }

    private void UpdateAmmoUI()
    {
        if (ammoText != null) ammoText.text = $"{currentAmmo} / {reserveAmmo}";
    }

    private void HandleMovementAnimation()
    {
        float speed = 0f;
        if (Mathf.Abs(Input.GetAxis("Horizontal")) > 0.1f || Mathf.Abs(Input.GetAxis("Vertical")) > 0.1f)
            speed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : WalkSpeed;
        weaponAnimator.SetFloat(SpeedHash, speed);
    }
}