using UnityEngine;

namespace Dropy.Gameplay
{
    public enum FaceExpression
    {
        Neutral,
        Angry,
        Sad,
        Surprised,
        Suspicious,
        Happy
    }

    /// <summary>
    /// Karakterin kaşlarını ve ağzını ifadeye göre hareket ettirir.
    /// Yüz parçaları Head kemiğinin altında durur; prefab'daki duruşları "dinlenme pozu" sayılır,
    /// ifadeler bu pozun üzerine ofset olarak uygulanır.
    /// Kullanım: GetComponentInChildren&lt;FaceController&gt;().SetExpression(FaceExpression.Angry);
    /// </summary>
    public class FaceController : MonoBehaviour
    {
        [System.Serializable]
        public struct PartPose
        {
            [Tooltip("Yukarı (+) / aşağı (-) kayma, metre")]
            public float raise;
            [Tooltip("Yüzün ortasına doğru kayma, metre")]
            public float inward;
            [Tooltip("Derece. Kaşta + iç uç aşağı (kızgın); ağızda + köşe yukarı (gülümseme)")]
            public float tilt;
            [Tooltip("Kalınlık çarpanı")]
            public float thickness;
            [Tooltip("Uzunluk çarpanı")]
            public float length;

            public PartPose(float raise, float inward, float tilt, float thickness = 1f, float length = 1f)
            {
                this.raise = raise;
                this.inward = inward;
                this.tilt = tilt;
                this.thickness = thickness;
                this.length = length;
            }

            public static PartPose Lerp(PartPose a, PartPose b, float t) => new PartPose(
                Mathf.Lerp(a.raise, b.raise, t),
                Mathf.Lerp(a.inward, b.inward, t),
                Mathf.Lerp(a.tilt, b.tilt, t),
                Mathf.Lerp(a.thickness, b.thickness, t),
                Mathf.Lerp(a.length, b.length, t));
        }

        [System.Serializable]
        public struct ExpressionPose
        {
            public FaceExpression expression;
            public PartPose leftBrow;
            public PartPose rightBrow;
            public PartPose leftMouth;
            public PartPose rightMouth;
            [Tooltip("Açık \"O\" ağzın büyüklüğü (0 = kapalı)")]
            [Range(0f, 1f)] public float mouthOpen;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly PartPose RestPose = new PartPose(0f, 0f, 0f);

        [Header("Parçalar")]
        [SerializeField] Transform leftBrow;
        [SerializeField] Transform rightBrow;
        [Tooltip("Ağzın sol yarısı; pivotu ağzın ortasında")]
        [SerializeField] Transform leftMouth;
        [Tooltip("Ağzın sağ yarısı; pivotu ağzın ortasında")]
        [SerializeField] Transform rightMouth;
        [Tooltip("Şaşırınca görünen açık ağız (prefab'da kapalı durur, script açar)")]
        [SerializeField] Transform openMouth;

        [SerializeField] FaceExpression expression = FaceExpression.Neutral;
        [Tooltip("Geçiş yumuşaklığı (saniye). Küçük = daha sert/hızlı geçiş")]
        [SerializeField, Min(0.01f)] float smoothTime = 0.06f;

        [Header("Görünüm")]
        [SerializeField] Color browColor = new Color(0.05f, 0.04f, 0.035f);
        [SerializeField] Color mouthColor = new Color(0.16f, 0.05f, 0.05f);

        [Header("Kızgınken titreme")]
        [SerializeField, Min(0f)] float angryTremble = 0.0005f;
        [SerializeField, Min(0f)] float trembleSpeed = 22f;

        [SerializeField] ExpressionPose[] poses = DefaultPoses();

        Part leftBrowPart;
        Part rightBrowPart;
        Part leftMouthPart;
        Part rightMouthPart;
        Vector3 openMouthRestScale;
        float currentOpen;
        FaceExpression baseExpression;
        float temporaryTimer;

        public FaceExpression Expression => expression;

        /// <summary>Kalıcı ifadeyi değiştirir.</summary>
        public void SetExpression(FaceExpression newExpression)
        {
            expression = newExpression;
            baseExpression = newExpression;
            temporaryTimer = 0f;
        }

        /// <summary>İfadeyi bir süreliğine gösterir, sonra önceki ifadeye döner.</summary>
        public void PlayExpression(FaceExpression newExpression, float duration)
        {
            if (temporaryTimer <= 0f)
                baseExpression = expression;
            expression = newExpression;
            temporaryTimer = duration;
        }

        void Awake()
        {
            if (leftBrow == null || rightBrow == null || leftMouth == null || rightMouth == null || openMouth == null)
            {
                Debug.LogWarning($"{name}: FaceController parça referansları eksik.", this);
                enabled = false;
                return;
            }

            leftBrowPart = new Part(leftBrow, -1f);
            rightBrowPart = new Part(rightBrow, 1f);
            leftMouthPart = new Part(leftMouth, -1f);
            rightMouthPart = new Part(rightMouth, 1f);
            openMouthRestScale = openMouth.localScale;
            baseExpression = expression;
            ApplyColors();
        }

        void Update()
        {
            if (temporaryTimer > 0f)
            {
                temporaryTimer -= Time.deltaTime;
                if (temporaryTimer <= 0f)
                    expression = baseExpression;
            }
            else
            {
                baseExpression = expression;
            }

            var target = FindPose(expression);
            float blend = 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
            bool angry = expression == FaceExpression.Angry;

            leftBrowPart.Step(target.leftBrow, blend, angry ? Tremble(0f) : 0f);
            rightBrowPart.Step(target.rightBrow, blend, angry ? Tremble(7.3f) : 0f);
            leftMouthPart.Step(target.leftMouth, blend, 0f);
            rightMouthPart.Step(target.rightMouth, blend, 0f);

            currentOpen = Mathf.Lerp(currentOpen, target.mouthOpen, blend);
            openMouth.localScale = openMouthRestScale * currentOpen;
            bool showOpen = currentOpen > 0.01f;
            if (openMouth.gameObject.activeSelf != showOpen)
                openMouth.gameObject.SetActive(showOpen);
        }

        void OnValidate() => ApplyColors();

        float Tremble(float seed) =>
            (Mathf.PerlinNoise(Time.time * trembleSpeed, seed) - 0.5f) * 2f * angryTremble;

        ExpressionPose FindPose(FaceExpression target)
        {
            foreach (var pose in poses)
            {
                if (pose.expression == target)
                    return pose;
            }
            return new ExpressionPose
            {
                expression = target,
                leftBrow = RestPose,
                rightBrow = RestPose,
                leftMouth = RestPose,
                rightMouth = RestPose
            };
        }

        void ApplyColors()
        {
            var block = new MaterialPropertyBlock();
            SetColor(block, browColor, leftBrow, rightBrow);
            SetColor(block, mouthColor, leftMouth, rightMouth, openMouth);
        }

        static void SetColor(MaterialPropertyBlock block, Color color, params Transform[] parts)
        {
            foreach (var part in parts)
            {
                if (part == null || !part.TryGetComponent(out Renderer partRenderer))
                    continue;
                partRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                partRenderer.SetPropertyBlock(block);
            }
        }

        static ExpressionPose Pose(FaceExpression expression, PartPose brow, PartPose mouth, float mouthOpen = 0f) =>
            new ExpressionPose
            {
                expression = expression,
                leftBrow = brow,
                rightBrow = brow,
                leftMouth = mouth,
                rightMouth = mouth,
                mouthOpen = mouthOpen
            };

        static ExpressionPose[] DefaultPoses() => new[]
        {
            Pose(FaceExpression.Neutral, RestPose, RestPose),
            Pose(FaceExpression.Angry,
                new PartPose(-0.004f, 0.003f, 22f, 1.15f),
                new PartPose(0f, 0f, -10f, 1.2f, 0.85f)),
            Pose(FaceExpression.Sad,
                new PartPose(0.002f, 0.002f, -18f),
                new PartPose(0.0015f, 0f, -22f, 1f, 0.9f)),
            Pose(FaceExpression.Surprised,
                new PartPose(0.009f, 0f, -4f, 0.9f),
                new PartPose(0f, 0f, 0f, 0f, 0f),
                mouthOpen: 1f),
            new ExpressionPose
            {
                expression = FaceExpression.Suspicious,
                leftBrow = new PartPose(-0.003f, 0.001f, 12f),
                rightBrow = new PartPose(0.007f, 0f, -6f),
                leftMouth = new PartPose(0f, 0f, -4f, 1f, 0.9f),
                rightMouth = new PartPose(0.001f, 0f, 16f)
            },
            Pose(FaceExpression.Happy,
                new PartPose(0.004f, 0f, -6f),
                new PartPose(-0.001f, 0f, 20f, 1f, 1.1f)),
        };

        /// <summary>Tek bir yüz parçasının dinlenme pozu ve anlık durumu.</summary>
        class Part
        {
            readonly Transform transform;
            readonly Vector3 restPosition;
            readonly Quaternion restRotation;
            readonly Vector3 restScale;
            // +1: karakterin sağı (+x), -1: solu. Ofsetler ve eğim buna göre aynalanır.
            readonly float side;
            PartPose current = RestPose;

            public Part(Transform transform, float side)
            {
                this.transform = transform;
                this.side = side;
                restPosition = transform.localPosition;
                restRotation = transform.localRotation;
                restScale = transform.localScale;
            }

            public void Step(PartPose target, float blend, float tremble)
            {
                current = PartPose.Lerp(current, target, blend);
                transform.localPosition = restPosition + new Vector3(-side * current.inward, current.raise + tremble, 0f);
                transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, side * current.tilt);
                transform.localScale = Vector3.Scale(restScale, new Vector3(current.length, current.thickness, 1f));
            }
        }
    }
}
