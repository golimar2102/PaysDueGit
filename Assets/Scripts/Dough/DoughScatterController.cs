using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro; // Используем TextMeshPro для качественного 3D текста

public class DoughScatterController : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Контроллер раскатки теста для проверки режима просмотра")]
    public DoughRollingController rollingController;

    [Tooltip("Точка (Anchor), где изначально лежат кружки в стопке")]
    public Transform stackAnchor;

    [Tooltip("20 точек (Anchors) на столе, куда разлетаются кружки")]
    public Transform[] scatterAnchors;

    [Tooltip("Коллайдер стопки для клика. Если пустой, добавится автоматически на stackAnchor")]
    public Collider stackCollider;

    [Header("Настройки полета")]
    [Tooltip("Время полета кружка до точки")]
    public float flyDuration = 0.5f;

    [Tooltip("Высота дуги полета")]
    public float flyArcHeight = 0.1f;

    [Tooltip("Толщина одного кружка теста для смещения стопки при сборе назад")]
    public float circleThickness = 0.015f;

    [Header("Звуки")]
    [Tooltip("Звук разлета кружков")]
    public AudioSource scatterSound;

    [Header("Счетчик заготовок")]
    [Tooltip("Компонент TextMeshPro для парящих цифр над стопкой. Если пустой, создастся автоматически.")]
    public TextMeshPro counterText;

    [Tooltip("Смещение счетчика относительно stackAnchor. Рассчитывается автоматически на основе его позиции в инспекторе.")]
    public Vector3 counterOffset = new Vector3(0f, 0.06f, 0f);

    [Header("Настройки пельменей")]
    [Tooltip("Префаб готового пельменя, который спавнится при защипывании заготовки")]
    public GameObject dumplingPrefab;

    [Tooltip("Эффект дыма при защипывании пельменя")]
    public ParticleSystem smokeEffectPrefab;

    [Tooltip("UI элемент для отображения счетчика пельменей. Если пустой, создастся автоматически.")]
    public TextMeshProUGUI dumplingCounterText;

    private GameObject dynamicCounterCanvas = null;
    private GameObject hoveredDumpling = null;
    private GameObject lastHoveredDumpling = null;

    private bool isAnimating = false;
    private int lastChildCount = -1;
    private KeyCode shootKey = KeyCode.Mouse0;

    void OnEnable()
    {
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
        RefreshKeyBindings();

        DumplingCounter.OnCountsChanged += UpdateDumplingCounterText;
        UpdateDumplingCounterText();
    }

    void OnDisable()
    {
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
        DumplingCounter.OnCountsChanged -= UpdateDumplingCounterText;
    }

    private void RefreshKeyBindings()
    {
        shootKey = (KeyCode)PlayerPrefs.GetInt("Key_Shoot", (int)KeyCode.Mouse0);
    }

    private void EnsureDoughCircleStates()
    {
        if (stackAnchor != null)
        {
            foreach (Transform child in stackAnchor)
            {
                if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
                if (child.GetComponent<DoughCircleState>() == null)
                {
                    child.gameObject.AddComponent<DoughCircleState>();
                }
            }
        }
        if (scatterAnchors != null)
        {
            foreach (var anchor in scatterAnchors)
            {
                if (anchor != null)
                {
                    foreach (Transform child in anchor)
                    {
                        if (child.GetComponent<DoughCircleState>() == null)
                        {
                            child.gameObject.AddComponent<DoughCircleState>();
                        }
                    }
                }
            }
        }
    }

    void Start()
    {
        if (rollingController == null)
        {
            rollingController = GetComponent<DoughRollingController>();
        }
        EnsureDoughCircleStates();

        if (stackAnchor == null && rollingController != null)
        {
            // Пытаемся найти stackAnchor у контроллера вырезания
            DoughCuttingController cutting = rollingController.GetComponent<DoughCuttingController>();
            if (cutting != null)
            {
                stackAnchor = cutting.stackAnchor;
            }
        }

        if (stackCollider == null && stackAnchor != null)
        {
            stackCollider = stackAnchor.GetComponent<Collider>();
            if (stackCollider == null)
            {
                // Создаем приподнятый триггер-коллайдер увеличенного размера, чтобы клик проходил гарантированно
                BoxCollider box = stackAnchor.gameObject.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(0.2f, 0.4f, 0.2f);
                box.center = new Vector3(0f, 0.2f, 0f);
                stackCollider = box;
            }
        }

        // Автоматически создаем или находим 3D TextMeshPro для счетчика над стопкой
        if (counterText == null && stackAnchor != null)
        {
            counterText = stackAnchor.GetComponentInChildren<TextMeshPro>();
            if (counterText == null)
            {
                GameObject textObj = new GameObject("StackCounterText_TMP");
                textObj.transform.SetParent(rollingController != null ? rollingController.transform : null);
                
                counterText = textObj.AddComponent<TextMeshPro>();
                counterText.alignment = TextAlignmentOptions.Center;
                counterText.fontSize = 2f; 
                counterText.color = Color.white;
                counterText.fontStyle = FontStyles.Bold;
            }
        }

        if (counterText != null && stackAnchor != null)
        {
            // Запоминаем исходное смещение, если счетчик был настроен вручную в инспекторе
            if (counterText.transform.parent == stackAnchor)
            {
                counterOffset = counterText.transform.localPosition;
            }
            else if (counterText.transform.parent != (rollingController != null ? rollingController.transform : null))
            {
                // Если счетчик не дочерний, переводим его мировую позицию в локальное смещение относительно stackAnchor
                counterOffset = stackAnchor.InverseTransformPoint(counterText.transform.position);
            }
            
            // Отвязываем счетчик от стопки и вешаем на родителя стола (чтобы избежать деформации текста при масштабировании/повороте стопки)
            counterText.transform.SetParent(rollingController != null ? rollingController.transform : null);
        }

        // Настройка UI счетчиков пельменей
        if (dumplingCounterText == null)
        {
            CreateDynamicDumplingCounterUI();
        }
        else
        {
            UpdateDumplingCounterText();
        }
    }

    void Update()
    {
        // Работает только если игрок находится в режиме просмотра стола и анимация не запущена
        if (rollingController == null || !rollingController.isViewing || isAnimating)
        {
            // Скрываем счетчик пельменей, если мы не смотрим на доску
            if (dynamicCounterCanvas != null && dynamicCounterCanvas.activeSelf)
            {
                dynamicCounterCanvas.SetActive(false);
            }
            else if (dumplingCounterText != null && dumplingCounterText.gameObject.activeSelf && dynamicCounterCanvas == null)
            {
                dumplingCounterText.gameObject.SetActive(false);
            }
            return;
        }

        // Показываем счетчик пельменей, когда мы смотрим на доску
        if (dynamicCounterCanvas != null && !dynamicCounterCanvas.activeSelf)
        {
            dynamicCounterCanvas.SetActive(true);
        }
        else if (dumplingCounterText != null && !dumplingCounterText.gameObject.activeSelf && dynamicCounterCanvas == null)
        {
            dumplingCounterText.gameObject.SetActive(true);
        }

        // Если на столе есть тесто или идет процесс вырезания, заготовки раскладывать НЕЛЬЗЯ
        bool tableHasDough = rollingController.hasDough || 
                             (rollingController.doughVisual != null && rollingController.doughVisual.activeSelf) ||
                             (rollingController.cuttingController != null && 
                              (rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Flashing || 
                               rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Dragging || 
                               rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Cutting));
        if (tableHasDough) return;

        // Perform hover detection
        PerformHoverRaycast();
        // Drag & drop logic
        if (MeatContainerController.currentlyDraggingCell == null)
        {
            // We are not dragging. Check if we start dragging meat ball or trigger click actions
            if (Input.GetKeyDown(shootKey))
            {
                if (hoveredDumpling != null)
                {
                    Dumpling dumpling = hoveredDumpling.GetComponent<Dumpling>();
                    if (dumpling != null)
                    {
                        dumpling.Collect();
                    }
                    hoveredDumpling = null;
                }
                else
                {
                    // Can we take meat balls? Only if dough circles are scattered (i.e. stackAnchor has no circles or there are circles in scatterAnchors)
                    bool hasScatteredCircles = false;
                    if (scatterAnchors != null)
                    {
                        foreach (var anchor in scatterAnchors)
                        {
                            if (anchor != null && anchor.childCount > 0)
                            {
                                hasScatteredCircles = true;
                                break;
                            }
                        }
                    }

                    if (hasScatteredCircles && MeatContainerController.hoveredCell != null && MeatContainerController.hoveredCell.isFilled && MeatContainerController.hoveredCell.currentPortions > 0)
                    {
                        // Start dragging meat ball
                        MeatContainerController.currentlyDraggingCell = MeatContainerController.hoveredCell;

                        // Create drag ghost
                        if (MeatContainerController.currentlyDraggingCell.meatFillingPrefab != null)
                        {
                            MeatContainerController.dragGhostInstance = Instantiate(MeatContainerController.currentlyDraggingCell.meatFillingPrefab);
                        }
                        else
                        {
                            // Fallback sphere
                            MeatContainerController.dragGhostInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            MeatContainerController.dragGhostInstance.transform.localScale = new Vector3(0.04f, 0.02f, 0.04f);
                            Renderer r = MeatContainerController.dragGhostInstance.GetComponent<Renderer>();
                            if (r != null)
                            {
                                r.material.color = new Color(0.6f, 0.2f, 0.2f);
                            }
                        }

                        // Set Ignore Raycast layer (2) recursively on ghost
                        SetLayerRecursively(MeatContainerController.dragGhostInstance, 2);

                        // Destroy all colliders on ghost instance so raycasts don't hit it
                        Collider[] ghostColliders = MeatContainerController.dragGhostInstance.GetComponentsInChildren<Collider>(true);
                        foreach (var col in ghostColliders)
                        {
                            Destroy(col);
                        }

                        // Initial position for ghost
                        UpdateDragGhostPosition();
                        Debug.Log($"[MeatDrag] Started dragging meat ball from cell: {MeatContainerController.currentlyDraggingCell.name}");
                    }
                    else if (MeatContainerController.hoveredBlank != null)
                    {
                        // Clicked a scattered blank (or meat ball inside it)
                        DoughCircleState state = MeatContainerController.hoveredBlank.GetComponent<DoughCircleState>();
                        if (state != null && state.isFilled)
                        {
                            if (MeatContainerController.hoveredMeatBall)
                            {
                                // Return meat ball back to cell
                                if (state.originCell != null)
                                {
                                    state.originCell.ReturnPortion();
                                    Debug.Log($"[MeatDrag] Returned meat ball to cell: {state.originCell.name}");
                                }
                                if (state.meatVisual != null)
                                {
                                    Destroy(state.meatVisual);
                                }
                                state.isFilled = false;
                                state.filledMeatItemID = -1;
                                state.originCell = null;
                                state.meatVisual = null;

                                MeatContainerController.hoveredMeatBall = false;
                                MeatContainerController.hoveredBlank = null;
                            }
                            else
                            {
                                // Pinch/wrap dough into a dumpling
                                Vector3 spawnPos = MeatContainerController.hoveredBlank.transform.position;
                                StartCoroutine(PinchDoughCoroutine(MeatContainerController.hoveredBlank, state.meatVisual, spawnPos));
                                MeatContainerController.hoveredBlank = null;
                            }
                        }
                    }
                    else
                    {
                        // If we didn't click anything else, check for stack scatter click
                        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);

                        bool hitStack = false;
                        foreach (var hit in hits)
                        {
                            if (hit.collider == stackCollider || hit.transform.IsChildOf(stackAnchor))
                            {
                                hitStack = true;
                                break;
                            }
                        }

                        if (hitStack)
                        {
                            TryScatterCircles();
                        }
                    }
                }
            }
        }
        else
        {
            // We are dragging!
            if (Input.GetKey(shootKey))
            {
                UpdateDragGhostPosition();
            }

            if (Input.GetKeyUp(shootKey))
            {
                Debug.Log($"[MeatDrag] Mouse released. hoveredBlank={(MeatContainerController.hoveredBlank != null ? MeatContainerController.hoveredBlank.name : "null")}, currentlyDraggingCell={(MeatContainerController.currentlyDraggingCell != null ? MeatContainerController.currentlyDraggingCell.name : "null")}");

                // Try dropping meat ball
                if (MeatContainerController.hoveredBlank != null)
                {
                    DoughCircleState state = MeatContainerController.hoveredBlank.GetComponent<DoughCircleState>();
                    if (state == null)
                    {
                        state = MeatContainerController.hoveredBlank.AddComponent<DoughCircleState>();
                    }

                    Debug.Log($"[MeatDrag] Target blank state: isFilled={state.isFilled}");

                    if (!state.isFilled)
                    {
                        PlaceMeatOnBlank(MeatContainerController.hoveredBlank, MeatContainerController.currentlyDraggingCell);
                        Debug.Log("[MeatDrag] Meat ball dropped successfully!");
                    }
                }
                else
                {
                    Debug.LogWarning("[MeatDrag] Drop failed: no hovered blank under cursor!");
                }

                // Cleanup
                if (MeatContainerController.dragGhostInstance != null)
                {
                    Destroy(MeatContainerController.dragGhostInstance);
                    MeatContainerController.dragGhostInstance = null;
                }
                MeatContainerController.currentlyDraggingCell = null;
            }
        }
    }

    private void PerformHoverRaycast()
    {
        MeatContainerController.hoveredCell = null;
        MeatContainerController.hoveredBlank = null;
        MeatContainerController.hoveredMeatBall = false;
        hoveredDumpling = null;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, ~0, QueryTriggerInteraction.Collide);
        if (hits.Length > 0)
        {
            // Sort by distance so we hit the closest object first
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 tableHitPoint = Vector3.zero;
            bool hitTable = false;

            foreach (var hit in hits)
            {
                // Проверяем, попали ли в пельмень в первую очередь
                Dumpling dumpling = hit.collider.GetComponent<Dumpling>();
                if (dumpling == null) dumpling = hit.collider.GetComponentInParent<Dumpling>();
                if (dumpling != null)
                {
                    hoveredDumpling = dumpling.gameObject;
                    break;
                }

                // 1. Check if we hit a MeatContainerCell
                MeatContainerCell cell = hit.collider.GetComponent<MeatContainerCell>();
                if (cell == null) cell = hit.collider.GetComponentInParent<MeatContainerCell>();
                if (cell != null)
                {
                    MeatContainerController.hoveredCell = cell;
                    break;
                }

                // 2. Check if we hit a scattered blank directly OR its anchor
                if (scatterAnchors != null)
                {
                    foreach (var anchor in scatterAnchors)
                    {
                        if (anchor != null)
                        {
                            if (hit.transform == anchor)
                            {
                                // We hit the anchor itself! Find the circle inside it
                                foreach (Transform child in anchor)
                                {
                                    if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
                                    MeatContainerController.hoveredBlank = child.gameObject;
                                    break;
                                }

                                // Treat hitting the anchor as a table surface hit too
                                tableHitPoint = hit.point;
                                hitTable = true;

                                if (MeatContainerController.hoveredBlank != null)
                                {
                                    break;
                                }
                            }
                            else if (hit.transform.IsChildOf(anchor))
                            {
                                Transform current = hit.transform;
                                while (current != null && current.parent != anchor)
                                {
                                    current = current.parent;
                                }
                                if (current != null)
                                {
                                    MeatContainerController.hoveredBlank = current.gameObject;

                                    // Check if we hit the meat ball itself
                                    DoughCircleState state = current.GetComponent<DoughCircleState>();
                                    if (state != null && state.isFilled && state.meatVisual != null)
                                    {
                                        if (hit.transform == state.meatVisual.transform || hit.transform.IsChildOf(state.meatVisual.transform))
                                        {
                                            MeatContainerController.hoveredMeatBall = true;
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }

                if (MeatContainerController.hoveredBlank != null)
                {
                    break;
                }

                // 3. Otherwise, if it's not a cell or blank, treat as table surface hit
                tableHitPoint = hit.point;
                hitTable = true;
            }

            // 4. Proximity-based detection fallback (if raycast didn't hit a blank directly but hit the table surface)
            if (MeatContainerController.hoveredBlank == null && hitTable && scatterAnchors != null)
            {
                float closestDistance = 0.1f; // 10 cm radius around cursor
                GameObject closestBlank = null;

                foreach (var anchor in scatterAnchors)
                {
                    if (anchor != null && anchor.childCount > 0)
                    {
                        foreach (Transform child in anchor)
                        {
                            if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;

                            float dist = Vector3.Distance(tableHitPoint, child.position);
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                closestBlank = child.gameObject;
                            }
                        }
                    }
                }

                if (closestBlank != null)
                {
                    MeatContainerController.hoveredBlank = closestBlank;
                }
            }

            // Diagnostic log every 30 frames while dragging/hovering
            bool shouldLog = (MeatContainerController.currentlyDraggingCell != null || (MeatContainerController.hoveredBlank != null && MeatContainerController.hoveredMeatBall)) && Time.frameCount % 30 == 0;
            if (shouldLog)
            {
                string hitList = "";
                foreach (var h in hits)
                {
                    hitList += $"{h.collider.gameObject.name} (parent: {h.collider.transform.parent?.name}), ";
                }
                Debug.Log($"[MeatDrag] PerformHoverRaycast: hits={hitList} | hitTable={hitTable}, hoveredCell={(MeatContainerController.hoveredCell != null ? MeatContainerController.hoveredCell.name : "null")}, hoveredBlank={(MeatContainerController.hoveredBlank != null ? MeatContainerController.hoveredBlank.name : "null")}, hoveredMeatBall={MeatContainerController.hoveredMeatBall}");
            }
        }

        // Подсвечиваем наведенный пельмень
        if (hoveredDumpling != lastHoveredDumpling)
        {
            if (lastHoveredDumpling != null)
            {
                Outline outlineComp = lastHoveredDumpling.GetComponent<Outline>();
                if (outlineComp != null) outlineComp.enabled = false;
            }
            if (hoveredDumpling != null)
            {
                Outline outlineComp = hoveredDumpling.GetComponent<Outline>();
                if (outlineComp == null)
                {
                    outlineComp = hoveredDumpling.AddComponent<Outline>();
                    outlineComp.OutlineMode = Outline.Mode.OutlineAll;
                    outlineComp.OutlineColor = Color.white;
                    outlineComp.OutlineWidth = 3f;
                }
                outlineComp.enabled = true;
            }
            lastHoveredDumpling = hoveredDumpling;
        }
    }

    private void UpdateDragGhostPosition()
    {
        if (MeatContainerController.dragGhostInstance == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
        {
            MeatContainerController.dragGhostInstance.transform.position = hit.point + Vector3.up * 0.005f;
        }
        else
        {
            float tableY = (scatterAnchors != null && scatterAnchors.Length > 0 && scatterAnchors[0] != null) ? scatterAnchors[0].position.y : 0f;
            Plane tablePlane = new Plane(Vector3.up, new Vector3(0f, tableY + 0.005f, 0f));
            if (tablePlane.Raycast(ray, out float enter))
            {
                MeatContainerController.dragGhostInstance.transform.position = ray.GetPoint(enter);
            }
        }
    }

    private void TryScatterCircles()
    {
        if (stackAnchor == null) return;

        EnsureDoughCircleStates();

        // Собираем все кружки из стопки (игнорируя сам счетчик текста)
        List<GameObject> circles = new List<GameObject>();
        foreach (Transform child in stackAnchor)
        {
            if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
            circles.Add(child.gameObject);
        }

        if (circles.Count == 0) return;

        StartCoroutine(ScatterCoroutine(circles));
    }

    private IEnumerator ScatterCoroutine(List<GameObject> circles)
    {
        isAnimating = true;

        for (int i = 0; i < circles.Count; i++)
        {
            GameObject circle = circles[i];
            Transform targetAnchor = FindNextEmptyAnchor();

            if (targetAnchor != null)
            {
                // Привязываем кружок к новой точке сразу, чтобы она считалась занятой
                circle.transform.SetParent(targetAnchor);
                StartCoroutine(FlyToAnchor(circle, targetAnchor));

                if (scatterSound != null)
                {
                    scatterSound.PlayOneShot(scatterSound.clip);
                }
                yield return new WaitForSeconds(0.08f); // Небольшая задержка для эффекта «раздачи карт»
            }
        }

        isAnimating = false;
    }

    private Transform FindNextEmptyAnchor()
    {
        if (scatterAnchors == null) return null;

        foreach (var anchor in scatterAnchors)
        {
            if (anchor != null && anchor.childCount == 0)
            {
                return anchor;
            }
        }
        return null;
    }

    private IEnumerator FlyToAnchor(GameObject circle, Transform targetAnchor)
    {
        // Включаем рендерер обратно, чтобы кружок был виден при полете и на столе
        Renderer r = circle.GetComponent<Renderer>();
        if (r == null) r = circle.GetComponentInChildren<Renderer>(true);
        if (r != null) r.enabled = true;

        Vector3 startPos = circle.transform.position;
        Quaternion startRot = circle.transform.rotation;

        Vector3 targetPos = targetAnchor.position;
        Quaternion targetRot = targetAnchor.rotation;

        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            if (circle == null) yield break;

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / flyDuration);

            // Линейная интерполяция пути с параболической дугой вверх
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            float heightOffset = Mathf.Sin(progress * Mathf.PI) * flyArcHeight;
            currentPos += Vector3.up * heightOffset;

            circle.transform.position = currentPos;
            circle.transform.rotation = Quaternion.Slerp(startRot, targetRot, progress);

            yield return null;
        }

        if (circle != null)
        {
            circle.transform.position = targetPos;
            circle.transform.rotation = targetRot;

            // Включаем все коллайдеры кружка, чтобы с ним можно было взаимодействовать на столе
            Collider[] cols = circle.GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                col.enabled = true;
            }
        }
    }

    void LateUpdate()
    {
        if (stackAnchor != null && counterText != null)
        {
            // Считаем только заготовки, игнорируя сам счетчик
            int currentCount = 0;
            foreach (Transform child in stackAnchor)
            {
                if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
                currentCount++;
            }
            
            // Обновляем текст только если количество заготовок изменилось
            if (currentCount != lastChildCount)
            {
                lastChildCount = currentCount;
                counterText.text = currentCount > 0 ? currentCount.ToString() : "";
            }

            // Позиционируем и поворачиваем счетчик только если в стопке есть заготовки
            if (currentCount > 0)
            {
                // Позиционируем счетчик на основе сохраненного смещения относительно stackAnchor в мировом пространстве
                counterText.transform.position = stackAnchor.TransformPoint(counterOffset);

                // Заставляем текст всегда смотреть на камеру (эффект Billboard)
                if (Camera.main != null)
                {
                    counterText.transform.rotation = Camera.main.transform.rotation;
                }
            }
            else
            {
                counterText.text = "";
            }

            // Логика мерцания заготовок (в стопке или разложенных)
            UpdateFlashingEffects(currentCount);
        }
    }

    private GameObject GetFirstCircleInStack()
    {
        if (stackAnchor == null) return null;
        
        foreach (Transform child in stackAnchor)
        {
            if (child.name.Contains("CounterText") || child.GetComponent<TextMeshPro>() != null || child.GetComponent<TextMesh>() != null) continue;
            return child.gameObject;
        }
        return null;
    }

    private void UpdateFlashingEffects(int currentCount)
    {
        // Мерцаем только если игрок смотрит на стол, на столе нет теста и анимация не идет
        bool tableHasDough = rollingController != null && (
            rollingController.hasDough || 
            (rollingController.doughVisual != null && rollingController.doughVisual.activeSelf) ||
            (rollingController.cuttingController != null && 
             (rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Flashing || 
              rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Dragging || 
              rollingController.cuttingController.currentState == DoughCuttingController.CuttingState.Cutting))
        );

        bool shouldFlash = rollingController != null && 
                           rollingController.isViewing && 
                           !tableHasDough && 
                           !isAnimating;

        // 1. Мерцание первой заготовки в стопке
        GameObject firstCircle = GetFirstCircleInStack();
        if (firstCircle != null)
        {
            Outline outline = firstCircle.GetComponent<Outline>();
            bool flashStack = shouldFlash && currentCount > 0;

            if (flashStack)
            {
                if (outline == null)
                {
                    outline = firstCircle.AddComponent<Outline>();
                    outline.OutlineMode = Outline.Mode.OutlineAll;
                    outline.OutlineColor = Color.yellow;
                    outline.OutlineWidth = 3f;
                }
                
                outline.enabled = true;
                outline.OutlineColor = Color.yellow;
                outline.OutlineWidth = Mathf.Lerp(1.5f, 6f, Mathf.PingPong(Time.time * 4f, 1f));
            }
            else
            {
                if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }

        // 2. Отключаем мерцание для разложенных на столе заготовок (поскольку они не должны мерцать на столе)
        if (scatterAnchors != null)
        {
            foreach (var anchor in scatterAnchors)
            {
                if (anchor == null) continue;
                
                foreach (Transform child in anchor)
                {
                    Outline outline = child.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.enabled = false;
                    }
                }
            }
        }
    }

    public void GatherCirclesToStack()
    {
        if (stackAnchor == null || scatterAnchors == null) return;

        foreach (var anchor in scatterAnchors)
        {
            if (anchor != null && anchor.childCount > 0)
            {
                // Собираем все дочерние кружки из этой точки разлета
                List<Transform> children = new List<Transform>();
                foreach (Transform child in anchor)
                {
                    children.Add(child);
                }

                foreach (var child in children)
                {
                    DoughCircleState state = child.GetComponent<DoughCircleState>();
                    if (state != null && state.isFilled && state.originCell != null)
                    {
                        state.originCell.ReturnPortion();
                    }

                    // Удаляем фарш перед сбором заготовки
                    Transform meatVisual = child.Find("MeatFillingVisual");
                    if (meatVisual != null)
                    {
                        Destroy(meatVisual.gameObject);
                    }

                    if (state != null)
                    {
                        state.isFilled = false;
                        state.filledMeatItemID = -1;
                        state.originCell = null;
                        state.meatVisual = null;
                    }

                    // Возвращаем в родительскую точку стопки
                    child.SetParent(stackAnchor);

                    // Рассчитываем индекс в стопке (игнорируя счетчик)
                    int stackIndex = 0;
                    foreach (Transform sChild in stackAnchor)
                    {
                        if (sChild.name.Contains("CounterText") || sChild.GetComponent<TextMeshPro>() != null || sChild.GetComponent<TextMesh>() != null) continue;
                        if (sChild != child) stackIndex++;
                    }

                    // Сбрасываем локальные координаты
                    child.localPosition = new Vector3(0f, stackIndex * circleThickness, 0f);
                    child.localRotation = Quaternion.identity;

                    // Отключаем все коллайдеры и физику
                    Collider[] cols = child.GetComponentsInChildren<Collider>(true);
                    foreach (var col in cols)
                    {
                        col.enabled = false;
                    }

                    Rigidbody rb = child.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;

                    // Оставляем видимым только первый (нижний) кружок стопки
                    Renderer r = child.GetComponent<Renderer>();
                    if (r == null) r = child.GetComponentInChildren<Renderer>(true);
                    if (r != null)
                    {
                        r.enabled = (stackIndex == 0);
                    }
                }
            }
        }
    }

    private void PlaceMeatOnBlank(GameObject blank, MeatContainerCell cell)
    {
        if (blank == null || cell == null) return;

        GameObject meatVisualObj = null;
        // Создаем визуальную модель фарша на заготовке
        if (cell.meatFillingPrefab != null)
        {
            meatVisualObj = Instantiate(cell.meatFillingPrefab, blank.transform);
            meatVisualObj.name = "MeatFillingVisual";
            // Размещаем по центру заготовки с небольшим подъемом
            meatVisualObj.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            meatVisualObj.transform.localRotation = Quaternion.identity;

            // Убедимся, что у модельки фарша есть коллайдер для рейкаста
            Collider c = meatVisualObj.GetComponentInChildren<Collider>(true);
            if (c == null)
            {
                SphereCollider sc = meatVisualObj.AddComponent<SphereCollider>();
                sc.center = Vector3.zero;
                sc.radius = 0.5f; // стандартный размер сферы
            }
        }
        else
        {
            // Резервный кубик/сфера для визуализации, если префаб не задан
            meatVisualObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            meatVisualObj.name = "MeatFillingVisual";
            meatVisualObj.transform.SetParent(blank.transform);
            meatVisualObj.transform.localPosition = new Vector3(0f, 0.005f, 0f);
            meatVisualObj.transform.localScale = new Vector3(0.04f, 0.02f, 0.04f);
            
            Renderer r = meatVisualObj.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = new Color(0.6f, 0.2f, 0.2f);
            }
            
            // НЕ удаляем коллайдер резервной сферы, чтобы на нее можно было навестись и кликнуть!
        }

        DoughCircleState state = blank.GetComponent<DoughCircleState>();
        if (state == null)
        {
            state = blank.AddComponent<DoughCircleState>();
        }
        state.isFilled = true;
        state.filledMeatItemID = cell.requiredItemID;
        state.originCell = cell;
        state.meatVisual = meatVisualObj;

        // Забираем одну порцию мяса
        cell.TakePortion();
    }

    private IEnumerator PinchDoughCoroutine(GameObject blank, GameObject meatVisual, Vector3 spawnPos)
    {
        // Cache meat type at start before any modifications/yields
        string meatType = "Beef";
        if (blank != null)
        {
            DoughCircleState state = blank.GetComponent<DoughCircleState>();
            if (state != null && state.originCell != null)
            {
                meatType = state.originCell.meatTypeName;
            }
        }

        // Unparent immediately to prevent being gathered on exit
        blank.transform.SetParent(null);

        // Disable all colliders so they cannot be hovered or clicked again
        Collider[] cols = blank.GetComponentsInChildren<Collider>(true);
        foreach (var col in cols)
        {
            col.enabled = false;
        }

        // Play smoke/particle effect immediately
        if (smokeEffectPrefab != null)
        {
            ParticleSystem smoke = Instantiate(smokeEffectPrefab, spawnPos, Quaternion.identity);
            smoke.Play();
            Destroy(smoke.gameObject, smoke.main.duration + 0.5f);
        }

        Vector3 startScaleBlank = blank.transform.localScale;
        Vector3 startScaleMeat = meatVisual != null ? meatVisual.transform.localScale : Vector3.zero;

        float duration = 0.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            if (blank != null)
            {
                blank.transform.localScale = Vector3.Lerp(startScaleBlank, Vector3.zero, progress);
            }
            if (meatVisual != null)
            {
                meatVisual.transform.localScale = Vector3.Lerp(startScaleMeat, Vector3.zero, progress);
            }
            yield return null;
        }

        GameObject spawnedDumpling = null;

        // Spawn dumpling
        if (dumplingPrefab != null)
        {
            spawnedDumpling = Instantiate(dumplingPrefab, spawnPos, Quaternion.identity);
            spawnedDumpling.name = "Dumpling";

            Rigidbody rb = spawnedDumpling.GetComponent<Rigidbody>();
            if (rb == null) rb = spawnedDumpling.GetComponentInChildren<Rigidbody>(true);
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
        else
        {
            // Fallback capsule representing dumpling
            spawnedDumpling = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            spawnedDumpling.name = "Dumpling";
            spawnedDumpling.transform.position = spawnPos;
            spawnedDumpling.transform.localScale = new Vector3(0.04f, 0.02f, 0.06f);

            Renderer r = spawnedDumpling.GetComponent<Renderer>();
            if (r != null)
            {
                r.material.color = Color.white;
            }

            Rigidbody rb = spawnedDumpling.GetComponent<Rigidbody>();
            if (rb == null) rb = spawnedDumpling.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        if (spawnedDumpling != null)
        {
            Dumpling dumplingComp = spawnedDumpling.GetComponent<Dumpling>();
            if (dumplingComp == null) dumplingComp = spawnedDumpling.AddComponent<Dumpling>();
            dumplingComp.meatType = meatType;
        }

        Destroy(blank);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        if (obj == null) return;
        obj.layer = newLayer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }

    void OnDestroy()
    {
        DumplingCounter.OnCountsChanged -= UpdateDumplingCounterText;
        if (dynamicCounterCanvas != null)
        {
            Destroy(dynamicCounterCanvas);
        }
    }

    private void CreateDynamicDumplingCounterUI()
    {
        dynamicCounterCanvas = new GameObject("DumplingCounterCanvas");
        Canvas canvas = dynamicCounterCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        dynamicCounterCanvas.AddComponent<UnityEngine.UI.CanvasScaler>();
        dynamicCounterCanvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        GameObject textObj = new GameObject("DumplingCounterText");
        textObj.transform.SetParent(dynamicCounterCanvas.transform, false);

        dumplingCounterText = textObj.AddComponent<TextMeshProUGUI>();
        dumplingCounterText.fontSize = 20f;
        dumplingCounterText.fontStyle = FontStyles.Bold;
        dumplingCounterText.color = Color.white;
        dumplingCounterText.outlineWidth = 0.2f;
        dumplingCounterText.outlineColor = Color.black;

        RectTransform rect = dumplingCounterText.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-20f, -20f);
        rect.sizeDelta = new Vector2(300f, 200f);
        dumplingCounterText.alignment = TextAlignmentOptions.TopRight;

        UpdateDumplingCounterText();
        dynamicCounterCanvas.SetActive(false);
    }

    private void UpdateDumplingCounterText()
    {
        if (dumplingCounterText != null)
        {
            dumplingCounterText.text = 
                "<b>-</b>\n" +
                $"Beef: {DumplingCounter.GetCount("Beef")}\n" +
                $"Pork: {DumplingCounter.GetCount("Pork")}\n" +
                $"Canine: {DumplingCounter.GetCount("Canine")}\n" +
                $"Feline: {DumplingCounter.GetCount("Feline")}\n" +
                $"Avian: {DumplingCounter.GetCount("Avian")}";
        }
    }

    public bool HasBlanksOnBoard()
    {
        // Проверяем только разложенные заготовки
        if (scatterAnchors != null)
        {
            foreach (var anchor in scatterAnchors)
            {
                if (anchor != null && anchor.childCount > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
