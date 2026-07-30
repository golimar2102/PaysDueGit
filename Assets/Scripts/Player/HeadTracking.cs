using UnityEngine;

public class HeadTracking : MonoBehaviour
{
    [Header("Кости и Камера")]
    [Tooltip("Раскрой модельку кота в иерархии и перетащи сюда кость шеи или головы")]
    public Transform headBone; 
    
    [Tooltip("Перетащи сюда твою Main Camera (FPS_Camera)")]
    public Transform fpsCamera;

    [Header("Настройки поворота")]
    [Tooltip("Насколько сильно голова поворачивается за камерой (1 - полностью, 0.5 - наполовину)")]
    [Range(0f, 1f)]
    public float lookWeight = 1f;

    // Смещение осей (на случай, если в Blender кость головы была повернута криво)
    public Vector3 rotationOffset = new Vector3(0, 0, 0);

    // LateUpdate вызывается ПОСЛЕ того, как отработает Animator
    void LateUpdate()
    {
        if (headBone == null || fpsCamera == null) return;

        // Берем текущий поворот головы (от анимации)
        Quaternion currentHeadRotation = headBone.rotation;

        // Берем поворот камеры и добавляем смещение (если нужно)
        Quaternion targetRotation = fpsCamera.rotation * Quaternion.Euler(rotationOffset);

        // Плавно или жестко (в зависимости от weight) смешиваем анимацию и поворот камеры
        headBone.rotation = Quaternion.Slerp(currentHeadRotation, targetRotation, lookWeight);
    }
}