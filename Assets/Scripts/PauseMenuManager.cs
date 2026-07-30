using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance;

    [Header("UI Панели")]
    [Tooltip("Главная панель меню паузы")]
    public GameObject pauseMenuPanel;
    
    [Tooltip("Панель настроек")]
    public GameObject settingsPanel; 

    [Header("Настройки сцен")]
    [Tooltip("Точное название сцены главного меню")]
    public string mainMenuSceneName = "MainMenu"; 

    [HideInInspector] 
    public bool isPaused = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Убеждаемся, что меню выключено при старте игры
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    void Update()
    {
        // Проверяем нажатие ESC
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (TVChairController.activeChair != null && !isPaused)
            {
                TVChairController.activeChair.StandUp();
                return;
            }

            MeatGrinderController[] mgs = FindObjectsByType<MeatGrinderController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MeatGrinderController mg in mgs)
            {
                if (mg.isUsing && !isPaused)
                {
                    // Выходим из режима мясорубки, не открывая меню
                    mg.ExitMeatGrinderMode();
                    return;
                }
            }

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }
    
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // Возобновляем время
        
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Прячем курсор, ТОЛЬКО если инвентарь сейчас закрыт И диалог не активен
        bool isInventoryOpen = InventoryManager.Instance != null && InventoryManager.Instance.isOpen;
        bool isDialogueActive = DialogueManager.Instance != null && DialogueManager.Instance.isDialogueActive;

        if (!isInventoryOpen && !isDialogueActive)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // Останавливаем время и физику
        
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false); 

        // Освобождаем курсор мыши
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    public void SaveGame()
    {
        Debug.Log("Игра сохранена! (Система сохранений в разработке)");
        // TODO: Добавить вызов системы сохранений
    }
    
    public void LoadGame()
    {
        Debug.Log("Игра загружена! (Система загрузки в разработке)");
        // TODO: Добавить вызов системы загрузки
    }
    
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
            
            // Прячем главное меню паузы, пока открыты настройки (и наоборот)
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(isActive);
        }
        else
        {
            Debug.LogWarning("Панель настроек не назначена в Инспекторе!");
        }
    }
    
    public void ExitToMainMenu()
    {
        // ВАЖНО: Возвращаем время в норму перед загрузкой, иначе Главное Меню тоже зависнет!
        Time.timeScale = 1f; 
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    public void ExitToDesktop()
    {
        Debug.Log("Выход из игры на рабочий стол!");
        Application.Quit();

        // Чтобы кнопка срабатывала даже внутри редактора Unity:
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}