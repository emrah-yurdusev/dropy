using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// NPC'lerin açıp içinden geçebildiği kapı.
    /// Bu objenin pivotu menteşede olmalı, kapı kanadı yerel +X yönüne uzanmalı
    /// (model pivotu ortadaysa boş bir parent objeyi menteşeye koyup Door'u ona ekleyin).
    /// Mavi ok (+Z) kapının bir yüzünü gösterir; kapı açanın tersine doğru açılır.
    /// </summary>
    public class Door : MonoBehaviour
    {
        [Tooltip("Dönen kanat. Boşsa bu obje döner")]
        [SerializeField] Transform leaf;
        [Tooltip("Kapı genişliği (metre), menteşeden yerel +X yönünde")]
        [SerializeField, Min(0.1f)] float width = 1f;
        [SerializeField] float openAngle = 95f;
        [Tooltip("Açılma/kapanma hızı (derece/saniye)")]
        [SerializeField, Min(1f)] float swingSpeed = 220f;
        [Tooltip("NPC'nin kapıya ne kadar uzakta durup açacağı / geçtikten sonra nerede duracağı")]
        [SerializeField, Min(0.2f)] float approachDistance = 0.8f;
        [Tooltip("NPC geçtikten sonra kapıyı arkasından kapatsın mı")]
        [SerializeField] bool closeBehind = true;

        Quaternion leafClosedRotation;
        Vector3 closedPosition;
        Quaternion closedRotation;
        float currentAngle;
        float targetAngle;

        public bool IsOpen => Mathf.Abs(currentAngle) > openAngle * 0.8f;
        public bool CloseBehind => closeBehind;

        Transform Leaf => leaf != null ? leaf : transform;

        // Kapının kapalıkenki orta noktası (yerden).
        public Vector3 Center => closedPosition + closedRotation * (Vector3.right * width * 0.5f);
        Vector3 Normal => closedRotation * Vector3.forward;

        void Awake()
        {
            closedPosition = transform.position;
            closedRotation = transform.rotation;
            leafClosedRotation = Leaf.localRotation;
        }

        void Update()
        {
            if (Mathf.Approximately(currentAngle, targetAngle))
                return;
            currentAngle = Mathf.MoveTowards(currentAngle, targetAngle, swingSpeed * Time.deltaTime);
            Leaf.localRotation = leafClosedRotation * Quaternion.Euler(0f, currentAngle, 0f);
        }

        /// <summary>Kapıyı açan kişinin tersine doğru açar.</summary>
        public void Open(Vector3 openerPosition)
        {
            if (targetAngle != 0f)
                return;
            // +açı kanadı -Z yüzüne doğru döndürür; açan +Z tarafındaysa kapı -Z'ye açılır.
            targetAngle = IsOnFrontSide(openerPosition) ? openAngle : -openAngle;
        }

        public void Close() => targetAngle = 0f;

        /// <summary>Verilen pozisyonun bulunduğu taraftaki (veya karşı taraftaki) bekleme noktası.</summary>
        public Vector3 GetApproachPoint(Vector3 from, bool otherSide)
        {
            float side = IsOnFrontSide(from) ? 1f : -1f;
            if (otherSide)
                side = -side;
            return Center + Normal * (side * approachDistance);
        }

        bool IsOnFrontSide(Vector3 position) => Vector3.Dot(position - Center, Normal) >= 0f;

        void OnDrawGizmos()
        {
            if (!Application.isPlaying)
            {
                closedPosition = transform.position;
                closedRotation = transform.rotation;
            }
            Gizmos.color = new Color(1f, 0.6f, 0.1f);
            Gizmos.DrawLine(closedPosition, closedPosition + closedRotation * (Vector3.right * width));
            Gizmos.DrawLine(Center, Center + Vector3.up * 2f);
            foreach (float side in new[] { 1f, -1f })
            {
                var point = Center + Normal * (side * approachDistance);
                Gizmos.DrawWireSphere(point + Vector3.up * 0.05f, 0.15f);
            }
        }
    }
}
