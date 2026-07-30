using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DoughCuttingController : MonoBehaviour
{
    public enum CuttingState { Inactive, Flashing, Dragging, Cutting, Completed }

    [Header("Настройки формы (Cutter)")]
    [Tooltip("3D модель формы (вырубки) в сцене")]
    public GameObject cutterVisual;

    [Tooltip("Компонент Outline для формы (если пустой, найдется автоматически или добавится)")]
    public Outline cutterOutline;

    [Tooltip("Скорость мигания (пульсации) контура формы")]
    public float flashSpeed = 4f;

    [Tooltip("Высота парения формы над тестом при перетаскивании")]
    public float hoverHeight = 0.02f;

    [Tooltip("Глубина опускания формы при вырезании")]
    public float cutDepth = 0.005f;

    [Tooltip("Длительность анимации вырезания")]
    public float cutDuration = 0.4f;

    [Tooltip("Угол прокрутки формы при вырезании")]
    public float twistAngle = 30f;

    [Header("Настройки кружков теста")]
    [Tooltip("Преаб маленького кружка теста, который спавнится при вырезании")]
    public GameObject doughCirclePrefab;

    [Tooltip("Точка (Anchor), куда улетают кружки теста")]
    public Transform stackAnchor;

    [Tooltip("Толщина одного кружка теста для смещения стопки")]
    public float circleThickness = 0.015f;

    [Tooltip("Максимальное количество кружков, которое нужно вырезать")]
    public int maxCuts = 5;

    [Tooltip("Минимальное расстояние между вырезами, чтобы избежать наложения")]
    public float minDistanceBetweenCuts = 0.12f;

    [Tooltip("Автоматически рассчитывать минимальное расстояние на основе размера формочки")]
    public bool autoCalculateDistance = true;

    [Tooltip("Запас в процентах от диаметра формочки (например, 0.05 = 5%, 0.10 = 10%)")]
    public float cutterSizeMarginPercent = 0.05f;

    private List<Vector3> cutPositions = new List<Vector3>();

    [Tooltip("Время полета кружка до стопки")]
    public float flyDuration = 0.6f;

    [Tooltip("Высота дуги полета")]
    public float flyArcHeight = 0.15f;

    [Header("Звуки")]
    [Tooltip("Звук вырезания")]
    public AudioSource cutSound;
    [Tooltip("Звук полета/приземления кружка")]
    public AudioSource flySound;

    public CuttingState currentState { get; private set; } = CuttingState.Inactive;

    private Vector3 originalCutterPos;
    private Quaternion originalCutterRot;
    public int currentCutCount = 0;
    private List<GameObject> spawnedCircles = new List<GameObject>();
    private DoughRollingController rollingController;

    void Start()
    {
        rollingController = GetComponent<DoughRollingController>();

        if (cutterVisual != null)
        {
            originalCutterPos = cutterVisual.transform.localPosition;
            originalCutterRot = cutterVisual.transform.localRotation;

            if (cutterOutline == null)
            {
                cutterOutline = cutterVisual.GetComponent<Outline>();
                if (cutterOutline == null)
                {
                    cutterOutline = cutterVisual.GetComponentInChildren<Outline>(true);
                }
                if (cutterOutline == null)
                {
                    // Динамически добавляем Outline для мигания, если его нет
                    cutterOutline = cutterVisual.AddComponent<Outline>();
                    cutterOutline.OutlineMode = Outline.Mode.OutlineAll;
                    cutterOutline.OutlineColor = Color.yellow;
                    cutterOutline.OutlineWidth = 4f;
                }
            }

            if (cutterOutline != null)
            {
                cutterOutline.enabled = false;
            }

            // Автоматически рассчитываем диаметр формочки с заданным процентом запаса
            if (autoCalculateDistance)
            {
                Renderer cutterRenderer = cutterVisual.GetComponent<Renderer>();
                if (cutterRenderer == null) cutterRenderer = cutterVisual.GetComponentInChildren<Renderer>(true);
                if (cutterRenderer != null)
                {
                    float diameter = Mathf.Max(cutterRenderer.bounds.size.x, cutterRenderer.bounds.size.z);
                    if (diameter > 0.01f)
                    {
                        minDistanceBetweenCuts = diameter * (1f + cutterSizeMarginPercent);
                    }
                }
            }
        }
    }

    public void StartFlashing()
    {
        if (currentState == CuttingState.Completed) return;
        
        currentState = CuttingState.Flashing;
        if (cutterOutline != null)
        {
            cutterOutline.enabled = true;
        }
    }

    public void ResetCutter()
    {
        if (cutterVisual != null)
        {
            cutterVisual.transform.localPosition = originalCutterPos;
            cutterVisual.transform.localRotation = originalCutterRot;
        }

        if (cutterOutline != null)
        {
            cutterOutline.enabled = false;
            cutterOutline.OutlineColor = Color.yellow;
        }
    }

    public void ResetCuttingProgress(bool clearStack = false)
    {
        currentCutCount = 0;
        cutPositions.Clear();
        
        List<GameObject> remainingCircles = new List<GameObject>();
        foreach (var circle in spawnedCircles)
        {
            if (circle != null)
            {
                // Если родитель - doughVisual, то это "дырка", её удаляем всегда при старте нового теста.
                // Если clearStack = true, то удаляем абсолютно всё (включая стопку).
                if (clearStack || (rollingController != null && rollingController.doughVisual != null && circle.transform.parent == rollingController.doughVisual.transform))
                {
                    Destroy(circle);
                }
                else
                {
                    remainingCircles.Add(circle);
                }
            }
        }
        spawnedCircles = remainingCircles;
        
        currentState = CuttingState.Inactive;
        ResetCutter();
    }

    public void StopCuttingMode()
    {
        ResetCutter();
        if (currentState == CuttingState.Dragging || currentState == CuttingState.Cutting)
        {
            currentState = CuttingState.Flashing;
        }
    }

    void Update()
    {
        if (rollingController == null || !rollingController.isViewing)
        {
            if (currentState != CuttingState.Inactive)
            {
                StopCuttingMode();
            }
            return;
        }

        switch (currentState)
        {
            case CuttingState.Flashing:
                UpdateFlashingState();
                break;
            case CuttingState.Dragging:
                UpdateDraggingState();
                break;
        }
    }

    private void UpdateFlashingState()
    {
        // Эффект мигания/пульсации толщины контура
        if (cutterOutline != null)
        {
            cutterOutline.enabled = true;
            cutterOutline.OutlineColor = Color.yellow;
            cutterOutline.OutlineWidth = Mathf.Lerp(1.5f, 6f, Mathf.PingPong(Time.time * flashSpeed, 1f));
        }

        // Проверяем клик по форме для начала миниигры
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                if (hit.collider.gameObject == cutterVisual || hit.transform.IsChildOf(cutterVisual.transform))
                {
                    currentState = CuttingState.Dragging;
                    if (cutterOutline != null)
                    {
                        // Оставляем тонкую подсветку при таскании
                        cutterOutline.OutlineWidth = 2f;
                    }
                }
            }
        }
    }

    private void UpdateDraggingState()
    {
        // Временно отключаем коллайдеры формы, чтобы рейкаст прошел сквозь нее
        Collider[] cutterColliders = cutterVisual.GetComponentsInChildren<Collider>(true);
        foreach (var c in cutterColliders) c.enabled = false;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, 100f, ~0, QueryTriggerInteraction.Collide);

        // Возвращаем коллайдеры на место
        foreach (var c in cutterColliders) c.enabled = true;

        // Проверяем, находится ли точка на тесте
        bool isOverDough = false;

        if (didHit)
        {
            // Форма следует за курсором над поверхностью
            cutterVisual.transform.position = hit.point + Vector3.up * hoverHeight;
            cutterVisual.transform.localRotation = originalCutterRot;

            isOverDough = rollingController.doughVisual != null && 
                (hit.collider.gameObject == rollingController.doughVisual || 
                 hit.transform.IsChildOf(rollingController.doughVisual.transform));

            // Проверяем расстояние до всех прошлых вырезов (считаем в 2D по горизонтали XZ, чтобы избежать погрешностей высоты)
            bool tooClose = false;
            Vector2 currentXZ = new Vector2(hit.point.x, hit.point.z);
            foreach (var pos in cutPositions)
            {
                Vector2 pastXZ = new Vector2(pos.x, pos.z);
                if (Vector2.Distance(currentXZ, pastXZ) < minDistanceBetweenCuts)
                {
                    tooClose = true;
                    break;
                }
            }

            // Динамически меняем цвет подсветки: зеленый (можно резать), красный (нельзя)
            if (cutterOutline != null)
            {
                cutterOutline.enabled = true;
                if (!isOverDough || tooClose)
                {
                    cutterOutline.OutlineColor = Color.red;
                    cutterOutline.OutlineWidth = 3f;
                }
                else
                {
                    cutterOutline.OutlineColor = Color.green;
                    cutterOutline.OutlineWidth = 3f;
                }
            }

            // Если кликаем ЛКМ на тесте и позиция корректна
            if (Input.GetMouseButtonDown(0))
            {
                if (isOverDough && !tooClose)
                {
                    cutPositions.Add(hit.point);
                    StartCoroutine(CutCoroutine(hit.point));
                }
            }
        }
        else
        {
            // Если луч никуда не попал, делаем подсветку красной
            if (cutterOutline != null)
            {
                cutterOutline.OutlineColor = Color.red;
                cutterOutline.OutlineWidth = 2f;
            }
        }
    }

    private IEnumerator CutCoroutine(Vector3 targetWorldPos)
    {
        currentState = CuttingState.Cutting;

        Vector3 hoverPos = targetWorldPos + Vector3.up * hoverHeight;
        Vector3 downPos = targetWorldPos - Vector3.up * cutDepth;

        // 1. Опускаем форму на тесто
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * (1f / (cutDuration * 0.3f));
            cutterVisual.transform.position = Vector3.Lerp(hoverPos, downPos, t);
            yield return null;
        }
        cutterVisual.transform.position = downPos;

        // 2. Проворачиваем форму (эффект прорезания)
        if (cutSound != null) cutSound.Play();
        Quaternion baseRot = cutterVisual.transform.localRotation;
        Quaternion twistRot = baseRot * Quaternion.Euler(0f, 0f, twistAngle);

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * (1f / (cutDuration * 0.4f));
            cutterVisual.transform.localRotation = Quaternion.Slerp(baseRot, twistRot, t);
            yield return null;
        }
        cutterVisual.transform.localRotation = twistRot;

        // 3. Создаем кружок теста, который полетит в стопку
        if (doughCirclePrefab != null)
        {
            GameObject circle = Instantiate(doughCirclePrefab, targetWorldPos, Quaternion.identity);

            // Отключаем PickUpItem сразу, чтобы остановить левитацию и вращение
            PickUpItem pickup = circle.GetComponent<PickUpItem>();
            if (pickup == null) pickup = circle.GetComponentInChildren<PickUpItem>(true);
            if (pickup != null)
            {
                pickup.enabled = false;
            }

            // Отключаем Outline и Light на спавнящемся объекте
            Outline outlineComp = circle.GetComponent<Outline>();
            if (outlineComp == null) outlineComp = circle.GetComponentInChildren<Outline>(true);
            if (outlineComp != null) outlineComp.enabled = false;

            Light[] lights = circle.GetComponentsInChildren<Light>(true);
            foreach (var l in lights) l.enabled = false;

            // Безопасно отключаем встроенный Halo компонент через рефлексию
            foreach (var comp in circle.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "Halo")
                {
                    if (comp is Behaviour b) b.enabled = false;
                }
            }

            foreach (var child in circle.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("glow") || nameLower.Contains("light") || nameLower.Contains("halo") || nameLower.Contains("effect"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // Копируем материал теста, чтобы кружок выглядел как тесто и не светился/был черным
            if (rollingController != null && rollingController.doughSkinnedMesh != null)
            {
                Renderer circleRenderer = circle.GetComponent<Renderer>();
                if (circleRenderer == null) circleRenderer = circle.GetComponentInChildren<Renderer>(true);
                if (circleRenderer != null)
                {
                    circleRenderer.sharedMaterial = rollingController.doughSkinnedMesh.sharedMaterial;
                }
            }

            StartCoroutine(FlyToStackCoroutine(circle));

            // Создаем круг-вырез (дырку) на месте вырезания с текстурой стола
            GameObject hole = Instantiate(doughCirclePrefab, targetWorldPos, Quaternion.identity);
            hole.transform.SetParent(rollingController.doughVisual.transform, true);
            
            // Выравниваем локальное вращение с родителем, чтобы избежать скоса (skew/shear)
            hole.transform.localRotation = Quaternion.identity;

            // Определяем, какая локальная ось родителя направлена вертикально
            Vector3 localUp = rollingController.doughVisual.transform.InverseTransformDirection(Vector3.up);
            float absX = Mathf.Abs(localUp.x);
            float absY = Mathf.Abs(localUp.y);
            float absZ = Mathf.Abs(localUp.z);

            Vector3 localOffset = Vector3.zero;
            Vector3 targetLocalScale = Vector3.one;

            if (absX > absY && absX > absZ)
            {
                localOffset.x = 0.001f * Mathf.Sign(localUp.x);
                targetLocalScale.x = 0.01f;
            }
            else if (absZ > absY && absZ > absX)
            {
                localOffset.z = 0.001f * Mathf.Sign(localUp.z);
                targetLocalScale.z = 0.01f;
            }
            else
            {
                localOffset.y = 0.001f * Mathf.Sign(localUp.y);
                targetLocalScale.y = 0.01f;
            }

            hole.transform.localPosition += localOffset;
            hole.transform.localScale = targetLocalScale;

            // Отключаем всю физику, коллайдеры и скрипты подбора на вырезе
            PickUpItem holePickup = hole.GetComponent<PickUpItem>();
            if (holePickup == null) holePickup = hole.GetComponentInChildren<PickUpItem>(true);
            if (holePickup != null) holePickup.enabled = false;

            foreach (var c in hole.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var r in hole.GetComponentsInChildren<Rigidbody>(true)) r.isKinematic = true;
            foreach (var l in hole.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var comp in hole.GetComponentsInChildren<Component>(true))
            {
                if (comp != null && comp.GetType().Name == "Halo")
                {
                    if (comp is Behaviour b) b.enabled = false;
                }
            }

            foreach (var child in hole.GetComponentsInChildren<Transform>(true))
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("glow") || nameLower.Contains("light") || nameLower.Contains("halo") || nameLower.Contains("effect"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // Устанавливаем текстуру/материал стола
            Renderer tableRenderer = rollingController.GetComponent<Renderer>();
            if (tableRenderer == null && rollingController.surfaceCollider != null)
            {
                tableRenderer = rollingController.surfaceCollider.GetComponent<Renderer>();
            }

            Renderer holeRenderer = hole.GetComponent<Renderer>();
            if (holeRenderer == null) holeRenderer = hole.GetComponentInChildren<Renderer>(true);

            if (holeRenderer != null)
            {
                if (tableRenderer != null)
                {
                    holeRenderer.sharedMaterial = tableRenderer.sharedMaterial;
                }
                else
                {
                    // Если материал стола не найден, делаем его темным
                    holeRenderer.sharedMaterial.color = new Color(0.15f, 0.1f, 0.05f);
                }
            }
            
            // Добавляем вырез в список заспавненных объектов, чтобы удалить при ресете
            spawnedCircles.Add(hole);
        }

        // 4. Поднимаем и возвращаем в исходное вращение
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * (1f / (cutDuration * 0.3f));
            cutterVisual.transform.position = Vector3.Lerp(downPos, hoverPos, t);
            cutterVisual.transform.localRotation = Quaternion.Slerp(twistRot, baseRot, t);
            yield return null;
        }
        cutterVisual.transform.position = hoverPos;
        cutterVisual.transform.localRotation = baseRot;

        currentCutCount++;

        if (currentCutCount >= maxCuts)
        {
            currentState = CuttingState.Completed;
            yield return new WaitForSeconds(0.4f); // Даем время начаться анимации последнего полета
            CompleteCuttingGame();
        }
        else
        {
            currentState = CuttingState.Dragging;
        }
    }

    private IEnumerator FlyToStackCoroutine(GameObject circle)
    {
        if (circle == null || stackAnchor == null) yield break;

        if (flySound != null) flySound.Play();

        Vector3 startPos = circle.transform.position;
        int stackIndex = 0;
        foreach (Transform child in stackAnchor)
        {
            if (child.name.Contains("CounterText") || child.GetComponent<TMPro.TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
            stackIndex++;
        }
        
        // Преобразуем локальные координаты стопки в мировые
        Vector3 targetPos = stackAnchor.TransformPoint(new Vector3(0f, stackIndex * circleThickness, 0f));
        Quaternion startRot = circle.transform.rotation;
        Quaternion targetRot = stackAnchor.rotation;

        float elapsed = 0f;
        circle.transform.parent = null;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flyDuration);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            float heightOffset = Mathf.Sin(progress * Mathf.PI) * flyArcHeight;
            currentPos += stackAnchor.up * heightOffset;

            circle.transform.position = currentPos;
            circle.transform.rotation = Quaternion.Slerp(startRot, targetRot, progress);

            yield return null;
        }

        if (circle != null)
        {
            circle.transform.SetParent(stackAnchor);
            circle.transform.localPosition = new Vector3(0f, stackIndex * circleThickness, 0f);
            circle.transform.localRotation = Quaternion.identity;

            Collider col = circle.GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Rigidbody rb = circle.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // Скрываем рендерер, если это не первый кружок в стопке (чтобы избежать роста башни)
            Renderer r = circle.GetComponent<Renderer>();
            if (r == null) r = circle.GetComponentInChildren<Renderer>(true);
            if (stackIndex > 0 && r != null)
            {
                r.enabled = false;
            }

            spawnedCircles.Add(circle);
        }
    }

    private void CompleteCuttingGame()
    {
        ResetCutter();
        
        if (rollingController != null)
        {
            rollingController.ClearDoughState();
        }
    }
}
