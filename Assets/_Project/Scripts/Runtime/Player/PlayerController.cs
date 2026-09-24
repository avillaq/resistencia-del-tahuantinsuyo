using UnityEngine;
using UnityEngine.InputSystem;
using ResistenciaTahuantinsuyo.Runtime.Audio;
using ResistenciaTahuantinsuyo.Runtime.Combat;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;

namespace ResistenciaTahuantinsuyo.Runtime.Player
{
    /// <summary>
    /// Controlador del jugador para movimiento top-down en 8 direcciones.
    /// controles WASD con Unity Input System,
    /// física 2D continua contra obstáculos, integración con Health y desacoplado de la UI.
    /// Responde a la finalización de misión bloqueando el control para evitar muertes posvictoria.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float rotationSpeed = 900f; // grados por segundo
        [SerializeField] private float stepInterval = 0.35f;

        [Header("Entrada (Opcional)")]
        [Tooltip("Acción del Input System opcional. Si no se asigna, lee directamente WASD del Input System.")]
        [SerializeField] private InputActionReference moveAction;

        private Rigidbody2D rb;
        private Health health;
        private Vector2 moveInput;
        private SpriteRenderer spriteRenderer;
        private float stepTimer = 0f;
        private bool isDead = false;
        private bool isGameplayLocked = false;

        public Vector2 MoveInput => moveInput;
        public bool IsDead => isDead;
        public bool IsGameplayLocked => isGameplayLocked;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            health = GetComponent<Health>();

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }

            if (health != null)
            {
                health.OnDamaged -= HandleDamage;
                health.OnDamaged += HandleDamage;
                health.OnDied -= HandleDeath;
                health.OnDied += HandleDeath;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnMissionFinished -= HandleMissionFinished;
                ScoreManager.Instance.OnMissionFinished += HandleMissionFinished;
            }
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }

            if (health != null)
            {
                health.OnDamaged -= HandleDamage;
                health.OnDied -= HandleDeath;
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnMissionFinished -= HandleMissionFinished;
            }
        }

        private void Start()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnMissionFinished -= HandleMissionFinished;
                ScoreManager.Instance.OnMissionFinished += HandleMissionFinished;

                if (ScoreManager.Instance.IsMissionFinished)
                {
                    LockPlayer();
                }
            }
        }

        private void HandleMissionFinished(bool isVictory, int finalScore)
        {
            LockPlayer();
        }

        public void LockPlayer()
        {
            isGameplayLocked = true;
            moveInput = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }
        }

        private void HandleDamage(int amount)
        {
            if (isDead || isGameplayLocked || (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished))
            {
                return;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayPlayerDamage();
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.RegisterDamageTaken();
            }
        }

        private void HandleDeath()
        {
            // Si la misión ya finalizó (ej. victoria alcanzada previamente), ignorar muerte
            if (ScoreManager.Instance != null && ScoreManager.Instance.IsMissionFinished)
            {
                return;
            }

            isDead = true;
            moveInput = Vector2.zero;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayMissionFailed();
            }

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.FinishMission(false);
            }
        }

        private void Update()
        {
            if (isDead || isGameplayLocked) return;
            ReadInput();
            HandleFootsteps();
        }

        private void FixedUpdate()
        {
            if (isDead || isGameplayLocked)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }
            ApplyMovement();
        }

        private void ReadInput()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveInput = moveAction.action.ReadValue<Vector2>();
            }
            else
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    float x = 0f;
                    float y = 0f;

                    if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) y += 1f;
                    if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) y -= 1f;
                    if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) x -= 1f;
                    if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) x += 1f;

                    moveInput = new Vector2(x, y);
                }
                else
                {
                    moveInput = Vector2.zero;
                }
            }

            if (moveInput.sqrMagnitude > 1f)
            {
                moveInput.Normalize();
            }
        }

        private void ApplyMovement()
        {
            rb.linearVelocity = moveInput * moveSpeed;

            if (moveInput.sqrMagnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = rb.rotation;
                float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
                rb.MoveRotation(newAngle);
            }
        }

        private void HandleFootsteps()
        {
            if (moveInput.sqrMagnitude > 0.01f)
            {
                stepTimer += Time.deltaTime;
                if (stepTimer >= stepInterval)
                {
                    stepTimer = 0f;
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayPlayerStep(true);
                    }
                }
            }
            else
            {
                stepTimer = stepInterval * 0.8f;
            }
        }
    }
}