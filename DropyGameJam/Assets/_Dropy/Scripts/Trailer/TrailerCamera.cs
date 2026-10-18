using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Dropy.Trailer
{
    /// <summary>
    /// Trailer çekimi için kamera: başlangıç pozundan hedefe (karakterin yüzü, masadaki kâğıt...) yumuşakça yaklaşır.
    /// Üstüne hafif el kamerası titremesi ve Kick() ile darbe sarsıntısı biner.
    /// Kayıtta: R sahneyi baştan başlatır, imleç gizlidir.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class TrailerCamera : MonoBehaviour
    {
        [Header("Hedef")]
        public Transform target;
        [Tooltip("Hedefte humanoid Animator varsa bu kemiğe bakılır (Head = yüz)")]
        public bool useBone = true;
        public HumanBodyBones bone = HumanBodyBones.Head;
        [Tooltip("Bakılan noktaya dünya ekseninde eklenen kayma")]
        public Vector3 targetOffset = new Vector3(0f, 0.04f, 0f);

        [Header("Başlangıç")]
        [Tooltip("Boşsa kameranın sahnedeki yeri")]
        public Transform startPoint;
        [Tooltip("Başta hedefe ne kadar dönük: 0 = başlangıç bakışı, 1 = doğrudan hedefe")]
        [Range(0f, 1f)] public float startLookAt = 1f;

        [Header("Hareket")]
        public float delay = 1f;
        public float duration = 5f;
        [Tooltip("Hareketin sonunda hedefe uzaklık (metre)")]
        public float endDistance = 0.6f;
        [Tooltip("Açık: hedef karakterin yüzünün önüne gelinir. Kapalı: kameranın geldiği yönden yaklaşılır")]
        public bool approachFromFront = true;
        [Tooltip("Son pozda yan/üst kayma (x sağ, y yukarı), hedefin yönüne göre")]
        public Vector2 endShift = new Vector2(0.08f, 0f);
        [Tooltip("Son pozdaki görüş açısı; 0 = değişmez")]
        public float endFov;
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("El kamerası")]
        public float handheld = 0.004f;
        public float handheldRotation = 0.3f;
        public float handheldSpeed = 0.5f;

        [Header("Darbe")]
        public float kickDecay = 7f;
        public float kickOffset = 0.012f;
        public float kickRotation = 1.4f;

        Camera cam;
        Vector3 startPosition;
        Quaternion startRotation;
        float startFov;
        float startTime;
        Transform focusTransform;
        Vector3 focus;
        Vector3 focusVelocity;
        bool focusReady;
        float kick;
        float seed;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (startPoint != null)
                transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
            startPosition = transform.position;
            startRotation = transform.rotation;
            startFov = cam != null ? cam.fieldOfView : 60f;
            startTime = Time.time;
            seed = Random.value * 100f;
            ResolveFocus();
            Cursor.visible = false;
            // OBS gibi bir kayıt programı öndeyken de oyun akmaya devam etsin.
            Application.runInBackground = true;
        }

        void OnDestroy() => Cursor.visible = true;

        void ResolveFocus()
        {
            focusTransform = target;
            if (target != null && useBone)
            {
                var animator = target.GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman && animator.GetBoneTransform(bone) != null)
                    focusTransform = animator.GetBoneTransform(bone);
                else if (bone == HumanBodyBones.Head && FindChild(target, "Head") is Transform head)
                    focusTransform = head;
                else
                    Debug.LogWarning($"{name}: {target.name} üzerinde {bone} kemiği bulunamadı; hedefin kökü kullanılıyor (targetOffset ile yükseltin).", this);
            }
        }

        static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>())
                if (t.name == childName)
                    return t;
            return null;
        }

        /// <summary>Mühür darbesi gibi anlarda kamerayı sarsar (0-1).</summary>
        public void Kick(float strength) => kick = Mathf.Max(kick, strength);

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                Restart();
        }

        /// <summary>Trailer sahneleri Build Settings'te olmadığı için editörde yol üzerinden yeniden yüklenir.</summary>
        static void Restart()
        {
            var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.buildIndex);
#endif
        }

        void LateUpdate()
        {
            if (focusTransform == null)
                return;

            var rawFocus = focusTransform.position + targetOffset;
            if (!focusReady)
            {
                focus = rawFocus;
                focusReady = true;
            }
            // Kafa nefesle/salınımla oynar; kamera onu yumuşak izlesin ki görüntü titremesin.
            focus = Vector3.SmoothDamp(focus, rawFocus, ref focusVelocity, 0.25f);

            float k = ease.Evaluate(Mathf.Clamp01((Time.time - startTime - delay) / Mathf.Max(0.01f, duration)));

            var owner = target != null ? target : focusTransform;
            Vector3 approach = approachFromFront
                ? Flat(owner.forward)
                : (startPosition - focus).normalized;
            var side = Vector3.Cross(Vector3.up, approach).normalized;
            if (side.sqrMagnitude < 0.0001f)
                side = owner.right;
            // approach hedeften kameraya bakar; kameranın sağı -side yönüdür.
            var endPosition = focus + approach * endDistance - side * endShift.x + Vector3.up * endShift.y;

            var position = Vector3.Lerp(startPosition, endPosition, k);
            var startLook = Quaternion.Slerp(startRotation, Quaternion.LookRotation(focus - startPosition), startLookAt);
            var rotation = Quaternion.Slerp(startLook, Quaternion.LookRotation(focus - position), k);

            float t = Time.time * handheldSpeed;
            position += rotation * new Vector3(Noise(0f, t), Noise(1f, t), 0f) * handheld;
            rotation *= Quaternion.Euler(Noise(2f, t) * handheldRotation, Noise(3f, t) * handheldRotation, 0f);

            if (kick > 0.001f)
            {
                float kt = Time.time * 40f;
                position += rotation * new Vector3(Noise(4f, kt), Noise(5f, kt) - 0.6f, 0f) * (kickOffset * kick);
                rotation *= Quaternion.Euler(kick * kickRotation, Noise(6f, kt) * kick * kickRotation * 0.4f, Noise(7f, kt) * kick * kickRotation * 0.3f);
                kick *= Mathf.Exp(-kickDecay * Time.deltaTime);
            }

            transform.SetPositionAndRotation(position, rotation);
            if (cam != null && endFov > 0f)
                cam.fieldOfView = Mathf.Lerp(startFov, endFov, k);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }

        float Noise(float channel, float t) => Mathf.PerlinNoise(seed + channel * 13.7f, t) * 2f - 1f;
    }
}
