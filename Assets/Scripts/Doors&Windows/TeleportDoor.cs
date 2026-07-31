using UnityEngine;
using UnityEngine.UI;
using System.Collections; 

public class TeleportDoor : MonoBehaviour
{
    [Header("Настройки Телепорта")]
    [Tooltip("Пустой объект или BoxCollider внутри дома, куда появится игрок")]
    public Transform destinationPoint;

    [Header("Телепорт с Эффектом")]
    [Tooltip("Если true, при входе происходит телепорт с черным экраном (или картинкой), звуком и плавным проявлением")]
    public bool isTeleport = false;

    public bool IsTeleport
    {
        get => isTeleport;
        set => isTeleport = value;
    }

    [Tooltip("Необязательное изображение для экрана телепорта (если не задано - будет сплошной черный цвет)")]
    public Sprite teleportOverlaySprite;

    [Tooltip("Цвет затемнения экрана (по умолчанию черный)")]
    public Color fadeColor = Color.black;

    [Tooltip("Звук, проигрываемый во время телепортации")]
    public AudioClip teleportSound;

    [Tooltip("AudioSource для звука (если не задан, задействуется AudioSource на этом объекте или воспроизведется в точке)")]
    public AudioSource audioSource;

    [Range(0f, 1f)]
    [Tooltip("Громкость звука телепортации")]
    public float soundVolume = 1.0f;

    [Tooltip("Задержка в черном экране перед началом плавного проявления (в секундах)")]
    public float blackScreenHoldDuration = 1.0f;

    [Tooltip("Длительность плавного возврата экрана в нормальное состояние (в секундах)")]
    public float fadeOutDuration = 1.0f;

    [Header("Квартирная дверь (Apartment Setup)")]
    [Tooltip("Если true, эта дверь является выходом из квартиры и будет искать заспавненную через DoorSummoner дверь на улице")]
    public bool isApartmentDoor = false;

    [Header("Освещение (День/Ночь)")]
    [Tooltip("Зона, в которую ведет этот телепорт")]
    public GameZone targetZone = GameZone.Farm;

    [Header("Визуал (Раздвижные створки)")]
    public Transform leftDoor;
    public Transform rightDoor;
    [Tooltip("На сколько метров створки разъедутся в стороны")]
    public float slideDistance = 0.2f;
    [Tooltip("Ось раздвигания (Обычно X(1,0,0) или Z(0,0,1))")]
    public Vector3 slideAxis = new Vector3(1, 0, 0);
    public float slideSpeed = 6f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private Vector3 leftOpenPos;
    private Vector3 rightOpenPos;

    private bool isHovered = false;
    private bool isTeleporting = false;

    [Header("Обводка (Outline)")]
    [Tooltip("Перетащи сюда объекты с компонентом Outline. Если оставить пустым, скрипт найдет их сам.")]
    public Outline[] outlines;

    private static Canvas fadeCanvas;
    private static Image fadeImage;

    void Start()
    {
        if (leftDoor != null)
        {
            leftClosedPos = leftDoor.localPosition;
            leftOpenPos = leftClosedPos - (slideAxis.normalized * slideDistance);
        }
        if (rightDoor != null)
        {
            rightClosedPos = rightDoor.localPosition;
            rightOpenPos = rightClosedPos + (slideAxis.normalized * slideDistance);
        }

        if (outlines == null || outlines.Length == 0)
        {
            outlines = GetComponentsInChildren<Outline>(true); 
        }

        SetOutlineState(false);
    }

    void Update()
    {
        if (leftDoor != null)
        {
            Vector3 target = isHovered ? leftOpenPos : leftClosedPos;
            leftDoor.localPosition = Vector3.Lerp(leftDoor.localPosition, target, Time.deltaTime * slideSpeed);
        }

        if (rightDoor != null)
        {
            Vector3 target = isHovered ? rightOpenPos : rightClosedPos;
            rightDoor.localPosition = Vector3.Lerp(rightDoor.localPosition, target, Time.deltaTime * slideSpeed);
        }
    }

    public void SetHover(bool state)
    {
        if (isHovered == state) return; 

        isHovered = state;
        SetOutlineState(state);
    }

    private void SetOutlineState(bool state)
    {
        if (outlines == null) return;

        foreach (Outline outline in outlines)
        {
            if (outline != null) outline.enabled = state;
        }
    }

    public void DoTeleport(GameObject player)
    {
        if (isTeleporting) return;

        if (isTeleport)
        {
            MonoBehaviour runner = (DayNightCycle.Instance != null) ? (MonoBehaviour)DayNightCycle.Instance : this;
            runner.StartCoroutine(TeleportWithEffectCoroutine(player));
        }
        else
        {
            ExecuteTeleport(player);
        }
    }

    private IEnumerator TeleportWithEffectCoroutine(GameObject player)
    {
        isTeleporting = true;

        // 1. Резкое затемнение (черный экран или картинка)
        EnsureFadeScreen();
        if (fadeImage != null)
        {
            if (teleportOverlaySprite != null)
            {
                fadeImage.sprite = teleportOverlaySprite;
                fadeImage.color = new Color(1f, 1f, 1f, 1f);
            }
            else
            {
                fadeImage.sprite = null;
                fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
            }
            if (fadeCanvas != null) fadeCanvas.gameObject.SetActive(true);
        }

        // 2. В это же время проигрывается звук
        if (teleportSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(teleportSound, soundVolume);
            }
            else
            {
                AudioSource src = GetComponent<AudioSource>();
                if (src != null)
                {
                    src.PlayOneShot(teleportSound, soundVolume);
                }
                else
                {
                    AudioSource.PlayClipAtPoint(teleportSound, player.transform.position, soundVolume);
                }
            }
        }

        // 3. Телепортируем игрока на поинт (сначала включится целевая зона)
        ExecuteTeleport(player);

        // 4. Держим черный экран заданное время (по умолчанию 1 сек)
        if (blackScreenHoldDuration > 0f)
        {
            yield return new WaitForSeconds(blackScreenHoldDuration);
        }

        // 5. Плавно возвращаем экран в нормальное состояние
        if (fadeImage != null && fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            Color baseColor = (fadeImage.sprite != null) ? Color.white : fadeColor;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
                fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }

            fadeImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        if (fadeCanvas != null)
        {
            fadeCanvas.gameObject.SetActive(false);
        }

        isTeleporting = false;
    }

    private void ExecuteTeleport(GameObject player)
    {
        // 1. Активируем целевую зону ДО перемещения игрока, чтобы ZoneOptimizationManager сразу включил [ZONE] HUB и ее поинты
        if (DayNightCycle.Instance != null)
        {
            DayNightCycle.Instance.SetZone(targetZone);
        }

        Transform targetPoint = destinationPoint;
        bool hasSummonedTarget = false;
        Vector3 targetPos = Vector3.zero;
        Quaternion targetRot = Quaternion.identity;

        if (isApartmentDoor)
        {
            if (DoorSummoner.Instance != null && DoorSummoner.Instance.ActiveDoorInstance != null)
            {
                GameObject summonedDoor = DoorSummoner.Instance.ActiveDoorInstance;

                Transform customDest = summonedDoor.transform.Find("spawnPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("SpawnPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("destinationPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("DestinationPoint");
                if (customDest == null) customDest = summonedDoor.transform.Find("Spawn");
                if (customDest == null) customDest = summonedDoor.transform.Find("Destination");

                if (customDest != null)
                {
                    targetPoint = customDest;
                }
                else
                {
                    targetPos = summonedDoor.transform.position + summonedDoor.transform.forward * 1.5f;
                    targetRot = summonedDoor.transform.rotation;
                    hasSummonedTarget = true;
                }
            }
        }

        if (targetPoint == null && !hasSummonedTarget)
        {
            return;
        }

        PlayerMovement pm = player.GetComponent<PlayerMovement>();
        if (pm == null) pm = player.GetComponentInChildren<PlayerMovement>();

        if (pm != null)
        {
            if (hasSummonedTarget)
            {
                pm.Teleport(targetPos, targetRot);
            }
            else
            {
                pm.Teleport(targetPoint);
            }
        }
        else
        {
            if (hasSummonedTarget)
            {
                player.transform.position = targetPos;
                player.transform.rotation = targetRot;
            }
            else
            {
                player.transform.position = targetPoint.position;
                player.transform.rotation = targetPoint.rotation;
            }
            Physics.SyncTransforms();
        }
    }

    private void EnsureFadeScreen()
    {
        if (fadeCanvas != null && fadeImage != null) return;

        GameObject existing = GameObject.Find("DynamicFadeCanvas");
        if (existing != null)
        {
            fadeCanvas = existing.GetComponent<Canvas>();
            fadeImage = existing.GetComponentInChildren<Image>();
            if (fadeCanvas != null && fadeImage != null) return;
        }

        GameObject canvasObj = new GameObject("DynamicFadeCanvas");
        fadeCanvas = canvasObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 1000;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        fadeImage = imageObj.AddComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rect = fadeImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
    }
}