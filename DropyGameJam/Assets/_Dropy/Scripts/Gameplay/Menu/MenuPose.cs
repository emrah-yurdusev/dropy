using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Ana menü / fotoğraf sahnesindeki karakteri bir animasyonun istenen karesinde dondurur.
    /// Elleri hedef noktalara (ör. masadaki mühür) uzatır, kafayı bir noktaya çevirir, istenirse
    /// eline bir obje tutturur. Animator'a IK geçişi açık <c>MenuPose.controller</c> verilmelidir.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class MenuPose : MonoBehaviour
    {
        public enum Pose { Idle, SitIdle, Think, Handshake, Walk }

        [SerializeField] Pose pose = Pose.SitIdle;
        [Tooltip("Animasyonun hangi anında durulacağı (0 = baş, 1 = son)")]
        [SerializeField, Range(0f, 1f)] float normalizedTime;
        [Tooltip("Kapalıysa animasyon oynamaya devam eder")]
        [SerializeField] bool freeze = true;

        [Header("Eller (IK)")]
        [SerializeField] Transform rightHandTarget;
        [SerializeField] Transform leftHandTarget;
        [SerializeField, Range(0f, 1f)] float handWeight = 1f;

        [Header("Bakış")]
        [SerializeField] Transform lookTarget;
        [SerializeField, Range(0f, 1f)] float lookWeight = 0.7f;

        [Header("Elde tutulan obje")]
        [Tooltip("Sağ ele bağlanır (ör. mühür)")]
        [SerializeField] Transform heldItem;
        [SerializeField] Vector3 heldLocalPosition;
        [SerializeField] Vector3 heldLocalEuler;

        [Header("Yüz")]
        [SerializeField] FaceExpression expression = FaceExpression.Neutral;

        Animator animator;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            // Kamera dışında kalan karakterler de pozunu korusun.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        void Start()
        {
            animator.Play(pose.ToString(), 0, normalizedTime);
            animator.Update(0f);
            if (freeze)
                animator.speed = 0f;

            if (heldItem != null)
            {
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    heldItem.SetParent(hand, false);
                    heldItem.localPosition = heldLocalPosition;
                    heldItem.localRotation = Quaternion.Euler(heldLocalEuler);
                }
            }

            var face = GetComponentInChildren<FaceController>();
            if (face != null)
                face.SetExpression(expression);
        }

        void OnAnimatorIK(int layerIndex)
        {
            SetHand(AvatarIKGoal.RightHand, rightHandTarget);
            SetHand(AvatarIKGoal.LeftHand, leftHandTarget);
            if (lookTarget != null)
            {
                animator.SetLookAtWeight(lookWeight, 0.2f, 0.8f, 1f, 0.5f);
                animator.SetLookAtPosition(lookTarget.position);
            }
        }

        void SetHand(AvatarIKGoal goal, Transform target)
        {
            float weight = target != null ? handWeight : 0f;
            animator.SetIKPositionWeight(goal, weight);
            animator.SetIKRotationWeight(goal, weight);
            if (target == null)
                return;
            animator.SetIKPosition(goal, target.position);
            animator.SetIKRotation(goal, target.rotation);
        }
    }
}
