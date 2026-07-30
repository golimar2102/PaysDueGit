using UnityEngine;

public class CameraTracker : MonoBehaviour
{ 
    [Tooltip("Перетащи сюда корневой объект игрока (например, Felix)")]
    public Transform target; 
    void LateUpdate()
    {
        if (target != null)
        {
            // Position остается неизменной (камера прибита к стене).
            // Меняется только Rotation - камера всегда смотрит точно на цель.
            transform.LookAt(target);
        }
    }
}