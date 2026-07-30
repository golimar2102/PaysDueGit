using UnityEngine;

public class CameraManager : MonoBehaviour
{
    [Header("Настройки камер")]
    [Tooltip("Камера от 1-го лица (в голове игрока)")]
    public GameObject firstPersonCamera;
    
    [Tooltip("Камера для вида сверху (Top-Down)")]
    public GameObject topDownCamera;

    [Header("Управление")]
    [Tooltip("Кнопка для смены вида")]
    public KeyCode switchKey = KeyCode.T;

    private bool isFirstPerson = true;

    void Start()
    {
        if (firstPersonCamera != null && topDownCamera != null)
        {
            SetCameraState(true);
        }
        else
        {
            Debug.LogError("Ты забыл назначить камеры в скрипте CameraManager!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            isFirstPerson = !isFirstPerson;
            SetCameraState(isFirstPerson);
        }
    }

    private void SetCameraState(bool isFPS)
    {
        firstPersonCamera.SetActive(isFPS);
        topDownCamera.SetActive(!isFPS);
    }
}