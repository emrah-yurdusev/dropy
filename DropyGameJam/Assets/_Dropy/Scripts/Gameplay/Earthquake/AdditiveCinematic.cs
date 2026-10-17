using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Bir ara sahneyi o anki oyun sahnesinin üstüne (additive) yükleyip oynatır, bitince kaldırır.
    /// Oynarken ev sahibi sahnenin kamerası, ses dinleyicisi, arayüzü ve güneşi kapatılır; büro ile ara sahneler
    /// aynı orijinde kurulu olduğu için ara sahne uzağa kaydırılır. Sahnedeki CinematicCaption siyahtan açar/kapatır.
    /// </summary>
    public static class AdditiveCinematic
    {
        /// <summary>Büronun duvarları ve lambaları görüntüye girmesin diye ara sahnenin kaydırıldığı yer.</summary>
        static readonly Vector3 Offset = new Vector3(0f, 0f, 5000f);
        const float FadeIn = 0.8f;
        const float FadeOut = 0.8f;

        /// <summary>
        /// Oyun akışı bir ara sahneyi kendisi yönetirken true; o sırada sahnelerdeki autoStart yok sayılır.
        /// Sahne tek başına açıldığında false kalır.
        /// </summary>
        public static bool IsPlaying { get; private set; }

        /// <param name="body">Sahne yüklenip siyahtan açıldıktan sonra çalışan akış (sekansı başlat, bitmesini bekle).</param>
        public static IEnumerator Play(string sceneName, Func<Scene, IEnumerator> body)
        {
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"AdditiveCinematic: '{sceneName}' sahnesi Build Settings'te yok, sahne atlandı.");
                yield break;
            }

            var host = SceneManager.GetActiveScene();
            IsPlaying = true;
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByName(sceneName);

            var caption = Find<CinematicCaption>(scene);
            caption?.SetBlack();
            foreach (var root in scene.GetRootGameObjects())
                root.transform.position += Offset;
            var hidden = HideHost(host);
            SceneManager.SetActiveScene(scene);

            if (caption != null)
                yield return caption.FadeFromBlack(FadeIn);
            yield return body(scene);
            if (caption != null)
                yield return caption.FadeToBlack(FadeOut);

            SceneManager.SetActiveScene(host);
            foreach (var b in hidden)
                if (b != null)
                    b.enabled = true;
            yield return SceneManager.UnloadSceneAsync(scene);
            IsPlaying = false;
        }

        public static T Find<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<T>(true)).FirstOrDefault(c => c != null);

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
    }
}
