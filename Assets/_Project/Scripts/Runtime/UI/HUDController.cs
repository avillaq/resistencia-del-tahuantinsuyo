using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ResistenciaTahuantinsuyo.Runtime.Audio;
using ResistenciaTahuantinsuyo.Runtime.Combat;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;

namespace ResistenciaTahuantinsuyo.Runtime.UI
{
    /// <summary>
    /// Controlador oficial del HUD en UI Toolkit según product.md (Sección 8.1, 8.2 y 8.6).
    /// Conecta reactivamente la barra de vida, el puntaje, las reliquias y el estado de alerta.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class HUDController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Health playerHealth;

        private UIDocument uiDocument;
        private VisualElement healthBarFill;
        private Label healthText;
        private Label stealthBadge;
        private Label scoreLabel;
        private Label relicsLabel;
        private VisualElement victoryModal;
        private Label victoryScoreLabel;
        private Button btnRestartVictory;
        private VisualElement defeatModal;
        private Label defeatScoreLabel;
        private Button btnRestartDefeat;

        private bool isScoreSubscribed = false;
        private bool isAudioSubscribed = false;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
        }

        private void OnEnable()
        {
            BindUIElements();
            SubscribeEvents();
        }

        private void Start()
        {
            // Garantizar suscripciones en Start una vez que todos los Singletons están en Awake
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void BindUIElements()
        {
            if (uiDocument == null) return;
            var root = uiDocument.rootVisualElement;
            if (root == null) return;

            healthBarFill = root.Q<VisualElement>("HealthBarFill");
            healthText = root.Q<Label>("HealthText");
            stealthBadge = root.Q<Label>("StealthBadge");
            scoreLabel = root.Q<Label>("ScoreLabel");
            relicsLabel = root.Q<Label>("RelicsLabel");

            victoryModal = root.Q<VisualElement>("VictoryModal");
            victoryScoreLabel = root.Q<Label>("VictoryScoreLabel");
            btnRestartVictory = root.Q<Button>("BtnRestartVictory");

            defeatModal = root.Q<VisualElement>("DefeatModal");
            defeatScoreLabel = root.Q<Label>("DefeatScoreLabel");
            btnRestartDefeat = root.Q<Button>("BtnRestartDefeat");

            if (btnRestartVictory != null)
            {
                btnRestartVictory.clicked -= HandleRestartClicked;
                btnRestartVictory.clicked += HandleRestartClicked;
            }
            if (btnRestartDefeat != null)
            {
                btnRestartDefeat.clicked -= HandleRestartClicked;
                btnRestartDefeat.clicked += HandleRestartClicked;
            }
        }

        private void SubscribeEvents()
        {
            if (playerHealth == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    playerHealth = player.GetComponent<Health>();
                }
            }

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHealthBar;
                playerHealth.OnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }

            if (ScoreManager.Instance != null && !isScoreSubscribed)
            {
                ScoreManager.Instance.OnScoreChanged += UpdateScore;
                ScoreManager.Instance.OnCulturalObjectsChanged += UpdateRelics;
                ScoreManager.Instance.OnMissionFinished += HandleMissionFinished;
                isScoreSubscribed = true;

                UpdateScore(ScoreManager.Instance.CurrentScore, 0);
                UpdateRelics(ScoreManager.Instance.CulturalObjectsCollected, ScoreManager.Instance.TotalCulturalObjects);
            }

            if (AudioManager.Instance != null && !isAudioSubscribed)
            {
                AudioManager.Instance.OnTensionChanged += SetStealthAlert;
                isAudioSubscribed = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (btnRestartVictory != null) btnRestartVictory.clicked -= HandleRestartClicked;
            if (btnRestartDefeat != null) btnRestartDefeat.clicked -= HandleRestartClicked;

            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHealthBar;
            }

            if (ScoreManager.Instance != null && isScoreSubscribed)
            {
                ScoreManager.Instance.OnScoreChanged -= UpdateScore;
                ScoreManager.Instance.OnCulturalObjectsChanged -= UpdateRelics;
                ScoreManager.Instance.OnMissionFinished -= HandleMissionFinished;
                isScoreSubscribed = false;
            }

            if (AudioManager.Instance != null && isAudioSubscribed)
            {
                AudioManager.Instance.OnTensionChanged -= SetStealthAlert;
                isAudioSubscribed = false;
            }
        }

        public void SetPlayerHealth(Health h)
        {
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged -= UpdateHealthBar;
            }

            playerHealth = h;
            if (playerHealth != null)
            {
                playerHealth.OnHealthChanged += UpdateHealthBar;
                UpdateHealthBar(playerHealth.CurrentHealth, playerHealth.MaxHealth);
            }
        }

        private void UpdateHealthBar(int current, int max)
        {
            if (healthBarFill != null)
            {
                float pct = Mathf.Clamp01((float)current / Mathf.Max(1, max)) * 100f;
                healthBarFill.style.width = new Length(pct, LengthUnit.Percent);
            }

            if (healthText != null)
            {
                healthText.text = current + " / " + max;
            }
        }

        public void SetStealthAlert(bool isAlerted)
        {
            if (stealthBadge == null) return;

            if (isAlerted)
            {
                stealthBadge.text = "¡ALERTA!";
                stealthBadge.RemoveFromClassList("badge-stealth");
                stealthBadge.AddToClassList("badge-alert");
            }
            else
            {
                stealthBadge.text = "SIGILO";
                stealthBadge.RemoveFromClassList("badge-alert");
                stealthBadge.AddToClassList("badge-stealth");
            }
        }

        private void UpdateScore(int currentScore, int delta)
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = "PUNTAJE: " + currentScore.ToString("D4");
            }
        }

        private void UpdateRelics(int current, int total)
        {
            if (relicsLabel != null)
            {
                relicsLabel.text = "RELIQUIAS: " + current + " / " + total;
            }
        }

        private void HandleMissionFinished(bool isVictory, int finalScore)
        {
            if (isVictory)
            {
                if (victoryScoreLabel != null)
                {
                    victoryScoreLabel.text = "PUNTAJE FINAL: " + finalScore.ToString("D4");
                }
                if (victoryModal != null)
                {
                    victoryModal.style.display = DisplayStyle.Flex;
                }
                if (defeatModal != null)
                {
                    defeatModal.style.display = DisplayStyle.None;
                }
            }
            else
            {
                if (defeatScoreLabel != null)
                {
                    defeatScoreLabel.text = "PUNTAJE ACUMULADO: " + finalScore.ToString("D4");
                }
                if (defeatModal != null)
                {
                    defeatModal.style.display = DisplayStyle.Flex;
                }
                if (victoryModal != null)
                {
                    victoryModal.style.display = DisplayStyle.None;
                }
            }
        }

        private void HandleRestartClicked()
        {
            Time.timeScale = 1f;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.buildIndex);
        }
    }
}
