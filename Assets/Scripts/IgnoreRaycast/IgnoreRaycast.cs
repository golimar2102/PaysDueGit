using UnityEngine;

public class IgnoreRaycast : MonoBehaviour
{
    private void Awake()
    {
        SetLayerRecursively(gameObject, 2);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        int childCount = obj.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
        }
    }
}