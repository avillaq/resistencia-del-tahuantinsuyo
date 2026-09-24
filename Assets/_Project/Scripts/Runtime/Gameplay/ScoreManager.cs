using System;
using UnityEngine;

namespace ResistenciaTahuantinsuyo.Runtime.Gameplay
{
    /// <summary>
    /// Administrador de puntaje y objetivos de misión.
    /// Registra objetos culturales recuperados, penalizaciones de detección y cálculo de maestría.
    /// </summary>
    public class ScoreManager : MonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [Header("Puntajes Base")]
        [SerializeField] private int pointsPerCulturalObject = 500;
        [SerializeField] private int stealthPenaltyPerAlert = 100;
        [SerializeField] private int damagePenaltyPerHit = 25;
        [SerializeField] private int missionCompletionBaseBonus = 1000;

        private int currentScore = 0;
        private int culturalObjectsCollected = 0;
        private int totalCulturalObjects = 2;
        private int timesDetected = 0;
        private int timesDamaged = 0;
        private bool isMissionFinished = false;
        private bool isVictory = false;

        public int CurrentScore => currentScore;
        public int CulturalObjectsCollected => culturalObjectsCollected;
        public int TotalCulturalObjects => totalCulturalObjects;
        public int TimesDetected => timesDetected;
        public int TimesDamaged => timesDamaged;
        public bool IsMissionFinished => isMissionFinished;
        public bool IsVictory => isVictory;

        public event Action<int, int> OnScoreChanged; // (currentScore, delta)
        public event Action<int, int> OnCulturalObjectsChanged; // (collected, total)
        public event Action<bool, int> OnMissionFinished; // (isVictory, finalScore)

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            OnScoreChanged?.Invoke(currentScore, 0);
            OnCulturalObjectsChanged?.Invoke(culturalObjectsCollected, totalCulturalObjects);
        }

        public void SetTotalCulturalObjects(int total)
        {
            totalCulturalObjects = Mathf.Max(1, total);
            OnCulturalObjectsChanged?.Invoke(culturalObjectsCollected, totalCulturalObjects);
        }

        public void AddScore(int amount, string reason = "")
        {
            if (amount <= 0 || isMissionFinished) return;
            currentScore += amount;
            OnScoreChanged?.Invoke(currentScore, amount);
        }

        public void DeductScore(int amount, string reason = "")
        {
            if (amount <= 0 || isMissionFinished) return;
            int previousScore = currentScore;
            currentScore = Mathf.Max(0, currentScore - amount);
            int delta = currentScore - previousScore;
            OnScoreChanged?.Invoke(currentScore, delta);
        }

        public void RegisterCulturalObjectCollected(string objectId = "", int customScore = -1)
        {
            if (isMissionFinished) return;
            culturalObjectsCollected++;
            int points = (customScore > 0) ? customScore : pointsPerCulturalObject;
            AddScore(points, "Cultural Object: " + objectId);
            OnCulturalObjectsChanged?.Invoke(culturalObjectsCollected, totalCulturalObjects);
        }

        public void RegisterDetection()
        {
            if (isMissionFinished) return;
            timesDetected++;
            DeductScore(stealthPenaltyPerAlert, "Detection Alert");
        }

        public void RegisterDamageTaken()
        {
            if (isMissionFinished) return;
            timesDamaged++;
            DeductScore(damagePenaltyPerHit, "Damage Received");
        }

        public void FinishMission(bool victory)
        {
            if (isMissionFinished) return;
            isMissionFinished = true;
            isVictory = victory;

            if (victory)
            {
                int stealthBonus = Mathf.Max(0, 500 - (timesDetected * 100));
                int relicBonus = culturalObjectsCollected * 200;
                int finalBonus = missionCompletionBaseBonus + stealthBonus + relicBonus;
                currentScore += finalBonus;
                OnScoreChanged?.Invoke(currentScore, finalBonus);
            }

            OnMissionFinished?.Invoke(victory, currentScore);
        }
    }
}