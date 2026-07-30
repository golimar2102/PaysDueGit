using System.Collections;
using UnityEngine;

public class SkinnedBlinkController : MonoBehaviour
{
    [Header("Настройки рендера")]
    public SkinnedMeshRenderer bodyRenderer;

    [Header("Материалы")]
    public Material eyesOpenMaterial;
    public Material eyesClosedMaterial;

    [Header("Тайминги")]
    public float minBlinkWait = 2f;
    public float maxBlinkWait = 6f;
    public float blinkDuration = 0.12f;

    // Кэшированный индекс лицевого слота и массив материалов
    private int faceSlotIndex = -1;
    private Material[] cachedMats;

    // Кэшированные WaitForSeconds — не создаём новый объект при каждом морге
    private WaitForSeconds waitBlink;

    void Start()
    {
        if (bodyRenderer == null) return;

        waitBlink = new WaitForSeconds(blinkDuration);

        // Находим индекс лицевого материала один раз
        Material[] mats = bodyRenderer.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].name.Contains("Face"))
            {
                faceSlotIndex = i;
                break;
            }
        }

        // Кэшируем рабочий массив нужного размера
        cachedMats = new Material[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            cachedMats[i] = mats[i];

        StartCoroutine(BlinkRoutine());
    }

    IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minBlinkWait, maxBlinkWait));

            ChangeFaceMaterial(eyesClosedMaterial);
            yield return waitBlink;

            ChangeFaceMaterial(eyesOpenMaterial);

            if (Random.value > 0.8f)
            {
                yield return waitBlink;
                ChangeFaceMaterial(eyesClosedMaterial);
                yield return waitBlink;
                ChangeFaceMaterial(eyesOpenMaterial);
            }
        }
    }

    void ChangeFaceMaterial(Material newMat)
    {
        if (faceSlotIndex < 0) return;

        // Читаем текущие материалы в кэшированный массив без лишней аллокации
        bodyRenderer.GetMaterials(new System.Collections.Generic.List<Material>(cachedMats));
        cachedMats[faceSlotIndex] = newMat;
        bodyRenderer.materials = cachedMats;
    }
}