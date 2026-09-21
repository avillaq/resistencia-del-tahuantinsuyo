using UnityEngine;
using UnityEngine.InputSystem;

namespace ResistenciaTahuantinsuyo.Runtime.Player
{
    /// <summary>
    /// Controlador del jugador para movimiento top-down en 8 direcciones.
    /// Respeta product.md secciones 2.3 y 7.1: controles WASD con Unity Input System,
    /// física 2D continua contra obstáculos y desacoplado de la UI.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movimiento")]
        [SerializeField] private float moveSpeed = 4.5f;
        [SerializeField] private float rotationSpeed = 900f; // grados por segundo

        [Header("Colores de Paleta Andina")]
        [SerializeField] private Color playerColor = new Color(0.26f, 0.32f, 0.43f); // #43526D Índigo textil andino

        [Header("Entrada (Opcional)")]
        [Tooltip("Acción del Input System opcional. Si no se asigna, lee directamente WASD del Input System.")]
        [SerializeField] private InputActionReference moveAction;

        private Rigidbody2D rb;
        private Vector2 moveInput;
        private SpriteRenderer spriteRenderer;

        public Vector2 MoveInput => moveInput;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = playerColor;
            }
        }

        private void OnEnable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (moveAction != null && moveAction.action != null)
            {
                moveAction.action.Disable();
            }
        }

        private void Update()
        {
            ReadInput();
        }

        private void FixedUpdate()
        {
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
                // Lectura mediante Input System (Keyboard.current)
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
    }
}
