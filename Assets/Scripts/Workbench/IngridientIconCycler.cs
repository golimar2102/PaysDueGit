using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class IngredientIconCycler : MonoBehaviour
{
    private Image targetImage;
    private List<Sprite> iconList;
    private float cycleInterval = 1.2f;
    private Coroutine cycleCoroutine;

    public void Init(Image image, List<Sprite> sprites, float interval = 1.2f)
    {
        targetImage = image;
        iconList = sprites;
        cycleInterval = interval;

        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        if (gameObject.activeInHierarchy && iconList != null && iconList.Count > 1 && targetImage != null)
        {
            cycleCoroutine = StartCoroutine(CycleRoutine());
        }
    }

    private IEnumerator CycleRoutine()
    {
        int currentIndex = 0;
        while (true)
        {
            yield return new WaitForSeconds(cycleInterval);
            if (iconList == null || iconList.Count <= 1 || targetImage == null) yield break;

            currentIndex = (currentIndex + 1) % iconList.Count;
            if (iconList[currentIndex] != null)
            {
                targetImage.sprite = iconList[currentIndex];
            }
        }
    }

    void OnEnable()
    {
        if (cycleCoroutine == null && iconList != null && iconList.Count > 1 && targetImage != null)
        {
            cycleCoroutine = StartCoroutine(CycleRoutine());
        }
    }

    void OnDisable()
    {
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }
    }
}