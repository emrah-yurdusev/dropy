using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Kameraya Perlin gürültüsüyle sarsıntı ekler. Ofseti her karede geri alıp yeniden uyguladığı için
    /// kamerayı hareket ettiren başka scriptlerle (ör. FollowCamera) birlikte çalışabilir.
    /// Şiddeti Amplitude ile dışarıdan (EarthquakeSequence) ayarlanır.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        [Tooltip("Amplitude 1 iken en büyük konum sapması (metre)")]
        [SerializeField] float maxOffset = 0.35f;
        [Tooltip("Amplitude 1 iken en büyük açı sapması (derece)")]
        [SerializeField] float maxRotation = 1.5f;
        [Tooltip("Sarsıntı frekansı")]
        [SerializeField, Min(0.1f)] float frequency = 11f;

        Vector3 appliedOffset;
        Quaternion appliedRotation = Quaternion.identity;
        float seed;

        /// <summary>0 = sarsıntı yok, 1 = tam şiddet.</summary>
        public float Amplitude { get; set; }

        void Awake()
        {
            seed = Random.value * 100f;
        }

        void LateUpdate()
        {
            transform.localPosition -= appliedOffset;
            transform.localRotation *= Quaternion.Inverse(appliedRotation);

            // Ayarlardan ekran sarsıntısı kapatıldıysa kamera titremez.
            float amplitude = Dropy.Core.GameSettings.ScreenShake ? Amplitude : 0f;
            if (amplitude <= 0f)
            {
                appliedOffset = Vector3.zero;
                appliedRotation = Quaternion.identity;
                return;
            }

            float t = Time.time * frequency;
            var n = new Vector3(Noise(0f, t), Noise(1f, t), Noise(2f, t));
            appliedOffset = n * (maxOffset * amplitude);
            appliedRotation = Quaternion.Euler(n.y * maxRotation * amplitude, n.x * maxRotation * amplitude, n.z * maxRotation * 0.5f * amplitude);
            transform.localPosition += appliedOffset;
            transform.localRotation *= appliedRotation;
        }

        float Noise(float channel, float t)
        {
            return Mathf.PerlinNoise(seed + channel * 17.3f, t) * 2f - 1f;
        }
    }
}
