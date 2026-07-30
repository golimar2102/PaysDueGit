using UnityEngine;
using UnityEngine.Localization; // Подключаем локализацию
using System.Collections.Generic;

[System.Serializable]
public class DialogueOption
{
    [Tooltip("Локализованный текст ответа")]
    public LocalizedString localizedButtonText;
    
    [Tooltip("Текст кнопки ответа (Запасной вариант, если перевода нет)")]
    public string buttonText = "Далее...";

    [Tooltip("Текст кнопки ответа на английском")]
    public string buttonTextEng = "Next...";
    
    [Tooltip("Индекс следующей фразы (Node). Впиши -1, чтобы закончить диалог.")]
    public int nextNodeIndex = -1;

    [Tooltip("Список анимаций NPC для проигрывания при выборе этого ответа")]
    public List<string> npcAnimationStates = new List<string>();

    [Tooltip("Список анимаций игрока для проигрывания при выборе этого ответа")]
    public List<string> playerAnimationStates = new List<string>();
}

[System.Serializable]
public class DialogueNode
{
    [Tooltip("Локализованный текст NPC")]
    public LocalizedString localizedNpcText;
    
    [TextArea(3, 5)]
    [Tooltip("Текст NPC (Запасной вариант)")]
    public string npcText = "Текст, который скажет персонаж...";

    [TextArea(3, 5)]
    [Tooltip("Текст NPC на английском")]
    public string npcTextEng = "NPC English text...";
    
    [Tooltip("Варианты ответов игрока на эту фразу")]
    public DialogueOption[] options;

    [Tooltip("Список анимаций NPC для проигрывания во время этой фразы")]
    public List<string> npcAnimationStates = new List<string>();

    [Tooltip("Список анимаций игрока для проигрывания во время этой фразы")]
    public List<string> playerAnimationStates = new List<string>();

    // НОВОЕ: Скрытая переменная для хранения координат этого блока в визуальном редакторе
    [HideInInspector]
    public Rect displayRect = new Rect(0, 0, 0, 0); 
}

public class NPCDialogue : MonoBehaviour
{
    [Header("Настройки NPC")]
    public LocalizedString localizedNpcName;
    public string npcName = "Незнакомец";
    
    [Tooltip("Пустой объект перед лицом NPC. Камера подлетит сюда при разговоре.")]
    public Transform dialogueCameraPoint;

    [Header("Первый диалог")]
    [Tooltip("Есть ли у NPC уникальный первый диалог?")]
    public bool hasFirstTimeDialogue;

    [Tooltip("Был ли первый диалог уже проигран?")]
    public bool firstTimeDialoguePlayed;

    [Tooltip("Уникальный ключ для сохранения статуса проигрывания первого диалога в PlayerPrefs (если оставить пустым, то сохраняться не будет)")]
    public string dialogueSaveKey;

    [Tooltip("Древо диалогов для ПЕРВОЙ встречи. Элемент 0 - это стартовая фраза")]
    public DialogueNode[] firstTimeNodes;

    [Header("Голос (Undertale)")]
    [Tooltip("Короткий звук 'пик' (blip) для этого персонажа")]
    public AudioClip voiceBlip;
    [Tooltip("Высота голоса (0.5 - басист, 1.5 - писклявый)")]
    [Range(0.5f, 2f)] public float voicePitch = 1f;

    [Header("Древо диалогов (Повторяющиеся)")]
    [Tooltip("Элемент 0 - это фраза, с которой всегда начинается разговор")]
    public DialogueNode[] nodes;

    private Animator cachedAnimator;

    public Animator GetAnimator()
    {
        if (cachedAnimator == null)
        {
            cachedAnimator = GetComponent<Animator>();
            if (cachedAnimator == null)
            {
                cachedAnimator = GetComponentInChildren<Animator>();
            }
        }
        return cachedAnimator;
    }
}