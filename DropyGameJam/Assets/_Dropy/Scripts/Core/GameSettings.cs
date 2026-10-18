using UnityEngine;

namespace Dropy.Core
{
    /// <summary>
    /// Ses dışındaki oyuncu ayarları (yazı hızı, tam ekran, ekran sarsıntısı, kalem öğretici), PlayerPrefs'te saklanır.
    /// Ana menüdeki ve duraklatma menüsündeki ayarlar kartı (<see cref="Dropy.Menu.SettingsPanel"/>) bunları değiştirir.
    /// </summary>
    public static class GameSettings
    {
        const string TextSpeedKey = "settings.textSpeed";
        const string ShakeKey = "settings.shake";
        const string PenKey = "settings.penTutorial";

        public static readonly string[] TextSpeedNames = { "YAVAŞ", "NORMAL", "HIZLI" };
        static readonly float[] TextSpeedMultipliers = { 0.55f, 1f, 1.8f };

        public static event System.Action Changed;

        /// <summary>0 yavaş, 1 normal, 2 hızlı.</summary>
        public static int TextSpeed
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(TextSpeedKey, 1), 0, TextSpeedNames.Length - 1);
            set => SetInt(TextSpeedKey, Mathf.Clamp(value, 0, TextSpeedNames.Length - 1));
        }

        /// <summary>Konuşma balonlarının yazılma hızına çarpan.</summary>
        public static float TextSpeedMultiplier => TextSpeedMultipliers[TextSpeed];

        /// <summary>Unity tam ekran tercihini oyunun kendi ayarlarında saklar; burada yalnız uygulanır.</summary>
        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Changed?.Invoke();
            }
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set => SetInt(ShakeKey, value ? 1 : 0);
        }

        public static bool PenTutorial
        {
            get => PlayerPrefs.GetInt(PenKey, 1) == 1;
            set => SetInt(PenKey, value ? 1 : 0);
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
