using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization;
using TMPro;

public class TrashSortingController : MonoBehaviour
{
    public static TrashSortingController activeMinigame;

    [Header("Точка стояния игрока")]
    [Tooltip("Точка, куда телепортируется игрок при старте миниигры")]
    public Transform standPoint;

    [Header("Конвейеры")]
    [Tooltip("Скрипты ScrollingTexture у конвейеров, которые будут включаться во время миниигры")]
    public ScrollingTexture[] conveyorTextures;
    [Tooltip("Аудиоисточник звука конвейера")]
    public AudioSource conveyorAudio;

    [Header("Молоток (Кнопка 2)")]
    [Tooltip("Модель молотка")]
    public Transform hammerTransform;
    [Tooltip("Расстояние, на которое молот опускается вниз")]
    public float hammerDropDistance = 1.2f;
    [Tooltip("Скорость опускания молота")]
    public float hammerDropSpeed = 25f;
    [Tooltip("Скорость подъема молота")]
    public float hammerReturnSpeed = 12f;
    [Tooltip("Зона поражения молотом (Trigger Collider под молотом)")]
    public Collider hammerHitZone;
    [Tooltip("Аудиоисточник для звука удара молота")]
    public AudioSource hammerAudio;
    [Tooltip("Звуковой клип удара молота")]
    public AudioClip hammerSound;

    public enum DividerRotationAxis { Y_Axis, Z_Axis }

    [Header("Перегородка / Заслонка (Кнопка 3)")]
    [Tooltip("Модель перегородки")]
    public Transform dividerTransform;
    [Tooltip("Ось поворота перегородки (Y или Z). Ось X остается фиксированной.")]
    public DividerRotationAxis dividerRotationAxis = DividerRotationAxis.Y_Axis;
    [Tooltip("Угол поворота при открытии/включении (например, -39)")]
    public float dividerTargetAngleY = -39f;
    [Tooltip("Скорость поворота перегородки")]
    public float dividerRotationSpeed = 10f;
    [Tooltip("Аудиоисточник для звука перегородки")]
    public AudioSource dividerAudio;
    [Tooltip("Звуковой клип движения перегородки")]
    public AudioClip dividerSound;

    [Header("Траектория движения мусора")]
    [Tooltip("Точка спавна мусора слева")]
    public Transform spawnPoint;
    [Tooltip("Точка развилки у перегородки (где путь делится)")]
    public Transform dividerJunctionPoint;
    [Tooltip("Конечная точка прямого конвейера (справа)")]
    public Transform rightEndPoint;
    [Tooltip("Конечная точка среднего конвейера (по центру)")]
    public Transform middleEndPoint;

    [Header("Выравнивание предметов при спавне")]
    [Tooltip("Использовать поворот точки Spawn Point для ориентирования предметов вдоль конвейера")]
    public bool useSpawnPointRotation = true;
    [Tooltip("Дополнительное смещение поворота спавнящихся предметов (Эйлеровы углы: X, Y, Z)")]
    public Vector3 spawnRotationOffset = Vector3.zero;
    [Tooltip("Дополнительное смещение позиции спавна (X, Y, Z)")]
    public Vector3 spawnPositionOffset = Vector3.zero;

    [Header("База предметов / Мусора")]
    [Tooltip("Кастомные предметы мусора (если пусто, автоматически берутся из InventoryManager.Instance.allItemsDatabase)")]
    public List<TrashItemData> customTrashItems = new List<TrashItemData>();

    [Header("Настройки Раунда")]
    [Tooltip("Минимальное кол-во предметов в раунде")]
    public int minItemsPerRound = 15;
    [Tooltip("Максимальное кол-во предметов в раунде")]
    public int maxItemsPerRound = 50;
    [Tooltip("Базовая скорость движения предметов по конвейеру")]
    public float baseTrashSpeed = 3f;
    [Tooltip("Коэффициент ускорения: чем больше предметов в раунде, тем выше скорость")]
    public float speedPerItemMultiplier = 0.08f;
    [Tooltip("Интервал между спавном предметов (в секундах)")]
    public float spawnInterval = 1.8f;
    [Tooltip("Количество очков за каждое правильное действие")]
    public int scorePerCorrect = 5;

    [Header("Мониторы (1 - 5)")]
    [Tooltip("Монитор 1: Иконка первой цели для Молотков (UI Image, SpriteRenderer или Material)")]
    public UnityEngine.UI.Image monitor1UI;
    public SpriteRenderer monitor1Sprite;
    public Renderer monitor1Renderer;

    [Tooltip("Монитор 2: Иконка второй цели для Молотков")]
    public UnityEngine.UI.Image monitor2UI;
    public SpriteRenderer monitor2Sprite;
    public Renderer monitor2Renderer;

    [Tooltip("Монитор 3: Иконка цели для Перегородки")]
    public UnityEngine.UI.Image monitor3UI;
    public SpriteRenderer monitor3Sprite;
    public Renderer monitor3Renderer;

    [Tooltip("Монитор 4: Текст прогресса (формат X / Y)")]
    public TMP_Text monitor4Text;
    public TextMeshProUGUI monitor4UIText;

    [Tooltip("Монитор 5: Текст заработанных очков")]
    public TMP_Text monitor5Text;
    public TextMeshProUGUI monitor5UIText;

    [Header("Локализованные сообщения (Опционально)")]
    public LocalizedString startPrompt;
    public LocalizedString hammerPrompt;
    public LocalizedString dividerPrompt;
    public LocalizedString stopPrompt;

    public bool IsMinigameActive => isMinigameActive;
    public bool IsDividerOpen => isDividerOpen;

    private bool isMinigameActive = false;
    private PlayerInteract activePlayerInteract;
    private PlayerMovement activePlayerMovement;
    private CharacterController activeCharController;
    private MouseLook activeMouseLook;

    // Игровой процесс раунда
    private int totalRoundItems = 0;
    private int itemsProcessedCount = 0;
    private int currentScore = 0;
    private float currentTrashSpeed = 3f;

    private TrashItemData smashTarget1;
    private TrashItemData smashTarget2;
    private TrashItemData divertTarget;

    private List<TrashItem> activeTrashItems = new List<TrashItem>();
    private Coroutine spawnCoroutine;

    // Сохранение начальных трансформаций
    private Vector3 hammerInitialLocalPos;
    private Vector3 dividerInitialLocalEuler;
    private bool isHammerAnimating = false;
    private bool isDividerOpen = false;
    private Coroutine hammerCoroutine;
    private Coroutine dividerCoroutine;

    void Awake()
    {
        if (hammerTransform != null)
        {
            hammerInitialLocalPos = hammerTransform.localPosition;
        }

        if (dividerTransform != null)
        {
            dividerInitialLocalEuler = dividerTransform.localEulerAngles;
        }
    }

    void Start()
    {
        SetConveyorsActive(false);
    }

    public void StartMinigame(PlayerInteract playerInteract)
    {
        if (isMinigameActive) return;

        activeMinigame = this;
        isMinigameActive = true;
        activePlayerInteract = playerInteract;

        if (activePlayerInteract != null)
        {
            activePlayerMovement = activePlayerInteract.GetComponentInParent<PlayerMovement>();
            activeCharController = activePlayerInteract.GetComponentInParent<CharacterController>();
            activeMouseLook = activePlayerInteract.GetComponentInParent<MouseLook>() ?? activePlayerInteract.GetComponentInChildren<MouseLook>();

            if (activePlayerMovement != null && standPoint != null)
            {
                activePlayerMovement.Teleport(standPoint);
            }

            if (activePlayerMovement != null) activePlayerMovement.enabled = false;
            if (activeCharController != null) activeCharController.enabled = false;
            if (activeMouseLook != null) activeMouseLook.enabled = true;
        }

        SetConveyorsActive(true);

        // Инициализация раунда
        StartNewRound();
    }

    public void StopMinigame()
    {
        if (!isMinigameActive) return;

        isMinigameActive = false;

        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
        }

        // Удаляем активный мусор
        ClearActiveTrash();

        if (activePlayerMovement != null) activePlayerMovement.enabled = true;
        if (activeCharController != null) activeCharController.enabled = true;
        if (activeMouseLook != null) activeMouseLook.enabled = true;

        SetConveyorsActive(false);

        activeMinigame = null;
        activePlayerInteract = null;
        activePlayerMovement = null;
        activeCharController = null;
        activeMouseLook = null;
    }

    private void StartNewRound()
    {
        itemsProcessedCount = 0;
        currentScore = 0;

        totalRoundItems = Random.Range(minItemsPerRound, maxItemsPerRound + 1);
        currentTrashSpeed = baseTrashSpeed + (totalRoundItems * speedPerItemMultiplier);

        List<TrashItemData> db = GetAvailableItemsDatabase();
        if (db.Count > 0)
        {
            List<TrashItemData> shuffled = new List<TrashItemData>(db);
            ShuffleList(shuffled);

            smashTarget1 = shuffled[0];
            smashTarget2 = shuffled.Count > 1 ? shuffled[1] : shuffled[0];
            divertTarget = shuffled.Count > 2 ? shuffled[2] : shuffled[0];
        }
        else
        {
            smashTarget1 = null;
            smashTarget2 = null;
            divertTarget = null;
        }

        // Обновляем иконки на мониторах
        SetMonitorIcon(monitor1UI, monitor1Sprite, monitor1Renderer, smashTarget1 != null ? smashTarget1.icon : null);
        SetMonitorIcon(monitor2UI, monitor2Sprite, monitor2Renderer, smashTarget2 != null ? smashTarget2.icon : null);
        SetMonitorIcon(monitor3UI, monitor3Sprite, monitor3Renderer, divertTarget != null ? divertTarget.icon : null);

        UpdateMonitorTexts();

        // Запускаем спавн
        if (spawnCoroutine != null) StopCoroutine(spawnCoroutine);
        spawnCoroutine = StartCoroutine(SpawnTrashRoutine());
    }

    private IEnumerator SpawnTrashRoutine()
    {
        int spawnedCount = 0;

        while (isMinigameActive && spawnedCount < totalRoundItems)
        {
            yield return new WaitForSeconds(spawnInterval);

            if (!isMinigameActive) break;

            List<TrashItemData> db = GetAvailableItemsDatabase();
            if (db.Count == 0 || spawnPoint == null) continue;

            TrashItemData chosenData = db[Random.Range(0, db.Count)];
            if (chosenData.prefab == null) continue;

            Quaternion spawnRot = (useSpawnPointRotation && spawnPoint != null) 
                ? spawnPoint.rotation * Quaternion.Euler(spawnRotationOffset) 
                : Quaternion.Euler(spawnRotationOffset);
            Vector3 spawnPos = spawnPoint.position + (spawnPoint != null ? spawnPoint.TransformDirection(spawnPositionOffset) : Vector3.zero);

            GameObject spawnedObj = Instantiate(chosenData.prefab, spawnPos, spawnRot);
            TrashItem trashItem = spawnedObj.GetComponent<TrashItem>();
            if (trashItem == null)
            {
                trashItem = spawnedObj.AddComponent<TrashItem>();
            }

            TrashItem.ItemCategory category = TrashItem.ItemCategory.Normal;
            if (smashTarget1 != null && chosenData.itemID == smashTarget1.itemID) category = TrashItem.ItemCategory.SmashTarget;
            else if (smashTarget2 != null && chosenData.itemID == smashTarget2.itemID) category = TrashItem.ItemCategory.SmashTarget;
            else if (divertTarget != null && chosenData.itemID == divertTarget.itemID) category = TrashItem.ItemCategory.DivertTarget;

            trashItem.Initialize(this, chosenData, category, dividerJunctionPoint, rightEndPoint, middleEndPoint, currentTrashSpeed);
            activeTrashItems.Add(trashItem);

            spawnedCount++;
        }
    }

    public void TriggerHammer()
    {
        if (hammerTransform == null) return;

        if (hammerCoroutine != null)
        {
            StopCoroutine(hammerCoroutine);
        }
        hammerCoroutine = StartCoroutine(HammerRoutine());
    }

    private IEnumerator HammerRoutine()
    {
        isHammerAnimating = true;
        Vector3 targetPos = hammerInitialLocalPos - new Vector3(0f, hammerDropDistance, 0f);

        while (Vector3.Distance(hammerTransform.localPosition, targetPos) > 0.01f)
        {
            hammerTransform.localPosition = Vector3.MoveTowards(
                hammerTransform.localPosition, 
                targetPos, 
                hammerDropSpeed * Time.deltaTime
            );
            yield return null;
        }
        hammerTransform.localPosition = targetPos;

        // Воспроизводим звук удара
        PlaySound(hammerAudio, hammerSound);

        // Проверяем уничтожение предметов под молотом
        CheckHammerSmash();

        while (Vector3.Distance(hammerTransform.localPosition, hammerInitialLocalPos) > 0.01f)
        {
            hammerTransform.localPosition = Vector3.MoveTowards(
                hammerTransform.localPosition, 
                hammerInitialLocalPos, 
                hammerReturnSpeed * Time.deltaTime
            );
            yield return null;
        }
        hammerTransform.localPosition = hammerInitialLocalPos;
        isHammerAnimating = false;
    }

    private void CheckHammerSmash()
    {
        if (hammerHitZone == null) return;

        List<TrashItem> itemsToSmash = new List<TrashItem>();

        // 1. Проверяем физическое перекрытие коллайдеров с учетом триггеров
        Collider[] hits = Physics.OverlapBox(
            hammerHitZone.bounds.center, 
            hammerHitZone.bounds.extents, 
            hammerHitZone.transform.rotation,
            ~0,
            QueryTriggerInteraction.Collide
        );

        foreach (var h in hits)
        {
            if (h == null) continue;
            TrashItem item = h.GetComponentInParent<TrashItem>() ?? h.GetComponentInChildren<TrashItem>();
            if (item != null && !item.isSmashed && !itemsToSmash.Contains(item))
            {
                itemsToSmash.Add(item);
            }
        }

        // 2. Дополнительная проверка по активным предметам на сцене
        for (int i = activeTrashItems.Count - 1; i >= 0; i--)
        {
            TrashItem item = activeTrashItems[i];
            if (item != null && !item.isSmashed && !itemsToSmash.Contains(item))
            {
                if (hammerHitZone.bounds.Contains(item.transform.position))
                {
                    itemsToSmash.Add(item);
                }
                else
                {
                    Collider itemCol = item.GetComponent<Collider>() ?? item.GetComponentInChildren<Collider>();
                    if (itemCol != null && hammerHitZone.bounds.Intersects(itemCol.bounds))
                    {
                        itemsToSmash.Add(item);
                    }
                }
            }
        }

        // Ломаем абсолютно ВСЕ предметы, попавшие под молот
        foreach (var item in itemsToSmash)
        {
            if (item != null && !item.isSmashed)
            {
                item.Smash();
            }
        }
    }

    public void OnTrashItemSmashed(TrashItem item)
    {
        if (activeTrashItems.Contains(item))
        {
            activeTrashItems.Remove(item);
        }

        itemsProcessedCount++;

        // Очки начисляются если уничтожен правильный предмет-цель
        if (item.category == TrashItem.ItemCategory.SmashTarget)
        {
            currentScore += scorePerCorrect;
        }

        UpdateMonitorTexts();
        CheckRoundCompletion();
    }

    public void OnTrashItemReachedEnd(TrashItem item, bool reachedMiddle)
    {
        if (activeTrashItems.Contains(item))
        {
            activeTrashItems.Remove(item);
        }

        itemsProcessedCount++;

        if (reachedMiddle)
        {
            // Очки начисляются если на средний конвейер попал правильный предмет
            if (item.category == TrashItem.ItemCategory.DivertTarget)
            {
                currentScore += scorePerCorrect;
            }
        }
        else
        {
            // Очки начисляются если обычный мусор доехал до правого конца
            if (item.category == TrashItem.ItemCategory.Normal)
            {
                currentScore += scorePerCorrect;
            }
        }

        UpdateMonitorTexts();
        CheckRoundCompletion();
    }

    private void CheckRoundCompletion()
    {
        if (itemsProcessedCount >= totalRoundItems)
        {
            // Раунд завершен!
            StartCoroutine(RoundCompletedRoutine());
        }
    }

    private IEnumerator RoundCompletedRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        if (isMinigameActive)
        {
            // Запускаем следующий раунд
            StartNewRound();
        }
    }

    private void UpdateMonitorTexts()
    {
        string progressStr = $"{itemsProcessedCount} / {totalRoundItems}";
        SetMonitorText(monitor4Text, monitor4UIText, progressStr);

        string scoreStr = $"{currentScore}";
        SetMonitorText(monitor5Text, monitor5UIText, scoreStr);
    }

    public void ToggleDivider()
    {
        if (dividerTransform == null) return;

        isDividerOpen = !isDividerOpen;

        if (dividerCoroutine != null)
        {
            StopCoroutine(dividerCoroutine);
        }
        dividerCoroutine = StartCoroutine(RotateDividerRoutine(isDividerOpen));

        PlaySound(dividerAudio, dividerSound);
    }

    private IEnumerator RotateDividerRoutine(bool open)
    {
        Vector3 targetEuler = dividerInitialLocalEuler;
        if (open)
        {
            if (dividerRotationAxis == DividerRotationAxis.Y_Axis)
            {
                targetEuler.y += dividerTargetAngleY;
            }
            else
            {
                targetEuler.z += dividerTargetAngleY;
            }
        }
        Quaternion targetRotation = Quaternion.Euler(targetEuler);

        while (Quaternion.Angle(dividerTransform.localRotation, targetRotation) > 0.1f)
        {
            dividerTransform.localRotation = Quaternion.Slerp(
                dividerTransform.localRotation, 
                targetRotation, 
                Time.deltaTime * dividerRotationSpeed
            );
            yield return null;
        }
        dividerTransform.localRotation = targetRotation;
    }

    public List<TrashItemData> GetAvailableItemsDatabase()
    {
        if (customTrashItems != null && customTrashItems.Count > 0)
        {
            return customTrashItems;
        }

        List<TrashItemData> result = new List<TrashItemData>();

        if (InventoryManager.Instance != null && InventoryManager.Instance.allItemsDatabase != null)
        {
            foreach (GameObject prefab in InventoryManager.Instance.allItemsDatabase)
            {
                if (prefab == null) continue;
                PickUpItem p = prefab.GetComponent<PickUpItem>() ?? prefab.GetComponentInChildren<PickUpItem>();
                if (p != null)
                {
                    result.Add(new TrashItemData(p.itemID.ToString(), p.itemName, prefab, p.itemIcon));
                }
            }
        }

        return result;
    }

    [Header("Настройки Иконки на Спрайтах (SpriteRenderer)")]
    [Tooltip("Автоматически масштабировать SpriteRenderer, чтобы иконка не вылезала за границы экрана")]
    public bool autoFitSpriteSize = true;
    [Tooltip("Максимальный размер иконки в 3D единицах (ширина/высота)")]
    public float maxSpriteSize = 0.8f;
    [Tooltip("Сбрасывать локальный поворот SpriteRenderer в 0, если объект был повернут")]
    public bool resetSpriteRotation = false;

    private void SetMonitorIcon(UnityEngine.UI.Image uiImg, SpriteRenderer spriteRend, Renderer meshRend, Sprite icon)
    {
        if (uiImg != null)
        {
            uiImg.sprite = icon;
            uiImg.enabled = (icon != null);
        }

        if (spriteRend != null)
        {
            spriteRend.sprite = icon;
            spriteRend.enabled = (icon != null);

            if (icon != null && autoFitSpriteSize)
            {
                float spriteW = icon.bounds.size.x;
                float spriteH = icon.bounds.size.y;
                if (spriteW > 0 && spriteH > 0)
                {
                    float scaleFactor = maxSpriteSize / Mathf.Max(spriteW, spriteH);
                    spriteRend.transform.localScale = new Vector3(scaleFactor, scaleFactor, 1f);
                }
            }

            if (resetSpriteRotation)
            {
                spriteRend.transform.localRotation = Quaternion.identity;
            }
        }

        if (meshRend != null)
        {
            if (icon != null && meshRend.material != null)
            {
                Texture2D tex = icon.texture;

                if (meshRend.material.HasProperty("_BaseMap"))
                {
                    meshRend.material.SetTexture("_BaseMap", tex);
                    float scaleX = icon.rect.width / tex.width;
                    float scaleY = icon.rect.height / tex.height;
                    float offsetX = icon.rect.x / tex.width;
                    float offsetY = icon.rect.y / tex.height;
                    meshRend.material.SetTextureScale("_BaseMap", new Vector2(scaleX, scaleY));
                    meshRend.material.SetTextureOffset("_BaseMap", new Vector2(offsetX, offsetY));
                }
                if (meshRend.material.HasProperty("_MainTex"))
                {
                    meshRend.material.SetTexture("_MainTex", tex);
                    float scaleX = icon.rect.width / tex.width;
                    float scaleY = icon.rect.height / tex.height;
                    float offsetX = icon.rect.x / tex.width;
                    float offsetY = icon.rect.y / tex.height;
                    meshRend.material.SetTextureScale("_MainTex", new Vector2(scaleX, scaleY));
                    meshRend.material.SetTextureOffset("_MainTex", new Vector2(offsetX, offsetY));
                }

                // Устанавливаем цвет материала в белый, иначе из-за черного цвета (black Material) текстура остается полностью черной
                if (meshRend.material.HasProperty("_BaseColor"))
                {
                    meshRend.material.SetColor("_BaseColor", Color.white);
                }
                if (meshRend.material.HasProperty("_Color"))
                {
                    meshRend.material.SetColor("_Color", Color.white);
                }
                meshRend.material.color = Color.white;
            }
            else if (meshRend.material != null)
            {
                if (meshRend.material.HasProperty("_BaseColor"))
                {
                    meshRend.material.SetColor("_BaseColor", Color.black);
                }
                if (meshRend.material.HasProperty("_Color"))
                {
                    meshRend.material.SetColor("_Color", Color.black);
                }
                meshRend.material.color = Color.black;
            }
        }
    }

    private void SetMonitorText(TMP_Text tmpText, TextMeshProUGUI uiText, string text)
    {
        if (tmpText != null) tmpText.text = text;
        if (uiText != null) uiText.text = text;
    }

    private void ClearActiveTrash()
    {
        foreach (var item in activeTrashItems)
        {
            if (item != null && item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
        activeTrashItems.Clear();
    }

    private void SetConveyorsActive(bool active)
    {
        if (conveyorTextures != null)
        {
            foreach (var conv in conveyorTextures)
            {
                if (conv != null)
                {
                    conv.isScrolling = active;
                    conv.enabled = active;
                }
            }
        }

        if (conveyorAudio != null)
        {
            if (active)
            {
                conveyorAudio.loop = true;
                if (!conveyorAudio.isPlaying)
                {
                    conveyorAudio.Play();
                }
            }
            else
            {
                conveyorAudio.Stop();
            }
        }
    }

    private void PlaySound(AudioSource source, AudioClip clip)
    {
        if (source != null)
        {
            if (clip != null)
            {
                source.PlayOneShot(clip);
            }
            else
            {
                source.Play();
            }
        }
        else if (clip != null && standPoint != null)
        {
            AudioSource.PlayClipAtPoint(clip, standPoint.position);
        }
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}
