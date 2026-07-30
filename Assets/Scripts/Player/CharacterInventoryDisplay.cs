using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CharacterInventoryDisplay : MonoBehaviour
{
    [Header("Камера персонажа")]
    [Tooltip("Ортографическая камера, направленная на персонажа")]
    public Camera characterCamera;

    [Header("Настройки RenderTexture")]
    [Tooltip("Авто-размер по RawImage (рекомендуется). Если выкл — используются Width/Height ниже.")]
    public bool autoSizeFromRect = true;
    [Tooltip("Множитель разрешения (2 = вдвое чётче чем RawImage, рекомендуется 2-4)")]
    public int resolutionMultiplier = 2;
    [Tooltip("Ширина текстуры (только если autoSizeFromRect = false)")]
    public int textureWidth = 200;
    [Tooltip("Высота текстуры (только если autoSizeFromRect = false)")]
    public int textureHeight = 400;

    [Header("Качество")]
    [Tooltip("Point — чёткий пиксель-арт. Bilinear/Trilinear — сглаженный 3D.")]
    public FilterMode filterMode = FilterMode.Point;

    private RenderTexture _renderTexture;
    private RawImage _rawImage;

    void Awake()
    {
        _rawImage = GetComponent<RawImage>();
    }

    void Start()
    {
        if (characterCamera == null)
        {
            Debug.LogWarning("[CharacterInventoryDisplay] Не назначена Camera персонажа");
            return;
        }

        CreateRenderTexture();
    }

    private void CreateRenderTexture()
    {
        // Освобождаем старую текстуру если была
        if (_renderTexture != null)
        {
            characterCamera.targetTexture = null;
            _renderTexture.Release();
            Destroy(_renderTexture);
        }

        int w = textureWidth;
        int h = textureHeight;

        if (autoSizeFromRect)
        {
            // Берём размер RawImage и умножаем на resolutionMultiplier
            Rect rect = (_rawImage.transform as RectTransform).rect;
            w = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rect.width)))  * resolutionMultiplier;
            h = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(rect.height))) * resolutionMultiplier;
        }

        // NOTE: не ставим antiAliasing — URP не поддерживает MSAA на RenderTexture через скрипт
        _renderTexture = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        _renderTexture.filterMode = filterMode;
        _renderTexture.Create();

        characterCamera.targetTexture = _renderTexture;
        _rawImage.texture = _renderTexture;
    }

    void OnDestroy()
    {
        if (characterCamera != null)
            characterCamera.targetTexture = null;

        if (_renderTexture != null)
        {
            _renderTexture.Release();
            Destroy(_renderTexture);
        }
    }

    public void Rebuild()
    {
        if (characterCamera != null)
            CreateRenderTexture();
    }
}
