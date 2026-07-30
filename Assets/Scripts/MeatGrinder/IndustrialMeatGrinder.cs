using UnityEngine;
using System.Collections;
using UnityEngine.Localization;

public class IndustrialMeatGrinder : MonoBehaviour
{
    public static IndustrialMeatGrinder activeGrinder = null;

    public enum GrinderState
    {
        Idle,
        Grinding
    }

    [Header("Состояние")]
    public GrinderState state = GrinderState.Idle;
    public bool isGrinderOn = false;
    public bool hasCorpse = false;
    public NPCCorpse placedCorpse = null;

    [Header("Электричество")]
    [Tooltip("Зависит ли прибор от работы генератора?")]
    public bool requiresPower = true;
    [Tooltip("Конкретный генератор, от которого зависит это устройство. Если пустой, используется глобальный Instance.")]
    public GeneratorController targetGenerator;

    [Header("Кнопки и Emission")]
    [Tooltip("Рендерер кнопки 1")]
    public Renderer button1;
    [Tooltip("Цвет Emission кнопки 1 во включенном (ready) состоянии")]
    public Color button1EmissionColor = Color.white;

    [Tooltip("Рендерер кнопки 2")]
    public Renderer button2;
    [Tooltip("Цвет Emission кнопки 2 во включенном (ready) состоянии")]
    public Color button2EmissionColor = Color.white;
    [Tooltip("На сколько вдавливается кнопка по оси Y при нажатии")]
    public float buttonPressDistance = 0.01f;
    [Tooltip("Длительность анимации нажатия кнопки")]
    public float buttonPressDuration = 0.2f;

    [Header("Конвейерная лента")]
    [Tooltip("Скрипт ScrollingTexture для конвейерной ленты")]
    public ScrollingTexture beltScroll;
    [Tooltip("Коллайдер конвейерной ленты")]
    public Collider beltCollider;

    [Header("Размещение трупа")]
    [Tooltip("Точка, куда изначально кладется труп")]
    public Transform corpsePlacePoint;
    [Tooltip("Точка, куда труп уезжает внутри мясорубки")]
    public Transform corpseTargetPoint;
    [Tooltip("Время движения трупа внутрь")]
    public float corpseMoveDuration = 3f;

    [Header("Контейнер для наполнения")]
    [Tooltip("Точка размещения контейнера (с голограммой)")]
    public PlacementPoint containerPlacementPoint;
    [Tooltip("Обязательно ли наличие контейнера для запуска мясорубки?")]
    public bool requireContainerToStart = true;
    [Tooltip("Имя дочернего объекта в контейнере, который нужно включить при наполнении")]
    public string containerChildName = "Content";
    [Tooltip("Время, через которое наполняется контейнер с начала переработки")]
    public float fillDelay = 2.5f;
    [Tooltip("Длительность постепенного наполнения контейнера мясом")]
    public float containerFillDuration = 2.5f;

    [Header("Звуки и Эффекты")]
    [Tooltip("Аудиоресурс для воспроизведения звуков")]
    public AudioSource audioSource;
    [Tooltip("Звук при включении / начале переработки")]
    public AudioClip startGrindingClip;
    [Tooltip("Звук при наполнении контейнера")]
    public AudioClip fillContainerClip;
    [Tooltip("Партиклы переработки")]
    public ParticleSystem grindingParticles;

    [Header("Локализация подсказок")]
    public LocalizedString locPromptPlace;         // "Положить тело"
    public LocalizedString locPromptPickUp;        // "Забрать тело"
    public LocalizedString locPromptStart;         // "Включить мясорубку"
    public LocalizedString locPromptStop;          // "Выключить мясорубку"
    public LocalizedString locPromptNoPower;       // "Нет питания"
    public LocalizedString locPromptNeedContainer;  // "Нужна емкость"

    [Header("Подсветка")]
    public Outline outline;

    private bool lastPowerState = false;
    private Coroutine grindCoroutine = null;

    void Awake()
    {
        if (outline == null) outline = GetComponent<Outline>();
        if (outline == null) outline = GetComponentInChildren<Outline>(true);
        if (outline != null) outline.enabled = false;
    }

    void Start()
    {
        lastPowerState = IsPowerWorking();
        UpdateButtonsEmission();

        // Изначально конвейер выключен
        if (beltScroll != null)
        {
            beltScroll.enabled = isGrinderOn;
        }
    }

    void Update()
    {
        bool currentPower = IsPowerWorking();
        if (currentPower != lastPowerState)
        {
            lastPowerState = currentPower;
            UpdateButtonsEmission();

            // Если электричество пропало, мясорубка гаснет и останавливается
            if (!currentPower && isGrinderOn)
            {
                StopGrindingForce();
            }
        }
    }

    public bool IsPowerWorking()
    {
        if (!requiresPower) return true;
        if (targetGenerator != null) return targetGenerator.isWorking;
        return GeneratorController.IsGeneratorWorking;
    }

    public bool IsLookingAtBelt(Collider hitCollider)
    {
        if (hitCollider == null) return false;
        if (beltCollider != null)
        {
            return hitCollider == beltCollider || hitCollider.transform.IsChildOf(beltCollider.transform);
        }
        if (beltScroll != null)
        {
            Renderer beltRend = beltScroll.GetComponent<Renderer>();
            if (beltRend != null)
            {
                Collider beltCol = beltRend.GetComponent<Collider>();
                return hitCollider == beltCol;
            }
        }
        return false;
    }

    public int GetLookArea(Collider hitCollider)
    {
        if (hitCollider == null) return 0;

        // 1. Check Button 1 (ON Button)
        if (button1 != null)
        {
            if (hitCollider.gameObject == button1.gameObject || hitCollider.transform.IsChildOf(button1.transform))
            {
                return 2;
            }
        }

        // 2. Check Button 2 (OFF Button)
        if (button2 != null)
        {
            if (hitCollider.gameObject == button2.gameObject || hitCollider.transform.IsChildOf(button2.transform))
            {
                return 3;
            }
        }

        // 3. Check Conveyor Belt
        if (IsLookingAtBelt(hitCollider))
        {
            return 1;
        }

        // 4. Default: housing
        return 0;
    }

    private void UpdateButtonsEmission()
    {
        bool active = IsPowerWorking();

        // Кнопка 1
        if (button1 != null && button1.material != null)
        {
            if (active)
            {
                button1.material.EnableKeyword("_EMISSION");
                button1.material.SetColor("_EmissionColor", button1EmissionColor);
            }
            else
            {
                button1.material.DisableKeyword("_EMISSION");
                if (button1.material.HasProperty("_EmissionColor"))
                {
                    button1.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }

        // Кнопка 2
        if (button2 != null && button2.material != null)
        {
            if (active)
            {
                button2.material.EnableKeyword("_EMISSION");
                button2.material.SetColor("_EmissionColor", button2EmissionColor);
            }
            else
            {
                button2.material.DisableKeyword("_EMISSION");
                if (button2.material.HasProperty("_EmissionColor"))
                {
                    button2.material.SetColor("_EmissionColor", Color.black);
                }
            }
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (outline != null) outline.enabled = isHighlighted;
    }

    public bool CanPickUpCorpse()
    {
        return hasCorpse && state == GrinderState.Idle;
    }

    public void PlaceCorpse(NPCCorpse corpse)
    {
        if (corpse == null || hasCorpse) return;

        placedCorpse = corpse;
        hasCorpse = true;
        corpse.currentGrinder = this;

        // Сбрасываем глобальную ссылку carriedCorpse
        if (NPCCorpse.carriedCorpse == corpse)
        {
            NPCCorpse.carriedCorpse = null;
        }

        // Прикрепляем к ленте
        corpse.transform.SetParent(corpsePlacePoint);
        corpse.transform.localPosition = Vector3.zero;
        corpse.transform.localRotation = Quaternion.identity;
        corpse.transform.localScale = Vector3.one;

        // Отключаем физику
        Rigidbody rb = corpse.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Делаем триггерами
        Collider[] colliders = corpse.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders)
        {
            if (col != null)
            {
                col.enabled = true;
                col.isTrigger = true;
            }
        }

        CapsuleCollider capsule = corpse.GetComponent<CapsuleCollider>();
        if (capsule == null) capsule = corpse.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.center = corpse.deadColliderCenter;
        }

        corpse.SetExtraButcheringModelsActive(true);

        Animator corpseAnimator = corpse.GetComponent<Animator>();
        if (corpseAnimator == null) corpseAnimator = corpse.GetComponentInChildren<Animator>();
        if (corpseAnimator != null)
        {
            corpseAnimator.SetBool("OnTable", true);
        }

        // Если мясорубка уже включена, труп сразу едет внутрь
        if (isGrinderOn)
        {
            StartGrinding();
        }
    }

    public void PickUpCorpse(GameObject player)
    {
        if (!CanPickUpCorpse()) return;

        NPCCorpse corpse = placedCorpse;
        if (corpse != null)
        {
            corpse.transform.SetParent(null);
            corpse.currentGrinder = null;
            corpse.originalParent = null;
        }

        placedCorpse = null;
        hasCorpse = false;

        if (corpse != null)
        {
            Animator corpseAnim = corpse.GetComponent<Animator>();
            if (corpseAnim == null) corpseAnim = corpse.GetComponentInChildren<Animator>();
            if (corpseAnim != null)
            {
                corpseAnim.SetBool("OnTable", false);
            }
            corpse.PickUp(player);
        }
    }

    public void ToggleGrinderState()
    {
        if (!IsPowerWorking()) return;

        // Если идет переработка, выключить нельзя!
        if (state == GrinderState.Grinding) return;

        isGrinderOn = !isGrinderOn;

        // Запуск анимации вдавливания соответствующей кнопки
        if (isGrinderOn)
        {
            if (button1 != null) StartCoroutine(PressButtonRoutine(button1.transform));
        }
        else
        {
            if (button2 != null) StartCoroutine(PressButtonRoutine(button2.transform));
        }

        if (beltScroll != null)
        {
            beltScroll.enabled = isGrinderOn;
        }

        if (isGrinderOn)
        {
            // Если включили и на ленте уже есть труп — запускаем переработку
            if (hasCorpse && placedCorpse != null)
            {
                StartGrinding();
            }
            else
            {
                // Просто звук включения
                if (audioSource != null && startGrindingClip != null)
                {
                    audioSource.PlayOneShot(startGrindingClip);
                }
            }
        }
        else
        {
            if (audioSource != null)
            {
                audioSource.Stop();
            }
        }
    }

    private IEnumerator PressButtonRoutine(Transform buttonTrans)
    {
        if (buttonTrans == null) yield break;
        Vector3 originalPos = buttonTrans.localPosition;
        Vector3 pressedPos = originalPos - new Vector3(0f, buttonPressDistance, 0f);

        float elapsed = 0f;
        float halfDuration = buttonPressDuration / 2f;

        // Вдавливание кнопки
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            buttonTrans.localPosition = Vector3.Lerp(originalPos, pressedPos, elapsed / halfDuration);
            yield return null;
        }
        buttonTrans.localPosition = pressedPos;

        elapsed = 0f;
        // Возврат в исходное положение
        while (elapsed < halfDuration)
        {
            elapsed += Time.deltaTime;
            buttonTrans.localPosition = Vector3.Lerp(pressedPos, originalPos, elapsed / halfDuration);
            yield return null;
        }
        buttonTrans.localPosition = originalPos;
    }

    public void StartGrinding()
    {
        if (state != GrinderState.Idle || !hasCorpse || placedCorpse == null) return;

        if (requireContainerToStart && !IsContainerPlaced())
        {
            Debug.LogWarning("Cannot start grinding: Need container!");
            return;
        }

        grindCoroutine = StartCoroutine(GrindRoutine());
    }

    private void StopGrindingForce()
    {
        isGrinderOn = false;
        if (beltScroll != null) beltScroll.enabled = false;

        if (grindCoroutine != null)
        {
            StopCoroutine(grindCoroutine);
            grindCoroutine = null;
        }

        if (audioSource != null) audioSource.Stop();
        if (grindingParticles != null) grindingParticles.Stop();

        state = GrinderState.Idle;
    }

    public bool IsContainerPlaced()
    {
        if (containerPlacementPoint == null) return false;
        return containerPlacementPoint.IsOccupied();
    }

    private PickUpItem GetPlacedContainer()
    {
        if (containerPlacementPoint == null) return null;

        // 1. Проверяем presetObjectToEnable
        if (containerPlacementPoint.presetObjectToEnable != null && containerPlacementPoint.presetObjectToEnable.activeInHierarchy)
        {
            return containerPlacementPoint.presetObjectToEnable.GetComponent<PickUpItem>();
        }

        // 2. Проверяем presetMappings
        foreach (var mapping in containerPlacementPoint.presetMappings)
        {
            if (mapping.presetObject != null && mapping.presetObject.activeInHierarchy)
            {
                return mapping.presetObject.GetComponent<PickUpItem>();
            }
        }

        // 3. Поиск по сфере OverlapSphere
        Collider[] cols = Physics.OverlapSphere(containerPlacementPoint.transform.position, 0.25f);
        foreach (var c in cols)
        {
            if (c.gameObject == containerPlacementPoint.gameObject || c.transform.IsChildOf(containerPlacementPoint.transform)) continue;
            PickUpItem p = c.GetComponentInParent<PickUpItem>();
            if (p != null && p.gameObject.activeInHierarchy)
            {
                return p;
            }
        }

        return null;
    }

    private IEnumerator GrindRoutine()
    {
        state = GrinderState.Grinding;

        if (audioSource != null && startGrindingClip != null)
        {
            audioSource.clip = startGrindingClip;
            audioSource.loop = true;
            audioSource.Play();
        }

        Vector3 startPos = placedCorpse.transform.position;
        Quaternion startRot = placedCorpse.transform.rotation;

        Vector3 targetPos = corpseTargetPoint != null ? corpseTargetPoint.position : startPos;
        Quaternion targetRot = corpseTargetPoint != null ? corpseTargetPoint.rotation : startRot;

        float elapsed = 0f;
        while (elapsed < corpseMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / corpseMoveDuration;

            if (placedCorpse != null)
            {
                placedCorpse.transform.position = Vector3.Lerp(startPos, targetPos, t);
                placedCorpse.transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            }
            yield return null;
        }

        if (placedCorpse != null)
        {
            placedCorpse.transform.position = targetPos;
            placedCorpse.transform.rotation = targetRot;
        }

        float remainingDelay = fillDelay - corpseMoveDuration;
        if (remainingDelay > 0)
        {
            yield return new WaitForSeconds(remainingDelay);
        }

        // Эффекты наполнения
        if (audioSource != null)
        {
            if (fillContainerClip != null)
            {
                audioSource.PlayOneShot(fillContainerClip);
            }
        }

        if (grindingParticles != null)
        {
            grindingParticles.Play();
        }

        // Наполняем контейнер постепенно
        PickUpItem container = GetPlacedContainer();
        if (container != null)
        {
            ConsumableItem cons = container.GetComponent<ConsumableItem>();
            if (cons == null) cons = container.GetComponentInChildren<ConsumableItem>();

            if (cons != null)
            {
                cons.currentLiquidType = LiquidType.Biomass;
                cons.currentAmount = 0;
                cons.UpdateVisuals();

                if (!string.IsNullOrEmpty(containerChildName))
                {
                    Transform child = container.transform.Find(containerChildName);
                    if (child == null)
                    {
                        child = FindChildRecursive(container.transform, containerChildName);
                    }

                    if (child != null)
                    {
                        child.gameObject.SetActive(true);
                    }
                }

                // Постепенное наполнение контейнера в цикле
                float fillElapsed = 0f;
                int startAmount = 0;
                int targetAmount = cons.maxAmount;

                while (fillElapsed < containerFillDuration)
                {
                    fillElapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(fillElapsed / containerFillDuration);
                    cons.currentAmount = Mathf.RoundToInt(Mathf.Lerp(startAmount, targetAmount, progress));
                    cons.UpdateVisuals();
                    yield return null;
                }

                cons.currentAmount = targetAmount;
                cons.UpdateVisuals();
            }
        }

        // Удаляем труп
        if (placedCorpse != null)
        {
            Destroy(placedCorpse.gameObject);
            placedCorpse = null;
        }
        hasCorpse = false;

        // Если после переработки мясорубка остается включенной
        if (isGrinderOn)
        {
            if (audioSource != null && startGrindingClip != null)
            {
                if (audioSource.clip != startGrindingClip || !audioSource.isPlaying)
                {
                    audioSource.clip = startGrindingClip;
                    audioSource.loop = true;
                    audioSource.Play();
                }
            }
        }

        state = GrinderState.Idle;
        grindCoroutine = null;
    }

    private Transform FindChildRecursive(Transform parent, string nameToFind)
    {
        if (parent.name == nameToFind) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), nameToFind);
            if (result != null) return result;
        }
        return null;
    }

    public string GetInteractPrompt(bool carryingCorpse, KeyCode interactKey, KeyCode toggleKey, int lookArea)
    {
        bool isEn = false;
        try
        {
            if (UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale != null)
            {
                string code = UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocale.Identifier.Code;
                isEn = code.StartsWith("en", System.StringComparison.OrdinalIgnoreCase);
            }
        }
        catch {}

        if (lookArea == 1) // Conveyor Belt
        {
            // На ленте: можно положить (E) или забрать труп (F)
            if (!hasCorpse)
            {
                if (carryingCorpse)
                {
                    string placeStr = (locPromptPlace != null && !locPromptPlace.IsEmpty) ? locPromptPlace.GetLocalizedString() : (isEn ? "Place body" : "Положить тело");
                    return $"<color=#FFD700>[{interactKey}]</color> {placeStr}";
                }
                return ""; // Если в руках ничего нет, на ленте подсказок нет
            }
            else
            {
                // Труп лежит на ленте
                if (state == GrinderState.Idle)
                {
                    string pickupStr = (locPromptPickUp != null && !locPromptPickUp.IsEmpty) ? locPromptPickUp.GetLocalizedString() : (isEn ? "Take body back" : "Забрать тело");
                    return $"<color=#FFD700>[{toggleKey}]</color> {pickupStr}";
                }
                else
                {
                    return (isEn ? "Processing..." : "Идет переработка...");
                }
            }
        }
        else if (lookArea == 2) // ON Button
        {
            if (state == GrinderState.Grinding)
            {
                return (isEn ? "Processing..." : "Идет переработка...");
            }

            if (!IsPowerWorking())
            {
                string noPowerStr = (locPromptNoPower != null && !locPromptNoPower.IsEmpty) ? locPromptNoPower.GetLocalizedString() : (isEn ? "No power" : "Нет питания");
                return $"<color=#FF4444>{noPowerStr}</color>";
            }

            if (isGrinderOn)
            {
                return ""; // Если уже включена, кнопка ON ничего не показывает
            }
            else
            {
                // Показываем "Включить мясорубку", если требуется емкость и она не установлена, пишем предупреждение
                if (requireContainerToStart && !IsContainerPlaced())
                {
                    string needContainerStr = (locPromptNeedContainer != null && !locPromptNeedContainer.IsEmpty) ? locPromptNeedContainer.GetLocalizedString() : (isEn ? "Need container" : "Нужна емкость");
                    return $"<color=#FFCC00>{needContainerStr}</color>";
                }
                
                string startStr = (locPromptStart != null && !locPromptStart.IsEmpty) ? locPromptStart.GetLocalizedString() : (isEn ? "Turn ON grinder" : "Включить мясорубку");
                return $"<color=#FFD700>[{interactKey}]</color> {startStr}";
            }
        }
        else if (lookArea == 3) // OFF Button
        {
            if (state == GrinderState.Grinding)
            {
                return (isEn ? "Processing..." : "Идет переработка...");
            }

            if (!IsPowerWorking())
            {
                string noPowerStr = (locPromptNoPower != null && !locPromptNoPower.IsEmpty) ? locPromptNoPower.GetLocalizedString() : (isEn ? "No power" : "Нет питания");
                return $"<color=#FF4444>{noPowerStr}</color>";
            }

            if (!isGrinderOn)
            {
                return ""; // Если выключена, кнопка OFF ничего не показывает
            }
            else
            {
                if (hasCorpse)
                {
                    return (isEn ? "Processing..." : "Идет переработка...");
                }
                string stopStr = (locPromptStop != null && !locPromptStop.IsEmpty) ? locPromptStop.GetLocalizedString() : (isEn ? "Turn OFF grinder" : "Выключить мясорубку");
                return $"<color=#FFD700>[{interactKey}]</color> {stopStr}";
            }
        }
        else // Housing (lookArea == 0)
        {
            // На корпусе только предупреждение о питании
            if (!IsPowerWorking())
            {
                string noPowerStr = (locPromptNoPower != null && !locPromptNoPower.IsEmpty) ? locPromptNoPower.GetLocalizedString() : (isEn ? "No power" : "Нет питания");
                return $"<color=#FF4444>{noPowerStr}</color>";
            }
            
            if (state == GrinderState.Grinding)
            {
                return (isEn ? "Processing..." : "Идет переработка...");
            }
            
            return "";
        }
    }
}
