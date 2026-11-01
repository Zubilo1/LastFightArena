using UnityEngine;
using UnityEngine.UI;

namespace LastFight
{
    /// <summary>Интерфейс: HP, волна, убийства, пауза, победа, поражение. Только отображает данные из других скриптов.</summary>
    public class UIManager : MonoBehaviour
    {
        [Header("Источники данных")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerHealth player;
        [SerializeField] private WaveManager waveManager;

        [Header("HUD")]
        [SerializeField] private RectTransform healthFill;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text healthText;
        [SerializeField] private Text waveText;
        [SerializeField] private Text killsText;
        [SerializeField] private Text waveBanner;
        [SerializeField] private Button pauseButton;

        [Header("Панели")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private GameObject defeatPanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseRestartButton;
        [SerializeField] private Button victoryRestartButton;
        [SerializeField] private Button defeatRestartButton;
        [SerializeField] private Button pauseMenuButton;
        [SerializeField] private Button victoryMenuButton;
        [SerializeField] private Button defeatMenuButton;
        [SerializeField] private Text victoryStatsText;
        [SerializeField] private Text defeatStatsText;

        [Header("Оформление")]
        [SerializeField] private Color healthyColor = new Color(0.25f, 0.8f, 0.3f, 1f);
        [SerializeField] private Color lowHealthColor = new Color(0.9f, 0.2f, 0.2f, 1f);
        [SerializeField, Min(0.5f)] private float bannerDuration = 2f;

        private float bannerHideTime;

        private void Start()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged += OnStateChanged;
                gameManager.KillsChanged += OnKillsChanged;
            }
            if (player != null) player.HealthChanged += OnHealthChanged;
            if (waveManager != null) waveManager.WaveStarted += OnWaveStarted;

            if (gameManager != null)
            {
                if (pauseButton != null) pauseButton.onClick.AddListener(gameManager.TogglePause);
                if (resumeButton != null) resumeButton.onClick.AddListener(gameManager.Resume);
                if (pauseRestartButton != null) pauseRestartButton.onClick.AddListener(gameManager.Restart);
                if (victoryRestartButton != null) victoryRestartButton.onClick.AddListener(gameManager.Restart);
                if (defeatRestartButton != null) defeatRestartButton.onClick.AddListener(gameManager.Restart);
                if (pauseMenuButton != null) pauseMenuButton.onClick.AddListener(gameManager.LoadMainMenu);
                if (victoryMenuButton != null) victoryMenuButton.onClick.AddListener(gameManager.LoadMainMenu);
                if (defeatMenuButton != null) defeatMenuButton.onClick.AddListener(gameManager.LoadMainMenu);
            }

            // Начальные значения.
            if (player != null) OnHealthChanged(player.Current, player.Max);
            OnKillsChanged(gameManager != null ? gameManager.Kills : 0);
            SetText(waveText, "WAVE 1 / " + (waveManager != null ? Mathf.Max(1, waveManager.TotalWaves) : 3));
            if (waveBanner != null) waveBanner.gameObject.SetActive(false);
            OnStateChanged(gameManager != null ? gameManager.State : GameState.Playing);
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.StateChanged -= OnStateChanged;
                gameManager.KillsChanged -= OnKillsChanged;
            }
            if (player != null) player.HealthChanged -= OnHealthChanged;
            if (waveManager != null) waveManager.WaveStarted -= OnWaveStarted;
        }

        private void Update()
        {
            if (waveBanner != null && waveBanner.gameObject.activeSelf && Time.unscaledTime >= bannerHideTime)
                waveBanner.gameObject.SetActive(false);
        }

        private void OnHealthChanged(int current, int max)
        {
            float ratio = max > 0 ? Mathf.Clamp01(current / (float)max) : 0f;
            if (healthFill != null) healthFill.anchorMax = new Vector2(ratio, 1f);
            if (healthFillImage != null) healthFillImage.color = Color.Lerp(lowHealthColor, healthyColor, ratio);
            SetText(healthText, "HP  " + current + " / " + max);
        }

        private void OnKillsChanged(int kills)
        {
            SetText(killsText, "KILLS: " + kills);
        }

        private void OnWaveStarted(int wave, int total)
        {
            SetText(waveText, "WAVE " + wave + " / " + total);
            if (waveBanner != null)
            {
                waveBanner.text = wave == total ? "FINAL WAVE" : "WAVE " + wave;
                waveBanner.gameObject.SetActive(true);
                bannerHideTime = Time.unscaledTime + bannerDuration;
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (pausePanel != null) pausePanel.SetActive(state == GameState.Paused);
            if (victoryPanel != null) victoryPanel.SetActive(state == GameState.Victory);
            if (defeatPanel != null) defeatPanel.SetActive(state == GameState.Defeat);
            if (pauseButton != null) pauseButton.interactable = state == GameState.Playing || state == GameState.Paused;

            int kills = gameManager != null ? gameManager.Kills : 0;
            if (state == GameState.Victory)
                SetText(victoryStatsText, "Enemies defeated: " + kills);
            else if (state == GameState.Defeat)
                SetText(defeatStatsText, "Wave " + Mathf.Max(1, waveManager != null ? waveManager.CurrentWave : 1) + "  |  Kills: " + kills);
        }

        private static void SetText(Text label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
