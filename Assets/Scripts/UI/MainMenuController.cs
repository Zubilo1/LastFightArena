using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastFight
{
    /// <summary>Главное меню: кнопка PLAY загружает игровую сцену, QUIT закрывает игру.</summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "LastFightArena";
        [SerializeField] private Button playButton;
        [SerializeField] private Button quitButton;

        private void Start()
        {
            Time.timeScale = 1f;
            if (playButton != null) playButton.onClick.AddListener(Play);
            if (quitButton != null) quitButton.onClick.AddListener(Quit);
        }

        public void Play()
        {
            if (Application.CanStreamedLevelBeLoaded(gameSceneName))
                SceneManager.LoadScene(gameSceneName);
            else
                Debug.LogError("MainMenu: сцена '" + gameSceneName + "' не найдена в Build Settings. Запустите Last Fight > Build Arena Scene.", this);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
