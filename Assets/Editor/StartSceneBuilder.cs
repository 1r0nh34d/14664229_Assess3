using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static BuilderUtils;

// PacStudent > Build Start Scene creates the title screen: title, subtitle, PacStudent and the sharks swimming,
// an animated bubble border, the Level 1 / Level 2 buttons and each level's saved high score and time.
public static class StartSceneBuilder
{
    static readonly string[] SharkNames = { "Bruce", "Jeff", "Kenny", "Lenny" };

    [MenuItem("PacStudent/Build Start Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        GameAudio audioLibrary = RefreshAudioLibrary();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera(5f);

        // Matching height keeps everything on screen for both 16:9 and 4:3
        GameObject canvasObject = new GameObject("StartCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        AddScaler(canvasObject, 1f);
        canvasObject.AddComponent<GraphicRaycaster>();
        Transform t = canvasObject.transform;
        Vector2 centre = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);

        // Bubbles travelling around the edge of the canvas, so the border follows the screen's aspect ratio
        GameObject border = new GameObject("AnimatedBorder", typeof(RectTransform));
        border.transform.SetParent(t, false);
        RectTransform borderRect = (RectTransform)border.transform;
        SetStretch(borderRect);
        borderRect.offsetMin = new Vector2(30f, 30f);
        borderRect.offsetMax = new Vector2(-30f, -30f);
        border.AddComponent<AnimatedBorder>().dotSprite = LoadSprite(SpritesDir + "/Items/PowerPellet_01.png");

        Text title = CreateText(t, "Title", "PacStudent", 160, TextAnchor.MiddleCenter, Gold, top, centre, new Vector2(0f, -190f), new Vector2(1200f, 200f), FontStyle.Bold);
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(5f, -5f);
        CreateText(t, "Subtitle", "Deep Sea Dash - out-swim the sharks of the coral reef!", 46, TextAnchor.MiddleCenter, Aqua,
            top, centre, new Vector2(0f, -310f), new Vector2(1200f, 70f), FontStyle.Italic);

        // PacStudent fleeing from the four sharks
        Image pacImage = CreateImage(t, "PacStudent", LoadSprite(SpritesDir + "/PacStudent/PacStudent_Left_01.png"), Color.white);
        SetRect((RectTransform)pacImage.transform, centre, centre, new Vector2(-380f, 70f), new Vector2(140f, 140f));
        pacImage.gameObject.AddComponent<UISpriteCycler>().frames = LoadFrames(SpritesDir + "/PacStudent/PacStudent_Left_");
        for (int i = 0; i < SharkNames.Length; i++)
        {
            string prefix = $"{SpritesDir}/Ghosts/Shark_{SharkNames[i]}_Left_";
            Image shark = CreateImage(t, "Ghost" + (i + 1), LoadSprite(prefix + "01.png"), Color.white);
            SetRect((RectTransform)shark.transform, centre, centre, new Vector2(-130f + i * 170f, 70f), new Vector2(130f, 130f));
            shark.gameObject.AddComponent<UISpriteCycler>().frames = LoadFrames(prefix);
        }

        GameObject managerObject = new GameObject("StartSceneManager");
        StartSceneManager manager = managerObject.AddComponent<StartSceneManager>();
        manager.audioLibrary = audioLibrary;
        manager.musicSource = AddAudioSource(managerObject, 0.6f);
        manager.sfxSource = AddAudioSource(managerObject, 1f);

        Button level1 = CreateButton(t, "Level1Button", "LEVEL 1", centre, centre, new Vector2(-330f, -150f), new Vector2(400f, 120f), 60);
        Button level2 = CreateButton(t, "Level2Button", "LEVEL 2", centre, centre, new Vector2(330f, -150f), new Vector2(400f, 120f), 60);
        UnityEventTools.AddPersistentListener(level1.onClick, manager.LoadLevel1);
        UnityEventTools.AddPersistentListener(level2.onClick, manager.PressLevel2);
        foreach (Button button in new[] { level1, level2 })
            UnityEventTools.AddPersistentListener(button.GetComponent<ButtonFeedback>().onHover, manager.PlayHover);

        manager.level1ScoreText = CreateText(t, "Level1HighScore", "High Score: 000000", 38, TextAnchor.MiddleCenter, Color.white, centre, centre, new Vector2(-330f, -255f), new Vector2(500f, 50f));
        manager.level1TimeText = CreateText(t, "Level1BestTime", "Time: 00:00:00", 38, TextAnchor.MiddleCenter, Color.white, centre, centre, new Vector2(-330f, -305f), new Vector2(500f, 50f));
        manager.level2ScoreText = CreateText(t, "Level2HighScore", "High Score: 000000", 38, TextAnchor.MiddleCenter, Color.white, centre, centre, new Vector2(330f, -255f), new Vector2(500f, 50f));
        manager.level2TimeText = CreateText(t, "Level2BestTime", "Time: 00:00:00", 38, TextAnchor.MiddleCenter, Color.white, centre, centre, new Vector2(330f, -305f), new Vector2(500f, 50f));

        CreateText(t, "Controls", "W A S D to swim", 32, TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 1f),
            bottom, centre, new Vector2(0f, 95f), new Vector2(1000f, 50f));

        CreateEventSystem();
        EditorSceneManager.SaveScene(scene, SceneDir + "/StartScene.unity");
        SetBuildScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("StartScene built.");
    }
}
