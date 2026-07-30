using UnityEngine;
using System.Collections;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class PlayerDeath : MonoBehaviour
{
    [Header("Звуки")]
    [Tooltip("Звук смерти игрока")]
    public AudioClip deathSound;
    [Tooltip("Опциональный AudioSource для проигрывания звука")]
    public AudioSource audioSource;

    [Header("Эффекты крови и ошметков (Gore)")]
    [Tooltip("Частицы взрыва крови (bloodMist)")]
    public GameObject bloodMistPrefab;
    [Tooltip("Декаль лужи крови (bloodDecal)")]
    public GameObject bloodDecalPrefab;
    [Tooltip("Настройки кусков мяса (gibs)")]
    public MeatConfig[] meatConfigs;
    
    [Range(0f, 50f)] public float explosionForce = 12f;
    [Range(0f, 10f)] public float explosionRadius = 3f;
    [Range(0f, 10f)] public float upwardsModifier = 2f;
    public float meatLifetime = 10f;

    [Header("Настройки вращения камеры (Orbit)")]
    [Tooltip("Радиус вращения вокруг точки смерти")]
    public float orbitRadius = 3.5f;
    [Tooltip("Высота камеры над точкой смерти")]
    public float orbitHeight = 1.8f;
    [Tooltip("Скорость вращения (градусов в секунду)")]
    public float orbitSpeed = 25f;
    [Tooltip("Смещение фокуса камеры вверх от точки смерти")]
    public float targetHeightOffset = 0.5f;

    [Header("Интерфейс смерти (UI)")]
    [Tooltip("Префаб экрана смерти (Canvas или панель)")]
    public GameObject deathUIPrefab;
    [Tooltip("Локализованная надпись смерти")]
    public LocalizedString localizedDeathMessage;
    public string defaultRuMessage = "ВЫ ПОГИБЛИ";
    public string defaultEnMessage = "YOU DIED";
    public float uiFadeDelay = 1.5f;
    public float uiFadeDuration = 2f;

    private bool isOrbiting = false;
    private Vector3 orbitCenter;
    private float currentOrbitAngle = 0f;
    private Transform playerCamera;

    /// <summary>
    /// Запуск последовательности смерти. Вызывается из PlayerStats при обнулении HP.
    /// </summary>
    public void OnDeath()
    {
        Vector3 deathPos = transform.position;
        Debug.Log("[PlayerDeath] Запуск процесса смерти игрока...");

        // 1. Воспроизведение звука
        PlayDeathSound();

        // 2. Отключение управления и взаимодействия
        DisablePlayerControls();

        // 2.5. Отключение ВСЕГО существующего UI в сцене
        DisableAllUI();

        // 3. Отключение графики игрока (тело)
        HidePlayerVisuals();

        // 4. Эффекты крови
        SpawnBloodEffects(deathPos);

        // 5. Взрыв на ошметки (мясо)
        SpawnMeatChunks(deathPos);

        // 6. Запуск вращения камеры вокруг точки смерти
        SetupCameraOrbit(deathPos);

        // 7. Показ экрана смерти
        StartCoroutine(ShowDeathUI());
    }

    private void DisableAllUI()
    {
        Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (Canvas canvas in allCanvases)
        {
            canvas.gameObject.SetActive(false);
        }
    }

    private void PlayDeathSound()
    {
        if (deathSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(deathSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(deathSound, transform.position, 1.0f);
            }
        }
    }

    private void DisablePlayerControls()
    {
        // Отключаем движение
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.enabled = false;
            if (movement.controller != null)
            {
                movement.controller.enabled = false;
            }
        }

        // Отключаем взаимодействие
        PlayerInteract interact = GetComponent<PlayerInteract>();
        if (interact != null)
        {
            interact.enabled = false;
            if (interact.interactText != null)
            {
                interact.interactText.gameObject.SetActive(false);
            }
        }

        // Отключаем эффекты безумия рассудка, чтобы не мешали
        PlayerSanityEffects sanityEffects = GetComponent<PlayerSanityEffects>();
        if (sanityEffects != null)
        {
            sanityEffects.enabled = false;
        }

        // Убираем все предметы из рук
        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.UnequipAll();
        }

        // Скрываем HUD статов
        if (PlayerStats.Instance != null && PlayerStats.Instance.statsUIPanel != null)
        {
            PlayerStats.Instance.statsUIPanel.SetActive(false);
        }

        // Освобождаем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HidePlayerVisuals()
    {
        // Находим все рендереры, исключая те, что на камере (хотя камеру мы отвяжем)
        // Для надежности сначала отвязываем камеру, чтобы ее вложенные объекты не пропали
        FindAndUnparentCamera();

        // Теперь скрываем все оставшиеся рендереры на игроке
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            r.enabled = false;
        }

        // Отключаем все коллайдеры на игроке, чтобы не мешали физике мяса
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider c in colliders)
        {
            c.enabled = false;
        }
    }

    private void FindAndUnparentCamera()
    {
        // Пробуем найти через PlayerMovement
        PlayerMovement movement = GetComponent<PlayerMovement>();
        if (movement != null && movement.playerCamera != null)
        {
            playerCamera = movement.playerCamera;
        }

        // Если не нашли, ищем по тегу MainCamera
        if (playerCamera == null && Camera.main != null)
        {
            playerCamera = Camera.main.transform;
        }

        // В крайнем случае ищем компонент Camera в дочерних объектах
        if (playerCamera == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null)
            {
                playerCamera = cam.transform;
            }
        }

        // Отвязываем камеру от игрока и выключаем скрипты поворота мыши на ней
        if (playerCamera != null)
        {
            playerCamera.SetParent(null);
            
            MouseLook mouseLook = playerCamera.GetComponent<MouseLook>();
            if (mouseLook == null) mouseLook = playerCamera.GetComponentInParent<MouseLook>();
            if (mouseLook == null) mouseLook = playerCamera.GetComponentInChildren<MouseLook>();
            
            if (mouseLook != null)
            {
                mouseLook.enabled = false;
            }

            // Также выключаем любые viewmodel-объекты или руки, висящие под камерой
            // (скрываем их рендереры, чтобы не плавали перед лицом при орбите)
            Renderer[] camRenderers = playerCamera.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in camRenderers)
            {
                r.enabled = false;
            }
        }
    }

    private void SpawnBloodEffects(Vector3 deathPos)
    {
        // 1. Спавн облака крови
        if (bloodMistPrefab != null)
        {
            GameObject mist = Instantiate(bloodMistPrefab, deathPos + Vector3.up * 1.0f, Quaternion.identity);
            mist.SetActive(true);
        }

        // 2. Спавн лужи крови на полу
        if (bloodDecalPrefab != null)
        {
            Vector3 puddlePos = deathPos;
            Quaternion puddleRot = Quaternion.identity;
            
            // Направляем луч вниз для выравнивания лужи по поверхности земли
            if (Physics.Raycast(deathPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 5f))
            {
                puddlePos = hit.point + Vector3.up * 0.02f; // Немного приподнимаем над землей от Z-fighting
                puddleRot = Quaternion.FromToRotation(Vector3.up, hit.normal);
                puddleRot *= Quaternion.Euler(0, Random.Range(0, 360), 0); // Рандомный поворот по оси Y
            }
            
            GameObject puddle = Instantiate(bloodDecalPrefab, puddlePos, puddleRot);
            puddle.SetActive(true);
        }
    }

    private void SpawnMeatChunks(Vector3 deathPos)
    {
        if (meatConfigs == null || meatConfigs.Length == 0) return;

        // Создаем физический материал для упругого отскока мяса
        PhysicsMaterial bouncyMat = new PhysicsMaterial("BouncyMeatPlayer");
        bouncyMat.bounciness = 0.3f;
        bouncyMat.staticFriction = 0.8f;
        bouncyMat.dynamicFriction = 0.8f;
        bouncyMat.bounceCombine = PhysicsMaterialCombine.Maximum;

        Vector3 explosionEpicenter = deathPos + Vector3.up * 1f;

        foreach (var meatInfo in meatConfigs)
        {
            if (meatInfo.meatPrefab == null) continue;

            for (int i = 0; i < meatInfo.spawnCount; i++)
            {
                Vector3 spawnPos = deathPos + Vector3.up * 1.2f + Random.insideUnitSphere * 0.4f;
                GameObject meat = Instantiate(meatInfo.meatPrefab, spawnPos, Random.rotation);
                
                meat.SetActive(true);

                // Настраиваем коллайдер
                Collider col = meat.GetComponent<Collider>();
                if (col != null && col is MeshCollider meshCollider)
                {
                    meshCollider.convex = true;
                }
                else if (col == null)
                {
                    SphereCollider sc = meat.AddComponent<SphereCollider>();
                    sc.radius = 0.15f;
                    col = sc;
                }
                col.material = bouncyMat;

                // Настраиваем физику
                Rigidbody rb = meat.GetComponent<Rigidbody>();
                if (rb == null) rb = meat.AddComponent<Rigidbody>();
                
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                // Добавляем скрипт ошметков для спавна крови на стенах/полу при столкновениях
                MeatChunk chunkScript = meat.GetComponent<MeatChunk>();
                if (chunkScript == null) chunkScript = meat.AddComponent<MeatChunk>();
                
                chunkScript.bloodDecalPrefab = bloodDecalPrefab;
                chunkScript.lifetime = meatLifetime;

                // Прикладываем силу взрыва
                rb.AddExplosionForce(explosionForce, explosionEpicenter, explosionRadius, upwardsModifier, ForceMode.Impulse);
                rb.AddForce(Vector3.up * (explosionForce * 0.4f), ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 15f, ForceMode.Impulse);
            }
        }
    }

    private void SetupCameraOrbit(Vector3 deathPos)
    {
        orbitCenter = deathPos;
        isOrbiting = true;

        if (playerCamera != null)
        {
            // Рассчитываем начальный угол камеры относительно точки смерти
            Vector3 dir = playerCamera.position - orbitCenter;
            dir.y = 0;
            if (dir.sqrMagnitude > 0.01f)
            {
                currentOrbitAngle = Mathf.Atan2(dir.z, dir.x) * Mathf.Rad2Deg;
            }
            else
            {
                currentOrbitAngle = 0f;
            }
        }
    }

    private void Update()
    {
        if (isOrbiting && playerCamera != null)
        {
            currentOrbitAngle += orbitSpeed * Time.deltaTime;
            if (currentOrbitAngle >= 360f) currentOrbitAngle -= 360f;

            float rad = currentOrbitAngle * Mathf.Deg2Rad;
            Vector3 targetPos = orbitCenter + new Vector3(
                Mathf.Cos(rad) * orbitRadius,
                orbitHeight,
                Mathf.Sin(rad) * orbitRadius
            );

            // Устанавливаем позицию строго на орбиту и направляем взгляд строго в точку смерти
            playerCamera.position = targetPos;
            playerCamera.LookAt(orbitCenter + Vector3.up * targetHeightOffset);
        }
    }

    private IEnumerator ShowDeathUI()
    {
        yield return new WaitForSeconds(uiFadeDelay);

        if (deathUIPrefab != null)
        {
            // Инстанцируем префаб UI
            GameObject uiInstance = Instantiate(deathUIPrefab);
            uiInstance.SetActive(true);
            
            // Если префаб не является корневым Canvas-ом, создаем для него новый Canvas
            // (так как все остальные Canvas-ы в сцене были выключены)
            Canvas canvasComp = uiInstance.GetComponent<Canvas>();
            if (canvasComp == null)
            {
                GameObject canvasGO = new GameObject("DeathUICanvas");
                Canvas newCanvas = canvasGO.AddComponent<Canvas>();
                newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                
                canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
                canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                
                uiInstance.transform.SetParent(canvasGO.transform, false);
            }

            // Находим текстовый компонент для вывода надписи смерти
            TMP_Text textComponent = uiInstance.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                string textToShow = "";
                
                // Проверяем локализацию
                if (localizedDeathMessage != null && !localizedDeathMessage.IsEmpty)
                {
                    textToShow = localizedDeathMessage.GetLocalizedString();
                }
                else
                {
                    // Фаллбек по коду языка
                    bool isEn = false;
                    try
                    {
                        if (LocalizationSettings.SelectedLocale != null)
                        {
                            string code = LocalizationSettings.SelectedLocale.Identifier.Code;
                            isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    catch {}

                    textToShow = isEn ? defaultEnMessage : defaultRuMessage;
                }

                textComponent.text = textToShow;
            }
            
            CanvasGroup canvasGroup = uiInstance.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = uiInstance.AddComponent<CanvasGroup>();
            }

            canvasGroup.alpha = 0f;
            float elapsed = 0f;
            while (elapsed < uiFadeDuration)
            {
                elapsed += Time.deltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / uiFadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }
        else
        {
            Debug.LogWarning("[PlayerDeath] Не задан deathUIPrefab в инспекторе!");
        }
    }
}
