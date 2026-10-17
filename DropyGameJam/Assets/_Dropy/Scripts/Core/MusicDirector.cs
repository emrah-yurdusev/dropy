using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.Core
{
    /// <summary>
    /// Oyun müziği: iki AudioSource arasında yumuşak geçişle çalar, sahne değişince kaybolmaz.
    /// İlk kullanımda kendini kurar (sahneye eklemeye gerek yok). Ses seviyesi GameAudio.MusicVolume'u izler.
    /// <code>MusicDirector.Play(MusicCue.Evening);</code>
    /// Tek sahneli yükleme (yeniden başlat, sahne değiştir) müziği susturur; ara sahneler (additive) susturmaz.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        const string LibraryPath = "MusicLibrary";
        const float DefaultFade = 1.5f;

        static MusicDirector instance;

        readonly AudioSource[] sources = new AudioSource[2];
        readonly float[] entryVolume = new float[2];
        readonly float[] fade = new float[2];
        MusicLibrary library;
        int active;
        Coroutine fading;

        public static MusicCue Current { get; private set; }

        static MusicDirector Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("MusicDirector");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<MusicDirector>();
                }
                return instance;
            }
        }

        /// <summary>Parçaya geçer. Aynı parça zaten çalıyorsa bir şey yapmaz.</summary>
        public static void Play(MusicCue cue, float fadeSeconds = DefaultFade) => Instance.PlayCue(cue, fadeSeconds);

        /// <summary>Müziği yavaşça susturur.</summary>
        public static void Stop(float fadeSeconds = DefaultFade) => Instance.PlayCue(MusicCue.None, fadeSeconds);

        void Awake()
        {
            library = Resources.Load<MusicLibrary>(LibraryPath);
            if (library == null)
                Debug.LogWarning("MusicDirector: Resources/MusicLibrary bulunamadı, müzik çalmayacak.");
            for (int i = 0; i < 2; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
                sources[i].priority = 0;
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (instance == this)
                instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single)
                PlayCue(MusicCue.None, 0.4f);
        }

        void Update()
        {
            float master = GameAudio.MusicVolume;
            for (int i = 0; i < 2; i++)
                sources[i].volume = entryVolume[i] * fade[i] * master;
        }

        void PlayCue(MusicCue cue, float fadeSeconds)
        {
            var entry = library != null ? library.Find(cue) : null;
            if (cue == Current && (cue == MusicCue.None || sources[active].isPlaying))
                return;
            Current = entry != null ? cue : MusicCue.None;

            int next = 1 - active;
            if (entry != null)
            {
                var src = sources[next];
                src.Stop();
                src.clip = entry.clip;
                src.loop = entry.loop;
                entryVolume[next] = entry.volume;
                fade[next] = fadeSeconds > 0f ? 0f : 1f;
                src.Play();
            }
            int previous = active;
            if (entry != null)
                active = next;
            if (fading != null)
                StopCoroutine(fading);
            fading = StartCoroutine(Crossfade(entry != null ? active : -1, previous, fadeSeconds));
        }

        IEnumerator Crossfade(int fadeIn, int fadeOut, float seconds)
        {
            int other = 1 - fadeOut;
            float startOut = fade[fadeOut], startIn = fadeIn >= 0 ? fade[fadeIn] : 0f;
            float startOther = fadeIn < 0 ? fade[other] : 0f;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                fade[fadeOut] = Mathf.Lerp(startOut, 0f, k);
                if (fadeIn >= 0)
                    fade[fadeIn] = Mathf.Lerp(startIn, 1f, k);
                else
                    fade[other] = Mathf.Lerp(startOther, 0f, k);
                yield return null;
            }
            fade[fadeOut] = 0f;
            sources[fadeOut].Stop();
            if (fadeIn >= 0)
                fade[fadeIn] = 1f;
            else
            {
                fade[other] = 0f;
                sources[other].Stop();
            }
            fading = null;
        }
    }
}
