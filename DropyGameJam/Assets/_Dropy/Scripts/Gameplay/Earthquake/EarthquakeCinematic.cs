using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Deprem sahnesini o anki oyun sahnesinin üstüne (additive) yükleyip oynatır, bitince kaldırır.
    /// Oynarken ev sahibi sahnenin kamerası, ses dinleyicisi, arayüzü ve güneşi kapatılır; büro ile
    /// deprem sahnesi aynı orijinde kurulu olduğu için deprem sahnesi uzağa kaydırılır.
    /// </summary>
    public static class EarthquakeCinematic
    {
        public const string SceneName = "Deprem";
        /// <summary>Büronun duvarları ve lambaları görüntüye girmesin diye deprem sahnesinin kaydırıldığı yer.</summary>
        static readonly Vector3 Offset = new Vector3(0f, 0f, 5000f);
        const float FadeIn = 0.8f;
        const float CalmBeforeQuake = 1.2f;
        /// <summary>Çöküş animasyonunda bina ~2. saniyede yıkılmaya başlar; manşet onunla gelir.</summary>
        const float CaptionDelay = 2.6f;
        const float HoldAfterQuake = 2f;
        const float FadeOut = 0.8f;

        public static IEnumerator Play(IList<string> captions)
        {
            if (!Application.CanStreamedLevelBeLoaded(SceneName))
            {
                Debug.LogWarning($"EarthquakeCinematic: '{SceneName}' sahnesi Build Settings'te yok, sahne atlandı.");
                yield break;
            }

            var host = SceneManager.GetActiveScene();
            EarthquakeSequence.ExternalControl = true;
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName(SceneName);

            var caption = Find<EarthquakeCaption>(scene);
            caption?.SetBlack();
            foreach (var root in scene.GetRootGameObjects())
                root.transform.position += Offset;
            var hidden = HideHost(host);
            SceneManager.SetActiveScene(scene);

            var sequence = Find<EarthquakeSequence>(scene);
            if (caption != null)
                yield return caption.FadeFromBlack(FadeIn);
            yield return new WaitForSeconds(CalmBeforeQuake);

            if (sequence != null)
            {
                sequence.Begin();
                Sfx.Rumble(sequence.Duration, 1f);
                if (caption != null)
                    caption.StartCoroutine(caption.ShowLines(captions, CaptionDelay));
                while (sequence.IsRunning)
                    yield return null;
            }
            yield return new WaitForSeconds(HoldAfterQuake);
            if (caption != null)
                yield return caption.FadeToBlack(FadeOut);

            SceneManager.SetActiveScene(host);
            foreach (var b in hidden)
                if (b != null)
                    b.enabled = true;
            yield return SceneManager.UnloadSceneAsync(scene);
            EarthquakeSequence.ExternalControl = false;
        }

        /// <summary>Ev sahibi sahnede açık olan kamera, ses dinleyicisi, Canvas ve yönlü ışıkları kapatır; geri açmak için listeyi döner.</summary>
        static List<Behaviour> HideHost(Scene host)
        {
            var hidden = new List<Behaviour>();
            foreach (var root in host.GetRootGameObjects())
            {
                var behaviours = root.GetComponentsInChildren<Camera>().Cast<Behaviour>()
                    .Concat(root.GetComponentsInChildren<AudioListener>())
                    .Concat(root.GetComponentsInChildren<Canvas>())
                    .Concat(root.GetComponentsInChildren<Light>().Where(l => l.type == LightType.Directional));
                foreach (var b in behaviours)
                {
                    if (!b.enabled)
                        continue;
                    b.enabled = false;
                    hidden.Add(b);
                }
            }
            return hidden;
        }

        static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);
    }
}
