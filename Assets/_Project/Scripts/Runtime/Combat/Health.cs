using System;
using System.Collections;
using UnityEngine;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;

namespace ResistenciaTahuantinsuyo.Runtime.Combat
{
    /// <summary>
    /// Componente desacoplado de vida y daño.
    /// Maneja vida, invulnerabilidad post-impacto, feedback visual no-gore y eventos de estado.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [Header("Configuración de Vida")]
        [SerializeField] private int maxHealth = 100;
        [SerializeField] private float invulnerabilityDuration = 0.8f;
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Feedback Visual No-Gore")]
        [SerializeField] private Color damageFlashColor = new Color(0.66f, 0.25f, 0.21f); // #A94136 Rojo andino

        private int currentHealth;
        private bool isDead = false;
        private bool isInvulnerable = false;
        private Coroutine flashCoroutine;
        private Color originalColor = Color.white;

        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public bool IsDead => isDead;
        public bool IsInvulnerable => isInvulnerable;

        public event Action<int, int> OnHealthChanged;
        public event Action<int> OnDamaged;
        public event Action<int> OnHealed;
        public event Action OnDied;

        private void Awake()
        {
            currentHealth = maxHealth;
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (targetRenderer != null)
            {
                originalColor = targetRenderer.color;
            }
        }

        private void Start()
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void TakeDamage(int amount, Vector2 hitDirection)
        {
            if (isDead || isInvulnerable || amount <= 0) return;

            // Bloquear daño si la misión ya finalizó (ej. victoria alcanzada o derrota previa)
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished) return;

            currentHealth = Mathf.Max(0, currentHealth - amount);

            OnDamaged?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0)
            {
                isDead = true;
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                if (targetRenderer != null) targetRenderer.color = damageFlashColor;
                OnDied?.Invoke();
            }
            else
            {
                if (invulnerabilityDuration > 0f)
                {
                    if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                    flashCoroutine = StartCoroutine(DamageFlashRoutine());
                }
            }
        }

        public void Heal(int amount)
        {
            if (isDead || amount <= 0 || currentHealth >= maxHealth) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealed?.Invoke(amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private IEnumerator DamageFlashRoutine()
        {
            isInvulnerable = true;
            float elapsed = 0f;
            float blinkInterval = 0.1f;
            bool showFlash = true;

            while (elapsed < invulnerabilityDuration)
            {
                if (targetRenderer != null)
                {
                    targetRenderer.color = showFlash ? damageFlashColor : originalColor;
                }

                showFlash = !showFlash;
                yield return new WaitForSeconds(blinkInterval);
                elapsed += blinkInterval;
            }

            if (targetRenderer != null)
            {
                targetRenderer.color = originalColor;
            }

            isInvulnerable = false;
            flashCoroutine = null;
        }

        public void SetMaxHealth(int newMax, bool resetCurrent = true)
        {
            maxHealth = Mathf.Max(1, newMax);
            if (resetCurrent)
            {
                currentHealth = maxHealth;
                isDead = false;
            }
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }
}