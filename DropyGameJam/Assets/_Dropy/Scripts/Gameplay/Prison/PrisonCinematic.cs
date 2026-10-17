using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dropy.Gameplay
{
    /// <summary>Hapishane ara sahnesi: mühendis hücrede oturur, parmaklık kapısı kapanır, altta yazılar belirir.</summary>
    public static class PrisonCinematic
    {
        public const string SceneName = "Hapishane";
        const float CalmBeforeDoor = 1.0f;
        /// <summary>Kapı ~1.2 saniyede çarpar; yazılar ondan hemen sonra gelir.</summary>
        const float CaptionDelay = 1.5f;

        /// <summary>Hapishane müziğindeki vuruş kapının çarptığı ana (~3 sn) denk gelir.</summary>
        public static IEnumerator Play(IList<string> captions) =>
            AdditiveCinematic.Play(SceneName, scene => Run(scene, captions), () => Dropy.Core.MusicDirector.Play(Dropy.Core.MusicCue.Prison, 0f));

        static IEnumerator Run(Scene scene, IList<string> captions)
        {
            var sequence = AdditiveCinematic.Find<CellSequence>(scene);
            var caption = AdditiveCinematic.Find<CinematicCaption>(scene);
            yield return new WaitForSeconds(CalmBeforeDoor);
            if (sequence != null)
            {
                sequence.Begin();
                if (caption != null)
                    caption.StartCoroutine(caption.ShowLines(captions, CaptionDelay));
                while (sequence.IsRunning)
                    yield return null;
            }
        }
    }
}
