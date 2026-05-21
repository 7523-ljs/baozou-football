using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI管理器 — 负责所有游戏内UI显示：
/// 计分板、计时器、局数、技能冷却、暂停/结束面板
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("比分（当前局）")]
    public TextMeshProUGUI player1ScoreText;
    public TextMeshProUGUI player2ScoreText;

    [Header("总局比分（三局两胜）")]
    public TextMeshProUGUI setScoreText;        // 如 "Sets: 1 - 0"
    public TextMeshProUGUI roundNumberText;     // 如 "Round 1/3"

    [Header("计时器")]
    public TextMeshProUGUI timerText;           // 当前局剩余时间

    [Header("进球提示")]
    public TextMeshProUGUI goalText;
    public CanvasGroup goalTextCanvas;
    public float goalTextDuration = 2f;

    [Header("倒计时")]
    public TextMeshProUGUI countdownText;

    [Header("技能冷却条")]
    public Slider player1EnergyBar;  // 复用作技能冷却条
    public Slider player2EnergyBar;

    [Header("技能就绪提示")]
    public TextMeshProUGUI player1SkillReady;
    public TextMeshProUGUI player2SkillReady;

    [Header("技能冷却文字")]
    public TextMeshProUGUI p1CooldownText;
    public TextMeshProUGUI p2CooldownText;

    [Header("暂停菜单")]
    public GameObject pauseMenuPanel;

    [Header("单局结束面板")]
    public GameObject roundOverPanel;
    public TextMeshProUGUI roundOverText;
    public TextMeshProUGUI roundScoreText;

    [Header("整场比赛结束面板")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI winnerText;
    public TextMeshProUGUI finalScoreText;

    // 内部状态
    private float goalTextTimer = 0f;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 初始化隐藏所有面板
        if (goalTextCanvas != null) goalTextCanvas.alpha = 0;
        if (goalText != null) goalText.gameObject.SetActive(false);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (roundOverPanel != null) roundOverPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        // 进球文字淡出效果
        if (goalTextTimer > 0)
        {
            goalTextTimer -= Time.deltaTime;
            if (goalTextCanvas != null)
                goalTextCanvas.alpha = Mathf.Clamp01(goalTextTimer / 0.5f);

            if (goalTextTimer <= 0 && goalText != null)
                goalText.gameObject.SetActive(false);
        }
    }

    // ==================== 计分板 ====================

    /// <summary>更新当前局比分</summary>
    public void UpdateRoundScore(int p1Score, int p2Score)
    {
        if (player1ScoreText != null) player1ScoreText.text = p1Score.ToString();
        if (player2ScoreText != null) player2ScoreText.text = p2Score.ToString();
    }

    /// <summary>更新总局比分（三局两胜）</summary>
    public void UpdateSetScore(int p1Sets, int p2Sets)
    {
        if (setScoreText != null)
            setScoreText.text = $"Sets: {p1Sets} - {p2Sets}";
    }

    /// <summary>更新当前局数显示</summary>
    public void UpdateRoundNumber(int round)
    {
        if (roundNumberText != null)
            roundNumberText.text = $"Round {round}/3";
    }

    /// <summary>更新计时器显示</summary>
    public void UpdateTimer(int seconds)
    {
        if (timerText != null)
            timerText.text = seconds.ToString();
    }

    // ==================== 进球提示 ====================

    public void ShowGoalText(int scoringPlayerIndex)
    {
        if (goalText == null) return;

        string[] messages = {
            "GOOOAL!", "NICE SHOT!",
            "PERFECT KICK!", "WONDER GOAL!", "SUPER RAGE!"
        };

        string message = messages[Random.Range(0, messages.Length)];
        goalText.text = $"PLAYER {scoringPlayerIndex + 1}\n{message}";
        goalText.gameObject.SetActive(true);

        if (goalTextCanvas != null) goalTextCanvas.alpha = 1f;
        goalTextTimer = goalTextDuration;

        StartCoroutine(GoalTextAnimation());
    }

    System.Collections.IEnumerator GoalTextAnimation()
    {
        float duration = 0.3f;
        float timer = 0f;
        Vector3 originalScale = goalText.transform.localScale;

        while (timer < duration)
        {
            float scale = Mathf.Lerp(2f, 1f, timer / duration);
            goalText.transform.localScale = originalScale * scale;
            timer += Time.deltaTime;
            yield return null;
        }
        goalText.transform.localScale = originalScale;
    }

    // ==================== 倒计时 ====================

    public void ShowCountdown(int count)
    {
        if (countdownText == null) return;
        countdownText.gameObject.SetActive(true);
        countdownText.text = count > 0 ? count.ToString() : "FIGHT!";
        StartCoroutine(CountdownPulse());
    }

    System.Collections.IEnumerator CountdownPulse()
    {
        float duration = 0.5f;
        float timer = 0f;
        Vector3 originalScale = countdownText.transform.localScale;

        while (timer < duration)
        {
            float scale = Mathf.Lerp(1.5f, 1f, timer / duration);
            countdownText.transform.localScale = originalScale * scale;
            timer += Time.deltaTime;
            yield return null;
        }
        countdownText.transform.localScale = originalScale;
    }

    public void HideCountdown()
    {
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    // ==================== 技能冷却 ====================

    /// <summary>
    /// 更新技能冷却条（百分比）
    /// </summary>
    public void UpdateEnergyBar(int playerIndex, float percent)
    {
        Slider bar = playerIndex == 0 ? player1EnergyBar : player2EnergyBar;
        TextMeshProUGUI skillReady = playerIndex == 0 ? player1SkillReady : player2SkillReady;
        TextMeshProUGUI cooldownText = playerIndex == 0 ? p1CooldownText : p2CooldownText;

        if (bar != null)
        {
            bar.value = percent;
            Image fillImage = bar.fillRect?.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = percent >= 1f ? Color.yellow : Color.green;
        }

        // 显示"就绪"或"冷却中"
        if (skillReady != null)
        {
            skillReady.gameObject.SetActive(percent >= 1f);
            if (percent >= 1f)
            {
                skillReady.text = playerIndex == 0 ? "[G] READY!" : "[.] READY!";
                skillReady.color = Mathf.Sin(Time.time * 10f) > 0 ? Color.yellow : Color.white;
            }
        }

        if (cooldownText != null)
        {
            if (percent < 1f)
            {
                float remain = (1f - percent) * 5f; // 假设总冷却5秒
                cooldownText.text = $"{remain:F1}s";
                cooldownText.gameObject.SetActive(true);
            }
            else
            {
                cooldownText.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>显示技能释放提示（复用了goal文字的UI区域）</summary>
    public void ShowSkillActivation(int playerIndex, string skillName)
    {
        if (goalText != null)
        {
            goalText.text = $"PLAYER {playerIndex + 1}\n{skillName}!";
            goalText.gameObject.SetActive(true);
            goalTextTimer = 1f;
            if (goalTextCanvas != null) goalTextCanvas.alpha = 1f;
        }
    }

    // ==================== 单局结束面板 ====================

    /// <summary>
    /// 显示单局结算界面
    /// </summary>
    public void ShowRoundOver(string result, int p1Score, int p2Score)
    {
        if (roundOverPanel != null) roundOverPanel.SetActive(true);
        if (roundOverText != null) roundOverText.text = result;
        if (roundScoreText != null)
            roundScoreText.text = $"Score: {p1Score} - {p2Score}";
    }

    public void HideRoundOver()
    {
        if (roundOverPanel != null) roundOverPanel.SetActive(false);
    }

    // ==================== 暂停面板 ====================

    public void ShowPauseMenu()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    public void HidePauseMenu()
    {
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
    }

    // ==================== 比赛结束面板 ====================

    /// <summary>
    /// 显示整场比赛结束界面（含三局两胜总比分）
    /// </summary>
    public void ShowGameOver(string winner, int roundP1, int roundP2, int setP1, int setP2)
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (winnerText != null) winnerText.text = winner == "Draw" ? "DRAW!" : $"{winner} WINS!";

        if (finalScoreText != null)
        {
            bool isDraw = winner == "Draw";
            if (isDraw)
                finalScoreText.text = $"Final Sets\n{setP1} - {setP2}\n\nLast Round\n{roundP1} - {roundP2}";
            else
                finalScoreText.text = $"Sets: {setP1} - {setP2}\n\nRound Score\n{roundP1} - {roundP2}";
        }
    }

    // ==================== 按钮回调 ====================

    public void OnRestartButton()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    public void OnMainMenuButton()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void OnResumeButton()
    {
        GameManager.Instance?.ResumeGame();
    }
}
