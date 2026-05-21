using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 场景自动搭建工具 — 在 Unity 菜单中点击即可生成完整游戏场景
/// 菜单路径: 暴走足球 > 生成游戏场景
/// </summary>
public class SceneSetupEditor : Editor
{
    private static readonly Color GrassColor = new Color(0.1f, 0.15f, 0.2f, 0.3f); // 半透明地面，让城市背景可见
    private static readonly Color GoalColor = new Color(0.8f, 0.8f, 0.8f);
    private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.6f);

    // 球场尺寸
    private const float FieldWidth = 20f;
    private const float FieldHeight = 12f;
    private const float WallThickness = 0.5f;
    private const float GoalWidth = 3f;
    private const float GoalDepth = 1.5f;

    // 素材路径
    private static readonly string BaseAssetPath = "D:/cc/暴走足球/";

    [MenuItem("暴走足球/生成游戏场景")]
    static void SetupGameScene()
    {
        EnsureDirectoryExists("Assets/Scenes");

        // 删除旧场景
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GameScene.unity") != null)
            AssetDatabase.DeleteAsset("Assets/Scenes/GameScene.unity");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();
        CreateBackground();
        CreateField();
        CreateGoals();
        CreateBall();
        CreatePlayers();
        CreateUI();
        CreateGameManager();

        SetupTags();

        string scenePath = "Assets/Scenes/GameScene.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // 添加到Build Settings
        AddScenesToBuildSettings();

        Debug.Log("=== 暴走足球游戏场景生成完毕！===");
        Debug.Log("按 Play 按钮即可运行游戏。");
        Debug.Log("操作说明：");
        Debug.Log("  玩家1: WASD移动, F踢球, G技能");
        Debug.Log("  玩家2: 方向键移动, 小键盘0踢球, 小键盘.技能");
    }

    [MenuItem("暴走足球/生成主菜单场景")]
    static void SetupMenuScene()
    {
        EnsureDirectoryExists("Assets/Scenes");

        // 删除旧场景
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity") != null)
            AssetDatabase.DeleteAsset("Assets/Scenes/MainMenu.unity");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 添加摄像机（ScreenSpaceOverlay 仍需摄像机渲染背景色）
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0, 0, -10f);
        cam.tag = "MainCamera";

        CreateMenuUI();

        string scenePath = "Assets/Scenes/MainMenu.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        // 添加到Build Settings
        AddScenesToBuildSettings();

        Debug.Log("=== 主菜单场景生成完毕！===");
    }

    [MenuItem("暴走足球/配置Build Settings")]
    static void MenuAddScenesToBuildSettings()
    {
        AddScenesToBuildSettings();
        Debug.Log("=== Build Settings 配置完成！===");
    }

    static void AddScenesToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();

        // 主菜单场景(index 0)
        var menuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/MainMenu.unity");
        if (menuAsset != null)
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/MainMenu.unity", true));

        // 游戏场景(index 1)
        var gameAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/GameScene.unity");
        if (gameAsset != null)
            scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/GameScene.unity", true));

        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"Build Settings 已更新: {scenes.Count} 个场景");
    }

    static void CreateCamera()
    {
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
        cam.transform.position = new Vector3(0, 0f, -10f);
        cam.depth = -1;
        camObj.tag = "MainCamera";

        camObj.AddComponent<CameraController>();
        camObj.AddComponent<ScreenShake>();
    }

    static void CreateBackground()
    {
        // 暗色占位背景 — 运行时 GameManager.SetupBackground() 会创建真实背景
        GameObject bgObj = new GameObject("DarkPlaceholder");
        bgObj.transform.position = new Vector3(0, 0, 15f);

        SpriteRenderer sr = bgObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        sr.sortingOrder = -25;
        bgObj.transform.localScale = new Vector3(50f, 30f, 1);
    }

    static void CreateField()
    {
        GameObject field = new GameObject("Field");

        // 场地背景（暗色地面）
        CreateSprite("Ground", field.transform,
            new Vector3(0, 0, 0),
            new Vector2(FieldWidth, FieldHeight),
            GrassColor, 0);

        // === 球门开口在底部的全封闭场地 ===

        // 地面
        CreateColliderWall("BottomWall", field.transform,
            new Vector3(0, -FieldHeight / 2f, 0),
            new Vector2(FieldWidth + WallThickness * 2, WallThickness));

        // 天花板（全封闭）
        CreateColliderWall("TopWall", field.transform,
            new Vector3(0, FieldHeight / 2f, 0),
            new Vector2(FieldWidth + WallThickness * 2, WallThickness));

        // 左墙（球门开口在底部，墙从球门上方到天花板）
        float wallHeight = FieldHeight - GoalWidth;
        float wallY = -FieldHeight / 2f + GoalWidth + wallHeight / 2f;
        CreateColliderWall("LeftWall", field.transform,
            new Vector3(-FieldWidth / 2f, wallY, 0),
            new Vector2(WallThickness, wallHeight));

        // 右墙（球门开口在底部）
        CreateColliderWall("RightWall", field.transform,
            new Vector3(FieldWidth / 2f, wallY, 0),
            new Vector2(WallThickness, wallHeight));

        // === 球门后方拦网（防止角色/足球掉出场景） ===
        // 左球门拦网：放在球门触发器后方，封住底部角落
        float backstopY = -FieldHeight / 2f + GoalWidth / 2f; // 与球门同高
        float backstopH = GoalWidth + WallThickness;           // 略高于球门，确保与上下墙体重叠密封
        CreateColliderWall("LeftBackstop", field.transform,
            new Vector3(-FieldWidth / 2f - GoalDepth - WallThickness / 2f, backstopY, 0),
            new Vector2(WallThickness, backstopH));
        // 右球门拦网
        CreateColliderWall("RightBackstop", field.transform,
            new Vector3(FieldWidth / 2f + GoalDepth + WallThickness / 2f, backstopY, 0),
            new Vector2(WallThickness, backstopH));

        // 场地装饰线
        CreateLine("CenterLine", field.transform,
            new Vector3(0, 0, 0),
            new Vector2(0.05f, FieldHeight * 0.8f));

        // 地面标记线
        CreateLine("GroundLine", field.transform,
            new Vector3(0, -FieldHeight / 2f + WallThickness, 0),
            new Vector2(FieldWidth, 0.05f));
    }

    static void CreateGoals()
    {
        // 球门放在底部（y = -FieldHeight/2 + GoalWidth/2 = -4.5）
        float goalY = -FieldHeight / 2f + GoalWidth / 2f;

        // 左球门（P1的球门在左侧底角）
        GameObject leftGoal = new GameObject("LeftGoal");
        BoxCollider2D col = leftGoal.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(GoalDepth, GoalWidth);
        leftGoal.transform.position = new Vector3(-FieldWidth / 2f - GoalDepth / 2f, goalY, 0);
        leftGoal.tag = "Goal";

        GoalZone goalZone = leftGoal.AddComponent<GoalZone>();
        goalZone.goalOwnerIndex = 0;
        goalZone.goalColor = Color.red;

        CreateSprite("GoalVisual", leftGoal.transform,
            Vector3.zero,
            new Vector2(GoalDepth, GoalWidth),
            new Color(0.9f, 0.9f, 0.9f, 0.5f), 1);

        // 右球门（P2的球门在右侧底角）
        GameObject rightGoal = new GameObject("RightGoal");
        BoxCollider2D col2 = rightGoal.AddComponent<BoxCollider2D>();
        col2.isTrigger = true;
        col2.size = new Vector2(GoalDepth, GoalWidth);
        rightGoal.transform.position = new Vector3(FieldWidth / 2f + GoalDepth / 2f, goalY, 0);
        rightGoal.tag = "Goal";

        GoalZone goalZone2 = rightGoal.AddComponent<GoalZone>();
        goalZone2.goalOwnerIndex = 1;
        goalZone2.goalColor = Color.blue;

        CreateSprite("GoalVisual", rightGoal.transform,
            Vector3.zero,
            new Vector2(GoalDepth, GoalWidth),
            new Color(0.9f, 0.9f, 0.9f, 0.5f), 1);
    }

    static void CreateBall()
    {
        GameObject ball = new GameObject("Ball");
        ball.tag = "Ball";
        ball.transform.position = Vector3.zero;

        // 加载足球素材
        string ballPath = BaseAssetPath + "足球球类素材包/64x64/football.png";
        Sprite ballSprite = LoadSpriteFromFile(ballPath);

        SpriteRenderer sr = ball.AddComponent<SpriteRenderer>();
        sr.sprite = ballSprite != null ? ballSprite : CreateCircleSprite();
        sr.color = Color.white;
        sr.sortingOrder = 10;

        // 调整球的缩放以适配场地比例
        ball.transform.localScale = new Vector3(0.6f, 0.6f, 1);

        CircleCollider2D col = ball.AddComponent<CircleCollider2D>();
        col.radius = 0.4f;

        Rigidbody2D rb = ball.AddComponent<Rigidbody2D>();
        rb.gravityScale = 1.5f; // 有重力
        rb.mass = 0.5f;
        rb.drag = 0.5f;
        rb.angularDrag = 0.1f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        ball.AddComponent<Ball>();

        TrailRenderer trail = ball.AddComponent<TrailRenderer>();
        trail.time = 0.3f;
        trail.startWidth = 0.4f;
        trail.endWidth = 0.05f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = new Color(1, 1, 1, 0.5f);
        trail.endColor = new Color(1, 1, 1, 0);
        trail.sortingOrder = 9;
    }

    static void CreatePlayers()
    {
        // P1 默认用 thin，P2 默认用 thick — 直接放在场景中
        CreatePlayerObject("Player1", 0, Color.red,
            new Vector3(-5f, -4f, 0), false);
        CreatePlayerObject("Player2", 1, Color.blue,
            new Vector3(5f, -4f, 0), true);
    }

    static GameObject CreatePlayerObject(string name, int index, Color color, Vector3 pos, bool useThick)
    {
        GameObject player = new GameObject(name);
        player.tag = "Player";
        player.transform.position = pos;

        // 主 SpriteRenderer
        SpriteRenderer sr = player.AddComponent<SpriteRenderer>();
        sr.color = color;
        sr.sortingOrder = 8;

        // 碰撞体
        CapsuleCollider2D col = player.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.8f, 1.2f);
        col.direction = CapsuleDirection2D.Vertical;

        // 物理
        Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 2f;
        rb.mass = 1f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 动画控制器 — 素材由 GameManager 运行时通过 LoadCharacter 加载
        player.AddComponent<PlayerAnimator>();

        // 控制器
        PlayerController controller = player.AddComponent<PlayerController>();
        controller.playerIndex = index;
        controller.playerColor = color;

        return player;
    }

    static void CreateUI()
    {
        // EventSystem（UI交互必需）
        CreateEventSystem();

        GameObject canvas = new GameObject("Canvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f; // 宽高平衡适配

        canvas.AddComponent<GraphicRaycaster>();

        UIManager uiManager = canvas.AddComponent<UIManager>();

        // === 顶部区域：计时器 ===
        GameObject timerObj = CreateTextElement("Timer", canvas.transform,
            new Vector2(0, 280), "60", 48, Color.white);
        uiManager.timerText = timerObj.GetComponent<TextMeshProUGUI>();

        // === 次顶部：局数信息 ===
        GameObject setScore = CreateTextElement("SetScore", canvas.transform,
            new Vector2(0, 240), "Sets: 0 - 0", 24, Color.white);
        uiManager.setScoreText = setScore.GetComponent<TextMeshProUGUI>();

        GameObject roundNum = CreateTextElement("RoundNumber", canvas.transform,
            new Vector2(0, 215), "Round 1/3", 20, new Color(1, 1, 1, 0.7f));
        uiManager.roundNumberText = roundNum.GetComponent<TextMeshProUGUI>();

        // === 顶部区域：比分 ===
        GameObject scoreP1 = CreateTextElement("P1Score", canvas.transform,
            new Vector2(-200, 160), "0", 72, Color.red);
        uiManager.player1ScoreText = scoreP1.GetComponent<TextMeshProUGUI>();

        CreateTextElement("ScoreDivider", canvas.transform,
            new Vector2(0, 160), ":", 72, Color.white);

        GameObject scoreP2 = CreateTextElement("P2Score", canvas.transform,
            new Vector2(200, 160), "0", 72, Color.blue);
        uiManager.player2ScoreText = scoreP2.GetComponent<TextMeshProUGUI>();

        // === 中央提示 ===
        GameObject goalText = CreateTextElement("GoalText", canvas.transform,
            Vector2.zero, "GOAL!", 80, Color.yellow);
        goalText.SetActive(false);
        uiManager.goalText = goalText.GetComponent<TextMeshProUGUI>();
        uiManager.goalTextCanvas = goalText.AddComponent<CanvasGroup>();

        GameObject countdown = CreateTextElement("Countdown", canvas.transform,
            Vector2.zero, "3", 120, Color.white);
        countdown.SetActive(false);
        uiManager.countdownText = countdown.GetComponent<TextMeshProUGUI>();

        // === 底部区域：技能冷却条 ===
        // P1 冷却条
        GameObject energy1 = CreateEnergyBar("P1SkillCooldown", canvas.transform,
            new Vector2(-400, -180), Color.green);
        uiManager.player1EnergyBar = energy1.GetComponent<Slider>();

        // P2 冷却条
        GameObject energy2 = CreateEnergyBar("P2SkillCooldown", canvas.transform,
            new Vector2(400, -180), Color.green);
        uiManager.player2EnergyBar = energy2.GetComponent<Slider>();

        // P1 技能就绪提示
        GameObject skill1 = CreateTextElement("P1SkillReady", canvas.transform,
            new Vector2(-400, -210), "[G] READY!", 20, Color.yellow);
        skill1.SetActive(false);
        uiManager.player1SkillReady = skill1.GetComponent<TextMeshProUGUI>();

        // P2 技能就绪提示
        GameObject skill2 = CreateTextElement("P2SkillReady", canvas.transform,
            new Vector2(400, -210), "[.] READY!", 20, Color.yellow);
        skill2.SetActive(false);
        uiManager.player2SkillReady = skill2.GetComponent<TextMeshProUGUI>();

        // P1 冷却倒计时文字
        GameObject p1Cd = CreateTextElement("P1Cooldown", canvas.transform,
            new Vector2(-400, -150), "0.0s", 16, Color.white);
        p1Cd.SetActive(false);
        uiManager.p1CooldownText = p1Cd.GetComponent<TextMeshProUGUI>();

        // P2 冷却倒计时文字
        GameObject p2Cd = CreateTextElement("P2Cooldown", canvas.transform,
            new Vector2(400, -150), "0.0s", 16, Color.white);
        p2Cd.SetActive(false);
        uiManager.p2CooldownText = p2Cd.GetComponent<TextMeshProUGUI>();

        // === 面板 ===
        // 暂停面板
        GameObject pausePanel = CreatePanel("PausePanel", canvas.transform);
        pausePanel.SetActive(false);
        uiManager.pauseMenuPanel = pausePanel;

        CreateTextElement("PauseText", pausePanel.transform,
            Vector2.zero, "PAUSED", 60, Color.white);
        CreateButton("ResumeBtn", pausePanel.transform,
            new Vector2(0, -60), "Resume", () => uiManager.OnResumeButton());
        CreateButton("MenuBtn_Pause", pausePanel.transform,
            new Vector2(0, -120), "Menu", () => uiManager.OnMainMenuButton());

        // 单局结束面板（局间过渡）
        GameObject roundOverPanel = CreatePanel("RoundOverPanel", canvas.transform);
        roundOverPanel.SetActive(false);
        uiManager.roundOverPanel = roundOverPanel;

        GameObject roundOverText = CreateTextElement("RoundOverText", roundOverPanel.transform,
            new Vector2(0, 50), "P1 wins Round 1!", 48, Color.yellow);
        uiManager.roundOverText = roundOverText.GetComponent<TextMeshProUGUI>();

        GameObject roundScoreInfo = CreateTextElement("RoundScoreInfo", roundOverPanel.transform,
            new Vector2(0, 0), "Score: 3 - 1", 28, Color.white);
        uiManager.roundScoreText = roundScoreInfo.GetComponent<TextMeshProUGUI>();

        CreateTextElement("PressAnyKey", roundOverPanel.transform,
            new Vector2(0, -60), "Press any key to continue...", 22, new Color(1, 1, 1, 0.6f));

        // 整场比赛结束面板
        GameObject gameOverPanel = CreatePanel("GameOverPanel", canvas.transform);
        gameOverPanel.SetActive(false);
        uiManager.gameOverPanel = gameOverPanel;

        GameObject winnerText = CreateTextElement("WinnerText", gameOverPanel.transform,
            new Vector2(0, 100), "WINNER!", 60, Color.yellow);
        uiManager.winnerText = winnerText.GetComponent<TextMeshProUGUI>();

        GameObject finalScore = CreateTextElement("FinalScore", gameOverPanel.transform,
            new Vector2(0, 20), "Sets: 2 - 0\n\nRound Score:\n3 - 1", 28, Color.white);
        uiManager.finalScoreText = finalScore.GetComponent<TextMeshProUGUI>();

        CreateTextElement("GameOverHint", gameOverPanel.transform,
            new Vector2(0, -60), "[R] Restart  [Q] Menu", 22, new Color(1, 1, 1, 0.6f));

        CreateButton("RestartBtn", gameOverPanel.transform,
            new Vector2(-100, -120), "Restart", () => uiManager.OnRestartButton());
        CreateButton("MenuBtn_GameOver", gameOverPanel.transform,
            new Vector2(100, -120), "Menu", () => uiManager.OnMainMenuButton());
    }

    static void CreateGameManager()
    {
        GameObject gm = new GameObject("GameManager");
        GameManager manager = gm.AddComponent<GameManager>();

        GameObject spawnRoot = new GameObject("SpawnPoints");

        GameObject ballSpawn = new GameObject("BallSpawn");
        ballSpawn.transform.SetParent(spawnRoot.transform);
        ballSpawn.transform.position = new Vector3(0, -4f, 0);
        manager.ballSpawnPoint = ballSpawn.transform;

        GameObject p1Spawn = new GameObject("P1Spawn");
        p1Spawn.transform.SetParent(spawnRoot.transform);
        p1Spawn.transform.position = new Vector3(-5f, -4f, 0);
        manager.player1SpawnPoint = p1Spawn.transform;

        GameObject p2Spawn = new GameObject("P2Spawn");
        p2Spawn.transform.SetParent(spawnRoot.transform);
        p2Spawn.transform.position = new Vector3(5f, -4f, 0);
        manager.player2SpawnPoint = p2Spawn.transform;

        // 不再使用预制体 — GameManager 直接查找场景中的对象
    }

    // === 辅助方法 ===

    static void CreateSprite(string name, Transform parent, Vector3 pos, Vector2 size, Color color, int order)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(size.x, size.y, 1);

        SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = color;
        sr.sortingOrder = order;
    }

    static void CreateColliderWall(string name, Transform parent, Vector3 pos, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.tag = "Wall";

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;

        // 不添加 SpriteRenderer — 墙不可见，只保留碰撞体
        // 城市背景会透过显示
    }

    static void CreateLine(string name, Transform parent, Vector3 pos, Vector2 size)
    {
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent);
        line.transform.position = pos;
        line.transform.localScale = new Vector3(size.x, size.y, 1);

        SpriteRenderer sr = line.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = LineColor;
        sr.sortingOrder = 2;
    }

    static void CreateCircle(string name, Transform parent, Vector3 pos, float radius)
    {
        GameObject circle = new GameObject(name);
        circle.transform.SetParent(parent);
        circle.transform.position = pos;
        circle.transform.localScale = new Vector3(radius * 2, radius * 2, 1);

        SpriteRenderer sr = circle.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = LineColor;
        sr.sortingOrder = 2;
    }

    static GameObject CreateTextElement(string name, Transform parent, Vector2 pos, string text, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = Vector3.zero;

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 100);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;

        return obj;
    }

    static GameObject CreateEnergyBar(string name, Transform parent, Vector2 pos, Color fillColor)
    {
        GameObject bar = new GameObject(name);
        bar.transform.SetParent(parent);
        RectTransform barRt = bar.AddComponent<RectTransform>();
        barRt.anchoredPosition = pos;
        barRt.sizeDelta = new Vector2(150, 15);

        Image bgImg = bar.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(bar.transform);
        RectTransform fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;
        fillRt.anchoredPosition = Vector2.zero;

        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;

        Slider slider = bar.AddComponent<Slider>();
        slider.fillRect = fillRt;
        slider.maxValue = 1f;

        return bar;
    }

    static GameObject CreatePanel(string name, Transform parent)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        Image img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.7f);

        return panel;
    }

    static void CreateButton(string name, Transform parent, Vector2 pos, string text, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(200, 50);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.3f, 0.3f, 0.3f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        if (action != null) btn.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    static GameObject CreateButtonObj(string name, Transform parent, Vector2 pos, string text, UnityEngine.Events.UnityAction action = null)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(250, 55);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.2f, 0.5f, 0.2f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        if (action != null) btn.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        return btnObj;
    }

    static void CreateEventSystem()
    {
        EventSystem existing = Object.FindObjectOfType<EventSystem>();
        if (existing != null)
        {
            // 替换旧的 StandaloneInputModule 为 SimpleInputModule
            var oldModule = existing.GetComponent<StandaloneInputModule>();
            if (oldModule != null)
            {
                Object.DestroyImmediate(oldModule);
                existing.gameObject.AddComponent<SimpleInputModule>();
            }
            return;
        }
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<SimpleInputModule>();
    }

    // === 素材加载 ===

    static Sprite LoadSpriteFromFile(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning($"文件不存在: {path}");
            return null;
        }

        byte[] fileData = System.IO.File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        if (!tex.LoadImage(fileData))
        {
            Debug.LogWarning($"无法加载图片: {path}");
            return null;
        }

        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 64f);
    }

    // === 程序化Sprite生成（备用） ===

    static Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    static Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                colors[y * size + x] = dist <= radius ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    static void SetupTags() { }

    static void CreateMenuUI()
    {
        CreateEventSystem();

        GameObject canvas = new GameObject("Canvas");
        Canvas c = canvas.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 100;

        CanvasScaler scaler = canvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f; // 宽高平衡适配

        canvas.AddComponent<GraphicRaycaster>();

        MainMenuController menu = canvas.AddComponent<MainMenuController>();

        // === 背景 ===
        string bgPath = BaseAssetPath + "背景/background 1/orig.png";
        Sprite bgSprite = LoadSpriteFromFile(bgPath);

        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(canvas.transform);
        bgObj.transform.SetAsFirstSibling();
        RectTransform bgRt = bgObj.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.sizeDelta = Vector2.zero;

        if (bgSprite != null)
        {
            RawImage bgRaw = bgObj.AddComponent<RawImage>();
            bgRaw.texture = bgSprite.texture;
            bgRaw.color = new Color(0.3f, 0.3f, 0.4f, 1f);
        }
        else
        {
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = new Color(0.08f, 0.08f, 0.12f, 1f);
        }

        // === 主菜单面板 ===
        menu.mainMenuPanel = new GameObject("MainMenuPanel");
        menu.mainMenuPanel.transform.SetParent(canvas.transform);
        RectTransform mmRt = menu.mainMenuPanel.AddComponent<RectTransform>();
        mmRt.anchorMin = Vector2.zero;
        mmRt.anchorMax = Vector2.one;
        mmRt.sizeDelta = Vector2.zero;

        // 标题
        GameObject title = CreateTextElement("Title", menu.mainMenuPanel.transform,
            new Vector2(0, 200), "RAGE SOCCER", 80, Color.yellow);
        menu.titleText = title.GetComponent<TextMeshProUGUI>();

        CreateTextElement("Subtitle", menu.mainMenuPanel.transform,
            new Vector2(0, 130), "2-Player Fighting Soccer", 32, Color.white);

        CreateTextElement("Rules", menu.mainMenuPanel.transform,
            new Vector2(0, 80), "Best of 3 | 60s Round | Golden Goal | 3 Skills", 22, new Color(1, 1, 1, 0.7f));

        GameObject startBtn = CreateButtonObj("StartBtn", menu.mainMenuPanel.transform,
            new Vector2(0, 0), "Start Game", () => menu.OnStartButton());
        menu.startButton = startBtn;

        GameObject quitBtn = CreateButtonObj("QuitBtn", menu.mainMenuPanel.transform,
            new Vector2(0, -70), "Quit", () => menu.OnQuitButton());
        menu.quitButton = quitBtn;

        CreateTextElement("Controls", menu.mainMenuPanel.transform,
            new Vector2(0, -250),
            "P1: [A/D] Move  [W] Jump  [F] Kick  [G] Skill\nP2: [Arrows] Move  [Up] Jump  [Num0] Kick  [Num.] Skill",
            18, new Color(1, 1, 1, 0.5f));

        // === 角色选择面板 ===
        GameObject charPanel = CreatePanel("CharacterSelect", canvas.transform);
        charPanel.SetActive(false);
        menu.characterSelectPanel = charPanel;

        CreateTextElement("CharTitle", charPanel.transform,
            new Vector2(0, 240), "CHOOSE YOUR CHARACTER", 52, Color.yellow);

        // 4个角色卡片
        menu.charSlots = new GameObject[4];
        menu.charNameTexts = new TextMeshProUGUI[4];
        menu.charAttrTexts = new TextMeshProUGUI[4];
        menu.charSkillTexts = new TextMeshProUGUI[4];

        float startX = -360f;
        float spacing = 240f;

        for (int i = 0; i < CharacterDatabase.Count; i++)
        {
            CharacterData data = CharacterDatabase.Get(i);
            float x = startX + i * spacing;

            // 角色卡片背景
            GameObject slot = CreatePanel($"CharSlot_{i}", charPanel.transform);
            RectTransform slotRt = slot.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = new Vector2(210, 350);
            slotRt.anchoredPosition = new Vector2(x, 0);
            menu.charSlots[i] = slot;

            // 角色名 + 类型
            GameObject nameObj = CreateTextElement($"CharName_{i}", slot.transform,
                new Vector2(0, 130), data.displayName, 28, data.characterColor);
            menu.charNameTexts[i] = nameObj.GetComponent<TextMeshProUGUI>();

            CreateTextElement($"CharType_{i}", slot.transform,
                new Vector2(0, 95), data.typeName, 20, Color.white);

            // 角色预览 — 从CharacterData的idle pose加载
            Sprite previewSprite = LoadSpriteFromFile(data.previewPath);
            if (previewSprite != null)
            {
                GameObject preview = new GameObject($"CharPreview_{i}");
                preview.transform.SetParent(slot.transform);
                RectTransform pRt = preview.AddComponent<RectTransform>();
                pRt.anchoredPosition = new Vector2(0, 30);
                pRt.sizeDelta = new Vector2(60, 80);

                Image pImg = preview.AddComponent<Image>();
                pImg.sprite = previewSprite;
                pImg.color = data.characterColor;
                pImg.preserveAspect = true;
            }

            // 属性文字
            string attrText = $"SPD: {new string('*', data.speed)}{new string('-', 5 - data.speed)}\n" +
                             $"POW: {new string('*', data.power)}{new string('-', 5 - data.power)}\n" +
                             $"TEC: {new string('*', data.technique)}{new string('-', 5 - data.technique)}";
            GameObject attrObj = CreateTextElement($"CharAttr_{i}", slot.transform,
                new Vector2(0, -40), attrText, 16, Color.white);
            menu.charAttrTexts[i] = attrObj.GetComponent<TextMeshProUGUI>();

            // 技能说明
            string skillText = $"Skill: {data.skillName}\n{data.skillDesc}";
            GameObject skillObj = CreateTextElement($"CharSkill_{i}", slot.transform,
                new Vector2(0, -120), skillText, 14, Color.yellow);
            menu.charSkillTexts[i] = skillObj.GetComponent<TextMeshProUGUI>();
        }

        // P1/P2 状态
        GameObject p1Status = CreateTextElement("P1Status", charPanel.transform,
            new Vector2(-250, -200), "P1: Choosing...", 24, Color.red);
        menu.p1StatusText = p1Status.GetComponent<TextMeshProUGUI>();

        GameObject p2Status = CreateTextElement("P2Status", charPanel.transform,
            new Vector2(250, -200), "P2: Choosing...", 24, Color.blue);
        menu.p2StatusText = p2Status.GetComponent<TextMeshProUGUI>();

        // 操作提示
        GameObject instr = CreateTextElement("Instructions", charPanel.transform,
            new Vector2(0, -240), "P1: [W/S] Browse  [F] Confirm    P2: [Up/Down] Browse  [Num0] Confirm", 20, Color.white);
        menu.instructionsText = instr.GetComponent<TextMeshProUGUI>();

        // === 背景选择面板 ===
        GameObject bgPanel = CreatePanel("BgSelectPanel", canvas.transform);
        bgPanel.SetActive(false);
        menu.bgSelectPanel = bgPanel;

        CreateTextElement("BgTitle", bgPanel.transform,
            new Vector2(0, 240), "CHOOSE ARENA", 52, Color.yellow);

        menu.bgSlots = new GameObject[12];
        menu.bgPreviewImages = new Image[12];
        menu.bgNameTexts = new TextMeshProUGUI[12];

        // 12个背景缩略图 — 4列x3行
        int cols = 4;
        float bgStartX = -360f;
        float bgStartY = 100f;
        float bgSpacingX = 240f;
        float bgSpacingY = -160f;
        float slotW = 220f;
        float slotH = 130f;

        for (int i = 0; i < BackgroundDatabase.Count; i++)
        {
            BackgroundData bgData = BackgroundDatabase.Get(i);
            int col = i % cols;
            int row = i / cols;
            float x = bgStartX + col * bgSpacingX;
            float y = bgStartY + row * bgSpacingY;

            GameObject bgSlot = CreatePanel($"BgSlot_{i}", bgPanel.transform);
            RectTransform slotRt = bgSlot.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0.5f, 0.5f);
            slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = new Vector2(slotW, slotH);
            slotRt.anchoredPosition = new Vector2(x, y);
            menu.bgSlots[i] = bgSlot;

            // 缩略图
            Sprite thumbSprite = LoadSpriteFromFile(bgData.thumbnailPath);
            if (thumbSprite != null)
            {
                GameObject thumbObj = new GameObject($"BgThumb_{i}");
                thumbObj.transform.SetParent(bgSlot.transform);
                RectTransform tRt = thumbObj.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero;
                tRt.anchorMax = Vector2.one;
                tRt.sizeDelta = new Vector2(-8, -8);
                tRt.anchoredPosition = Vector2.zero;

                Image tImg = thumbObj.AddComponent<Image>();
                tImg.sprite = thumbSprite;
                tImg.preserveAspect = true;
                tImg.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                menu.bgPreviewImages[i] = tImg;
            }

            // 背景名
            GameObject bgName = CreateTextElement($"BgName_{i}", bgSlot.transform,
                new Vector2(0, -slotH / 2 + 15), bgData.displayName, 14, Color.white);
            menu.bgNameTexts[i] = bgName.GetComponent<TextMeshProUGUI>();
        }

        // 背景选择状态
        GameObject bgP1 = CreateTextElement("BgP1Status", bgPanel.transform,
            new Vector2(-250, -230), "P1: Choosing...", 24, Color.red);
        menu.bgP1StatusText = bgP1.GetComponent<TextMeshProUGUI>();

        GameObject bgP2 = CreateTextElement("BgP2Status", bgPanel.transform,
            new Vector2(250, -230), "P2: Choosing...", 24, Color.blue);
        menu.bgP2StatusText = bgP2.GetComponent<TextMeshProUGUI>();

        GameObject bgInstr = CreateTextElement("BgInstructions", bgPanel.transform,
            new Vector2(0, -260), "P1: [WASD] Browse  [F] Confirm    P2: [Arrows] Browse  [Num0] Confirm", 18, Color.white);
        menu.bgInstructionsText = bgInstr.GetComponent<TextMeshProUGUI>();
    }

    static void EnsureDirectoryExists(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
