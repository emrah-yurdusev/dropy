using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.Gameplay
{
    /// <summary>Deprem ara sahnesi: binalar sallanır, yıkılacak olan çöker, altta manşetler belirir.</summary>
    public static class EarthquakeCinematic
    {
        public const string SceneName = "Deprem";
        const float CalmBeforeQuake = 1.2f;
        /// <summary>Çöküş animasyonunda bina ~2. saniyede yıkılmaya başlar; manşet onunla gelir.</summary>
        const float CaptionDelay = 2.6f;
        const float HoldAfterQuake = 2f;

        public static IEnumerator Play(IList<string> captions) =>
            AdditiveCinematic.Play(SceneName, scene => Run(scene, captions));

        static IEnumerator Run(Scene scene, IList<string> captions)
        {
            var sequence = AdditiveCinematic.Find<EarthquakeSequence>(scene);
            var caption = AdditiveCinematic.Find<CinematicCaption>(scene);
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
        }
    }
}
