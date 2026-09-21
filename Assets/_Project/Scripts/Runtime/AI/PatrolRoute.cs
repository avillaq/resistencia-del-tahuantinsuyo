using System.Collections.Generic;
using UnityEngine;

namespace ResistenciaTahuantinsuyo.Runtime.AI
{
    /// <summary>
    /// Define una ruta de patrullaje para enemigos (Wandering).
    /// </summary>
    public class PatrolRoute : MonoBehaviour
    {
        [Header("Configuración de Ruta")]
        [Tooltip("Puntos de la ruta. Si se deja vacío, tomará automáticamente los hijos del GameObject.")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        [Tooltip("Si es verdadero, al llegar al final vuelve al inicio (0->1->2->0). Si es falso, hace ping-pong (0->1->2->1->0).")]
        [SerializeField] private bool loop = true;

        [Header("Visualización")]
        [SerializeField] private Color gizmoColor = new Color(0.79f, 0.65f, 0.42f, 0.9f); // #C9A66B Arena cálida

        public int WaypointCount => waypoints.Count;

        private void Awake()
        {
            InitializeWaypointsIfNeeded();
        }

        private void Reset()
        {
            InitializeWaypointsIfNeeded();
        }

        private void InitializeWaypointsIfNeeded()
        {
            if (waypoints == null || waypoints.Count == 0)
            {
                waypoints = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    waypoints.Add(transform.GetChild(i));
                }
            }
        }

        public Transform GetWaypoint(int index)
        {
            if (waypoints == null || waypoints.Count == 0) return null;
            if (index < 0 || index >= waypoints.Count) return waypoints[0];
            return waypoints[index];
        }

        public int GetNextIndex(int currentIndex, ref int direction)
        {
            if (waypoints == null || waypoints.Count <= 1) return 0;

            if (loop)
            {
                return (currentIndex + 1) % waypoints.Count;
            }
            else
            {
                int nextIndex = currentIndex + direction;
                if (nextIndex >= waypoints.Count)
                {
                    direction = -1;
                    return Mathf.Max(0, waypoints.Count - 2);
                }
                else if (nextIndex < 0)
                {
                    direction = 1;
                    return Mathf.Min(waypoints.Count - 1, 1);
                }
                return nextIndex;
            }
        }

        public Transform GetClosestWaypoint(Vector3 currentPosition, out int closestIndex)
        {
            closestIndex = 0;
            if (waypoints == null || waypoints.Count == 0) return null;

            float minDistanceSqr = float.MaxValue;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                float distSqr = (waypoints[i].position - currentPosition).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    closestIndex = i;
                }
            }

            return waypoints[closestIndex];
        }

        private void OnDrawGizmos()
        {
            InitializeWaypointsIfNeeded();
            if (waypoints == null || waypoints.Count == 0) return;

            Gizmos.color = gizmoColor;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Gizmos.DrawSphere(waypoints[i].position, 0.25f);

                int nextIdx = (i + 1) % waypoints.Count;
                if (!loop && i == waypoints.Count - 1) break;
                if (waypoints[nextIdx] != null)
                {
                    Gizmos.DrawLine(waypoints[i].position, waypoints[nextIdx].position);
                }
            }
        }
    }
}
