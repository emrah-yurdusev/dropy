using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Dropy.Gameplay
{
    /// <summary>
    /// NavMesh üzerinde yürüyen, kapı açan, sandalyeye oturan NPC.
    /// Inspector'daki "Routine" listesi sırayla oynatılır; koddan da tek tek çağrılabilir:
    /// <code>StartCoroutine(npc.SitOn(chair));</code>
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class NpcController : MonoBehaviour
    {
        public enum ActionType
        {
            MoveTo,
            OpenDoor,
            SitOn,
            StandUp,
            Wait,
            Handshake,
            Think,
            Expression,
            LookAt
        }

        [System.Serializable]
        public struct NpcAction
        {
            public ActionType type;
            [Tooltip("MoveTo / LookAt / Handshake için hedef (Handshake'te karşıdaki kişi, boş olabilir)")]
            public Transform target;
            public Door door;
            public Chair chair;
            [Tooltip("Wait süresi (saniye)")]
            public float duration;
            public FaceExpression expression;
        }

        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int WalkSpeedId = Animator.StringToHash("WalkSpeed");
        static readonly int SittingId = Animator.StringToHash("Sitting");
        static readonly int HandshakeId = Animator.StringToHash("Handshake");
        static readonly int ThinkId = Animator.StringToHash("Think");
        static readonly int GestureTag = Animator.StringToHash("Gesture");
        static readonly int SitTag = Animator.StringToHash("Sit");

        [SerializeField] Animator animator;
        [SerializeField] FaceController face;

        [Header("Hareket")]
        [SerializeField, Min(0.1f)] float walkSpeed = 0.85f;
        [Tooltip("Walk animasyonu 1x hızdayken karakterin kat ettiği mesafe (m/sn). Klipteki basan ayağın hızından ölçüldü; ayak kayıyorsa bunu ayarlayın")]
        [SerializeField, Min(0.05f)] float walkAnimSpeed = 0.31f;
        [Tooltip("Yerinde dönme hızı (derece/saniye)")]
        [SerializeField, Min(1f)] float turnSpeed = 360f;
        [Tooltip("Oturmadan önce Sit Point'e kayarak hizalanma süresi")]
        [SerializeField, Min(0f)] float sitAlignTime = 0.3f;

        [Header("Rutin")]
        [SerializeField] bool playRoutineOnStart = true;
        [SerializeField] bool loopRoutine;
        [SerializeField] NpcAction[] routine;

        NavMeshAgent agent;
        Chair currentChair;
        Coroutine routineCoroutine;

        public bool IsSitting => currentChair != null;
        public bool IsRunningRoutine => routineCoroutine != null;
        public FaceController Face => face;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = walkSpeed;
            agent.updateRotation = true;
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (face == null)
                face = GetComponentInChildren<FaceController>();
            animator.applyRootMotion = false;
        }

        void Start()
        {
            if (playRoutineOnStart && routine != null && routine.Length > 0)
                PlayRoutine();
        }

        void Update()
        {
            float speed = agent.enabled ? agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedId, speed);
            animator.SetFloat(WalkSpeedId, Mathf.Max(0.5f, speed / walkAnimSpeed));
        }

        /// <summary>Yürüme hızını Inspector'daki değerin katı yapar; animasyon hıza kendisi uyar.</summary>
        public void SetWalkSpeedMultiplier(float multiplier)
        {
            agent.speed = walkSpeed * multiplier;
        }

        /// <summary>Inspector'daki rutini (baştan) oynatır.</summary>
        public void PlayRoutine()
        {
            StopRoutine();
            routineCoroutine = StartCoroutine(RunRoutine());
        }

        public void StopRoutine()
        {
            if (routineCoroutine != null)
                StopCoroutine(routineCoroutine);
            routineCoroutine = null;
            if (agent.enabled && agent.isOnNavMesh)
                agent.ResetPath();
        }

        IEnumerator RunRoutine()
        {
            do
            {
                foreach (var action in routine)
                    yield return Run(action);
            }
            while (loopRoutine);
            routineCoroutine = null;
        }

        IEnumerator Run(NpcAction action)
        {
            switch (action.type)
            {
                case ActionType.MoveTo:
                    if (action.target != null)
                        yield return MoveTo(action.target.position);
                    break;
                case ActionType.OpenDoor:
                    if (action.door != null)
                        yield return WalkThrough(action.door);
                    break;
                case ActionType.SitOn:
                    if (action.chair != null)
                        yield return SitOn(action.chair);
                    break;
                case ActionType.StandUp:
                    yield return StandUp();
                    break;
                case ActionType.Wait:
                    yield return new WaitForSeconds(action.duration);
                    break;
                case ActionType.Handshake:
                    yield return Handshake(action.target);
                    break;
                case ActionType.Think:
                    yield return PlayGesture(ThinkId);
                    break;
                case ActionType.Expression:
                    if (face != null)
                        face.SetExpression(action.expression);
                    break;
                case ActionType.LookAt:
                    if (action.target != null)
                        yield return TurnTowards(action.target.position);
                    break;
            }
        }

        /// <summary>NavMesh üzerinden hedefe yürür; oturuyorsa önce kalkar.</summary>
        public IEnumerator MoveTo(Vector3 destination)
        {
            if (IsSitting)
                yield return StandUp();

            agent.isStopped = false;
            if (!agent.SetDestination(destination))
            {
                Debug.LogWarning($"{name}: {destination} noktasına yol bulunamadı.", this);
                yield break;
            }
            while (agent.pathPending)
                yield return null;
            while (agent.remainingDistance > agent.stoppingDistance + 0.05f)
            {
                if (agent.pathStatus == NavMeshPathStatus.PathInvalid)
                    break;
                yield return null;
            }
            agent.ResetPath();
        }

        /// <summary>Kapıya yürür, açar, karşı tarafa geçer ve (kapı ayarına göre) arkasından kapatır.</summary>
        public IEnumerator WalkThrough(Door door)
        {
            yield return MoveTo(door.GetApproachPoint(transform.position, otherSide: false));
            yield return TurnTowards(door.Center);
            door.Open(transform.position);
            while (!door.IsOpen)
                yield return null;
            yield return MoveTo(door.GetApproachPoint(transform.position, otherSide: true));
            if (door.CloseBehind)
                door.Close();
        }

        /// <summary>Sandalyeye yürür, arkasını döner ve oturur.</summary>
        public IEnumerator SitOn(Chair chair)
        {
            if (!chair.TryOccupy(this))
            {
                Debug.LogWarning($"{name}: {chair.name} dolu.", chair);
                yield break;
            }

            var sitPoint = chair.SitPoint;
            yield return MoveTo(sitPoint.position);

            // NavMesh'ten çıkıp Sit Point'e tam hizalan.
            agent.enabled = false;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;
            for (float t = 0f; t < sitAlignTime; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / sitAlignTime);
                transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, sitPoint.position, k),
                    Quaternion.Slerp(startRot, sitPoint.rotation, k));
                yield return null;
            }
            transform.SetPositionAndRotation(sitPoint.position, sitPoint.rotation);

            currentChair = chair;
            animator.SetBool(SittingId, true);
            yield return WaitForState(SitTag);
            while (animator.GetCurrentAnimatorStateInfo(0).IsName("SitDown"))
                yield return null;
        }

        /// <summary>Oturuyorsa ayağa kalkar ve NavMesh'e geri döner.</summary>
        public IEnumerator StandUp()
        {
            if (!IsSitting)
                yield break;

            animator.SetBool(SittingId, false);
            yield return WaitForStateExit(SitTag);

            currentChair.Release(this);
            currentChair = null;
            agent.enabled = true;
            if (NavMesh.SamplePosition(transform.position, out var hit, 1f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        /// <summary>Yerinde bir noktaya doğru döner.</summary>
        public IEnumerator TurnTowards(Vector3 point)
        {
            var direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
                yield break;

            var target = Quaternion.LookRotation(direction);
            while (Quaternion.Angle(transform.rotation, target) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
                yield return null;
            }
            transform.rotation = target;
        }

        /// <summary>
        /// Karşıdakine döner ve el sıkışır. Karşıdaki de bir NPC ise (ve oturmuyorsa) o da dönüp el sıkışır.
        /// </summary>
        public IEnumerator Handshake(Transform other)
        {
            if (other != null)
            {
                if (other.TryGetComponent(out NpcController partner) && !partner.IsSitting)
                    partner.StartCoroutine(partner.ReceiveHandshake(transform));
                yield return TurnTowards(other.position);
            }
            yield return PlayGesture(HandshakeId);
        }

        IEnumerator ReceiveHandshake(Transform from)
        {
            yield return TurnTowards(from.position);
            yield return PlayGesture(HandshakeId);
        }

        public IEnumerator Think() => PlayGesture(ThinkId);

        IEnumerator PlayGesture(int trigger)
        {
            if (IsSitting)
                yield return StandUp();
            animator.SetTrigger(trigger);
            yield return WaitForState(GestureTag);
            yield return WaitForStateExit(GestureTag);
        }

        IEnumerator WaitForState(int tag)
        {
            while (animator.GetCurrentAnimatorStateInfo(0).tagHash != tag)
                yield return null;
        }

        IEnumerator WaitForStateExit(int tag)
        {
            while (animator.GetCurrentAnimatorStateInfo(0).tagHash == tag || animator.IsInTransition(0))
                yield return null;
        }
    }
}
