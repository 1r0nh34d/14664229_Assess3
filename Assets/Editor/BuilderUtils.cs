using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class BuilderUtils
{
    public const string SpritesDir = "Assets/Sprites";
    public const string AudioDir = "Assets/Audio Clips";
    public const string SceneDir = "Assets/Scenes";

    public static readonly Color OceanColour = new Color(0.02f, 0.07f, 0.16f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.2f);
    public static readonly Color Aqua = new Color(0.35f, 0.95f, 1f);

    static Font uiFont;
    static Font UIFont => uiFont != null ? uiFont : (uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

    public static void SetBuildScenes()
    {
        EditorBuildSettings.scenes = new[] { "StartScene", "Level1" }
            .Select(name => $"{SceneDir}/{name}.unity")
            .Where(File.Exists)
            .Select(path => new EditorBuildSettingsScene(path, true))
            .ToArray();
    }

    [MenuItem("PacStudent/Refresh Audio Library")]
    public static GameAudio RefreshAudioLibrary()
    {
        string path = AudioDir + "/GameAudio.asset";
        GameAudio library = AssetDatabase.LoadAssetAtPath<GameAudio>(path);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<GameAudio>();
            AssetDatabase.CreateAsset(library, path);
        }

        List<AudioClip> clips = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioDir })
            .Select(guid => AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();
        AudioClip Find(string clipName)
        {
            AudioClip clip = clips.FirstOrDefault(c => string.Equals(c.name, clipName, System.StringComparison.OrdinalIgnoreCase));
            if (clip == null) Debug.LogWarning($"Audio clip '{clipName}' missing from {AudioDir}");
            return clip;
        }

        library.startMusic = Find("Music_StartScene");
        library.introMusic = Find("Music_Intro");
        library.normalMusic = Find("Music_GhostNormal");
        library.scaredMusic = Find("Music_GhostScared");
        library.ghostDeadMusic = Find("Music_GhostDead");
        library.move = Find("SFX_Move");
        library.moveEat = Find("SFX_EatPellet");
        library.wallBump = Find("SFX_WallCollision");
        library.death = Find("SFX_Death");
        library.ghostEat = Find("SFX_EatGhost");
        library.cherryEat = Find("SFX_EatCherry");
        library.buttonHover = Find("SFX_EatPellet");
        library.buttonClick = Find("SFX_Gulp");

        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return library;
    }

    public static AudioSource AddAudioSource(GameObject go, float volume)
    {
        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = volume;
        return source;
    }

    public static void AddScaler(GameObject canvasObject, float matchHeight)
    {
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = matchHeight;
    }

    public static Text CreateText(Transform parent, string name, string content, int size, TextAnchor alignment, Color colour,
        Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 rectSize, FontStyle style = FontStyle.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = UIFont;
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = colour;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        go.AddComponent<Shadow>().effectDistance = new Vector2(3f, -3f);
        SetRect((RectTransform)go.transform, anchor, pivot, position, rectSize);
        return text;
    }

    public static Image CreateImage(Transform parent, string name, Sprite sprite, Color colour)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.preserveAspect = sprite != null;
        image.raycastTarget = false;
        return image;
    }

    public static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, int fontSize)
    {
        Image image = CreateImage(parent, name, AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), Color.white);
        image.type = Image.Type.Sliced;
        image.preserveAspect = false;
        image.raycastTarget = true;
        SetRect((RectTransform)image.transform, anchor, pivot, position, size);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colours = button.colors;
        colours.normalColor = new Color(0.1f, 0.35f, 0.6f);
        colours.highlightedColor = new Color(0.15f, 0.7f, 0.95f);
        colours.selectedColor = new Color(0.15f, 0.7f, 0.95f);
        colours.pressedColor = Gold;
        colours.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        button.colors = colours;
        image.gameObject.AddComponent<ButtonFeedback>();
        CreateText(image.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, FontStyle.Bold);
        return button;
    }

    public static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    public static void SetStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void CreateEventSystem()
    {
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    public static Camera CreateCamera(float orthographicSize)
    {
        GameObject cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera camera = cameraObject.AddComponent<Camera>();
        cameraObject.AddComponent<AudioListener>();
        camera.orthographic = true;
        camera.orthographicSize = orthographicSize;
        camera.transform.position = new Vector3(0f, 0f, -10f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = OceanColour;
        return camera;
    }

    public static Sprite LoadSprite(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path);

    public static Sprite[] LoadFrames(string prefix)
    {
        List<Sprite> frames = new List<Sprite>();
        for (int i = 1; i < 20; i++)
        {
            Sprite sprite = LoadSprite($"{prefix}{i:00}.png");
            if (sprite == null) break;
            frames.Add(sprite);
        }
        return frames.ToArray();
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
