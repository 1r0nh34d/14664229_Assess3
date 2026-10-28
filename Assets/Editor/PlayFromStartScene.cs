using UnityEditor;
using UnityEditor.SceneManagement;

// Pressing Play in the editor always starts from the main menu (StartScene), whichever scene is open,
// the same as the built game. Untick PacStudent > Play From Start Scene to play the open scene directly.
[InitializeOnLoad]
public static class PlayFromStartScene
{
    const string ScenePath = "Assets/Scenes/StartScene.unity";
    const string MenuPath = "PacStudent/Play From Start Scene";
    const string PrefKey = "PacStudent.PlayFromStartScene";

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    static PlayFromStartScene()
    {
        EditorApplication.delayCall += Apply;
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) : null;
        Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
    }
}
