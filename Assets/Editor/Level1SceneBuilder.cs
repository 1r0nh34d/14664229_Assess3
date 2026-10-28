using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static BuilderUtils;

public static class Level1SceneBuilder
{
    const string TilesDir = "Assets/Tiles";
    const string AnimDir = "Assets/Animation";
    const string PrefabDir = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Level1.unity";

    static readonly string[] SharkNames = { "Bruce", "Jeff", "Kenny", "Lenny" };
    static readonly string[] Directions = { "Right", "Down", "Left", "Up" };

    static readonly List<string> warnings = new List<string>();

    [MenuItem("PacStudent/Build Level1 Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        warnings.Clear();

        RuntimeAnimatorController pacController = BuildPacStudentAnimator();
        RuntimeAnimatorController[] ghostControllers = SharkNames.Select(BuildGhostAnimator).ToArray();
        RuntimeAnimatorController powerController = BuildPowerPelletAnimator();
        GameObject pelletPrefab = BuildPelletPrefab("Pellet", LoadSprite(SpritesDir + "/Items/Pellet.png"), false, null);
        GameObject powerPrefab = BuildPelletPrefab("PowerPellet", LoadSprite(SpritesDir + "/Items/PowerPellet_01.png"), true, powerController);
        GameObject cherryPrefab = BuildCherryPrefab();
        GameAudio audioLibrary = RefreshAudioLibrary();

        BuildScene(pacController, ghostControllers, pelletPrefab, powerPrefab, cherryPrefab, audioLibrary);

        SetBuildScenes();
        AssetDatabase.SaveAssets();

        string report = "Level1 scene built.";
        if (warnings.Count > 0) report += "\nStill to do:\n - " + string.Join("\n - ", warnings);
        Debug.Log(report);
    }

    static RuntimeAnimatorController BuildPacStudentAnimator()
    {
        string dir = AnimDir + "/PacStudent/";
        string sprites = SpritesDir + "/PacStudent/PacStudent_";
        List<(string, AnimationClip)> states = new List<(string, AnimationClip)>();
        foreach (string direction in Directions)
            states.Add(("Walk_" + direction, CreateClip($"{dir}PacStudent_Walk_{direction}.anim", LoadFrames(sprites + direction + "_"), 10f, true)));
        states.Add(("Dead", CreateClip(dir + "PacStudent_Dead.anim", LoadFrames(sprites + "Dead_"), 3f, false)));
        CreateCyclingController(dir + "PacStudentAnimator.controller", states);
        return CreateController(dir + "PacStudent.controller", states);
    }

    static RuntimeAnimatorController BuildGhostAnimator(string shark)
    {
        string dir = $"{AnimDir}/Ghosts/{shark}/";
        EnsureFolder(dir.TrimEnd('/'));
        string sprites = $"{SpritesDir}/Ghosts/Shark_{shark}_";
        Color scaredBlue = new Color(0.35f, 0.5f, 1f);
        Color deadGhost = new Color(0.75f, 0.85f, 1f, 0.35f);
        List<(string, AnimationClip)> states = new List<(string, AnimationClip)>();

        foreach (string direction in Directions)
            states.Add(("Walk_" + direction, CreateClip($"{dir}{shark}_Walk_{direction}.anim", LoadFrames(sprites + direction + "_"), 8f, true)));

        Sprite[] scaredLeft = null;
        foreach (string direction in Directions)
        {
            Sprite[] scared = FirstFound(sprites + "Scared_" + direction + "_", $"{SpritesDir}/Ghosts/Shark_Scared_{direction}_");
            if (direction == "Left") scaredLeft = scared;
            states.Add(("Scared_" + direction, scared.Length > 0
                ? CreateClip($"{dir}{shark}_Scared_{direction}.anim", scared, 8f, true)
                : CreateClip($"{dir}{shark}_Scared_{direction}.anim", LoadFrames(sprites + direction + "_"), 6f, true, scaredBlue, scaredBlue, 1)));
        }
        if (scaredLeft.Length == 0) warnings.Add($"Shark_{shark}: no scared sprites yet, using blue-tinted swimming frames (draw Shark_Scared_Left_01.png etc.)");

        Sprite[] recoveringFrames = scaredLeft.Length > 0 ? scaredLeft : LoadFrames(sprites + "Left_");
        states.Add(("Recovering", CreateClip($"{dir}{shark}_Recovering.anim", recoveringFrames, 6f, true, scaredBlue, Color.white, 4)));

        Sprite[] dead = FirstFound(sprites + "Dead_", SpritesDir + "/Ghosts/Shark_Dead_");
        if (dead.Length == 0) warnings.Add($"Shark_{shark}: no dead sprites yet, using see-through swimming frames");
        states.Add(("Dead", dead.Length > 0
            ? CreateClip($"{dir}{shark}_Dead.anim", dead, 6f, true)
            : CreateClip($"{dir}{shark}_Dead.anim", LoadFrames(sprites + "Left_"), 4f, true, deadGhost, deadGhost, 1)));

        CreateCyclingController($"{AnimDir}/Ghosts/GhostAnimator_{shark}.controller", states);
        return CreateController($"{AnimDir}/Ghosts/Ghost_{shark}.controller", states);
    }

    static RuntimeAnimatorController BuildPowerPelletAnimator()
    {
        string path = AnimDir + "/Pellets/PowerPellet_Flash.anim";
        AnimationClip clip = ReuseClip(path, 30f);

        Sprite[] frames = LoadFrames(SpritesDir + "/Items/PowerPellet_");
        if (frames.Length > 1) SetSpriteCurve(clip, frames, frames.Length / 0.6f);
        else warnings.Add("Power pellet has only 1 frame; draw PowerPellet_02.png (needs 2 visually distinct frames)");

        AnimationCurve scale = new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(0.3f, 0.65f), new Keyframe(0.6f, 1.1f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.x"), scale);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalScale.y"), scale);
        AnimationCurve alpha = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.3f, 0.3f), new Keyframe(0.6f, 1f));
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color.a"), alpha);
        SetLooping(clip, true);
        EditorUtility.SetDirty(clip);

        return CreateCyclingController(AnimDir + "/Pellets/PowerPelletAnimator.controller",
            new List<(string, AnimationClip)> { ("Flash", clip) });
    }

    static AnimationClip CreateClip(string path, Sprite[] frames, float fps, bool loop,
        Color? colourA = null, Color? colourB = null, int colourSteps = 1)
    {
        AnimationClip clip = ReuseClip(path, fps);
        if (frames.Length == 0) warnings.Add($"No sprites found for {Path.GetFileNameWithoutExtension(path)}");
        else SetSpriteCurve(clip, frames, fps);

        if (colourA.HasValue)
        {
            float length = Mathf.Max(frames.Length, 1) / fps;
            string[] channels = { "r", "g", "b", "a" };
            for (int c = 0; c < 4; c++)
            {
                AnimationCurve curve = new AnimationCurve();
                for (int step = 0; step <= colourSteps; step++)
                {
                    Color colour = step % 2 == 0 ? colourA.Value : (colourB ?? colourA.Value);
                    curve.AddKey(length * step / colourSteps, colour[c]);
                }
                for (int k = 0; k < curve.length; k++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_Color." + channels[c]), curve);
            }
        }

        SetLooping(clip, loop);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimationClip ReuseClip(string path, float fps)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        else
        {
            clip.ClearCurves();
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }
        clip.frameRate = fps;
        return clip;
    }

    static void SetSpriteCurve(AnimationClip clip, Sprite[] frames, float fps)
    {
        ObjectReferenceKeyframe[] keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"), keys);
    }

    static void SetLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    static AnimatorController CreateCyclingController(string path, List<(string name, AnimationClip clip)> states)
    {
        AnimatorController controller = CreateController(path, states);
        AnimatorState[] created = controller.layers[0].stateMachine.states.Select(s => s.state).ToArray();

        if (created.Length > 1)
        {
            for (int i = 0; i < created.Length; i++)
            {
                AnimationClip clip = states[i].clip;
                bool loops = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
                float length = Mathf.Max(clip.length, 0.01f);
                AnimatorStateTransition transition = created[i].AddTransition(created[(i + 1) % created.Length]);
                transition.hasExitTime = true;
                transition.exitTime = loops ? Mathf.Max(3f, 2f / length) : 1f;
                transition.hasFixedDuration = true;
                transition.duration = 0f;
            }
        }
        return controller;
    }

    static AnimatorController CreateController(string path, List<(string name, AnimationClip clip)> states)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState old in machine.states) machine.RemoveState(old.state);
        for (int i = 0; i < states.Count; i++)
        {
            AnimatorState state = machine.AddState(states[i].name, new Vector3(300f, i * 70f, 0f));
            state.motion = states[i].clip;
        }
        machine.defaultState = machine.states[0].state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static GameObject BuildPelletPrefab(string name, Sprite sprite, bool power, RuntimeAnimatorController controller)
    {
        GameObject go = new GameObject(name);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 1;
        go.AddComponent<Pellet>().isPowerPellet = power;
        AddTrigger(go, power ? 0.3f : 0.15f);
        if (controller != null) go.AddComponent<Animator>().runtimeAnimatorController = controller;
        return SavePrefab(go, name);
    }

    static GameObject BuildCherryPrefab()
    {
        GameObject go = new GameObject("BonusCherry");
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = LoadSprite(SpritesDir + "/Items/Cherry.png");
        if (renderer.sprite == null) warnings.Add("No bonus cherry sprite (add Assets/Sprites/Items/Cherry.png and rebuild)");
        renderer.sortingOrder = 50;
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        go.AddComponent<Cherry>();
        AddTrigger(go, 0.4f);
        return SavePrefab(go, "BonusCherry");
    }

    static CircleCollider2D AddTrigger(GameObject go, float radius)
    {
        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = radius;
        return collider;
    }

    static void AddKinematicBody(GameObject go, float radius)
    {
        Rigidbody2D body = go.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.sleepMode = RigidbodySleepMode2D.NeverSleep;
        AddTrigger(go, radius);
    }

    static GameObject SavePrefab(GameObject go, string name)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static ParticleSystem CreateParticles(string name, Transform parent, Color colourA, Color colourB,
        float size, float lifetime, float speed, float rate, int burst, bool loop, float gravity, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        ParticleSystem particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particles.main;
        main.loop = loop;
        main.playOnAwake = false;
        main.duration = loop ? 1f : 0.5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
        main.startColor = new ParticleSystem.MinMaxGradient(colourA, colourB);
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = rate;
        if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule colour = particles.colorOverLifetime;
        colour.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        renderer.sortingOrder = sortingOrder;
        return particles;
    }

    static void BuildScene(RuntimeAnimatorController pacController, RuntimeAnimatorController[] ghostControllers,
        GameObject pelletPrefab, GameObject powerPrefab, GameObject cherryPrefab, GameAudio audioLibrary)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera(LevelGenerator.Rows / 2f + 3f);

        GameObject managerObject = new GameObject("GameManager");
        GameManager game = managerObject.AddComponent<GameManager>();
        game.levelKey = "Level1";
        game.levelName = "Level 1";
        game.audioLibrary = audioLibrary;
        game.musicSource = AddAudioSource(managerObject, 0.6f);
        game.sfxSource = AddAudioSource(managerObject, 1f);

        GameObject level = new GameObject("Level");
        level.AddComponent<Grid>();
        level.AddComponent<LevelGenerator>();
        level.transform.position = new Vector3(-LevelGenerator.Cols / 2f, (LevelGenerator.Rows - 1) / 2f - 0.5f, 0f);

        GameObject wallsObject = new GameObject("Walls");
        wallsObject.transform.SetParent(level.transform, false);
        Tilemap walls = wallsObject.AddComponent<Tilemap>();
        wallsObject.AddComponent<TilemapRenderer>();
        GameObject pellets = Child(level.transform, "Pellets");
        GameObject powerPellets = Child(level.transform, "PowerPellets");

        Dictionary<int, TileBase> tiles = new Dictionary<int, TileBase>
        {
            { 1, LoadTile("Tile_OutsideCorner") },
            { 2, LoadTile("Tile_OutsideWall") },
            { 3, LoadTile("Tile_InsideCorner") },
            { 4, LoadTile("Tile_InsideWall") },
            { 7, LoadTile("Tile_TJunction") },
            { 8, LoadTile("Tile_SharkCage") },
        };

        for (int row = 0; row < LevelGenerator.Rows; row++)
        {
            for (int col = 0; col < LevelGenerator.Cols; col++)
            {
                Vector2Int cell = new Vector2Int(col, row);
                int tile = LevelGenerator.TileAt(cell);
                if (tiles.TryGetValue(tile, out TileBase tileAsset))
                {
                    Vector3Int position = new Vector3Int(col, -row, 0);
                    walls.SetTile(position, tileAsset);
                    walls.SetTileFlags(position, TileFlags.None);
                    walls.SetTransformMatrix(position, Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, LevelGenerator.GetWallRotation(cell))));
                }
                else if (tile == 5 || tile == 6)
                {
                    GameObject prefab = tile == 6 ? powerPrefab : pelletPrefab;
                    Transform parent = tile == 6 ? powerPellets.transform : pellets.transform;
                    GameObject pellet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    pellet.transform.position = LevelGenerator.CellToWorld(cell);
                    pellet.name = $"{prefab.name}_{col}_{row}";
                }
            }
        }

        GameObject characters = new GameObject("Characters");
        GameObject pac = SpriteObject(characters.transform, "PacStudent", LoadSprite(SpritesDir + "/PacStudent/PacStudent_Right_01.png"), 5);
        pac.transform.position = LevelGenerator.CellToWorld(new Vector2Int(1, 1));
        Animator pacAnimator = pac.AddComponent<Animator>();
        pacAnimator.runtimeAnimatorController = pacController;
        PacStudentController pacStudent = pac.AddComponent<PacStudentController>();
        pacStudent.startCell = new Vector2Int(1, 1);
        pacStudent.animator = pacAnimator;
        pacStudent.audioLibrary = audioLibrary;
        pacStudent.moveSource = AddAudioSource(pac, 0.7f);
        pacStudent.dust = CreateParticles("SandDust", pac.transform, new Color(0.85f, 0.72f, 0.45f), new Color(0.6f, 0.48f, 0.3f),
            0.35f, 0.6f, 0.6f, 25f, 0, true, -0.05f, 3);
        pacStudent.dust.transform.localPosition = new Vector3(0f, -0.35f, 0f);
        ParticleSystem.EmissionModule dustEmission = pacStudent.dust.emission;
        dustEmission.enabled = false;
        pacStudent.wallBump = CreateParticles("WallBump", pac.transform, Color.white, Aqua, 0.3f, 0.35f, 3f, 0f, 14, false, 0f, 6);
        pacStudent.deathEffect = CreateParticles("DeathBubbles", pac.transform, Aqua, new Color(0.9f, 1f, 1f), 0.6f, 1.4f, 2.5f, 0f, 45, false, -0.4f, 7);
        AddKinematicBody(pac, 0.35f);
        game.pacStudent = pacStudent;

        GameObject ghosts = Child(characters.transform, "Ghosts");
        game.ghosts = new GhostController[SharkNames.Length];
        for (int i = 0; i < SharkNames.Length; i++)
        {
            GameObject ghost = SpriteObject(ghosts.transform, $"Ghost{i + 1}_{SharkNames[i]}",
                LoadSprite($"{SpritesDir}/Ghosts/Shark_{SharkNames[i]}_Right_01.png"), 4);
            Vector2Int start = new Vector2Int(12 + i, 14);
            ghost.transform.position = LevelGenerator.CellToWorld(start);
            Animator animator = ghost.AddComponent<Animator>();
            animator.runtimeAnimatorController = ghostControllers[i];
            AddKinematicBody(ghost, 0.35f);
            GhostController controller = ghost.AddComponent<GhostController>();
            controller.ghostNumber = i + 1;
            controller.startCell = start;
            controller.animator = animator;
            game.ghosts[i] = controller;
            CreateGhostLabel(ghost.transform, i + 1);
        }

        GameObject mask = new GameObject("LevelMask");
        mask.AddComponent<SpriteMask>().sprite = LoadSprite(SpritesDir + "/Level/Tile_OutsideWall.png");
        mask.transform.localScale = new Vector3(LevelGenerator.Cols, LevelGenerator.Rows, 1f);
        CherryController cherry = new GameObject("CherryController").AddComponent<CherryController>();
        cherry.cherryPrefab = cherryPrefab;
        game.cherryController = cherry;

        game.hud = BuildHud("Level 1");
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    static void CreateGhostLabel(Transform ghost, int number)
    {
        GameObject canvasObject = new GameObject($"Ghost{number}Canvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(ghost, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 30;
        RectTransform rect = (RectTransform)canvasObject.transform;
        rect.sizeDelta = new Vector2(100f, 60f);
        rect.localScale = Vector3.one * 0.01f;
        rect.localPosition = new Vector3(0f, 0.85f, 0f);
        CreateText(canvasObject.transform, "Number", number.ToString(), 52, TextAnchor.MiddleCenter, Color.white,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 60f), FontStyle.Bold);
    }

    static HUDController BuildHud(string levelName)
    {
        GameObject hudObject = new GameObject("HUD");
        Canvas canvas = hudObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        AddScaler(hudObject, 1f);
        hudObject.AddComponent<GraphicRaycaster>();
        HUDController hud = hudObject.AddComponent<HUDController>();
        Transform t = hudObject.transform;

        Vector2 topLeft = new Vector2(0f, 1f), topRight = new Vector2(1f, 1f), top = new Vector2(0.5f, 1f);
        Vector2 bottomLeft = Vector2.zero, bottomRight = new Vector2(1f, 0f), bottom = new Vector2(0.5f, 0f);
        Vector2 left = new Vector2(0f, 0.5f), right = new Vector2(1f, 0.5f), centre = new Vector2(0.5f, 0.5f);

        CreateText(t, "ScoreLabel", "SCORE", 38, TextAnchor.MiddleLeft, Aqua, topLeft, left, new Vector2(30f, -46f), new Vector2(160f, 60f), FontStyle.Bold);
        hud.scoreText = CreateText(t, "Score", "000000", 48, TextAnchor.MiddleLeft, Color.white, topLeft, left, new Vector2(185f, -46f), new Vector2(240f, 60f), FontStyle.Bold);
        hud.levelNameText = CreateText(t, "LevelName", levelName, 50, TextAnchor.MiddleCenter, Gold, top, centre, new Vector2(0f, -46f), new Vector2(400f, 60f), FontStyle.Bold);
        hud.timeText = CreateText(t, "GameTimer", "00:00:00", 48, TextAnchor.MiddleRight, Color.white, topRight, right, new Vector2(-30f, -46f), new Vector2(240f, 60f), FontStyle.Bold);
        CreateText(t, "TimeLabel", "TIME", 38, TextAnchor.MiddleRight, Aqua, topRight, right, new Vector2(-280f, -46f), new Vector2(130f, 60f), FontStyle.Bold);

        CreateText(t, "LivesLabel", "LIVES", 38, TextAnchor.MiddleLeft, Aqua, bottomLeft, left, new Vector2(30f, 46f), new Vector2(140f, 60f), FontStyle.Bold);
        GameObject lives = new GameObject("Lives", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        lives.transform.SetParent(t, false);
        SetRect((RectTransform)lives.transform, bottomLeft, left, new Vector2(170f, 46f), new Vector2(240f, 64f));
        HorizontalLayoutGroup layout = lives.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        Sprite lifeSprite = LoadSprite(SpritesDir + "/Items/LifeIcon.png");
        hud.lifeIcons = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            Image icon = CreateImage(lives.transform, "Life" + (i + 1), lifeSprite, Color.white);
            ((RectTransform)icon.transform).sizeDelta = new Vector2(64f, 64f);
            hud.lifeIcons[i] = icon;
        }

        hud.scaredTimerText = CreateText(t, "GhostScaredTimer", "Scared: 10", 46, TextAnchor.MiddleCenter, new Color(0.45f, 0.65f, 1f),
            bottom, centre, new Vector2(0f, 46f), new Vector2(400f, 60f), FontStyle.Bold);
        hud.scaredTimerText.gameObject.SetActive(false);

        hud.exitButton = CreateButton(t, "ExitButton", "EXIT", bottomRight, right, new Vector2(-30f, 46f), new Vector2(180f, 66f), 40);
        UnityEventTools.AddPersistentListener(hud.exitButton.onClick, hud.ExitToStart);

        hud.blocker = CreateImage(t, "Blocker", null, new Color(0f, 0f, 0f, 0.65f));
        hud.blocker.raycastTarget = true;
        SetStretch((RectTransform)hud.blocker.transform);
        hud.countdownText = CreateText(t, "Countdown", "3", 260, TextAnchor.MiddleCenter, Gold, centre, centre, Vector2.zero, new Vector2(900f, 400f), FontStyle.Bold);
        hud.gameOverText = CreateText(t, "GameOver", "GAME OVER", 170, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.35f), centre, centre, Vector2.zero, new Vector2(1400f, 300f), FontStyle.Bold);
        foreach (Component overlay in new Component[] { hud.blocker, hud.countdownText, hud.gameOverText })
            overlay.gameObject.SetActive(false);
        return hud;
    }

    static GameObject Child(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static GameObject SpriteObject(Transform parent, string name, Sprite sprite, int sortingOrder)
    {
        GameObject go = Child(parent, name);
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    static Sprite[] FirstFound(params string[] prefixes)
    {
        foreach (string prefix in prefixes)
        {
            Sprite[] frames = LoadFrames(prefix);
            if (frames.Length > 0) return frames;
        }
        return new Sprite[0];
    }

    static TileBase LoadTile(string name) => AssetDatabase.LoadAssetAtPath<TileBase>($"{TilesDir}/{name}.asset");
}
