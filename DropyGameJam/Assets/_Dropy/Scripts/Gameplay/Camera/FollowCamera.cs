using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Hedefi sabit bir dünya ofsetinden yumuşakça takip eden basit kamera.
    /// Target'ı Play sırasında Inspector'dan değiştirebilirsiniz.
    /// </summary>
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [Tooltip("Hedefe göre kamera konumu (dünya ekseninde)")]
        [SerializeField] Vector3 offset = new Vector3(2.5f, 3.2f, -2.5f);
        [Tooltip("Kameranın baktığı yükseklik (hedefin ayağından itibaren)")]
        [SerializeField] float lookHeight = 1.2f;
        [SerializeField, Min(0.01f)] float smoothTime = 0.35f;

        Vector3 velocity;

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        void Start()
        {
            if (target != null)
                Snap();
        }

        void LateUpdate()
        {
            if (target == null)
                return;
            transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }

        [ContextMenu("Hedefe Işınlan")]
        void Snap()
        {
            transform.position = target.position + offset;
            transform.LookAt(target.position + Vector3.up * lookHeight);
        }
    }
}
