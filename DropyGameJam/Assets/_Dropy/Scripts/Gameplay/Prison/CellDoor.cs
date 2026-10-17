using System.Collections;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Hücrenin sürgülü parmaklık kapısı: Blender'da hazırlanan kapanma animasyonunu (alt objedeki Legacy Animation)
    /// oynatır, kapı direğe çarptığı anda tok bir ses çıkarır. Başta açık durur.
    /// </summary>
    public class CellDoor : MonoBehaviour
    {
        [Tooltip("Animasyonda kapının direğe çarptığı an (saniye)")]
        [SerializeField, Min(0f)] float slamTime = 1.23f;

        Animation anim;

        public bool IsClosing { get; private set; }

        public float Duration => Anim != null && Anim.clip != null ? Anim.clip.length : 0f;

        Animation Anim
        {
            get
            {
                if (anim == null)
                    anim = GetComponentInChildren<Animation>(true);
                return anim;
            }
        }

        void Awake()
        {
            if (Anim == null)
            {
                Debug.LogWarning($"{name}: alt objelerde Animation bileşeni yok.", this);
                return;
            }
            Anim.playAutomatically = false;
            Anim.cullingType = AnimationCullingType.AlwaysAnimate;
            SetOpen();
        }

        /// <summary>Kapıyı açık haline (animasyonun ilk karesi) getirir.</summary>
        [ContextMenu("Aç")]
        public void SetOpen()
        {
            var state = Anim != null && Anim.clip != null ? Anim[Anim.clip.name] : null;
            if (state == null)
                return;
            Anim.Stop();
            state.enabled = true;
            state.weight = 1f;
            state.time = 0f;
            Anim.Sample();
            state.enabled = false;
        }

        /// <summary>Kapıyı kapatır; animasyon bitince IsClosing false olur.</summary>
        [ContextMenu("Kapat")]
        public void Close()
        {
            if (Anim == null || IsClosing)
                return;
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            IsClosing = true;
            Anim.Stop();
            Anim.Play();
            yield return new WaitForSeconds(slamTime);
            Sfx.Stamp();
            yield return new WaitForSeconds(Mathf.Max(0f, Duration - slamTime));
            IsClosing = false;
        }
    }
}
