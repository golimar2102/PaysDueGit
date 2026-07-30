#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class DialogueNodeEditorWindow : EditorWindow
{
    private NPCDialogue currentDialogue;
    private Vector2 scrollPosition;
    private System.Action deferredAction;

    private enum DialogueType { Repeating, FirstTime }
    private DialogueType selectedDialogueType = DialogueType.Repeating;
    private bool autoHeightEnabled = true;

    private SerializedObject serializedDialogueObject;

    private static Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    private bool GetFoldoutState(string key, bool defaultVal = false)
    {
        if (!foldoutStates.TryGetValue(key, out bool val))
        {
            foldoutStates[key] = defaultVal;
            return defaultVal;
        }
        return val;
    }

    private void SetFoldoutState(string key, bool val)
    {
        foldoutStates[key] = val;
    }

    private void DrawAnimationList(string label, ref List<string> list, string foldoutKey)
    {
        if (list == null)
        {
            Undo.RecordObject(currentDialogue, "Initialize Animation List");
            list = new List<string>();
            GUI.changed = true;
        }

        bool expanded = GetFoldoutState(foldoutKey);
        
        GUILayout.BeginHorizontal();
        expanded = EditorGUILayout.Foldout(expanded, $"{label} ({list.Count})", true);
        SetFoldoutState(foldoutKey, expanded);
        
        if (GUILayout.Button("+", GUILayout.Width(20), GUILayout.Height(15)))
        {
            Undo.RecordObject(currentDialogue, "Add Animation");
            list.Add("");
            expanded = true;
            SetFoldoutState(foldoutKey, expanded);
            GUI.changed = true;
        }
        GUILayout.EndHorizontal();

        if (expanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
            {
                GUILayout.BeginHorizontal();
                string oldVal = list[i];
                string newVal = EditorGUILayout.TextField(oldVal);
                if (newVal != oldVal)
                {
                    Undo.RecordObject(currentDialogue, "Edit Animation Name");
                    list[i] = newVal;
                    GUI.changed = true;
                }
                
                if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(18)))
                {
                    Undo.RecordObject(currentDialogue, "Remove Animation");
                    list.RemoveAt(i);
                    i--;
                    GUI.changed = true;
                }
                GUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }
    }

    [MenuItem("Window/Редактор Диалогов (Ноды)")]
    public static void ShowWindow()
    {
        GetWindow<DialogueNodeEditorWindow>("Ноды Диалогов");
    }

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject != null)
        {
            NPCDialogue dialogue = Selection.activeGameObject.GetComponent<NPCDialogue>();
            if (dialogue != null)
            {
                currentDialogue = dialogue;
                serializedDialogueObject = new SerializedObject(currentDialogue);
                Repaint();
            }
        }
    }

    private DialogueNode[] GetActiveNodes()
    {
        if (currentDialogue == null) return null;
        if (selectedDialogueType == DialogueType.FirstTime)
        {
            if (currentDialogue.firstTimeNodes == null) currentDialogue.firstTimeNodes = new DialogueNode[0];
            return currentDialogue.firstTimeNodes;
        }
        else
        {
            if (currentDialogue.nodes == null) currentDialogue.nodes = new DialogueNode[0];
            return currentDialogue.nodes;
        }
    }

    private void SetActiveNodes(DialogueNode[] newNodes)
    {
        if (currentDialogue == null) return;
        if (selectedDialogueType == DialogueType.FirstTime)
            currentDialogue.firstTimeNodes = newNodes;
        else
            currentDialogue.nodes = newNodes;
    }

    private void OnGUI()
    {
        if (currentDialogue == null)
        {
            GUILayout.Label("Выделите NPC (со скриптом NPCDialogue) на сцене!", EditorStyles.boldLabel);
            return;
        }

        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (GUILayout.Button("Добавить новый узел (Фразу NPC)", EditorStyles.toolbarButton))
        {
            deferredAction = () => CreateNewNode(new Rect(50, 50, 280, 250));
        }
        autoHeightEnabled = GUILayout.Toggle(autoHeightEnabled, "Авто-высота узлов", EditorStyles.toolbarButton);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Редактируем: {currentDialogue.gameObject.name}", EditorStyles.boldLabel);
        GUILayout.EndHorizontal();

        // Переключение режима редактирования
        GUILayout.BeginHorizontal();
        GUILayout.Label("Режим редактирования:", GUILayout.Width(150));
        var newSelectedType = (DialogueType)GUILayout.Toolbar((int)selectedDialogueType, new string[] { "Повторяющийся диалог (Part 2)", "Первый диалог (Part 1)" });
        if (newSelectedType != selectedDialogueType)
        {
            selectedDialogueType = newSelectedType;
            Repaint();
        }
        GUILayout.EndHorizontal();

        DialogueNode[] activeNodes = GetActiveNodes();
        if (activeNodes == null || activeNodes.Length == 0)
        {
            GUILayout.Space(20);
            GUILayout.Label("Диалог пуст. Нажмите 'Добавить новый узел' на панели выше.", EditorStyles.boldLabel);
            ExecuteDeferredAction();
            return;
        }

        // --- МАГИЯ ДЛЯ ПОДДЕРЖКИ ЛОКАЛИЗАЦИИ В РЕДАКТОРЕ ---
        if (serializedDialogueObject == null || serializedDialogueObject.targetObject != currentDialogue)
        {
            serializedDialogueObject = new SerializedObject(currentDialogue);
        }
        serializedDialogueObject.Update();
        SerializedProperty nodesProp = selectedDialogueType == DialogueType.FirstTime 
            ? serializedDialogueObject.FindProperty("firstTimeNodes") 
            : serializedDialogueObject.FindProperty("nodes");

        scrollPosition = GUI.BeginScrollView(new Rect(0, 40, position.width, position.height - 40), scrollPosition, new Rect(0, 0, 4000, 4000));
        DrawNodeCurves();

        BeginWindows();
        for (int i = 0; i < activeNodes.Length; i++)
        {
            DialogueNode node = activeNodes[i];
            
            if (node.displayRect.width == 0) 
                node.displayRect = new Rect(50 + (i * 320), 50, 280, 250);

            // Передаем SerializedProperty в отрисовку, чтобы работали выпадающие списки ключей перевода
            SerializedProperty singleNodeProp = nodesProp.GetArrayElementAtIndex(i);
            
            node.displayRect = GUI.Window(i, node.displayRect, (id) => DrawNodeWindow(id, singleNodeProp), "Узел (ID: " + i + ")");
        }
        EndWindows();
        GUI.EndScrollView();

        serializedDialogueObject.ApplyModifiedProperties();
        ExecuteDeferredAction();

        if (GUI.changed)
            EditorUtility.SetDirty(currentDialogue);
    }

    private void DrawNodeWindow(int id, SerializedProperty nodeProp)
    {
        DialogueNode[] activeNodes = GetActiveNodes();
        DialogueNode node = activeNodes[id];

        GUILayout.BeginHorizontal();
        GUILayout.Label("Фраза NPC:", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("X", GUILayout.Width(20)))
        {
            deferredAction = () => DeleteNode(id);
        }
        GUILayout.EndHorizontal();

        // Поле для выбора ключа локализации NPC
        EditorGUILayout.PropertyField(nodeProp.FindPropertyRelative("localizedNpcText"), new GUIContent("Ключ перевода"));
        
        GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
        textAreaStyle.wordWrap = true;
        
        // Расчет динамической высоты на основе содержимого RU текста
        float textHeightRu = textAreaStyle.CalcHeight(new GUIContent(node.npcText), 260f);
        GUILayout.Label("Текст NPC (RU):", EditorStyles.miniLabel);
        node.npcText = EditorGUILayout.TextArea(node.npcText, textAreaStyle, GUILayout.Height(Mathf.Max(35f, textHeightRu)));

        // Расчет динамической высоты на основе содержимого EN текста
        float textHeightEn = textAreaStyle.CalcHeight(new GUIContent(node.npcTextEng), 260f);
        GUILayout.Label("Текст NPC (EN):", EditorStyles.miniLabel);
        node.npcTextEng = EditorGUILayout.TextArea(node.npcTextEng, textAreaStyle, GUILayout.Height(Mathf.Max(35f, textHeightEn)));

        // Анимации фразы NPC
        var npcAnims = node.npcAnimationStates;
        var playerAnims = node.playerAnimationStates;
        DrawAnimationList("Анимации NPC", ref npcAnims, $"node_{selectedDialogueType}_{id}_npcAnims");
        DrawAnimationList("Анимации Игрока", ref playerAnims, $"node_{selectedDialogueType}_{id}_playerAnims");
        node.npcAnimationStates = npcAnims;
        node.playerAnimationStates = playerAnims;

        GUILayout.Space(10);
        GUILayout.Label("Ответы игрока:", EditorStyles.boldLabel);

        if (node.options != null)
        {
            SerializedProperty optionsProp = nodeProp.FindPropertyRelative("options");

            for (int i = 0; i < node.options.Length; i++)
            {
                int optIndex = i; 
                SerializedProperty singleOptProp = optionsProp.GetArrayElementAtIndex(i);

                GUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Поле для выбора ключа локализации кнопки ответа
                EditorGUILayout.PropertyField(singleOptProp.FindPropertyRelative("localizedButtonText"), new GUIContent("Ключ"));

                // Поля ответов RU & EN
                GUILayout.BeginHorizontal();
                GUILayout.Label("RU:", GUILayout.Width(25));
                node.options[i].buttonText = EditorGUILayout.TextField(node.options[i].buttonText, GUILayout.ExpandWidth(true));
                GUILayout.Label("-> ID:", GUILayout.Width(40));
                node.options[i].nextNodeIndex = EditorGUILayout.IntField(node.options[i].nextNodeIndex, GUILayout.Width(30));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("EN:", GUILayout.Width(25));
                node.options[i].buttonTextEng = EditorGUILayout.TextField(node.options[i].buttonTextEng, GUILayout.ExpandWidth(true));
                
                if (GUILayout.Button("+Узел", GUILayout.Width(55)))
                    deferredAction = () => AutoCreateLinkedNode(id, optIndex);
                
                if (GUILayout.Button("X", GUILayout.Width(25)))
                    deferredAction = () => RemoveOption(id, optIndex);
                
                GUILayout.EndHorizontal();

                // Анимации ответа игрока
                var optNpcAnims = node.options[i].npcAnimationStates;
                var optPlayerAnims = node.options[i].playerAnimationStates;
                DrawAnimationList("Аним. NPC", ref optNpcAnims, $"node_{selectedDialogueType}_{id}_opt_{i}_npcAnims");
                DrawAnimationList("Аним. Игрока", ref optPlayerAnims, $"node_{selectedDialogueType}_{id}_opt_{i}_playerAnims");
                node.options[i].npcAnimationStates = optNpcAnims;
                node.options[i].playerAnimationStates = optPlayerAnims;

                GUILayout.EndVertical();
            }
        }

        if (GUILayout.Button("+ Добавить ответ", GUILayout.Height(25)))
            deferredAction = () => AddOption(id);

        if (autoHeightEnabled && Event.current.type == EventType.Repaint)
        {
            float targetHeight = GUILayoutUtility.GetLastRect().yMax + 10f;
            if (node.displayRect.height != targetHeight)
            {
                node.displayRect.height = targetHeight;
                Repaint(); 
            }
        }

        GUI.DragWindow();
    }

    private void DrawNodeCurves()
    {
        DialogueNode[] activeNodes = GetActiveNodes();
        if (activeNodes == null) return;

        for (int i = 0; i < activeNodes.Length; i++)
        {
            DialogueNode node = activeNodes[i];

            if (node.options == null || node.options.Length == 0)
            {
                if (i + 1 < activeNodes.Length)
                {
                    Rect startRect = node.displayRect;
                    Rect endRect = activeNodes[i + 1].displayRect;
                    Vector3 startPos = new Vector3(startRect.x + startRect.width, startRect.y + startRect.height / 2, 0);
                    Vector3 endPos = new Vector3(endRect.x, endRect.y + endRect.height / 2, 0);
                    Handles.DrawBezier(startPos, endPos, startPos + Vector3.right * 80, endPos + Vector3.left * 80, Color.gray, null, 3f);
                }
                continue; 
            }

            for (int j = 0; j < node.options.Length; j++)
            {
                int targetIndex = node.options[j].nextNodeIndex;
                if (targetIndex >= 0 && targetIndex < activeNodes.Length)
                {
                    Rect startRect = node.displayRect;
                    Rect endRect = activeNodes[targetIndex].displayRect;
                    // Опускаем стрелку чуть ниже, чтобы учесть увеличенное поле ответа
                    Vector3 startPos = new Vector3(startRect.x + startRect.width, startRect.y + 145 + (j * 45), 0);
                    Vector3 endPos = new Vector3(endRect.x, endRect.y + endRect.height / 2, 0);
                    Handles.DrawBezier(startPos, endPos, startPos + Vector3.right * 80, endPos + Vector3.left * 80, Color.green, null, 3f);
                }
            }
        }
    }

    private void ExecuteDeferredAction()
    {
        if (deferredAction != null)
        {
            deferredAction.Invoke();
            deferredAction = null;
            GUI.changed = true; 
        }
    }

    private void CreateNewNode(Rect position)
    {
        DialogueNode[] activeNodes = GetActiveNodes() ?? new DialogueNode[0];
        var list = new List<DialogueNode>(activeNodes);
        var newNode = new DialogueNode();
        newNode.displayRect = position;
        list.Add(newNode);
        SetActiveNodes(list.ToArray());
    }

    private void AddOption(int nodeIndex)
    {
        DialogueNode[] activeNodes = GetActiveNodes();
        var node = activeNodes[nodeIndex];
        var list = new List<DialogueOption>(node.options ?? new DialogueOption[0]);
        list.Add(new DialogueOption() { buttonText = "Новый ответ" });
        node.options = list.ToArray();
    }

    private void RemoveOption(int nodeIndex, int optionIndex)
    {
        DialogueNode[] activeNodes = GetActiveNodes();
        var node = activeNodes[nodeIndex];
        var list = new List<DialogueOption>(node.options);
        list.RemoveAt(optionIndex);
        node.options = list.ToArray();
    }

    private void AutoCreateLinkedNode(int sourceNodeIndex, int optionIndex)
    {
        DialogueNode[] activeNodes = GetActiveNodes() ?? new DialogueNode[0];
        var srcNode = activeNodes[sourceNodeIndex];
        Rect newPos = new Rect(srcNode.displayRect.x + 350, srcNode.displayRect.y + (optionIndex * 60), 280, 250);
        
        var list = new List<DialogueNode>(activeNodes);
        var newNode = new DialogueNode();
        newNode.displayRect = newPos;
        list.Add(newNode);
        SetActiveNodes(list.ToArray());
        
        // Retrieve updated activeNodes
        activeNodes = GetActiveNodes();
        activeNodes[sourceNodeIndex].options[optionIndex].nextNodeIndex = activeNodes.Length - 1;
    }

    private void DeleteNode(int indexToDelete)
    {
        DialogueNode[] activeNodes = GetActiveNodes() ?? new DialogueNode[0];
        var list = new List<DialogueNode>(activeNodes);
        list.RemoveAt(indexToDelete);
        SetActiveNodes(list.ToArray());
        
        // Retrieve updated activeNodes
        activeNodes = GetActiveNodes();
        foreach(var node in activeNodes)
        {
            if (node.options == null) continue;
            foreach(var opt in node.options)
            {
                if (opt.nextNodeIndex == indexToDelete) 
                    opt.nextNodeIndex = -1; 
                else if (opt.nextNodeIndex > indexToDelete) 
                    opt.nextNodeIndex--;    
            }
        }
    }
}
#endif