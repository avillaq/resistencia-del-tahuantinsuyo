using UnityEngine;
using ResistenciaTahuantinsuyo.Runtime.Audio;

namespace ResistenciaTahuantinsuyo.Runtime.Gameplay
{
    /// <summary>
    /// Zona de evacuación/salida segura según product.md (Secciones 3.1 y 6.3).
    /// Al alcanzarla tras recuperar las reliquias requeridas, finaliza la misión con éxito y calcula el puntaje.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MissionExitZone : MonoBehaviour
    {
        [Header("Condiciones de Misión")]
        [Tooltip("Si es verdadero, requiere recuperar todos los objetos culturales antes de poder escapar.")]
        [SerializeField] private bool requireAllCulturalObjects = true;

        private bool isTriggered = false;

        public bool RequireAllCulturalObjects
        {
            get => requireAllCulturalObjects;
            set => requireAllCulturalObjects = value;
        }

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isTriggered) return;

            if (other.CompareTag("Player"))
            {
                if (requireAllCulturalObjects && ScoreManager.Instance != null &&
                    ScoreManager.Instance.CulturalObjectsCollected < ScoreManager.Instance.TotalCulturalObjects)
                {
                    Debug.Log("<color=yellow>[Resistencia Tahuantinsuyo]</color> Aún no has recuperado todos los objetos culturales requeridos para escapar.");
                    return;
                }

                isTriggered = true;
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.FinishMission(true);
                }

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayMissionComplete();
                }
            }
        }
    }
}