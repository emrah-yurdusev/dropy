using UnityEditor;
using UnityEditor.SceneManagement;

namespace Dropy.EditorTools
{
    /// <summary>
    /// Editörde Play'e basınca hangi sahne açık olursa olsun oyun ana menüden (MenuSahnesi) başlar; çıkış dosyasındaki
    /// akışın aynısı. Bir sahneyi tek başına denemek için "Dropy > Play: Ana menüden başla" işaretini kaldır.
    /// Ayar her bilgisayarda ayrı tutulur (EditorPrefs).
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromMenu
    {
        const string ScenePath = "Assets/_Dropy/Scenes/Menu/MenuSahnesi.unity";
        const string MenuPath = "Dropy/Play: Ana menüden başla";
        const string PrefKey = "Dropy.PlayFromMenu";

        static PlayFromMenu()
        {
            EditorApplication.delayCall += Apply;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => Apply();
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        // Trailer sahneleri (Scenes/Trailer) çekim için tek başına açılır; menüden başlamaz.
        static void Apply() =>
            EditorSceneManager.playModeStartScene = Enabled && !EditorSceneManager.GetActiveScene().path.Contains("/Trailer/")
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
                : null;
    }
}
