using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Deprem anını yönetir: kamerayı sarsar, ayakta kalan binaları tabanları etrafında sallar,
    /// yıkılacak binaların çöküş animasyonunu başlatır. Otomatik başlayabilir ya da Begin() ile tetiklenir.
    /// </summary>
    public class EarthquakeSequence : MonoBehaviour
    {
        [Header("Zamanlama")]
        [SerializeField] bool autoStart = true;
        [Tooltip("Sahne açıldıktan kaç saniye sonra başlasın (autoStart açıksa)")]
        [SerializeField, Min(0f)] float startDelay = 2f;
        [Tooltip("Sarsıntı süresi; çöküş animasyonu da 8 saniye")]
        [SerializeField, Min(0.1f)] float duration = 8f;
        [Tooltip("Zamana göre şiddet (0-1 arası, x ekseni süreye oranlı)")]
        [SerializeField] AnimationCurve intensity = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.55f, 0.85f), new Keyframe(1f, 0f));

        [Header("Hedefler")]
        [SerializeField] CameraShake cameraShake;
        [Tooltip("Yıkılmayan, sadece sallanan binalar")]
        [SerializeField] Transform[] swayingBuildings;
        [Tooltip("Tam şiddette binaların en büyük yatma açısı (derece)")]
        [SerializeField] float swayDegrees = 0.6f;
        [SerializeField, Min(0.1f)] float swayFrequency = 1.6f;
        [SerializeField] CollapsibleBuilding[] collapsibleBuildings;

        [Header("Olaylar")]
        public UnityEvent onQuakeStarted;
        public UnityEvent onQuakeFinished;

        /// <summary>
        /// Oyun akışı (EarthquakeCinematic) sahneyi yükleyip kendisi başlatırken true olur;
        /// o sırada autoStart yok sayılır. Sahne tek başına açıldığında false kalır.
        /// </summary>
        public static bool ExternalControl { get; set; }

        public bool IsRunning { get; private set; }
        public float Duration => duration;

        void Start()
        {
            if (autoStart && !ExternalControl)
                StartCoroutine(BeginAfterDelay());
        }

        IEnumerator BeginAfterDelay()
        {
            yield return new WaitForSeconds(startDelay);
            Begin();
        }

        /// <summary>Depremi başlatır. Zaten sürüyorsa bir şey yapmaz.</summary>
        [ContextMenu("Depremi Başlat")]
        public void Begin()
        {
            if (IsRunning)
                return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsRunning = true;
            onQuakeStarted?.Invoke();

            var swayers = new List<Transform>();
            if (swayingBuildings != null)
                swayers.AddRange(swayingBuildings);
            if (collapsibleBuildings != null)
            {
                foreach (var b in collapsibleBuildings)
                {
                    if (b == null)
                        continue;
                    if (b.WillCollapse)
                        b.Collapse();
                    else
                        swayers.Add(b.transform);
                }
            }
            swayers.RemoveAll(s => s == null);
            var baseRotations = new Quaternion[swayers.Count];
            for (int i = 0; i < swayers.Count; i++)
                baseRotations[i] = swayers[i].localRotation;

            float t = 0f;
            while (t < duration)
            {
                float k = Mathf.Clamp01(intensity.Evaluate(t / duration));
                if (cameraShake != null)
                    cameraShake.Amplitude = k;
                for (int i = 0; i < swayers.Count; i++)
                {
                    float phase = i * 1.37f;
                    float x = Mathf.Sin((t * swayFrequency + phase) * Mathf.PI * 2f);
                    float z = Mathf.Sin((t * swayFrequency * 0.83f + phase * 2.1f) * Mathf.PI * 2f);
                    swayers[i].localRotation = baseRotations[i] * Quaternion.Euler(x * swayDegrees * k, 0f, z * swayDegrees * k);
                }
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < swayers.Count; i++)
                swayers[i].localRotation = baseRotations[i];
            if (cameraShake != null)
                cameraShake.Amplitude = 0f;
            IsRunning = false;
            onQuakeFinished?.Invoke();
        }
    }
}
