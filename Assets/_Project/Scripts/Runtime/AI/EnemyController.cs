using UnityEngine;

namespace ResistenciaTahuantinsuyo.Runtime.AI
{
    /// <summary>
    /// Controlador principal de IA de enemigos según product.md (Secciones 7.2, 7.3, 7.5).
    /// Coordina la máquina de estados entre WANDER y SEEK, y el retorno tras perder la visión.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(EnemyPerception))]
    public class EnemyController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private PatrolRoute patrolRoute;
        [SerializeField] private Transform targetPlayer;
        [SerializeField] private SpriteRenderer bodyRenderer;
        [SerializeField] private SpriteRenderer alertIndicatorRenderer;

        [Header("Velocidades")]
        [SerializeField] private float wanderSpeed = 2.0f;
        [SerializeField] private float seekSpeed = 3.5f;
        [SerializeField] private float rotationSpeed = 720f; // grados por segundo

        [Header("Configuración de Patrulla (Wander)")]
        [SerializeField] private float arrivalDistance = 0.25f;
        [SerializeField] private float waypointWaitTime = 1.0f;

        [Header("Configuración de Persecución (Seek)")]
        [Tooltip("Tiempo en segundos que busca en la última posición conocida tras perder la línea de visión antes de volver a WANDER.")]
        [SerializeField] private float lostSightCooldown = 1.5f;

        [Header("Colores de Estado (Paleta Andina)")]
        [SerializeField] private Color wanderColor = new Color(0.60f, 0.36f, 0.24f); // #9A5B3E Arcilla / Terracota
        [SerializeField] private Color seekColor = new Color(0.66f, 0.25f, 0.21f);   // #A94136 Rojo alerta

        // Componentes internos
        private Rigidbody2D rb;
        private EnemyPerception perception;

        // Estado actual
        private EnemyState currentState = EnemyState.Wander;
        private int currentWaypointIndex = 0;
        private int patrolDirection = 1;
        private float waitTimer = 0f;
        private float lostSightTimer = 0f;
        private Vector2 lastKnownTargetPos;
        private bool isWaitingAtWaypoint = false;

        public EnemyState CurrentState => currentState;
        public EnemyPerception Perception => perception;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            perception = GetComponent<EnemyPerception>();

            // Configurar Rigidbody2D para 2D cenital
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void Start()
        {
            // Si no se asignó jugador manualmente, buscar por Tag Player
            if (targetPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    targetPlayer = playerObj.transform;
                }
            }

            TransitionToWander();
        }

        private void Update()
        {
            UpdatePerceptionAndStateTransitions();
        }

        private void FixedUpdate()
        {
            switch (currentState)
            {
                case EnemyState.Wander:
                    HandleWanderPhysics();
                    break;
                case EnemyState.Seek:
                    HandleSeekPhysics();
                    break;
            }
        }

        #region Máquina de Estados y Transiciones

        private void UpdatePerceptionAndStateTransitions()
        {
            bool seesPlayer = false;

            if (targetPlayer != null)
            {
                seesPlayer = perception.CanSeeTarget(targetPlayer, out Vector2 seenPos);
                if (seesPlayer)
                {
                    lastKnownTargetPos = seenPos;
                }
            }

            if (currentState == EnemyState.Wander)
            {
                if (seesPlayer)
                {
                    TransitionToSeek();
                }
            }
            else if (currentState == EnemyState.Seek)
            {
                if (seesPlayer)
                {
                    lostSightTimer = 0f;
                }
                else
                {
                    perception.SetTargetDetected(false);
                    lostSightTimer += Time.deltaTime;

                    // Si transcurrió el tiempo de gracia sin ver al jugador, retornar a WANDER
                    if (lostSightTimer >= lostSightCooldown)
                    {
                        TransitionToWander();
                    }
                }
            }
        }

        private void TransitionToSeek()
        {
            currentState = EnemyState.Seek;
            lostSightTimer = 0f;
            isWaitingAtWaypoint = false;
            waitTimer = 0f;

            UpdateVisualState(seekColor, true);
        }

        private void TransitionToWander()
        {
            currentState = EnemyState.Wander;
            perception.SetTargetDetected(false);
            lostSightTimer = 0f;
            isWaitingAtWaypoint = false;
            waitTimer = 0f;

            // Retomar el waypoint más cercano en la ruta
            if (patrolRoute != null && patrolRoute.WaypointCount > 0)
            {
                patrolRoute.GetClosestWaypoint(transform.position, out currentWaypointIndex);
            }

            UpdateVisualState(wanderColor, false);
        }

        private void UpdateVisualState(Color bodyColor, bool alertActive)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.color = bodyColor;
            }

            if (alertIndicatorRenderer != null)
            {
                alertIndicatorRenderer.enabled = alertActive;
            }
        }

        #endregion

        #region Comportamientos de Movimiento

        private void HandleWanderPhysics()
        {
            if (patrolRoute == null || patrolRoute.WaypointCount == 0)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Transform targetWaypoint = patrolRoute.GetWaypoint(currentWaypointIndex);
            if (targetWaypoint == null)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 currentPos = transform.position;
            Vector2 targetPos = targetWaypoint.position;
            Vector2 toWaypoint = targetPos - currentPos;
            float distance = toWaypoint.magnitude;

            if (isWaitingAtWaypoint)
            {
                rb.linearVelocity = Vector2.zero;
                waitTimer += Time.fixedDeltaTime;

                if (waitTimer >= waypointWaitTime)
                {
                    isWaitingAtWaypoint = false;
                    waitTimer = 0f;
                    currentWaypointIndex = patrolRoute.GetNextIndex(currentWaypointIndex, ref patrolDirection);
                }
                return;
            }

            if (distance <= arrivalDistance)
            {
                isWaitingAtWaypoint = true;
                waitTimer = 0f;
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 moveDirection = toWaypoint.normalized;
            rb.linearVelocity = moveDirection * wanderSpeed;
            RotateTowards(moveDirection);
        }

        private void HandleSeekPhysics()
        {
            Vector2 currentPos = transform.position;
            Vector2 targetDestination = (targetPlayer != null && perception.IsTargetDetected) 
                ? (Vector2)targetPlayer.position 
                : lastKnownTargetPos;

            Vector2 toTarget = targetDestination - currentPos;
            float distance = toTarget.magnitude;

            if (distance <= arrivalDistance)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }

            Vector2 moveDirection = toTarget.normalized;
            rb.linearVelocity = moveDirection * seekSpeed;
            RotateTowards(moveDirection);
        }

        private void RotateTowards(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;

            // En 2D cenital con transform.up como forward
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = rb.rotation;
            float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(newAngle);
        }

        #endregion

        public void SetPatrolRoute(PatrolRoute route)
        {
            patrolRoute = route;
            if (currentState == EnemyState.Wander && route != null && route.WaypointCount > 0)
            {
                route.GetClosestWaypoint(transform.position, out currentWaypointIndex);
            }
        }
    }
}
