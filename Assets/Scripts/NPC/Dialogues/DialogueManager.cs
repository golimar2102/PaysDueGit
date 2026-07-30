using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Localization; 
using UnityEngine.Localization.Settings;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    [Header("UI Элементы")]
    public GameObject dialoguePanel; 
    public TextMeshProUGUI nameText; 
    public TextMeshProUGUI dialogueText; 
    
    [Header("Кнопки ответов")]
    public Transform optionsParent; 
    public GameObject optionButtonPrefab; 

    [Header("Настройки печати (Undertale)")]
    public float typingSpeed = 0.03f; 
    public int blipFrequency = 2; 
    public AudioSource audioSource; 

    [HideInInspector] public bool isDialogueActive = false;

    private NPCDialogue currentNPC;
    public NPCDialogue CurrentNPC => currentNPC;
    private Coroutine typingCoroutine;
    private Coroutine cameraCoroutine;
    private bool isTyping = false;
    private string currentFullSentence;
    private DialogueNode currentNode;

    private int currentNodeIndex = 0;
    private bool isWaitingForNext = false; 

    private Camera playerCam;
    
    // --- НОВОЕ: Временная камера для катсцен ---
    private GameObject cinematicCameraObj; 
    
    private KeyCode cachedInteractKey;
    private bool isPlayingFirstTime = false;
    private bool ignoreInputFrame = false;

    void Awake()
    {
        Instance = this;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        RefreshKeyBindings();
    }

    void OnEnable()
    {
        SettingsManager.OnSettingsSaved += RefreshKeyBindings;
    }

    void OnDisable()
    {
        SettingsManager.OnSettingsSaved -= RefreshKeyBindings;
    }

    private void RefreshKeyBindings()
    {
        cachedInteractKey = (KeyCode)PlayerPrefs.GetInt("Key_Interact", (int)KeyCode.E);
    }

    void Update()
    {
        if (!isDialogueActive) return;

        if (ignoreInputFrame)
        {
            ignoreInputFrame = false;
            return;
        }

        bool inputPressed = Input.GetMouseButtonDown(0) || Input.GetKeyDown(cachedInteractKey);

        if (isTyping && inputPressed)
        {
            StopCoroutine(typingCoroutine);
            dialogueText.text = currentFullSentence;
            isTyping = false;
            ShowOptions(); 
        }
        else if (!isTyping && isWaitingForNext && inputPressed)
        {
            isWaitingForNext = false;
            DisplayNode(currentNodeIndex + 1); 
        }
    }

    public void StartDialogue(NPCDialogue npc, Camera pCam)
    {
        if (isDialogueActive) return;

        isPlayingFirstTime = false;
        if (npc.hasFirstTimeDialogue)
        {
            bool alreadyPlayed = false;
            if (!string.IsNullOrEmpty(npc.dialogueSaveKey))
            {
                alreadyPlayed = PlayerPrefs.GetInt(npc.dialogueSaveKey, 0) == 1;
            }
            else
            {
                alreadyPlayed = npc.firstTimeDialoguePlayed;
            }

            if (!alreadyPlayed && npc.firstTimeNodes != null && npc.firstTimeNodes.Length > 0)
            {
                isPlayingFirstTime = true;
            }
        }

        DialogueNode[] activeNodes = isPlayingFirstTime ? npc.firstTimeNodes : npc.nodes;
        if (activeNodes == null || activeNodes.Length == 0) return;

        isDialogueActive = true;
        isWaitingForNext = false;
        currentNPC = npc;
        playerCam = pCam;

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        dialoguePanel.SetActive(true);
        ClearOptions();

        if (npc.localizedNpcName != null && !npc.localizedNpcName.IsEmpty)
            nameText.text = GetLocalizedText(npc.localizedNpcName);
        else
            nameText.text = npc.npcName;

        if (audioSource != null && npc.voiceBlip != null)
        {
            audioSource.clip = npc.voiceBlip;
        }

        // === БЕЗОПАСНАЯ СИСТЕМА КАМЕРЫ (КЛОНИРОВАНИЕ) ===
        // Создаем пустышку с камерой
        cinematicCameraObj = new GameObject("CinematicDialogueCamera");
        Camera cineCam = cinematicCameraObj.AddComponent<Camera>();
        cineCam.CopyFrom(playerCam); // Копируем FOV и настройки графики
        
        // Копируем все дополнительные компоненты (например, Post Processing или URP дополнительные данные) динамически
        foreach (var component in playerCam.GetComponents<Component>())
        {
            if (component is Camera || component is AudioListener || component is Transform)
                continue;
            
            Component copy = cinematicCameraObj.AddComponent(component.GetType());
            
            // Копируем поля
            foreach (var field in component.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try { field.SetValue(copy, field.GetValue(component)); } catch {}
            }
            // Копируем свойства
            foreach (var prop in component.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (prop.CanWrite && prop.CanRead && prop.GetIndexParameters().Length == 0)
                {
                    try { prop.SetValue(copy, prop.GetValue(component, null), null); } catch {}
                }
            }
        }
        
        // Ставим клона прямо в глаза игроку
        cinematicCameraObj.transform.position = playerCam.transform.position;
        cinematicCameraObj.transform.rotation = playerCam.transform.rotation;

        // ВЫКЛЮЧАЕМ камеру игрока (но оставляем AudioListener, чтобы звук работал)
        playerCam.enabled = false;

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        // Несем клона к лицу NPC
        cameraCoroutine = StartCoroutine(MoveCamera(cinematicCameraObj.transform, npc.dialogueCameraPoint, 0.6f));

        DisplayNode(0);
    }

    private void DisplayNode(int nodeIndex)
    {
        ignoreInputFrame = true;
        DialogueNode[] activeNodes = isPlayingFirstTime ? currentNPC.firstTimeNodes : currentNPC.nodes;
        if (nodeIndex < 0 || nodeIndex >= activeNodes.Length)
        {
            EndDialogue();
            return;
        }

        currentNodeIndex = nodeIndex; 
        currentNode = activeNodes[nodeIndex];
        
        if (currentNode.localizedNpcText != null && !currentNode.localizedNpcText.IsEmpty)
            currentFullSentence = GetLocalizedText(currentNode.localizedNpcText);
        else
        {
            currentFullSentence = IsEnglishSelected() ? currentNode.npcTextEng : currentNode.npcText;
            if (string.IsNullOrEmpty(currentFullSentence))
            {
                currentFullSentence = currentNode.npcText;
            }

            if (!string.IsNullOrEmpty(currentFullSentence))
            {
                string pName = (PlayerStats.Instance != null && !string.IsNullOrEmpty(PlayerStats.Instance.playerName)) ? PlayerStats.Instance.playerName : "Player";
                currentFullSentence = currentFullSentence.Replace("{playerName}", pName)
                                                         .Replace("{PlayerName}", pName)
                                                         .Replace("{username}", pName)
                                                         .Replace("{Username}", pName);
            }
        }
        
        ClearOptions();

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = StartCoroutine(TypeSentence(currentFullSentence));

        // Проигрываем анимации для данной ноды
        PlayDialogueAnimations(currentNode);
    }

    private IEnumerator TypeSentence(string sentence)
    {
        dialogueText.text = "";
        isTyping = true;
        int charCount = 0;

        foreach (char letter in sentence.ToCharArray())
        {
            dialogueText.text += letter;
            
            if (letter != ' ' && letter != '\n')
            {
                charCount++;
                if (charCount % blipFrequency == 0 && audioSource != null && audioSource.clip != null)
                {
                    audioSource.pitch = currentNPC.voicePitch + Random.Range(-0.05f, 0.05f);
                    audioSource.Stop();
                    audioSource.Play();
                }
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        isTyping = false;
        ShowOptions();
    }

    private void ShowOptions()
    {
        ClearOptions();

        if (currentNode.options == null || currentNode.options.Length == 0)
        {
            isWaitingForNext = true;
            return; 
        }

        for (int i = 0; i < currentNode.options.Length; i++)
        {
            DialogueOption opt = currentNode.options[i];
            
            string btnText;
            if (opt.localizedButtonText != null && !opt.localizedButtonText.IsEmpty)
                btnText = GetLocalizedText(opt.localizedButtonText);
            else
            {
                btnText = IsEnglishSelected() ? opt.buttonTextEng : opt.buttonText;
                if (string.IsNullOrEmpty(btnText))
                {
                    btnText = opt.buttonText;
                }

                if (!string.IsNullOrEmpty(btnText))
                {
                    string pName = (PlayerStats.Instance != null && !string.IsNullOrEmpty(PlayerStats.Instance.playerName)) ? PlayerStats.Instance.playerName : "Player";
                    btnText = btnText.Replace("{playerName}", pName)
                                     .Replace("{PlayerName}", pName)
                                     .Replace("{username}", pName)
                                     .Replace("{Username}", pName);
                }
            }
                
            CreateOptionButton(btnText, opt);
        }
    }

    private void CreateOptionButton(string text, DialogueOption opt)
    {
        GameObject btnObj = Instantiate(optionButtonPrefab, optionsParent);
        btnObj.SetActive(true);
        
        TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = text;

        Button btn = btnObj.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnOptionClicked(opt));
        }
    }

    private void OnOptionClicked(DialogueOption opt)
    {
        // Проигрываем анимации для выбранного ответа
        PlayOptionAnimations(opt);

        int nextNode = opt.nextNodeIndex;
        if (nextNode == -1)
        {
            EndDialogue();
        }
        else
        {
            DisplayNode(nextNode);
        }
    }

    private void ClearOptions()
    {
        foreach (Transform child in optionsParent)
        {
            Destroy(child.gameObject);
        }
    }

    public void EndDialogue()
    {
        isDialogueActive = false;
        isWaitingForNext = false;
        dialoguePanel.SetActive(false);

        if (isPlayingFirstTime && currentNPC != null)
        {
            currentNPC.firstTimeDialoguePlayed = true;
            if (!string.IsNullOrEmpty(currentNPC.dialogueSaveKey))
            {
                PlayerPrefs.SetInt(currentNPC.dialogueSaveKey, 1);
                PlayerPrefs.Save();
            }
            isPlayingFirstTime = false;
        }

        if (EquipmentManager.Instance != null)
        {
            EquipmentManager.Instance.SetWeaponVisibility(true);
        }

        if (InventoryManager.Instance == null || !InventoryManager.Instance.isOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (cameraCoroutine != null) StopCoroutine(cameraCoroutine);
        
        // Летим ОБРАТНО (клоном к камере игрока)
        cameraCoroutine = StartCoroutine(MoveCameraBack(0.5f));
    }

    // Измененная функция: теперь она следит за целью (вдруг цель двигается)
    private IEnumerator MoveCamera(Transform camToMove, Transform targetAnchor, float duration)
    {
        float elapsed = 0f;
        Vector3 startPos = camToMove.position;
        Quaternion startRot = camToMove.rotation;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / duration); 
            
            camToMove.position = Vector3.Lerp(startPos, targetAnchor.position, t);
            camToMove.rotation = Quaternion.Slerp(startRot, targetAnchor.rotation, t);
            yield return null;
        }

        camToMove.position = targetAnchor.position;
        camToMove.rotation = targetAnchor.rotation;
    }

    private IEnumerator MoveCameraBack(float duration)
    {
        // Несем клона обратно в голову игрока (даже если игрок слегка сдвинулся)
        yield return StartCoroutine(MoveCamera(cinematicCameraObj.transform, playerCam.transform, duration));
        
        // Включаем настоящую камеру
        playerCam.enabled = true;
        
        // Уничтожаем временного клона
        Destroy(cinematicCameraObj);
    }

    private string GetLocalizedText(LocalizedString locString)
    {
        if (locString == null || locString.IsEmpty) return "";
        
        string pName = "Player";
        if (PlayerStats.Instance != null && !string.IsNullOrEmpty(PlayerStats.Instance.playerName))
        {
            pName = PlayerStats.Instance.playerName;
        }

        return locString.GetLocalizedString(new { 
            playerName = pName, 
            PlayerName = pName,
            username = pName,
            Username = pName
        });
    }

    private bool IsEnglishSelected()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            return LocalizationSettings.SelectedLocale.Identifier.Code.ToLower().Contains("en");
        }
        return PlayerPrefs.GetInt("LanguageSetting", 0) == 1;
    }

    private void PlayDialogueAnimations(DialogueNode node)
    {
        if (currentNPC != null)
        {
            Animator npcAnim = currentNPC.GetAnimator();
            if (npcAnim != null && node.npcAnimationStates != null && node.npcAnimationStates.Count > 0)
            {
                PlayRandomAnimation(npcAnim, node.npcAnimationStates);
            }
        }

        if (PlayerInteract.Instance != null && PlayerInteract.Instance.animator != null)
        {
            Animator playerAnim = PlayerInteract.Instance.animator;
            if (playerAnim != null && node.playerAnimationStates != null && node.playerAnimationStates.Count > 0)
            {
                PlayRandomAnimation(playerAnim, node.playerAnimationStates);
            }
        }
    }

    private void PlayOptionAnimations(DialogueOption opt)
    {
        if (currentNPC != null)
        {
            Animator npcAnim = currentNPC.GetAnimator();
            if (npcAnim != null && opt.npcAnimationStates != null && opt.npcAnimationStates.Count > 0)
            {
                PlayRandomAnimation(npcAnim, opt.npcAnimationStates);
            }
        }

        if (PlayerInteract.Instance != null && PlayerInteract.Instance.animator != null)
        {
            Animator playerAnim = PlayerInteract.Instance.animator;
            if (playerAnim != null && opt.playerAnimationStates != null && opt.playerAnimationStates.Count > 0)
            {
                PlayRandomAnimation(playerAnim, opt.playerAnimationStates);
            }
        }
    }

    private void PlayRandomAnimation(Animator anim, List<string> animations)
    {
        if (anim == null || animations == null || animations.Count == 0) return;
        string animName = animations[Random.Range(0, animations.Count)];
        if (string.IsNullOrEmpty(animName)) return;

        bool isTrigger = false;
        foreach (var param in anim.parameters)
        {
            if (param.name == animName && param.type == AnimatorControllerParameterType.Trigger)
            {
                isTrigger = true;
                break;
            }
        }

        if (isTrigger)
        {
            anim.SetTrigger(animName);
        }
        else
        {
            anim.CrossFadeInFixedTime(animName, 0.2f);
        }
    }
}