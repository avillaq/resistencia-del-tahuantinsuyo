using UnityEngine;
using ResistenciaTahuantinsuyo.Runtime.Audio;
using ResistenciaTahuantinsuyo.Runtime.Combat;
using ResistenciaTahuantinsuyo.Runtime.Gameplay;

namespace ResistenciaTahuantinsuyo.Runtime.AI
{
    /// <summary>
    /// Controlador principal de IA de enemigos.
    /// Coordina la máquina de estados entre WANDER y SEEK, ataque cuerpo a cuerpo y eventos de Audio/Score.
    /// Responde a la finalización de misión congelando comportamiento y silenciando pasos.
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

        [Header("Combate Táctico")]
        [SerializeField] private int attackDamage = 25;
        [SerializeField] private float attackDistance = 0.85f;
        [SerializeField] private float attackCooldown = 1.0f;

        [Header("Colores de Estado")]
        [SerializeField] private Color wanderColor = Color.white;
        [SerializeField] private Color seekColor = new Color(1.0f, 0.6f, 0.6f);

        // Componentes internos
        private Rigidbody2D rb;
        private EnemyPerception perception;

        // Estado actual
        private EnemyState currentState = EnemyState.Wander;
        private int currentWaypointIndex = 0;
        private int patrolDirection = 1;
        private float waitTimer = 0f;
        private float lostSightTimer = 0f;
        private float stepTimer = 0f;
        private float attackTimer = 0f;
        private Vector2 lastKnownTargetPos;
        private bool isWaitingAtWaypoint = false;
        private bool wasInSeek = false;
        private bool isMissionFinished = false;

        public EnemyState CurrentState => currentState;
        public EnemyPerception Perception => perception;
        public bool IsMissionFinished => isMissionFinished;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            perception = GetComponent<EnemyPerception>();

            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (bodyRenderer == null)
            {
                bodyRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.OnMissionFinished -= HandleMissionFinished;
                ScoreManager.Instance.OnMissionFinished += HandleMissionFinished;
            }
        }

        private void OnDisable()
        {
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
                    FreezeEnemy();
                    return;
                }
            }

            if (targetPlayer == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    targetPlayer = playerObj.transform;
                }
            }

            TransitionToWander(false);
        }

        private void HandleMissionFinished(bool isVictory, int finalScore)
        {
            FreezeEnemy();
        }

        public void FreezeEnemy()
        {
            isMissionFinished = true;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;
            }

            if (alertIndicatorRenderer != null)
            {
                alertIndicatorRenderer.enabled = false;
            }

            if (bodyRenderer != null)
            {
                bodyRenderer.color = wanderColor;
            }

            if (perception != null)
            {
                perception.SetTargetDetected(false);
            }
        }

        private void Update()
        {
            if (isMissionFinished) return;

            UpdatePerceptionAndStateTransitions();
            HandleAudioSteps();
        }

        private void FixedUpdate()
        {
            if (isMissionFinished)
            {
                if (rb != null) rb.linearVelocity = Vector2.zero;
                return;
            }

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

                    if (lostSightTimer >= lostSightCooldown)
                    {
                        TransitionToWander(true);
                    }
                }
            }
        }

        private void TransitionToSeek()
        {
            currentState = EnemyState.Seek;
            wasInSeek = true;
            lostSightTimer = 0f;
            isWaitingAtWaypoint = false;
            waitTimer = 0f;
            attackTimer = attackCooldown * 0.8f; // Pre-cargar ataque para rápida respuesta

            UpdateVisualState(seekColor, true);

            // Audio: Alerta y transición a música de tensión
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEnemyAlert();
                AudioManager.Instance.SetTensionMusic(true);
            }

            // Score: Registrar penalización por detección de sigilo
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.RegisterDetection();
            }
        }

        private void TransitionToWander(bool playLostSightSound = true)
        {
            bool hadBeenSeeking = wasInSeek;
            currentState = EnemyState.Wander;
            wasInSeek = false;
            perception.SetTargetDetected(false);
            lostSightTimer = 0f;
            isWaitingAtWaypoint = false;
            waitTimer = 0f;

            if (patrolRoute != null && patrolRoute.WaypointCount > 0)
            {
                patrolRoute.GetClosestWaypoint(transform.position, out currentWaypointIndex);
            }

            UpdateVisualState(wanderColor, false);

            if (hadBeenSeeking && AudioManager.Instance != null)
            {
                if (playLostSightSound)
                {
                    AudioManager.Instance.PlayEnemyLostSight();
                }
                AudioManager.Instance.SetTensionMusic(false);
            }
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

        private void HandleAudioSteps()
        {
            if (rb.linearVelocity.sqrMagnitude > 0.05f)
            {
                float interval = (currentState == EnemyState.Seek) ? 0.35f : 0.5f;
                stepTimer += Time.deltaTime;
                if (stepTimer >= interval)
                {
                    stepTimer = 0f;
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayEnemyStep();
                    }
                }
            }
        }

        #endregion

        #region Comportamientos de Movimiento y Combate

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

            // Ataque táctico cuerpo a cuerpo al jugador
            if (targetPlayer != null && distance <= attackDistance)
            {
                attackTimer += Time.fixedDeltaTime;
                if (attackTimer >= attackCooldown)
                {
                    attackTimer = 0f;
                    var damageable = targetPlayer.GetComponent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.TakeDamage(attackDamage, toTarget.normalized);
                    }
                }
            }
            else
            {
                attackTimer = attackCooldown * 0.7f;
            }

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