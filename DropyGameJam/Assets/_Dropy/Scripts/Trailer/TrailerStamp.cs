using System.Collections;
using Dropy.Gameplay;
using UnityEngine;

namespace Dropy.Trailer
{
    /// <summary>
    /// Trailer için mühür vuruşu: damga havada bekler, geri çekilir, kâğıda sertçe iner. Vuruş anında ses, kamera
    /// sarsıntısı, toz ve kâğıtta mürekkep izi çıkar; sonra damga kalkıp izi gösterir. Kökün yeri kâğıda değdiği noktadır.
    /// </summary>
    public class TrailerStamp : MonoBehaviour
    {
        [Tooltip("Hareket eden damga modeli (kökün çocuğu, tabanı kökte)")]
        public Transform stamp;
        [Tooltip("Vuruşta hafifçe sekecek kâğıt")]
        public Transform paper;
        [Tooltip("Vuruşta beliren mürekkep izi (başta kapalı)")]
        public Transform ink;
        public ParticleSystem dust;
        public TrailerCamera shakeCamera;

        [Header("Zamanlama")]
        public float delay = 1.6f;
        public float hoverHeight = 0.16f;
        public float windUpHeight = 0.3f;
        public float windUpTime = 0.5f;
        public float holdTime = 0.18f;
        public float slamTime = 0.08f;
        public float pressTime = 0.45f;
        public float liftTime = 0.7f;
        [Tooltip("Kalkarken damganın kayacağı yön (kökün ekseninde)")]
        public Vector3 liftAway = new Vector3(0.12f, 0.28f, 0.08f);
        [Range(0f, 1f)] public float shake = 1f;

        Vector3 inkScale;

        void Start()
        {
            if (ink != null)
            {
                inkScale = ink.localScale;
                ink.gameObject.SetActive(false);
            }
            if (stamp != null)
                stamp.localPosition = Vector3.up * hoverHeight;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            yield return Hover(delay);

            // Geri çekil: biraz yukarı ve öne eğik.
            yield return Move(Vector3.up * windUpHeight, Quaternion.Euler(-8f, 0f, 4f), windUpTime, EaseOut);
            yield return Hold(holdTime);

            // Vuruş.
            yield return Move(Vector3.zero, Quaternion.identity, slamTime, t => t * t * t);
            Impact();

            // Bastır: damga hafifçe ezilir, sonra toparlanır.
            var restScale = stamp.localScale;
            for (float t = 0f; t < pressTime; t += Time.deltaTime)
            {
                float k = t / pressTime;
                float squash = Mathf.Sin(k * Mathf.PI) * 0.08f * Mathf.Exp(-k * 3f);
                stamp.localScale = new Vector3(restScale.x * (1f + squash), restScale.y * (1f - squash), restScale.z * (1f + squash));
                if (paper != null)
                    paper.localScale = new Vector3(1f, 1f, 1f) * (1f + Mathf.Sin(k * Mathf.PI * 3f) * 0.012f * (1f - k));
                InkPop(Mathf.Clamp01(t / 0.12f));
                yield return null;
            }
            stamp.localScale = restScale;
            if (paper != null)
                paper.localScale = Vector3.one;
            InkPop(1f);

            // Kalk ve izi göster.
            yield return Move(liftAway, Quaternion.Euler(10f, 0f, -6f), liftTime, EaseInOut);
            yield return Hover(float.MaxValue);
        }

        void Impact()
        {
            Sfx.Stamp();
            if (shakeCamera != null)
                shakeCamera.Kick(shake);
            if (dust != null)
                dust.Play();
            if (ink != null)
                ink.gameObject.SetActive(true);
        }

        void InkPop(float k)
        {
            if (ink == null)
                return;
            float s = Mathf.Lerp(1.18f, 1f, EaseOut(k));
            ink.localScale = inkScale * s;
        }

        IEnumerator Hover(float seconds)
        {
            var basePos = stamp.localPosition;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                stamp.localPosition = basePos + Vector3.up * (Mathf.Sin(t * 2.2f) * 0.006f);
                yield return null;
            }
        }

        IEnumerator Hold(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
                yield return null;
        }

        IEnumerator Move(Vector3 toPosition, Quaternion toRotation, float seconds, System.Func<float, float> curve)
        {
            var fromPosition = stamp.localPosition;
            var fromRotation = stamp.localRotation;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = curve(t / seconds);
                stamp.localPosition = Vector3.LerpUnclamped(fromPosition, toPosition, k);
                stamp.localRotation = Quaternion.SlerpUnclamped(fromRotation, toRotation, k);
                yield return null;
            }
            stamp.localPosition = toPosition;
            stamp.localRotation = toRotation;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t) * (1f - t);
        static float EaseInOut(float t) => t * t * (3f - 2f * t);
    }
}
