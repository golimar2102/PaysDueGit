using UnityEngine;

public class WindowController : MonoBehaviour
{
    [Header("Статус окна")]
    public bool isOpen = false;
    public bool isLocked = false; 

    [Header("Настройки движения")]
    public float openHeightOffset = 1.2f;
    public float slideSpeed = 5f;
    public Vector3 slideAxis = new Vector3(0, 1, 0);

    private Vector3 closedPosition;
    private Vector3 openPosition;
    

    private bool isShaking = false;
    private Vector3 shakeOriginalPos;

    void Start()
    {
        closedPosition = transform.localPosition;
        openPosition = closedPosition + (slideAxis.normalized * openHeightOffset);
    }

    void Update()
    {
        if (isShaking) return;
        Vector3 targetPosition = isOpen ? openPosition : closedPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * slideSpeed);
    }

    public void ToggleWindow()
    {
        if (isLocked)
        {
            Debug.Log("Окно заперто или заколочено!");
            if (!isShaking) StartCoroutine(ShakeWindow());
            return;
        }

        isOpen = !isOpen;
    }
    
    private System.Collections.IEnumerator ShakeWindow()
    {
        isShaking = true;
        shakeOriginalPos = transform.localPosition;
        
        float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // Трясем немного вверх-вниз по оси открытия
            float offset = Mathf.Sin(elapsed * 50f) * 0.05f; 
            transform.localPosition = shakeOriginalPos + (slideAxis.normalized * offset);
            yield return null;
        }

        transform.localPosition = shakeOriginalPos;
        isShaking = false;
    }
}