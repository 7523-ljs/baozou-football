using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 比赛管理器 — 负责完整对战流程：
/// - 单局60秒倒计时
/// - 进球计分（先到3分/时间到判定）
/// - 三局两胜制（Best of 3）
/// - 局间过渡、比赛结算
/// - 暂停/恢复
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("游戏设置")]
    public float goalPauseDuration = 2f;      // 进球后暂停秒数
    public float countdownDuration = 3f;      // 开局倒计时秒数
    public float roundDuration = 60f;         // 单局时长（秒）
    public int winGoalCount = 3;              // 单局先得几分获胜
    public int winSetCount = 2;               // 三局两胜需要赢几局

    [Header("引用")]
    public Transform ballSpawnPoint;
    public Transform player1SpawnPoint;
    public Transform player2SpawnPoint;

    // === 游戏状态枚举 ===
    public enum GameState
    {
        Countdown,    // 开局倒计时
        Playing,      // 对战中
        GoalScored,   // 进球后暂停
        RoundOver,    // 单局结束
        GameOver,     // 整场比赛结束
        Paused        // 暂停
    }
    public GameState currentState = GameState.Countdown;

    // === 当前局比分 ===
    private int player1RoundScore = 0;
    private int player2RoundScore = 0;

    // === 总局比分（Best of 3） ===
    private int player1SetScore = 0;
    private int player2SetScore = 0;
    private int currentRound = 1; // 当前是第几局

    // === 计时器 ===
    private float roundTimer = 0f;         // 单局剩余时间
    private float goalPauseTimer = 0f;
    private float countdownTimer = 0f;

    // === 引用缓存 ===
    private PlayerController player1;
    private PlayerController player2;
    private Ball ball;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        // 自动修复物理边界：球门后方添加拦网，防止角色/足球掉出场景
        if (FindObjectsByType<FieldBoundsFixer>(FindObjectsSortMode.None).Length == 0)
        {
            gameObject.AddComponent<FieldBoundsFixer>();
        }
    }

    void Start()
    {
        InitializeGame();
    }

    // ==================== 初始化 ====================

    void InitializeGame()
    {
        // 读取玩家在菜单中的选择
        int p1Char = PlayerPrefs.GetInt("P1Character", 0);
        int p2Char = PlayerPrefs.GetInt("P2Character", 1);
        int bgIndex = PlayerPrefs.GetInt("SelectedBackground", 0);

        // 初始化比分和局数
        player1SetScore = 0;
        player2SetScore = 0;
        currentRound = 1;

        SetupBackground(bgIndex);

        // 查找场景中的玩家和球
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        foreach (var p in players)
        {
            if (p.playerIndex == 0) player1 = p;
            else if (p.playerIndex == 1) player2 = p;
        }

        ball = FindObjectOfType<Ball>();

        // 应用角色数据
        ApplyCharacterData(0, p1Char);
        ApplyCharacterData(1, p2Char);

        // 初始位置复位
        ResetPositions();

        // 更新UI
        UIManager.Instance?.UpdateRoundScore(0, 0);
        UIManager.Instance?.UpdateSetScore(0, 0);
        UIManager.Instance?.UpdateRoundNumber(1);

        // 开始倒计时
        StartCountdown();
    }

    void ApplyCharacterData(int playerIndex, int charIndex)
    {
        CharacterData data = CharacterDatabase.Get(charIndex);
        PlayerController player = playerIndex == 0 ? player1 : player2;
        if (player == null || data == null) return;

        player.playerColor = data.characterColor;
        player.SetSkillType(data.skillType);

        // 应用角色视觉
        PlayerAnimator anim = player.GetComponent<PlayerAnimator>();
        if (anim != null) anim.LoadCharacter(data);
    }

    void SetupBackground(int bgIndex)
    {
        BackgroundData bg = BackgroundDatabase.Get(bgIndex);
        if (bg == null || bg.layerPaths == null) return;

        float fieldWidth = 20f;
        float fieldHeight = 12f;

        // 清除旧的背景层
        GameObject oldBg = GameObject.Find("_DynamicBackground");
        if (oldBg != null) Destroy(oldBg);

        GameObject bgRoot = new GameObject("_DynamicBackground");

        for (int i = 0; i < bg.layerPaths.Length; i++)
        {
            Sprite sprite = PlayerAnimator.LoadSingleSprite(bg.layerPaths[i]);
            if (sprite == null) continue;

            GameObject layer = new GameObject($"BGLayer_{i}");
            layer.transform.SetParent(bgRoot.transform);
            layer.transform.position = new Vector3(0, bg.layerYOffsets[i], 10f + i * 3f);

            SpriteRenderer sr = layer.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -20 + i;
            float alpha = (bg.layerAlphas != null && i < bg.layerAlphas.Length) ? bg.layerAlphas[i] : 0.7f;
            sr.color = new Color(1f, 1f, 1f, alpha);

            // 缩放背景到合适的尺寸
            float targetW = fieldWidth * 2f;
            float targetH = fieldHeight * 1.5f;
            float sx = targetW / sprite.bounds.size.x;
            float sy = targetH / sprite.bounds.size.y;
            layer.transform.localScale = new Vector3(sx, sy, 1);
        }
    }

    // ==================== 更新循环 ====================

    void Update()
    {
        switch (currentState)
        {
            case GameState.Countdown:   UpdateCountdown();  break;
            case GameState.Playing:     UpdatePlaying();    break;
            case GameState.GoalScored:  UpdateGoalPause();  break;
            case GameState.RoundOver:   HandleRoundOverInput(); break;
            case GameState.GameOver:    HandleGameOverInput();  break;
            case GameState.Paused:      HandlePauseInput();     break;
        }

        // ESC 暂停
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Playing)
                PauseGame();
            else if (currentState == GameState.Paused)
                ResumeGame();
        }
    }

    // ==================== 倒计时 ====================

    void StartCountdown()
    {
        currentState = GameState.Countdown;
        countdownTimer = countdownDuration;
        UIManager.Instance?.ShowCountdown((int)countdownTimer);
    }

    void UpdateCountdown()
    {
        countdownTimer -= Time.deltaTime;
        int display = Mathf.CeilToInt(countdownTimer);
        UIManager.Instance?.ShowCountdown(display);

        if (countdownTimer <= 0)
        {
            currentState = GameState.Playing;
            roundTimer = roundDuration;
            UIManager.Instance?.HideCountdown();
        }
    }

    // ==================== 对战中 ====================

    void UpdatePlaying()
    {
        // 更新单局倒计时
        roundTimer -= Time.deltaTime;
        UIManager.Instance?.UpdateTimer(Mathf.CeilToInt(roundTimer));

        // 更新能量条（用于技能冷却显示）
        if (player1 != null) UIManager.Instance?.UpdateEnergyBar(0, player1.GetSkillCooldownPercent());
        if (player2 != null) UIManager.Instance?.UpdateEnergyBar(1, player2.GetSkillCooldownPercent());

        // 时间到 → 单局结束
        if (roundTimer <= 0)
        {
            EndRound();
        }
    }

    // ==================== 进球处理 ====================

    /// <summary>
    /// 由 GoalZone 触发，通知管理器进球
    /// </summary>
    /// <param name="scoringPlayerIndex">得分的玩家索引（0=P1, 1=P2）</param>
    public void OnGoalScored(int scoringPlayerIndex)
    {
        if (currentState != GameState.Playing) return;

        // 累加当前局比分
        if (scoringPlayerIndex == 0)
            player1RoundScore++;
        else
            player2RoundScore++;

        // 更新UI
        UIManager.Instance?.UpdateRoundScore(player1RoundScore, player2RoundScore);
        UIManager.Instance?.ShowGoalText(scoringPlayerIndex);

        // 屏幕震动
        ScreenShake.Instance?.Shake(0.3f, 0.2f);

        // 进球暂停
        currentState = GameState.GoalScored;
        goalPauseTimer = goalPauseDuration;

        // 停止球
        if (ball != null)
        {
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }

        // 检查是否有人先到 winGoalCount（3分）
        if (player1RoundScore >= winGoalCount || player2RoundScore >= winGoalCount)
        {
            // 进球暂停结束后直接结束本局（暂停期间不继续计时）
            // pause 结束后在 UpdateGoalPause 里调 EndRound
        }
    }

    void UpdateGoalPause()
    {
        goalPauseTimer -= Time.deltaTime;

        if (goalPauseTimer <= 0)
        {
            // 检查是否因为达到3分而结束本局
            if (player1RoundScore >= winGoalCount || player2RoundScore >= winGoalCount)
            {
                EndRound();
            }
            else
            {
                // 进球后复位，继续
                ResetPositions();
                StartCountdown();
            }
        }
    }

    // ==================== 单局结束 ====================

    /// <summary>
    /// 结束当前局，判定谁赢了这一局
    /// </summary>
    void EndRound()
    {
        currentState = GameState.RoundOver;

        // 判定本局胜者
        int roundWinner = -1; // -1=平局
        if (player1RoundScore > player2RoundScore) roundWinner = 0;
        else if (player2RoundScore > player1RoundScore) roundWinner = 1;

        if (roundWinner >= 0)
        {
            if (roundWinner == 0) player1SetScore++;
            else player2SetScore++;

            UIManager.Instance?.UpdateSetScore(player1SetScore, player2SetScore);
        }

        // 停止所有物体运动
        if (ball != null)
        {
            var rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null) rb.velocity = Vector2.zero;
        }
        if (player1 != null) player1.Freeze();
        if (player2 != null) player2.Freeze();

        // 显示本局结果
        string roundResult;
        if (roundWinner == 0) roundResult = $"P1 wins Round {currentRound}!";
        else if (roundWinner == 1) roundResult = $"P2 wins Round {currentRound}!";
        else roundResult = $"Round {currentRound} — Draw!";

        UIManager.Instance?.ShowRoundOver(roundResult, player1RoundScore, player2RoundScore);

        // 检查是否有人赢得了整个比赛（三局两胜制，先赢2局者胜）
        if (player1SetScore >= winSetCount || player2SetScore >= winSetCount)
        {
            // 比赛结束
            EndMatch();
            return;
        }

        // 检查是否已达到最大局数上限（3局），但无人赢得比赛（理论上不会发生，但以防万一）
        if (currentRound >= 3)
        {
            // 如果三局打完还无人达到2胜，则比较总局分
            if (player1SetScore == player2SetScore && (player1SetScore > 0 || player2SetScore > 0))
            {
                // 理论上已经有人在上面赋值了 setScore，无需额外处理
            }
            EndMatch();
            return;
        }
    }

    /// <summary>
    /// 单局结束后按任意键继续下一局
    /// </summary>
    void HandleRoundOverInput()
    {
        if (Input.anyKeyDown)
        {
            StartNextRound();
        }
    }

    /// <summary>
    /// 开始下一局
    /// </summary>
    void StartNextRound()
    {
        currentRound++;
        player1RoundScore = 0;
        player2RoundScore = 0;

        UIManager.Instance?.UpdateRoundScore(0, 0);
        UIManager.Instance?.UpdateRoundNumber(currentRound);
        UIManager.Instance?.HideRoundOver();

        // 重置玩家技能
        if (player1 != null) player1.ResetSkillCooldown();
        if (player2 != null) player2.ResetSkillCooldown();

        ResetPositions();
        StartCountdown();
    }

    // ==================== 比赛结束 ====================

    void EndMatch()
    {
        currentState = GameState.GameOver;

        string winner;
        int finalP1Score = player1SetScore;
        int finalP2Score = player2SetScore;

        if (player1SetScore > player2SetScore)
            winner = "Player 1";
        else if (player2SetScore > player1SetScore)
            winner = "Player 2";
        else
            winner = "Draw";

        UIManager.Instance?.ShowGameOver(winner,
            player1RoundScore, player2RoundScore,
            finalP1Score, finalP2Score);
    }

    void HandleGameOverInput()
    {
        // [R] 重新开始全部比赛
        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        // [Q] 返回主菜单
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }

    // ==================== 暂停 ====================

    void PauseGame()
    {
        currentState = GameState.Paused;
        Time.timeScale = 0f;
        UIManager.Instance?.ShowPauseMenu();
    }

    public void ResumeGame()
    {
        currentState = GameState.Playing;
        Time.timeScale = 1f;
        UIManager.Instance?.HidePauseMenu();
    }

    void HandlePauseInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ResumeGame();
    }

    // ==================== 复位 ====================

    void ResetPositions()
    {
        if (player1 != null)
        {
            player1.ResetPosition(player1SpawnPoint.position);
            player1.Unfreeze();
        }
        if (player2 != null)
        {
            player2.ResetPosition(player2SpawnPoint.position);
            player2.Unfreeze();
        }
        if (ball != null) ball.ResetBall(ballSpawnPoint.position);
    }

    // ==================== 公开查询方法 ====================

    public GameState GetCurrentState() => currentState;
    public int GetPlayer1RoundScore() => player1RoundScore;
    public int GetPlayer2RoundScore() => player2RoundScore;
    public int GetPlayer1SetScore() => player1SetScore;
    public int GetPlayer2SetScore() => player2SetScore;
    public int GetCurrentRound() => currentRound;
    public float GetRoundTimer() => roundTimer;
    public PlayerController GetPlayer(int index) => index == 0 ? player1 : player2;
}
