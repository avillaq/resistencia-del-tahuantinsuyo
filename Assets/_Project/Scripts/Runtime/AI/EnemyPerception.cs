using UnityEngine;

namespace ResistenciaTahuantinsuyo.Runtime.AI
{
    /// <summary>
    /// Maneja la percepción sensorial (campo y línea de visión 2D) del enemigo.
    /// Cumple con product.md sección 7.4: FOV + Línea de visión libre de obstáculos = Detección.
    /// </summary>
    public class EnemyPerception : MonoBehaviour
    {
        [Header("Parámetros de Visión")]
        [Tooltip("Rango máximo de distancia de visión.")]
        [SerializeField] private float viewDistance = 6.5f;

        [Tooltip("Ángulo total del cono de visión en grados.")]
        [Range(10f, 360f)]
        [SerializeField] private float viewAngle = 90f;

        [Header("Máscaras de Capas")]
        [Tooltip("Capa de objetos que bloquean la visión (muros, coberturas).")]
        [SerializeField] private LayerMask obstacleMask;

        [Tooltip("Capa del objetivo a detectar (Jugador).")]
        [SerializeField] private LayerMask targetMask;

        [Tooltip("Tag del jugador para validación adicional.")]
        [SerializeField] private string targetTag = "Player";

        [Header("Visualización y Debug")]
        [SerializeField] private bool showGizmos = true;
        [SerializeField] private Color normalFovColor = new Color(0.79f, 0.65f, 0.42f, 0.4f); // #C9A66B Arena cálida
        [SerializeField] private Color alertFovColor = new Color(0.66f, 0.25f, 0.21f, 0.6f);  // #A94136 Rojo alerta

        private bool isTargetDetected = false;
        private readonly RaycastHit2D[] raycastHits = new RaycastHit2D[16];

        public float ViewDistance => viewDistance;
        public float ViewAngle => viewAngle;
        public bool IsTargetDetected => isTargetDetected;

        /// <summary>
        /// Dirección frontal 2D hacia donde mira la entidad (transform.up por convención en 2D cenital).
        /// </summary>
        public Vector2 FacingDirection => transform.up;

        /// <summary>
        /// Evalúa si el objetivo especificado está dentro del campo y de la línea de visión sin obstrucciones.
        /// </summary>
        public bool CanSeeTarget(Transform target, out Vector2 lastKnownPosition)
        {
            lastKnownPosition = Vector2.zero;
            isTargetDetected = false;

            if (target == null) return false;

            Vector2 origin = transform.position;
            Vector2 targetPos = target.position;
            Vector2 directionToTarget = targetPos - origin;
            float distanceToTarget = directionToTarget.magnitude;

            // 1. Verificación de distancia
            if (distanceToTarget > viewDistance || distanceToTarget < 0.001f)
            {
                return false;
            }

            // 2. Verificación de ángulo de visión (FOV)
            Vector2 forward = FacingDirection;
            float angleToTarget = Vector2.Angle(forward, directionToTarget);
            if (angleToTarget > (viewAngle * 0.5f))
            {
                return false;
            }

            // 3. Verificación de línea de visión (Raycast 2D sin impactar en sí mismo)
            LayerMask combinedMask = obstacleMask | targetMask;
            int hitCount = Physics2D.RaycastNonAlloc(origin, directionToTarget.normalized, raycastHits, distanceToTarget, combinedMask);

            // Ordenar por distancia si hay múltiples impactos
            System.Array.Sort(raycastHits, 0, hitCount, RaycastDistanceComparer.Instance);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D col = raycastHits[i].collider;
                if (col == null) continue;

                // Ignorar colisionadores pertenecientes a este mismo enemigo
                if (col.transform == transform || col.transform.IsChildOf(transform))
                {
                    continue;
                }

                // Si impactó primero con un obstáculo, la línea de visión está bloqueada
                if (((1 << col.gameObject.layer) & obstacleMask) != 0)
                {
                    return false;
                }

                // Si impactó con el objetivo o con el tag Player
                if (col.transform == target || col.CompareTag(targetTag))
                {
                    isTargetDetected = true;
                    lastKnownPosition = targetPos;
                    return true;
                }
            }

            return false;
        }

        public void SetTargetDetected(bool detected)
        {
            isTargetDetected = detected;
        }

        private void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Vector3 origin = transform.position;
            Vector2 forward = FacingDirection;

            Gizmos.color = isTargetDetected ? alertFovColor : normalFovColor;

            // Líneas límite del cono
            float halfAngle = viewAngle * 0.5f;
            Vector3 leftRayDirection = Quaternion.Euler(0, 0, halfAngle) * forward;
            Vector3 rightRayDirection = Quaternion.Euler(0, 0, -halfAngle) * forward;

            Gizmos.DrawRay(origin, leftRayDirection * viewDistance);
            Gizmos.DrawRay(origin, rightRayDirection * viewDistance);

            // Arco del cono
            int segments = 20;
            Vector3 previousPoint = origin + leftRayDirection * viewDistance;
            float stepAngle = viewAngle / segments;

            for (int i = 1; i <= segments; i++)
            {
                float currentAngle = halfAngle - (stepAngle * i);
                Vector3 currentDir = Quaternion.Euler(0, 0, currentAngle) * forward;
                Vector3 currentPoint = origin + currentDir * viewDistance;
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }
        }

        private class RaycastDistanceComparer : System.Collections.Generic.IComparer<RaycastHit2D>
        {
            public static readonly RaycastDistanceComparer Instance = new RaycastDistanceComparer();
            public int Compare(RaycastHit2D x, RaycastHit2D y)
            {
                return x.distance.CompareTo(y.distance);
            }
        }
    }
}
