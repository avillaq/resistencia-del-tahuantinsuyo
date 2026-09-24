using UnityEngine;
using ResistenciaTahuantinsuyo.Runtime.Audio;
using ResistenciaTahuantinsuyo.Runtime.Combat;

namespace ResistenciaTahuantinsuyo.Runtime.Gameplay
{
    /// <summary>
    /// Suministro medicinal andino (Matico / Muña) que restaura salud al jugador.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HealingPickup : MonoBehaviour
    {
        [Header("Configuración de Curación")]
        [SerializeField] private int healAmount = 35;
        [SerializeField] private float bobSpeed = 2.5f;
        [SerializeField] private float bobHeight = 0.08f;

        private Vector3 startPos;
        private bool isUsed = false;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
            startPos = transform.position;
        }

        private void Update()
        {
            if (!isUsed)
            {
                float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(startPos.x, newY, startPos.z);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isUsed) return;

            if (other.CompareTag("Player"))
            {
                var health = other.GetComponent<Health>();
                if (health != null && health.CurrentHealth < health.MaxHealth && !health.IsDead)
                {
                    isUsed = true;
                    health.Heal(healAmount);

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayUISelect();
                    }

                    gameObject.SetActive(false);
                }
            }
        }
    }
}
