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
        const string DevPanelKey = "settings.devPanel";

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

        /// <summary>Son seçilen tam ekran değeri: Unity değişikliği kare sonunda uygular, düğme o ana kadar eskisini okumasın.</summary>
        static bool? requestedFullscreen;

        /// <summary>Unity tam ekran tercihini oyunun kendi ayarlarında saklar; burada yalnız uygulanır.</summary>
        public static bool Fullscreen
        {
            get => requestedFullscreen ?? Screen.fullScreen;
            set
            {
                requestedFullscreen = value;
                Screen.fullScreenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Changed?.Invoke();
            }
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set => SetInt(ShakeKey, value ? 1 : 0);
        }

        /// <summary>F1 geliştirici paneli kullanılabilir mi (jüri farklı sonları denesin diye). Editörde varsayılan açık.</summary>
        public static bool DevPanel
        {
            get => PlayerPrefs.GetInt(DevPanelKey, Application.isEditor ? 1 : 0) == 1;
            set => SetInt(DevPanelKey, value ? 1 : 0);
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
