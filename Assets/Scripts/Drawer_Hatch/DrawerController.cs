using UnityEngine;

public class DrawerController : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Настройки ящика")]
    [Tooltip("По какой оси выезжает ящик? Обычно это Z (вперед) или X")]
    public Axis slideAxis = Axis.Z; 
    
    public bool isOpen = false;
    
    [Tooltip("На сколько метров ящик выезжает вперед")]
    public float slideDistance = 0.5f; 
    
    [Tooltip("Скорость выдвижения")]
    public float slideSpeed = 5f;
    
    private Vector3 closedPosition;
    private Vector3 openPosition;

    void Start()
    {
        closedPosition = transform.localPosition;
        
        Vector3 offset = Vector3.zero;
        switch (slideAxis)
        {
            case Axis.X: 
                offset = new Vector3(slideDistance, 0, 0); 
                break;
            case Axis.Y: 
                offset = new Vector3(0, slideDistance, 0); // Редко используется, это движение вверх
                break;
            case Axis.Z: 
                offset = new Vector3(0, 0, slideDistance); 
                break;
        }
        openPosition = closedPosition + offset;
    }

    void Update()
    {
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;
        // Пропускаем Lerp, если уже у цели (экономим CPU на неподвижных ящиках)
        if ((transform.localPosition - targetPosition).sqrMagnitude > 0.00001f)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * slideSpeed);
        }
    }
    
    public void ToggleDrawer()
    {
        isOpen = !isOpen;
    }
}