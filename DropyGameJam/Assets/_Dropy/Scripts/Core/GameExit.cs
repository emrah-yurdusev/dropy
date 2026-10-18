using UnityEngine;

namespace Dropy.Core
{
    /// <summary>
    /// Oyundan çıkış: ses ayarlarını kaydeder, oyunu kapatır. Editörde Application.Quit bir şey yapmaz;
    /// orada Play modu durdurulur.
    /// </summary>
    public static class GameExit
    {
        public static void Quit()
        {
            GameAudio.Save();
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
