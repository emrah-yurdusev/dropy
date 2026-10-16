using UnityEngine;

namespace Dropy.Gameplay
{
    public enum BrowExpression
    {
        Neutral,
        Angry,
        Sad,
        Surprised,
        Suspicious,
        Happy
    }

    /// <summary>
    /// Karakterin kaşlarını ifadeye göre hareket ettirir.
    /// Kaşlar Head kemiğinin altında durur; prefab'daki duruşları "dinlenme pozu" sayılır,
    /// ifadeler bu pozun üzerine ofset olarak uygulanır.
    /// Kullanım: GetComponentInChildren&lt;EyebrowController&gt;().SetExpression(BrowExpression.Angry);
    /// </summary>
    public class EyebrowController : MonoBehaviour
    {
        [System.Serializable]
        public struct BrowPose
        {
            [Tooltip("Yukarı (+) / aşağı (-) kayma, metre")]
            public float raise;
            [Tooltip("Burna doğru kayma, metre")]
            public float inward;
            [Tooltip("Derece. + iç uç aşağı (kızgın), - iç uç yukarı (üzgün)")]
            public float tilt;
            [Tooltip("Kalınlık çarpanı")]
            public float thickness;

            public BrowPose(float raise, float inward, float tilt, float thickness = 1f)
            {
                this.raise = raise;
                this.inward = inward;
                this.tilt = tilt;
                this.thickness = thickness;
            }

            public static BrowPose Lerp(BrowPose a, BrowPose b, float t) => new BrowPose(
                Mathf.Lerp(a.raise, b.raise, t),
                Mathf.Lerp(a.inward, b.inward, t),
                Mathf.Lerp(a.tilt, b.tilt, t),
                Mathf.Lerp(a.thickness, b.thickness, t));
        }

        [System.Serializable]
        public struct ExpressionPose
        {
            public BrowExpression expression;
            public BrowPose left;
            public BrowPose right;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly BrowPose RestPose = new BrowPose(0f, 0f, 0f);

        [SerializeField] Transform leftBrow;
        [SerializeField] Transform rightBrow;

        [SerializeField] BrowExpression expression = BrowExpression.Neutral;
        [Tooltip("Geçiş yumuşaklığı (saniye). Küçük = daha sert/hızlı geçiş")]
        [SerializeField, Min(0.01f)] float smoothTime = 0.06f;

        [Header("Görünüm")]
        [SerializeField] Color browColor = new Color(0.05f, 0.04f, 0.035f);

        [Header("Kızgınken titreme")]
        [SerializeField, Min(0f)] float angryTremble = 0.0005f;
        [SerializeField, Min(0f)] float trembleSpeed = 22f;

        [SerializeField] ExpressionPose[] poses = DefaultPoses();

        Brow left;
        Brow right;
        BrowExpression baseExpression;
        float temporaryTimer;

        public BrowExpression Expression => expression;

        /// <summary>Kalıcı ifadeyi değiştirir.</summary>
        public void SetExpression(BrowExpression newExpression)
        {
            expression = newExpression;
            baseExpression = newExpression;
            temporaryTimer = 0f;
        }

        /// <summary>İfadeyi bir süreliğine gösterir, sonra önceki ifadeye döner.</summary>
        public void PlayExpression(BrowExpression newExpression, float duration)
        {
            if (temporaryTimer <= 0f)
                baseExpression = expression;
            expression = newExpression;
            temporaryTimer = duration;
        }

        void Awake()
        {
            if (leftBrow == null || rightBrow == null)
            {
                Debug.LogWarning($"{name}: EyebrowController kaş referansları eksik.", this);
                enabled = false;
                return;
            }

            left = new Brow(leftBrow);
            right = new Brow(rightBrow);
            baseExpression = expression;
            ApplyColor();
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
            bool angry = expression == BrowExpression.Angry;

            left.Step(target.left, blend, angry ? Tremble(0f) : 0f);
            right.Step(target.right, blend, angry ? Tremble(7.3f) : 0f);
        }

        void OnValidate() => ApplyColor();

        float Tremble(float seed) =>
            (Mathf.PerlinNoise(Time.time * trembleSpeed, seed) - 0.5f) * 2f * angryTremble;

        ExpressionPose FindPose(BrowExpression target)
        {
            foreach (var pose in poses)
            {
                if (pose.expression == target)
                    return pose;
            }
            return new ExpressionPose { expression = target, left = RestPose, right = RestPose };
        }

        void ApplyColor()
        {
            var block = new MaterialPropertyBlock();
            foreach (var brow in new[] { leftBrow, rightBrow })
            {
                if (brow == null || !brow.TryGetComponent(out Renderer browRenderer))
                    continue;
                browRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, browColor);
                browRenderer.SetPropertyBlock(block);
            }
        }

        static ExpressionPose Symmetric(BrowExpression expression, BrowPose pose) =>
            new ExpressionPose { expression = expression, left = pose, right = pose };

        static ExpressionPose[] DefaultPoses() => new[]
        {
            Symmetric(BrowExpression.Neutral, RestPose),
            Symmetric(BrowExpression.Angry, new BrowPose(-0.004f, 0.003f, 22f, 1.15f)),
            Symmetric(BrowExpression.Sad, new BrowPose(0.002f, 0.002f, -18f)),
            Symmetric(BrowExpression.Surprised, new BrowPose(0.009f, 0f, -4f, 0.9f)),
            new ExpressionPose
            {
                expression = BrowExpression.Suspicious,
                left = new BrowPose(-0.003f, 0.001f, 12f),
                right = new BrowPose(0.007f, 0f, -6f)
            },
            Symmetric(BrowExpression.Happy, new BrowPose(0.004f, 0f, -6f)),
        };

        /// <summary>Tek bir kaşın dinlenme pozu ve anlık durumu.</summary>
        class Brow
        {
            readonly Transform transform;
            readonly Vector3 restPosition;
            readonly Quaternion restRotation;
            readonly Vector3 restScale;
            // +1: karakterin sağı (+x), -1: solu. Ofsetler ve eğim buna göre aynalanır.
            readonly float side;
            BrowPose current = RestPose;

            public Brow(Transform transform)
            {
                this.transform = transform;
                restPosition = transform.localPosition;
                restRotation = transform.localRotation;
                restScale = transform.localScale;
                side = Mathf.Sign(restPosition.x);
            }

            public void Step(BrowPose target, float blend, float tremble)
            {
                current = BrowPose.Lerp(current, target, blend);
                transform.localPosition = restPosition + new Vector3(-side * current.inward, current.raise + tremble, 0f);
                transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, side * current.tilt);
                transform.localScale = Vector3.Scale(restScale, new Vector3(1f, current.thickness, 1f));
            }
        }
    }
}
