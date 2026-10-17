using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Blender'da bake edilmiş çöküş animasyonunu (alt objedeki Legacy Animation) oynatan bina.
    /// WillCollapse kapalıysa bina yıkılmaz, EarthquakeSequence onu diğer binalar gibi sadece sallar.
    /// </summary>
    public class CollapsibleBuilding : MonoBehaviour
    {
        [Tooltip("Depremde bu bina yıkılsın mı? (ör. denetimden rüşvetle geçtiyse)")]
        [SerializeField] bool willCollapse = true;
        [Tooltip("Boş bırakılırsa Animation bileşeninin varsayılan klibi kullanılır")]
        [SerializeField] string clipName = "";

        Animation anim;

        public bool WillCollapse
        {
            get => willCollapse;
            set => willCollapse = value;
        }

        public bool IsCollapsing => Anim != null && Anim.isPlaying;

        Animation Anim
        {
            get
            {
                if (anim == null)
                    anim = GetComponentInChildren<Animation>(true);
                return anim;
            }
        }

        string ClipName => string.IsNullOrEmpty(clipName) && Anim != null && Anim.clip != null ? Anim.clip.name : clipName;

        void Awake()
        {
            if (Anim == null)
            {
                Debug.LogWarning($"{name}: alt objelerde Animation bileşeni yok.", this);
                return;
            }
            Anim.playAutomatically = false;
            Anim.cullingType = AnimationCullingType.AlwaysAnimate;
            ResetToIntact();
        }

        /// <summary>Çöküş animasyonunu baştan oynatır (animasyonun ilk 2 saniyesi binanın sallanmasıdır).</summary>
        [ContextMenu("Çöktür")]
        public void Collapse()
        {
            if (Anim == null)
                return;
            Anim.Stop();
            Anim.Play(ClipName);
        }

        /// <summary>Binayı animasyonun ilk karesine, yani sağlam haline döndürür.</summary>
        [ContextMenu("Sağlam Hale Getir")]
        public void ResetToIntact()
        {
            var state = Anim != null ? Anim[ClipName] : null;
            if (state == null)
            {
                Debug.LogWarning($"{name}: '{ClipName}' animasyonu bulunamadı.", this);
                return;
            }
            Anim.Stop();
            state.enabled = true;
            state.weight = 1f;
            state.time = 0f;
            Anim.Sample();
            state.enabled = false;
        }
    }
}
