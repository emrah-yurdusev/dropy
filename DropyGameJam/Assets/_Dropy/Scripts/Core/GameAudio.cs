using UnityEngine;

namespace Dropy.Core
{
    /// <summary>
    /// Müzik ve ses efekti ayarları (seviye 0-1 + sessize alma), PlayerPrefs'te saklanır.
    /// Müzik çalan kaynaklar MusicVolume'u, efekt çalanlar SfxVolume'u kullanmalı ve
    /// ayar değişince Changed olayıyla güncellenmelidir.
    /// </summary>
    public static class GameAudio
    {
        const string MusicKey = "audio.music";
        const string SfxKey = "audio.sfx";
        const string MusicMutedKey = "audio.music.muted";
        const string SfxMutedKey = "audio.sfx.muted";

        public static event System.Action Changed;

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
            set => SetFloat(MusicKey, value);
        }

        public static float Sfx
        {
            get => PlayerPrefs.GetFloat(SfxKey, 1f);
            set => SetFloat(SfxKey, value);
        }

        public static bool MusicMuted
        {
            get => PlayerPrefs.GetInt(MusicMutedKey, 0) == 1;
            set => SetBool(MusicMutedKey, value);
        }

        public static bool SfxMuted
        {
            get => PlayerPrefs.GetInt(SfxMutedKey, 0) == 1;
            set => SetBool(SfxMutedKey, value);
        }

        /// <summary>Sessize alma hesaba katılmış müzik seviyesi.</summary>
        public static float MusicVolume => MusicMuted ? 0f : Music;

        /// <summary>Sessize alma hesaba katılmış efekt seviyesi.</summary>
        public static float SfxVolume => SfxMuted ? 0f : Sfx;

        public static void Save() => PlayerPrefs.Save();

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, Mathf.Clamp01(value));
            Changed?.Invoke();
        }

        static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            Changed?.Invoke();
        }
    }
}
