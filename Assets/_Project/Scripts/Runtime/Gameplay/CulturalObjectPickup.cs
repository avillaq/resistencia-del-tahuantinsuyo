using UnityEngine;
using ResistenciaTahuantinsuyo.Runtime.Audio;

namespace ResistenciaTahuantinsuyo.Runtime.Gameplay
{
    /// <summary>
    /// Objeto cultural recuperable.
    /// Otorga puntaje, reproduce feedback sonoro y registra el objetivo de misión.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CulturalObjectPickup : MonoBehaviour
    {
        [Header("Datos del Objeto")]
        [SerializeField] private string objectId = "OBJ_Quipu_01";
        [SerializeField] private string objectName = "Quipu Administrativo";
        [SerializeField] private int scoreValue = 500;

        [Header("Animación Flotante")]
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float bobHeight = 0.12f;

        private Vector3 startPos;
        private bool isCollected = false;

        public string ObjectId => objectId;
        public string ObjectName => objectName;
        public int ScoreValue => scoreValue;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
            startPos = transform.position;
        }

        private void Update()
        {
            if (!isCollected)
            {
                float newY = startPos.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                transform.position = new Vector3(startPos.x, newY, startPos.z);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isCollected) return;

            if (other.CompareTag("Player"))
            {
                Collect();
            }
        }

        public void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.RegisterCulturalObjectCollected(objectId, scoreValue);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayObjectRecovered();
            }

            gameObject.SetActive(false);
        }
    }
}