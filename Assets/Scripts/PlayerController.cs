using UnityEngine;
using System.Collections;

/// <summary>
/// 玩家控制器 — 移动、跳跃、蓄力踢球（三种传球模式）、技能
/// 蓄力系统：F(或Keypad0)按住蓄力，松开出球
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("玩家设置")]
    public int playerIndex = 0;

    [Header("移动参数")]
    public float moveSpeed = 8f;
    public float jumpForce = 12f;

    [Header("踢球参数")]
    public float kickForce = 15f;
    public float kickRange = 1.2f;
    public float kickCooldown = 0.3f;

    [Header("空中踢球参数（跳跃中）")]
    public float airKickForce = 13f;
    public float airKickRange = 1.4f;
    public float airKickVerticalBias = 0.3f;
    public float airSpinMultiplier = 1.4f;

    [Header("头球参数（跳跃+球较高时）")]
    public float headerForce = 16f;

    [Header("弧线球参数")]
    [Tooltip("踢球时spin输入灵敏度")]
    public float spinInputSensitivity = 1.2f;

    [Header("蓄力系统（三种传球模式）")]
    [Tooltip("蓄力时间低于此值视为轻点→普通直传")]
    public float tapThreshold = 0.18f;
    [Tooltip("蓄力满所需时间")]
    public float chargeMaxTime = 1.2f;
    [Tooltip("蓄力满时额外力量倍率")]
    public float chargePowerBonus = 1.4f;
    [Tooltip("蓄力时直传(无方向)的额外力量倍率")]
    public float drivenPowerBonus = 1.2f;

    [Header("蓄力指示器")]
    public bool showChargeIndicator = true;
    public Color chargeDefaultColor = Color.white;
    public Color chargeLobColor = Color.yellow;
    public Color chargeCurveColor = Color.cyan;
    public float chargeIndicatorYOffset = 1.2f;

    [Header("技能系统")]
    public SkillType currentSkill = SkillType.PowerShot;
    public float skillCooldownDuration = 5f;
    private float skillCooldownTimer = 0f;

    [Header("分裂球技能")]
    [Tooltip("复制球相对主球的前进偏移角度")]
    public float splitFanAngle = 15f;
    [Tooltip("复制球速度占主球速度的比例")]
    [Range(0.5f, 1f)]
    public float splitPowerRatio = 0.8f;
    [Tooltip("分裂球技能持续时间")]
    public float splitBallDuration = 5f;
    public Color splitBallColor = new Color(0.7f, 0f, 1f);

    [Header("角色属性")]
    public Color playerColor = Color.red;
    public string playerName = "Player";

    [Header("地面检测")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer = -1;

    // 组件缓存
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private TrailRenderer trailRenderer;
    private PlayerAnimator playerAnimator;

    // 移动状态
    private float kickTimer = 0f;
    private bool facingRight = true;
    private bool isGrounded;
    private bool isFrozen = false;

    // 技能状态
    private float originalKickForce;
    private bool isSpeedBoosted = false;
    private bool hasShield = false;
    private GameObject shieldVisual;

    private GameObject powerShotEffect;

    // === 蓄力系统状态 ===
    private bool isCharging = false;
    private float chargeTimer = 0f;
    private bool wHeldDuringCharge = false;
    private bool sHeldDuringCharge = false;
    private GameObject chargeIndicator;

    // === 分裂球技能状态 ===
    private bool isSplitBallActive = false;
    private GameObject splitBallEffect;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        trailRenderer = GetComponent<TrailRenderer>();
        playerAnimator = GetComponent<PlayerAnimator>();
        originalKickForce = kickForce;

        CreateChargeIndicator();
    }

    void Start()
    {
        rb.gravityScale = 2f;
        rb.freezeRotation = true;

        if (spriteRenderer != null)
            spriteRenderer.color = playerColor;

        if (playerIndex == 1)
        {
            facingRight = false;
            if (playerAnimator != null) playerAnimator.SetFlipX(true);
            else if (spriteRenderer != null) spriteRenderer.flipX = true;
        }

        if (groundCheck == null)
        {
            GameObject gc = new GameObject("GroundCheck");
            gc.transform.SetParent(transform);
            gc.transform.localPosition = new Vector3(0, -0.6f, 0);
            groundCheck = gc.transform;
        }
    }

    void Update()
    {
        if (isFrozen)
        {
            if (playerAnimator != null) playerAnimator.PlayIdle();
            return;
        }

        if (GameManager.Instance != null)
        {
            var state = GameManager.Instance.GetCurrentState();
            if (state != GameManager.GameState.Playing)
            {
                if (playerAnimator != null) playerAnimator.PlayIdle();
                return;
            }
        }

        if (skillCooldownTimer > 0f)
            skillCooldownTimer -= Time.deltaTime;

        kickTimer -= Time.deltaTime;

        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // === 蓄力系统 ===
        HandleKickCharge();

        // === 跳跃（仍为KeyDown，不受蓄力影响） ===
        if (GetJumpKeyDown() && isGrounded && !isCharging)
        {
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            if (playerAnimator != null) playerAnimator.PlayJump();
        }

        if (GetSkillKeyDown() && skillCooldownTimer <= 0f)
            ActivateSkill();

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        if (isFrozen) return;

        if (GameManager.Instance != null)
        {
            var state = GameManager.Instance.GetCurrentState();
            if (state != GameManager.GameState.Playing) return;
        }

        MovePlayer();
    }

    // ==================== 蓄力系统 ====================

    void HandleKickCharge()
    {
        if (isCharging)
        {
            // 蓄力中
            if (GetKickKey())
            {
                // 仍在按住→持续蓄力
                chargeTimer += Time.deltaTime;

                // 记录蓄力期间的垂直方向输入（用于判断传球类型）
                float v = GetVerticalInput();
                if (v > 0.5f) wHeldDuringCharge = true;
                if (v < -0.5f) sHeldDuringCharge = true;

                // 限制最大蓄力时间
                chargeTimer = Mathf.Min(chargeTimer, chargeMaxTime);

                UpdateChargeIndicator(true);
            }
            else
            {
                // 松手→执行蓄力踢球
                if (kickTimer <= 0)
                {
                    ExecuteChargedKick();
                }
                CancelCharge();
            }
        }
        else
        {
            // 非蓄力中→检测起手
            if (GetKickKeyDown() && kickTimer <= 0)
            {
                StartCharge();
            }
        }
    }

    void StartCharge()
    {
        isCharging = true;
        chargeTimer = 0f;
        wHeldDuringCharge = false;
        sHeldDuringCharge = false;
        UpdateChargeIndicator(true);
    }

    void CancelCharge()
    {
        isCharging = false;
        chargeTimer = 0f;
        UpdateChargeIndicator(false);
    }

    void ExecuteChargedKick()
    {
        // 根据蓄力时间和垂直输入确定传球类型
        PassType passType;

        if (chargeTimer < tapThreshold)
        {
            // 轻点→普通直传
            passType = PassType.Straight;
        }
        else
        {
            // 蓄力后根据方向键选择
            if (wHeldDuringCharge)
                passType = PassType.Lob;
            else if (sHeldDuringCharge)
                passType = PassType.Curve;
            else
                passType = PassType.Straight; // 无方向=大力直传
        }

        DoKick(passType);
        kickTimer = kickCooldown;
    }

    // ==================== 输入映射 ====================

    float GetHorizontalInput()
    {
        if (playerIndex == 0)
        {
            if (Input.GetKey(KeyCode.A)) return -1f;
            if (Input.GetKey(KeyCode.D)) return 1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow)) return -1f;
            if (Input.GetKey(KeyCode.RightArrow)) return 1f;
        }
        return 0f;
    }

    float GetVerticalInput()
    {
        if (playerIndex == 0)
        {
            if (Input.GetKey(KeyCode.W)) return 1f;
            if (Input.GetKey(KeyCode.S)) return -1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.UpArrow)) return 1f;
            if (Input.GetKey(KeyCode.DownArrow)) return -1f;
        }
        return 0f;
    }

    bool GetJumpKeyDown()
    {
        return playerIndex == 0 ? Input.GetKeyDown(KeyCode.W) : Input.GetKeyDown(KeyCode.UpArrow);
    }

    bool GetKickKeyDown()
    {
        return playerIndex == 0 ? Input.GetKeyDown(KeyCode.F) : Input.GetKeyDown(KeyCode.Keypad0);
    }

    bool GetKickKey()
    {
        return playerIndex == 0 ? Input.GetKey(KeyCode.F) : Input.GetKey(KeyCode.Keypad0);
    }

    bool GetKickKeyUp()
    {
        return playerIndex == 0 ? Input.GetKeyUp(KeyCode.F) : Input.GetKeyUp(KeyCode.Keypad0);
    }

    bool GetSkillKeyDown()
    {
        return playerIndex == 0 ? Input.GetKeyDown(KeyCode.G) : Input.GetKeyDown(KeyCode.KeypadPeriod);
    }

    // ==================== 移动 ====================

    void MovePlayer()
    {
        float h = GetHorizontalInput();
        float speed = isSpeedBoosted ? moveSpeed * 2f : moveSpeed;

        float targetX = h * speed;
        rb.velocity = new Vector2(
            Mathf.MoveTowards(rb.velocity.x, targetX, speed * 10f * Time.fixedDeltaTime),
            rb.velocity.y
        );

        if (h > 0 && !facingRight)
        {
            facingRight = true;
            if (playerAnimator != null) playerAnimator.SetFlipX(false);
            else if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else if (h < 0 && facingRight)
        {
            facingRight = false;
            if (playerAnimator != null) playerAnimator.SetFlipX(true);
            else if (spriteRenderer != null) spriteRenderer.flipX = true;
        }
    }

    // ==================== 踢球核心逻辑 ====================

    /// <summary>
    /// 统一的踢球入口 — 根据地面/空中状态自动选择行为
    /// </summary>
    void DoKick(PassType passType = PassType.Straight)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, kickRange);
        Ball targetBall = null;
        Rigidbody2D targetRb = null;

        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Ball"))
            {
                targetBall = hit.GetComponent<Ball>();
                targetRb = hit.GetComponent<Rigidbody2D>();
                break;
            }
        }

        if (targetBall == null) return;

        // 蓄力力量加成
        float chargeRatio = Mathf.Clamp01(chargeTimer / chargeMaxTime);
        float currentKickForce = kickForce;

        if (chargeTimer >= tapThreshold)
        {
            if (passType == PassType.Straight)
                currentKickForce *= drivenPowerBonus; // 直传强化
            else
                currentKickForce *= (1f + chargeRatio * (chargePowerBonus - 1f)); // 弧线球按蓄力比例
        }

        float vertInput = GetVerticalInput();

        // 先保存分裂球状态和球的位置（执行踢球前）
        bool willSplit = isSplitBallActive;
        Vector3 ballPos = targetBall.transform.position;

        if (isGrounded)
            GroundKick(targetBall, targetRb, vertInput, passType, currentKickForce);
        else
            AirKick(targetBall, targetRb, vertInput);

        // 分裂球：踢击完成后生成1个复制球
        if (willSplit && targetBall != null)
        {
            SpawnSplitBalls(targetBall, ballPos, passType);
            isSplitBallActive = false;
            CreateSplitBallEffect(false);
        }

        if (playerAnimator != null)
            playerAnimator.PlayKick();
    }

    // ==================== 地面踢球（三种传球模式） ====================

    void GroundKick(Ball ball, Rigidbody2D ballRb, float vertInput, PassType passType, float currentKickForce)
    {
        // 1. 计算基础踢球方向
        Vector2 kickDir = facingRight ? Vector2.right : Vector2.left;

        // 2. 计算旋转（基于垂直输入 + 水平输入）
        float verticalSpin = vertInput * spinInputSensitivity;
        float horizontalSpin = 0f;

        // 水平旋转：根据角色朝向 + 水平方向键
        float hInput = GetHorizontalInput();
        if (Mathf.Abs(hInput) > 0.1f)
        {
            if ((facingRight && hInput < 0) || (!facingRight && hInput > 0))
                horizontalSpin += hInput * 0.6f;
            else
                horizontalSpin += hInput * 0.2f;
        }
        else
        {
            horizontalSpin = Random.Range(-0.2f, 0.2f);
        }

        // 弧线球强化水平旋转
        if (passType == PassType.Curve)
        {
            // 无水平输入时也给予一定基础弧线
            if (Mathf.Abs(horizontalSpin) < 0.3f)
                horizontalSpin = facingRight ? -0.4f : 0.4f;
            // 按反方向时弧线更陡
            if ((facingRight && hInput < 0) || (!facingRight && hInput > 0))
                horizontalSpin *= 1.5f;
        }

        // 高空球保留一定侧旋增加飘忽感
        if (passType == PassType.Lob && Mathf.Abs(horizontalSpin) < 0.1f)
        {
            horizontalSpin = Random.Range(-0.3f, 0.3f);
        }

        // 3. 施加踢击（带传球类型）
        ball.ApplyKick(kickDir * currentKickForce, currentKickForce, horizontalSpin, verticalSpin, passType);

        // 4. 特效
        if (currentKickForce > originalKickForce * 1.5f && ScreenShake.Instance != null)
            ScreenShake.Instance.Shake(0.15f, 0.2f);
    }

    // ==================== 空中踢球 ====================

    void AirKick(Ball ball, Rigidbody2D ballRb, float vertInput)
    {
        float currentKickForce = airKickForce;

        Vector2 ballLocalPos = transform.InverseTransformPoint(ball.transform.position);
        bool ballIsAbove = ballLocalPos.y > 0.3f;

        if (ballIsAbove)
        {
            // === 头球：球在头顶上方 ===
            Vector2 headerDir = facingRight ? Vector2.right : Vector2.left;
            headerDir += Vector2.up * 0.6f;
            headerDir.Normalize();

            float vSpin = Mathf.Max(vertInput, 0.2f) * spinInputSensitivity;
            // 头球默认Straight，但也可用Lob类型增强高度
            PassType headerPassType = vertInput > 0.5f ? PassType.Lob : PassType.Straight;
            ball.ApplyKick(headerDir * headerForce, headerForce, Random.Range(-0.2f, 0.2f), vSpin, headerPassType);
        }
        else
        {
            // === 空中抽射/凌空踢 ===
            Vector2 kickDir = facingRight ? Vector2.right : Vector2.left;

            float vertBias = airKickVerticalBias;
            if (vertInput > 0.1f)
                vertBias = 0.6f;
            else if (vertInput < -0.1f)
                vertBias = -0.2f;

            kickDir += Vector2.up * vertBias;
            kickDir.Normalize();

            float verticalSpin = vertInput * spinInputSensitivity * airSpinMultiplier;

            float horizontalSpin = Random.Range(-0.3f, 0.3f);

            float hInput = GetHorizontalInput();
            if (Mathf.Abs(hInput) > 0.1f)
            {
                if ((facingRight && hInput < 0) || (!facingRight && hInput > 0))
                    horizontalSpin += hInput * 0.6f;
            }

            // 空中踢球判断传球类型
            PassType airPassType = PassType.Straight;
            if (vertInput > 0.5f) airPassType = PassType.Lob;
            else if (vertInput < -0.5f) airPassType = PassType.Curve;

            ball.ApplyKick(kickDir * currentKickForce, currentKickForce, horizontalSpin, verticalSpin, airPassType);

            if (vertInput < -0.1f && ScreenShake.Instance != null)
                ScreenShake.Instance.Shake(0.12f, 0.15f);
        }
    }

    // ==================== 分裂球 ====================

    /// <summary>
    /// 分裂球：踢击后在主球前方 +15° 生成 1 个复制球，形成双轨推进
    /// 方向安全检测确保复制球不会向后飞
    /// </summary>
    void SpawnSplitBalls(Ball mainBall, Vector3 spawnPos, PassType passType)
    {
        Rigidbody2D mainRb = mainBall.GetComponent<Rigidbody2D>();
        if (mainRb == null) return;

        Vector2 mainVelocity = mainRb.velocity;
        Vector2 mainDir = mainVelocity.normalized;
        float mainSpeed = mainVelocity.magnitude;

        if (mainSpeed < 0.5f) return;

        // 复制球方向：主球前进方向向垂直向上偏移 splitFanAngle
        float angleRad = splitFanAngle * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);

        Vector2 cloneDir = new Vector2(
            mainDir.x * cos - mainDir.y * sin,
            mainDir.x * sin + mainDir.y * cos
        );

        // 安全检测：防止旋转导致水平方向反转（高空球大角度时可能发生）
        if (Mathf.Sign(cloneDir.x) != Mathf.Sign(mainDir.x))
        {
            // 方向反转时，强制保持前进方向，仅叠加垂直偏移
            cloneDir = new Vector2(mainDir.x, Mathf.Abs(mainDir.x) * Mathf.Tan(angleRad)).normalized;
        }

        // 复制球速度 = 主球速度 × 力量比例
        float cloneSpeed = mainSpeed * splitPowerRatio;
        Vector2 cloneVelocity = cloneDir * cloneSpeed;

        // 生成复制球（位置 = 球的位置，即从踢球点分裂）
        Ball cloneBall = mainBall.CloneForSplit(spawnPos, cloneVelocity);
        if (cloneBall == null) return;

        // 紫色拖尾以示区分
        TrailRenderer trail = cloneBall.GetComponent<TrailRenderer>();
        if (trail != null)
        {
            trail.startColor = splitBallColor;
            trail.endColor = new Color(splitBallColor.r, splitBallColor.g, splitBallColor.b, 0);
        }

        // 复制球与主球不互相碰撞
        Collider2D mainCol = mainBall.GetComponent<Collider2D>();
        Collider2D cloneCol = cloneBall.GetComponent<Collider2D>();
        if (mainCol != null && cloneCol != null)
            Physics2D.IgnoreCollision(mainCol, cloneCol);
    }

    // ==================== 蓄力指示器 ====================

    void CreateChargeIndicator()
    {
        if (!showChargeIndicator) return;

        chargeIndicator = new GameObject("ChargeIndicator_" + playerIndex);
        chargeIndicator.transform.SetParent(transform);
        chargeIndicator.transform.localPosition = new Vector3(0, chargeIndicatorYOffset, 0);

        SpriteRenderer sr = chargeIndicator.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = chargeDefaultColor;
        sr.sortingOrder = 10;
        chargeIndicator.transform.localScale = Vector3.zero;
        chargeIndicator.SetActive(false);
    }

    void UpdateChargeIndicator(bool visible)
    {
        if (chargeIndicator == null || !showChargeIndicator) return;

        chargeIndicator.SetActive(visible);

        if (!visible) return;

        float chargeRatio = Mathf.Clamp01(chargeTimer / chargeMaxTime);
        float scale = Mathf.Lerp(0.3f, 0.8f, chargeRatio);
        chargeIndicator.transform.localScale = new Vector3(scale, scale, 1f);

        // 根据蓄力方向改变颜色
        if (wHeldDuringCharge)
        {
            chargeIndicator.GetComponent<SpriteRenderer>().color = chargeLobColor;
        }
        else if (sHeldDuringCharge)
        {
            chargeIndicator.GetComponent<SpriteRenderer>().color = chargeCurveColor;
        }
        else
        {
            chargeIndicator.GetComponent<SpriteRenderer>().color = chargeDefaultColor;
        }

        // 蓄力满时闪烁提示
        if (chargeRatio >= 1f)
        {
            float flash = Mathf.Sin(Time.time * 15f) > 0 ? 1f : 0.5f;
            Color c = chargeIndicator.GetComponent<SpriteRenderer>().color;
            c.a = flash;
            chargeIndicator.GetComponent<SpriteRenderer>().color = c;
        }
        else
        {
            Color c = chargeIndicator.GetComponent<SpriteRenderer>().color;
            c.a = Mathf.Lerp(0.5f, 1f, chargeRatio);
            chargeIndicator.GetComponent<SpriteRenderer>().color = c;
        }
    }

    // ==================== 技能系统 ====================

    void ActivateSkill()
    {
        if (skillCooldownTimer > 0f) return;
        skillCooldownTimer = skillCooldownDuration;

        switch (currentSkill)
        {
            case SkillType.PowerShot:   StartCoroutine(PowerShot());   break;
            case SkillType.SpeedBoost:  StartCoroutine(SpeedBoost());  break;
            case SkillType.Shield:      StartCoroutine(Shield());      break;
            case SkillType.SplitBall:   StartCoroutine(SplitBall());   break;
        }

        string skillName = GetSkillName(currentSkill);
        UIManager.Instance?.ShowSkillActivation(playerIndex, skillName);
    }

    string GetSkillName(SkillType type)
    {
        switch (type)
        {
            case SkillType.PowerShot:   return "POWER SHOT!";
            case SkillType.SpeedBoost:  return "SPEED BOOST!";
            case SkillType.Shield:      return "SHIELD!";
            case SkillType.SplitBall:   return "SPLIT BALL!";
            default:                    return "SKILL!";
        }
    }

    IEnumerator PowerShot()
    {
        kickForce = originalKickForce * 2f;
        airKickForce = originalKickForce * 1.8f;

        if (playerAnimator != null) playerAnimator.SetColor(Color.red);
        CreatePowerShotEffect(true);

        float timer = 0f;
        bool used = false;
        while (timer < 5f)
        {
            if (kickTimer > 0 && kickTimer < kickCooldown - 0.05f && !used)
            {
                used = true;
                break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        kickForce = originalKickForce;
        airKickForce = originalKickForce;
        if (playerAnimator != null) playerAnimator.SetColor(playerColor);
        CreatePowerShotEffect(false);
    }

    void CreatePowerShotEffect(bool show)
    {
        if (show)
        {
            if (powerShotEffect == null)
            {
                powerShotEffect = new GameObject("PowerShotEffect");
                powerShotEffect.transform.SetParent(transform);
                powerShotEffect.transform.localPosition = Vector3.zero;

                SpriteRenderer sr = powerShotEffect.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(1f, 0.3f, 0.1f, 0.4f);
                sr.sortingOrder = 7;
                powerShotEffect.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            }
            powerShotEffect.SetActive(true);
        }
        else if (powerShotEffect != null)
        {
            powerShotEffect.SetActive(false);
        }
    }

    /// <summary>
    /// 分裂球技能：下一次踢击生成3个球呈扇形飞出
    /// </summary>
    IEnumerator SplitBall()
    {
        isSplitBallActive = true;

        if (playerAnimator != null) playerAnimator.SetColor(splitBallColor);
        CreateSplitBallEffect(true);

        float timer = 0f;
        bool used = false;
        while (timer < splitBallDuration)
        {
            if (kickTimer > 0 && kickTimer < kickCooldown - 0.05f && !used)
            {
                used = true;
                break;
            }
            timer += Time.deltaTime;
            yield return null;
        }

        isSplitBallActive = false;
        if (playerAnimator != null) playerAnimator.SetColor(playerColor);
        CreateSplitBallEffect(false);
    }

    void CreateSplitBallEffect(bool show)
    {
        if (show)
        {
            if (splitBallEffect == null)
            {
                splitBallEffect = new GameObject("SplitBallEffect_" + playerIndex);
                splitBallEffect.transform.SetParent(transform);
                splitBallEffect.transform.localPosition = Vector3.zero;

                SpriteRenderer sr = splitBallEffect.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(splitBallColor.r, splitBallColor.g, splitBallColor.b, 0.4f);
                sr.sortingOrder = 7;
                splitBallEffect.transform.localScale = new Vector3(1.5f, 1.5f, 1);
            }
            splitBallEffect.SetActive(true);
        }
        else if (splitBallEffect != null)
        {
            splitBallEffect.SetActive(false);
        }
    }

    IEnumerator SpeedBoost()
    {
        isSpeedBoosted = true;

        if (playerAnimator != null) playerAnimator.SetColor(Color.cyan);
        if (trailRenderer != null)
        {
            trailRenderer.startColor = Color.cyan;
            trailRenderer.emitting = true;
        }

        yield return new WaitForSeconds(3f);

        isSpeedBoosted = false;
        if (playerAnimator != null) playerAnimator.SetColor(playerColor);
        if (trailRenderer != null) trailRenderer.emitting = false;
    }

    IEnumerator Shield()
    {
        hasShield = true;
        CreateShieldVisual(true);

        float timer = 0f;
        while (timer < 5f)
        {
            if (!hasShield) break;
            timer += Time.deltaTime;
            yield return null;
        }

        hasShield = false;
        CreateShieldVisual(false);
    }

    void CreateShieldVisual(bool show)
    {
        if (show)
        {
            if (shieldVisual == null)
            {
                shieldVisual = new GameObject("ShieldVisual");
                shieldVisual.transform.SetParent(transform);
                shieldVisual.transform.localPosition = Vector3.zero;

                SpriteRenderer sr = shieldVisual.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(0.3f, 0.6f, 1f, 0.4f);
                sr.sortingOrder = 9;
                shieldVisual.transform.localScale = new Vector3(1.8f, 1.8f, 1);
            }
            shieldVisual.SetActive(true);
        }
        else if (shieldVisual != null)
        {
            shieldVisual.SetActive(false);
        }
    }

    public bool TryBlockWithShield()
    {
        if (hasShield)
        {
            hasShield = false;
            CreateShieldVisual(false);
            if (ScreenShake.Instance != null)
                ScreenShake.Instance.Shake(0.1f, 0.05f);
            return true;
        }
        return false;
    }

    // ==================== 动画 ====================

    void UpdateAnimation()
    {
        if (playerAnimator == null) return;

        float h = GetHorizontalInput();
        bool isMoving = Mathf.Abs(h) > 0.1f;

        if (!isGrounded)
        {
            if (rb.velocity.y > 0.5f)
                playerAnimator.PlayJump();
            else if (rb.velocity.y < -0.5f)
                playerAnimator.PlayFall();
        }
        else if (isMoving)
        {
            playerAnimator.PlayRun();
        }
        else
        {
            playerAnimator.PlayIdle();
        }
    }

    // ==================== 公开接口 ====================

    public void Freeze()
    {
        isFrozen = true;
        rb.velocity = Vector2.zero;
        if (isCharging) CancelCharge();
    }

    public void Unfreeze()
    {
        isFrozen = false;
    }

    public void ResetPosition(Vector2 pos)
    {
        transform.position = pos;
        rb.velocity = Vector2.zero;
        isFrozen = false;
        isSpeedBoosted = false;
        hasShield = false;
        StopAllCoroutines();
        kickForce = originalKickForce;
        airKickForce = originalKickForce;

        if (isCharging) CancelCharge();

        if (playerAnimator != null)
        {
            playerAnimator.SetColor(playerColor);
            playerAnimator.ResetToIdle();
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.color = playerColor;
        }
        if (trailRenderer != null) trailRenderer.emitting = false;
        CreatePowerShotEffect(false);
        CreateShieldVisual(false);
        CreateSplitBallEffect(false);
        isSplitBallActive = false;
    }

    public void ResetSkillCooldown()
    {
        skillCooldownTimer = 0f;
    }

    public float GetSkillCooldownPercent()
    {
        if (skillCooldownTimer <= 0f) return 1f;
        return 1f - (skillCooldownTimer / skillCooldownDuration);
    }

    public void SetSkillType(SkillType type)
    {
        currentSkill = type;
    }

    public bool IsShieldActive() => hasShield;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!hasShield) return;
        if (!collision.gameObject.CompareTag("Ball")) return;

        Rigidbody2D ballRb = collision.gameObject.GetComponent<Rigidbody2D>();
        if (ballRb == null) return;

        if (ballRb.velocity.magnitude > 5f)
        {
            hasShield = false;
            CreateShieldVisual(false);

            Vector2 dir = (collision.transform.position - transform.position).normalized;
            ballRb.velocity = dir * 3f;

            if (ScreenShake.Instance != null)
                ScreenShake.Instance.Shake(0.15f, 0.08f);

            if (spriteRenderer != null)
                StartCoroutine(FlashWhite());
        }
    }

    IEnumerator FlashWhite()
    {
        spriteRenderer.color = Color.white;
        yield return new WaitForSeconds(0.1f);
        spriteRenderer.color = playerColor;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, kickRange);
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }

    static Sprite circleSpriteCache;

    Sprite CreateCircleSprite()
    {
        if (circleSpriteCache != null) return circleSpriteCache;

        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        float center = size / 2f;
        float radius = size / 2.2f;

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
        circleSpriteCache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSpriteCache;
    }
}

public enum SkillType
{
    PowerShot,
    SpeedBoost,
    Shield,
    SplitBall
}
