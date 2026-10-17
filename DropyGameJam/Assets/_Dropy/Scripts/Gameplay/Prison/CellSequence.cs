using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Hapishane ara sahnesi: kamera koridordan hücreye yavaşça yaklaşırken parmaklık kapısı kapanır.
    /// Otomatik başlayabilir ya da Begin() ile tetiklenir.
    /// </summary>
    public class CellSequence : MonoBehaviour
    {
        [Header("Zamanlama")]
        [SerializeField] bool autoStart = true;
        [Tooltip("Sahne açıldıktan kaç saniye sonra başlasın (autoStart açıksa)")]
        [SerializeField, Min(0f)] float startDelay = 1.5f;
        [Tooltip("Kapı kapandıktan sonra sahnenin sürdüğü süre")]
        [SerializeField, Min(0f)] float holdAfterClose = 2.5f;

        [Header("Hedefler")]
        [SerializeField] CellDoor door;
        [SerializeField] Transform cameraTransform;
        [Tooltip("Sekans boyunca kameranın kendi ileri yönünde kat ettiği mesafe (metre)")]
        [SerializeField] float cameraPush = 0.35f;

        [Header("Olaylar")]
        public UnityEvent onDoorClosed;

        public bool IsRunning { get; private set; }

        public float Duration => (door != null ? door.Duration : 0f) + holdAfterClose;

        void Start()
        {
            // Oyun akışı sahneyi kendisi başlatıyorsa (AdditiveCinematic) otomatik başlama.
            if (autoStart && !AdditiveCinematic.IsPlaying)
                StartCoroutine(BeginAfterDelay());
        }

        IEnumerator BeginAfterDelay()
        {
            yield return new WaitForSeconds(startDelay);
            Begin();
        }

        [ContextMenu("Sekansı Başlat")]
        public void Begin()
        {
            if (IsRunning)
                return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsRunning = true;
            Vector3 camStart = cameraTransform != null ? cameraTransform.position : Vector3.zero;
            Vector3 camEnd = cameraTransform != null ? camStart + cameraTransform.forward * cameraPush : Vector3.zero;
            float total = Mathf.Max(0.01f, Duration);

            if (door != null)
                door.Close();
            bool closedRaised = false;
            for (float t = 0f; t < total; t += Time.deltaTime)
            {
                if (cameraTransform != null)
                    cameraTransform.position = Vector3.Lerp(camStart, camEnd, Mathf.SmoothStep(0f, 1f, t / total));
                if (!closedRaised && door != null && !door.IsClosing)
                {
                    closedRaised = true;
                    onDoorClosed?.Invoke();
                }
                yield return null;
            }
            if (cameraTransform != null)
                cameraTransform.position = camEnd;
            IsRunning = false;
        }
    }
}
