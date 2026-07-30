using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    [Header("Скорость течения тумана")]
    [Tooltip("Движение по горизонтали (вправо/влево)")]
    public float scrollSpeedX = 0.02f;

    [Tooltip("Движение по вертикали (вперед/назад)")]
    public float scrollSpeedY = 0.01f;

    private Renderer rend;
    // Кэшируем экземпляр материала один раз, чтобы не создавать новый каждый кадр
    private Material materialInstance;
    private bool useBaseMap;

    void Start()
    {
        rend = GetComponent<Renderer>();
        // Берём (или создаём) экземпляр материала один раз
        materialInstance = rend.material;
        useBaseMap = materialInstance.HasProperty("_BaseMap");
    }

    void Update()
    {
        float offsetX = Time.time * scrollSpeedX;
        float offsetY = Time.time * scrollSpeedY;

        if (useBaseMap)
        {
            materialInstance.SetTextureOffset("_BaseMap", new Vector2(offsetX, offsetY));
        }
        else
        {
            materialInstance.mainTextureOffset = new Vector2(offsetX, offsetY);
        }
    }
}