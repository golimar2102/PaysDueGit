using UnityEngine;
using UnityEngine.SceneManagement; // Нужно для загрузки уровней
using UnityEngine.UI; // НОВОЕ: Для работы с черным экраном (Image)
using System.Collections; // НОВОЕ: Для работы с корутинами (IEnumerator)

public class MainMenuManager : MonoBehaviour
{
    [Header("Настройки сцен")]
    [Tooltip("Точное название сцены твоего главного уровня (например, GameScene)")]
    public string gameSceneName = "SampleScene";

    [Header("UI Панели")]
    [Tooltip("Перетащи сюда панель настроек (которую мы будем скрывать/показывать)")]
    public GameObject settingsPanel;

    [Header("Эффект перехода (Fade)")]
    [Tooltip("Черная картинка на весь экран для затемнения")]
    public Image fadeScreen;
    [Tooltip("С какой скоростью темнеет экран")]
    public float fadeSpeed = 1f;

    [Header("Анимация персонажа")]
    [Tooltip("Аниматор персонажа в меню")]
    public Animator menuCharacterAnimator;
    [Tooltip("Имя триггера для запуска анимации перехода")]
    public string startGameTrigger = "StartGame";
    [Tooltip("Сколько секунд ждать после старта анимации до начала затемнения")]
    public float delayBeforeFade = 1.5f;

    [Header("Звуки")]
    [Tooltip("Звук, который сыграет перед началом затемнения экрана")]
    public AudioSource fadeSound;

    void Start()
    {
        // При старте главного меню убеждаемся, что курсор мыши включен и откреплен
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Прячем настройки при старте
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        // НОВОЕ: Убеждаемся, что экран прозрачный при старте
        if (fadeScreen != null)
        {
            Color c = fadeScreen.color;
            c.a = 0f;
            fadeScreen.color = c;
            fadeScreen.gameObject.SetActive(false); // Выключаем, чтобы не блокировал клики мышки
        }
    }

    // --- ФУНКЦИИ ДЛЯ КНОПОК ---

    // 1. Новая игра
    public void StartNewGame()
    {
        Debug.Log("Запускаем новую игру...");
        
        // НОВОЕ: Отключаем возможность кликать на другие кнопки, пока идет загрузка
        UnityEngine.EventSystems.EventSystem.current.enabled = false;
        
        // Запускаем корутину с анимацией и затемнением
        StartCoroutine(StartGameSequence());
    }

    // НОВОЕ: Корутина для плавного перехода
    private IEnumerator StartGameSequence()
    {
        // 1. Запускаем анимацию персонажа
        if (menuCharacterAnimator != null)
        {
            menuCharacterAnimator.SetTrigger(startGameTrigger);
        }

        // 2. Ждем, пока проиграется нужный кусок анимации (или пока она не закончится)
        yield return new WaitForSeconds(delayBeforeFade);

        // --- НОВОЕ: ЗАПУСКАЕМ ЗВУК ---
        if (fadeSound != null)
        {
            fadeSound.Play();
        }

        // 3. Начинаем затемнение экрана
        if (fadeScreen != null)
        {
            fadeScreen.gameObject.SetActive(true); // Включаем черную картинку
            Color c = fadeScreen.color;
            
            while (c.a < 1f)
            {
                c.a += Time.deltaTime * fadeSpeed;
                fadeScreen.color = c;
                yield return null; // Ждем следующий кадр
            }
        }

        // 4. Загружаем сцену!
        SceneManager.LoadScene(gameSceneName);
    }

    // 2. Загрузка игры
    public void LoadGame()
    {
        // Пока у нас нет системы сохранений, сделаем просто заглушку
        Debug.Log("Попытка загрузить сохранение... (Система еще в разработке)");
    }

    // 3. Открыть/Закрыть настройки
    public void ToggleSettings()
    {
        if (settingsPanel != null)
        {
            // Переключаем: если были выключены - включаем, и наоборот
            bool isActive = settingsPanel.activeSelf;
            settingsPanel.SetActive(!isActive);
        }
        else
        {
            Debug.LogError("Ты не назначил Settings Panel в MainMenuManager!");
        }
    }

    // 4. Выход из игры
    public void ExitGame()
    {
        Debug.Log("Выходим из игры!");
        
        // Эта команда закроет игру после сборки (в редакторе Unity она не сработает)
        Application.Quit();
        
        // Специальный код, чтобы кнопка выхода работала даже в редакторе Unity
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}