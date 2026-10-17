using UnityEngine;

namespace Dropy.Core
{
    /// <summary>Oyunda müziğin değiştiği anlar.</summary>
    public enum MusicCue
    {
        None,
        Menu,
        OfficeCalm,
        OfficeTense,
        Evening,
        Earthquake,
        Prison,
        Final
    }

    /// <summary>
    /// Hangi anda hangi parça çalacağı. Resources/MusicLibrary olarak durur; MusicDirector oradan yükler.
    /// Parçalar koddan sentezlendi (Audio/Music); değiştirmek için klibi burada değiştirmek yeterli.
    /// </summary>
    [CreateAssetMenu(menuName = "Dropy/Müzik Kütüphanesi", fileName = "MusicLibrary")]
    public class MusicLibrary : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            public MusicCue cue;
            public AudioClip clip;
            [Tooltip("Döngü mü, tek seferlik mi (deprem/hapishane ara sahneleri tek seferlik)")]
            public bool loop = true;
            [Range(0f, 1f)] public float volume = 0.8f;
        }

        public Entry[] entries;

        public Entry Find(MusicCue cue)
        {
            if (entries == null)
                return null;
            foreach (var e in entries)
                if (e != null && e.cue == cue && e.clip != null)
                    return e;
            return null;
        }
    }
}
