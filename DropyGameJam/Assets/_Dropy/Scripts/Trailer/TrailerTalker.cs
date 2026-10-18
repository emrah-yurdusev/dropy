using System.Reflection;
using Dropy.Gameplay;
using UnityEngine;

namespace Dropy.Trailer
{
    /// <summary>
    /// Trailer için "bir şey anlatıyor" hâli: oturan (ya da ayakta duran) humanoid karakterin kolları animasyonun
    /// üstüne koddan kalkıp iner, eller anlatır gibi oynar, ağız konuşur gibi açılıp kapanır, kafa vurgu yapar.
    /// Açılar karakterin kendi eksenlerinde uygulanır; kollar ters yöne kalkıyorsa "ters" kutularını işaretleyin.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class TrailerTalker : MonoBehaviour
    {
        [Tooltip("Sahne açıldıktan kaç saniye sonra anlatmaya başlasın")]
        public float delay = 0.6f;
        [Range(0f, 1f)] public float weight = 1f;
        [Tooltip("Hareketlerin genel hızı")]
        public float tempo = 1f;
        public FaceExpression expression = FaceExpression.Happy;

        [Header("Kollar (derece)")]
        [Tooltip("Üst kolun öne kalkışı")]
        public float lift = 38f;
        public float liftSwing = 14f;
        [Tooltip("Dirseğin bükülüp eli yukarı getirmesi")]
        public float elbow = 30f;
        public float elbowSwing = 18f;
        [Tooltip("Kolların yana açılması")]
        public float spread = 22f;
        public float spreadSwing = 10f;
        [Tooltip("Ön kolun dışa açılması; oturan pozda eller kucakta birleştiği için gerekli")]
        public float open = 70f;
        public float openSwing = 20f;
        [Tooltip("Bileğin dönmesi (avuç açılıp kapanıyormuş gibi)")]
        public float wristSwing = 25f;
        public bool invertLift;
        public bool invertElbow;

        [Header("Kafa ve gövde")]
        public float nod = 5f;
        public float lean = 3f;
        [Tooltip("Endişeli kaçamak bakışlar: kafa ara ara yana ve aşağı kayar (derece, 0 = yok)")]
        public float glance;

        [Header("Ağız")]
        public bool moveMouth = true;
        public float mouthSpeed = 11f;

        static readonly FieldInfo OpenMouthField = typeof(FaceController).GetField("openMouth", BindingFlags.Instance | BindingFlags.NonPublic);

        Animator animator;
        FaceController face;
        Transform openMouth;
        Vector3 openMouthRest;
        float current;
        float seed;
        float startTime;

        void Start()
        {
            animator = GetComponentInChildren<Animator>();
            face = GetComponentInChildren<FaceController>();
            if (face != null)
            {
                face.SetExpression(expression);
                openMouth = OpenMouthField?.GetValue(face) as Transform;
                if (openMouth != null)
                    openMouthRest = openMouth.localScale;
            }
            seed = Random.value * 50f;
            startTime = Time.time;
        }

        void LateUpdate()
        {
            if (animator == null || !animator.isHuman)
                return;

            bool on = Time.time - startTime >= delay;
            current = Mathf.MoveTowards(current, on ? weight : 0f, Time.deltaTime / 0.6f);
            if (current <= 0f)
                return;

            float t = (Time.time - startTime) * tempo;
            // Konuşma cümle cümle gelir: yavaş bir zarf, vurgular ve kısa duraklar.
            float phrase = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.55f, Mathf.PerlinNoise(seed, t * 0.35f)));
            float energy = current * Mathf.Lerp(0.35f, 1f, phrase);

            var right = transform.right;
            var forward = transform.forward;

            var spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            if (spine != null)
                spine.rotation = Quaternion.AngleAxis(lean * energy * (0.5f + 0.5f * Mathf.Sin(t * 0.9f)), right) * spine.rotation;

            Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1f, t, energy, right, forward);
            Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -1f, t + 1.7f, energy * 0.8f, right, forward);

            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            if (head != null)
            {
                float beat = Mathf.Sin(t * 4.2f) * Noise(5f, t * 0.8f);
                head.rotation = Quaternion.AngleAxis(nod * energy * beat, right) * Quaternion.AngleAxis(nod * 0.6f * energy * Noise(6f, t * 0.5f), Vector3.up) * head.rotation;
                if (glance > 0f)
                {
                    // Bakış çoğu zaman kamerada, arada hızlıca yana ve aşağı kaçar.
                    float away = Noise(7f, t * 0.45f);
                    float shift = Mathf.Sign(away) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.45f, Mathf.Abs(away)));
                    head.rotation = Quaternion.AngleAxis(glance * shift * current, Vector3.up)
                        * Quaternion.AngleAxis(glance * 0.5f * Mathf.Abs(shift) * current, right) * head.rotation;
                }
            }

            if (moveMouth && openMouth != null)
            {
                float flap = Mathf.Clamp01(Mathf.PerlinNoise(seed + 9f, Time.time * mouthSpeed) * 1.8f - 0.55f) * phrase * current;
                openMouth.gameObject.SetActive(flap > 0.02f);
                openMouth.localScale = openMouthRest * flap;
            }
        }

        void Arm(HumanBodyBones upperBone, HumanBodyBones lowerBone, HumanBodyBones handBone, float side, float t, float energy, Vector3 right, Vector3 forward)
        {
            var upper = animator.GetBoneTransform(upperBone);
            var lower = animator.GetBoneTransform(lowerBone);
            var hand = animator.GetBoneTransform(handBone);
            if (upper == null || lower == null)
                return;

            float c = side > 0f ? 0f : 20f;
            float liftAngle = (lift + liftSwing * Noise(c + 1f, t * 0.9f)) * energy * (invertLift ? 1f : -1f);
            float spreadAngle = (spread + spreadSwing * Noise(c + 2f, t * 0.7f)) * energy * side;
            float elbowAngle = (elbow + elbowSwing * Mathf.Sin(t * 2.6f + c) * Mathf.Abs(Noise(c + 3f, t * 0.6f) + 0.4f)) * energy * (invertElbow ? 1f : -1f);

            float openAngle = (open + openSwing * Noise(c + 5f, t * 0.8f)) * energy * side;

            upper.rotation = Quaternion.AngleAxis(liftAngle, right) * Quaternion.AngleAxis(spreadAngle, forward) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis(openAngle, transform.up) * Quaternion.AngleAxis(elbowAngle, right) * lower.rotation;
            if (hand != null)
            {
                var along = (hand.position - lower.position).normalized;
                hand.rotation = Quaternion.AngleAxis(wristSwing * energy * Noise(c + 4f, t * 1.3f), along) * hand.rotation;
            }
        }

        float Noise(float channel, float t) => Mathf.PerlinNoise(seed + channel * 7.1f, t) * 2f - 1f;
    }
}
