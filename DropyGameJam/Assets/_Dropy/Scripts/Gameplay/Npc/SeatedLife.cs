using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oturan NPC'yi canlı tutar. SitIdle sabit bir pozdur (FBX'teki kısa döngünün başı ve sonu tutmadığı için
    /// her turda kafa geri sıçrıyordu); nefes ve bakış burada, animasyonun üstüne koddan eklenir:
    /// göğüs yavaşça inip kalkar, kafa oyuncuya döner ve hafifçe salınır.
    /// </summary>
    [RequireComponent(typeof(NpcController))]
    public class SeatedLife : MonoBehaviour
    {
        static readonly int SitIdleState = Animator.StringToHash("SitIdle");

        [Tooltip("Kafanın döneceği nokta; boşsa yalnızca salınır")]
        public Transform lookTarget;
        [SerializeField] float breathPeriod = 3.8f;
        [SerializeField] float breathAngle = 1.4f;
        [Range(0f, 1f)]
        [SerializeField] float lookWeight = 0.7f;
        [SerializeField] float maxYaw = 35f;
        [SerializeField] float maxPitch = 20f;
        [Tooltip("Kafanın kendi kendine hafif salınımı (derece)")]
        [SerializeField] float swayAngle = 2.5f;
        [Tooltip("Pozdan pozla geçişte kemiklerin yumuşak bağlanma süresi")]
        [SerializeField] float blendTime = 0.6f;

        NpcController npc;
        Animator animator;
        Transform chest;
        Transform head;
        float weight;
        float yaw, pitch, yawVelocity, pitchVelocity;
        float seed;

        void Awake()
        {
            npc = GetComponent<NpcController>();
            animator = GetComponentInChildren<Animator>();
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                if (t.name == "Chest") chest = t;
                else if (t.name == "Head") head = t;
            }
            seed = Random.value * 100f;
        }

        void LateUpdate()
        {
            if (head == null || animator == null)
                return;

            bool seated = npc.IsSitting
                && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == SitIdleState
                && !animator.IsInTransition(0);
            weight = Mathf.MoveTowards(weight, seated ? 1f : 0f, Time.deltaTime / blendTime);
            if (weight <= 0f)
                return;

            float t = Time.time;
            if (chest != null)
            {
                float breath = Mathf.Sin(t * 2f * Mathf.PI / breathPeriod) * breathAngle * weight;
                chest.Rotate(transform.right, breath, Space.World);
            }

            float targetYaw = 0f, targetPitch = 0f;
            if (lookTarget != null)
            {
                var local = Quaternion.Inverse(transform.rotation) * (lookTarget.position - head.position).normalized;
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxYaw, maxYaw) * lookWeight;
                targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -maxPitch, maxPitch) * lookWeight;
            }
            targetYaw += (Mathf.PerlinNoise(seed, t * 0.15f) - 0.5f) * 2f * swayAngle;
            targetPitch += (Mathf.PerlinNoise(seed + 7f, t * 0.12f) - 0.5f) * 2f * swayAngle * 0.6f;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVelocity, 0.35f);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, 0.35f);

            head.rotation = Quaternion.AngleAxis(yaw * weight, Vector3.up)
                * Quaternion.AngleAxis(pitch * weight, transform.right)
                * head.rotation;
        }
    }
}
