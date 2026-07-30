using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Audio;
using UnityEngine.Localization.Settings; // НОВОЕ: Подключаем локализацию
using System.Collections; // НОВОЕ: Для корутин

public class SettingsManager : MonoBehaviour
{
    [Header("Вкладки (Панели)")]
    [Tooltip("0 - Графика, 1 - Управление, 2 - Звук")]
    public GameObject[] tabPanels;

    [Header("Кнопки вкладок (Для изменения цвета)")]
    public Button[] tabButtons;
    public Color activeTabColor = new Color(0.8f, 0.1f, 0.1f, 1f); 
    public Color inactiveTabColor = new Color(0.3f, 0.3f, 0.3f, 1f); 

    [Header("Графика (UI)")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown windowModeDropdown;
    
    [Header("Локализация (UI)")]
    public TMP_Dropdown languageDropdown; // <-- НОВОЕ ПОЛЕ ДЛЯ ТВОЕГО DROPDOWN

    private Resolution[] resolutions;

    [Header("Звук (UI)")]
    public AudioMixer mainAudioMixer;
    public Slider masterSlider;
    public TextMeshProUGUI masterText;
    public Slider musicSlider;
    public TextMeshProUGUI musicText;
    public Slider sfxSlider;
    public TextMeshProUGUI sfxText;

    [Header("Управление (UI)")]
    public Slider sensitivitySlider;
    public TextMeshProUGUI sensitivityText;

    [System.Serializable]
    public class KeybindUI
    {
        public string actionName; 
        public string prefsKey;   
        public KeyCode defaultKey;
        public TextMeshProUGUI buttonText; 
    }

    [Header("Назначение клавиш")]
    public List<KeybindUI> keybinds = new List<KeybindUI>();
    
    private int waitingForKeyIndex = -1;
    
    // Временное хранилище кнопок до нажатия "Save"
    private Dictionary<string, KeyCode> pendingKeybinds = new Dictionary<string, KeyCode>();

    // Событие, которое оповещает другие скрипты о том, что настройки сохранены
    public static event System.Action OnSettingsSaved;

    void Awake()
    {
        // Используем событие (Completed) вместо корутины.
        // Событие сработает на 100%, даже если панель выключит скрипт меню!
        LocalizationSettings.InitializationOperation.Completed += (handle) =>
        {
            InitResolutions();
            InitLanguageDropdown(); 
            LoadSettings();
            SwitchTab(0);
        };
    }

    void OnEnable()
    {
        // Сбросить визуальные изменения (кнопки, ползунки), если игрок вышел без сохранения
        if (LocalizationSettings.InitializationOperation.IsDone)
        {
            LoadSettings();
        }
    }

    // === ЛОКАЛИЗАЦИЯ (НОВОЕ) ===
    private void InitLanguageDropdown()
    {
        if (languageDropdown == null) return;

        // Очищаем то, что было написано в Инспекторе
        languageDropdown.ClearOptions();
        List<string> options = new List<string>();
        int selectedIndex = 0;

        // Проходимся по всем языкам, которые мы добавили в проект (RU, EN)
        for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; i++)
        {
            var locale = LocalizationSettings.AvailableLocales.Locales[i];
            
            // ЖЕЛЕЗОБЕТОННАЯ ПРОВЕРКА: Переводим всё в нижний регистр, чтобы избежать ошибок с "RU" и "ru"
            string code = locale.Identifier.Code.ToLower();
            string locName = locale.name.ToLower();
            
            // Если код или имя содержат "ru" или "russian" - пишем Русский
            string displayName = (code.Contains("ru") || locName.Contains("russian")) ? "Русский" : "English";
            
            options.Add(displayName); 
            
            // Проверяем, какой язык выбран сейчас
            if (LocalizationSettings.SelectedLocale == locale)
            {
                selectedIndex = i;
            }
        }
        
        languageDropdown.AddOptions(options);
        languageDropdown.value = selectedIndex;
        languageDropdown.RefreshShownValue();
    }

    // Эта функция будет вызываться при переключении Dropdown'а языков
    public void SetLanguage(int localeIndex)
    {
        // Проверка на ошибки
        if (localeIndex >= 0 && localeIndex < LocalizationSettings.AvailableLocales.Locales.Count)
        {
            // Меняем язык во всей игре!
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeIndex];
            
            // Сохраняем настройку, чтобы при перезапуске язык остался
            PlayerPrefs.SetInt("LanguageSetting", localeIndex);
            PlayerPrefs.Save();
            
            Debug.Log("Язык изменен на: " + LocalizationSettings.SelectedLocale.name);
        }
    }
    // ===========================

    public void SwitchTab(int tabIndex)
    {
        for (int i = 0; i < tabPanels.Length; i++)
        {
            if (tabPanels[i] != null) tabPanels[i].SetActive(i == tabIndex);
        }

        if (tabButtons != null && tabButtons.Length > 0)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] != null)
                {
                    ColorBlock cb = tabButtons[i].colors;
                    cb.normalColor = (i == tabIndex) ? activeTabColor : inactiveTabColor;
                    cb.selectedColor = (i == tabIndex) ? activeTabColor : inactiveTabColor;
                    tabButtons[i].colors = cb;
                }
            }
        }
    }

    // === 1. ГРАФИКА ===
    private void InitResolutions()
    {
        if (resolutionDropdown == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();
        List<string> options = new List<string>();

        int currentResIndex = 0;
        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);

            if (resolutions[i].width == Screen.currentResolution.width && 
                resolutions[i].height == Screen.currentResolution.height)
            {
                currentResIndex = i;
            }
        }
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResIndex;
        resolutionDropdown.RefreshShownValue();
    }

    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutions == null || resolutions.Length == 0) return;
        Resolution res = resolutions[resolutionIndex];
        Screen.SetResolution(res.width, res.height, Screen.fullScreenMode);
    }

    public void SetWindowMode(int modeIndex)
    {
        FullScreenMode mode = FullScreenMode.ExclusiveFullScreen;
        if (modeIndex == 1) mode = FullScreenMode.Windowed;
        else if (modeIndex == 2) mode = FullScreenMode.FullScreenWindow;
        
        Screen.fullScreenMode = mode;
    }

    // === 2. ЗВУК ===
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume("MasterVolume", volume, masterText);
    }

    public void SetMusicVolume(float volume)
    {
        SetMixerVolume("MusicVolume", volume, musicText);
    }

    public void SetSFXVolume(float volume)
    {
        SetMixerVolume("SFXVolume", volume, sfxText);
    }

    private void SetMixerVolume(string paramName, float sliderValue, TextMeshProUGUI textUI)
    {
        if (textUI != null) textUI.text = Mathf.RoundToInt(sliderValue * 100) + "";
        
        if (mainAudioMixer != null)
        {
            float dbVolume = Mathf.Log10(Mathf.Max(sliderValue, 0.0001f)) * 20f;
            mainAudioMixer.SetFloat(paramName, dbVolume);
        }
    }

    // === 3. УПРАВЛЕНИЕ ===
    public void SetSensitivity(float sensitivity)
    {
        if (sensitivityText != null) sensitivityText.text = Mathf.RoundToInt(sensitivity).ToString();
    }

    public void StartRebindBinding(int index)
    {
        if (index < 0 || index >= keybinds.Count) return;
        
        waitingForKeyIndex = index;
        if (keybinds[index].buttonText != null)
            keybinds[index].buttonText.text = "..."; 
    }

    void OnGUI()
    {
        if (waitingForKeyIndex != -1)
        {
            Event e = Event.current;
            if (e.isKey && e.keyCode != KeyCode.None)
            {
                ApplyNewKeybind(waitingForKeyIndex, e.keyCode);
                waitingForKeyIndex = -1; 
            }
            else if (e.isMouse && e.type == EventType.MouseDown)
            {
                if (e.button == 0) ApplyNewKeybind(waitingForKeyIndex, KeyCode.Mouse0);
                else if (e.button == 1) ApplyNewKeybind(waitingForKeyIndex, KeyCode.Mouse1);
                else if (e.button == 2) ApplyNewKeybind(waitingForKeyIndex, KeyCode.Mouse2);
                waitingForKeyIndex = -1;
            }
        }
    }

    private void ApplyNewKeybind(int index, KeyCode newKey)
    {
        keybinds[index].buttonText.text = newKey.ToString();
        // Сохраняем ТОЛЬКО в оперативной памяти (не в PlayerPrefs), пока не нажата кнопка Save
        pendingKeybinds[keybinds[index].prefsKey] = newKey;
    }

    // === СОХРАНЕНИЕ И ЗАГРУЗКА ===
    public void SaveSettings()
    {
        if (qualityDropdown != null) PlayerPrefs.SetInt("QualitySetting", qualityDropdown.value);
        if (resolutionDropdown != null) PlayerPrefs.SetInt("ResolutionSetting", resolutionDropdown.value);
        if (windowModeDropdown != null) PlayerPrefs.SetInt("WindowModeSetting", windowModeDropdown.value);

        if (masterSlider != null) PlayerPrefs.SetFloat("MasterVolume", masterSlider.value);
        if (musicSlider != null) PlayerPrefs.SetFloat("MusicVolume", musicSlider.value);
        if (sfxSlider != null) PlayerPrefs.SetFloat("SFXVolume", sfxSlider.value);

        if (sensitivitySlider != null) PlayerPrefs.SetFloat("MouseSensitivity", sensitivitySlider.value);

        // Сохраняем новые клавиши в PlayerPrefs
        foreach (var kvp in pendingKeybinds)
        {
            PlayerPrefs.SetInt(kvp.Key, (int)kvp.Value);
        }
        pendingKeybinds.Clear();

        PlayerPrefs.Save();
        
        // Оповещаем все скрипты, что настройки поменялись!
        OnSettingsSaved?.Invoke();
        
        Debug.Log("Все настройки сохранены!");
    }

    public void LoadSettings()
    {
        // Язык загружается автоматически через LocalizationSettings, но мы можем проверить сохраненный
        if (PlayerPrefs.HasKey("LanguageSetting") && LocalizationSettings.AvailableLocales.Locales.Count > 0)
        {
            int savedLangIndex = PlayerPrefs.GetInt("LanguageSetting");
            if (savedLangIndex < LocalizationSettings.AvailableLocales.Locales.Count)
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[savedLangIndex];
            }
        }

        if (qualityDropdown != null)
        {
            qualityDropdown.value = PlayerPrefs.GetInt("QualitySetting", 2);
            SetQuality(qualityDropdown.value);
        }
        if (resolutionDropdown != null && resolutions != null)
        {
            resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionSetting", resolutionDropdown.value);
            SetResolution(resolutionDropdown.value);
        }
        if (windowModeDropdown != null)
        {
            windowModeDropdown.value = PlayerPrefs.GetInt("WindowModeSetting", 0);
            SetWindowMode(windowModeDropdown.value);
        }

        if (masterSlider != null)
        {
            masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            SetMasterVolume(masterSlider.value);
        }
        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
            SetMusicVolume(musicSlider.value);
        }
        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            SetSFXVolume(sfxSlider.value);
        }

        if (sensitivitySlider != null)
        {
            sensitivitySlider.value = PlayerPrefs.GetFloat("MouseSensitivity", 300f);
            SetSensitivity(sensitivitySlider.value);
        }

        pendingKeybinds.Clear();

        for (int i = 0; i < keybinds.Count; i++)
        {
            int savedKeyCode = PlayerPrefs.GetInt(keybinds[i].prefsKey, (int)keybinds[i].defaultKey);
            KeyCode key = (KeyCode)savedKeyCode;
            if (keybinds[i].buttonText != null)
            {
                keybinds[i].buttonText.text = key.ToString();
            }
        }
    }
}