using UnityEngine;

public class ScrollingTexture : MonoBehaviour
{
    [Header("Скорость течения тумана")]
    [Tooltip("Движение по горизонтали (вправо/влево)")]
    public float scrollSpeedX = 0.02f;

    [Tooltip("Движение по вертикали (вперед/назад)")]
    public float scrollSpeedY = 0.01f;

    [Tooltip("Активно ли перемещение текстуры")]
    public bool isScrolling = true;

    private Renderer rend;
    private Material materialInstance;
    private bool useBaseMap;

    void Start()
    {
        rend = GetComponent<Renderer>();
        materialInstance = rend.material;
        useBaseMap = materialInstance.HasProperty("_BaseMap");
    }

    void Update()
    {
        if (!isScrolling) return;

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