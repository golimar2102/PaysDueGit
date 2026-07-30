using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Dumpling : MonoBehaviour
{
    public string meatType = "Beef";
    private bool isCollected = false;

    public void Collect()
    {
        if (isCollected) return;
        isCollected = true;
        StartCoroutine(CollectCoroutine());
    }

    private IEnumerator CollectCoroutine()
    {
        // Отключаем все коллайдеры
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            col.enabled = false;
        }

        // Отключаем физику, чтобы лететь ровно
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = GetComponentInChildren<Rigidbody>(true);
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Vector3 startPos = transform.position;
        // ЛЕТИМ на +2 по оси Z
        Vector3 targetPos = startPos + new Vector3(0f, 0f, 2f);
        Vector3 startScale = transform.localScale;

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(startPos, targetPos, progress);
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, progress);

            yield return null;
        }

        // Прибавляем счетчик
        DumplingCounter.Increment(meatType);

        Destroy(gameObject);
    }
}

public static class DumplingCounter
{
    private static Dictionary<string, int> counts = new Dictionary<string, int>()
    {
        { "Beef", 0 },
        { "Pork", 0 },
        { "Canine", 0 },
        { "Feline", 0 },
        { "Avian", 0 }
    };

    public static event System.Action OnCountsChanged;

    public static int GetCount(string typeName)
    {
        if (counts.ContainsKey(typeName))
            return counts[typeName];
        return 0;
    }

    public static void Increment(string typeName)
    {
        if (counts.ContainsKey(typeName))
        {
            counts[typeName]++;
        }
        else
        {
            counts[typeName] = 1;
        }
        OnCountsChanged?.Invoke();
    }

    public static void SetCount(string typeName, int val)
    {
        if (counts.ContainsKey(typeName))
        {
            if (counts[typeName] != val)
            {
                counts[typeName] = val;
                OnCountsChanged?.Invoke();
            }
        }
        else
        {
            counts[typeName] = val;
            OnCountsChanged?.Invoke();
        }
    }

    public static bool Deduct(string typeName, int amount)
    {
        if (counts.ContainsKey(typeName) && counts[typeName] >= amount)
        {
            counts[typeName] -= amount;
            OnCountsChanged?.Invoke();
            return true;
        }
        return false;
    }
}
