using UnityEngine;

/// <summary>
/// 传球类型枚举
/// Straight = 普通直传，Curve = 低平弧线渗透球，Lob = 高空弧线传中球
/// </summary>
public enum PassType
{
    Straight,
    Curve,
    Lob
}

public class Ball : MonoBehaviour
{
    [Header("物理参数")]
    public float maxSpeed = 20f;
    public float bounciness = 0.7f;
    public float friction = 0.1f;
    public float drag = 0.3f;
    public float gravityScale = 1.5f;

    [Header("弧线球（Spin）参数")]
    [Tooltip("旋转力对球飞行轨迹的影响强度")]
    public float spinEffect = 3f;
    [Tooltip("旋转影响纵向（Y轴）的强度")]
    public float spinVerticalEffect = 4f;
    public float spinDecay = 2f;
    public float maxSpin = 10f;
    [Tooltip("旋转影响球旋转速度的倍率")]
    public float spinVisualFactor = 50f;

    [Header("弧线球增强参数")]
    [Tooltip("弧线球水平旋转产生的垂直弯曲力倍率（大幅提升曲率）")]
    public float curveLateralForce = 6f;
    [Tooltip("弧线球最大弯曲力上限，防止过于超模")]
    public float curveMaxForce = 35f;
    [Tooltip("弧线球初始向上倾斜角度比例(0~1)")]
    public float curveUpAngle = 0.2f;

    [Header("高空球参数")]
    [Tooltip("高空球初始向上倾斜角度比例")]
    public float lobUpAngle = 0.65f;
    [Tooltip("高空球重力缩放（越小飞得越高）")]
    public float lobGravityScale = 0.7f;
    [Tooltip("高空球回旋升力")]
    public float lobBackspinLift = 10f;
    [Tooltip("高空球最高高度限制")]
    public float lobMaxHeight = 8f;
    [Tooltip("高空球水平阻力加成，减缓水平速度留出争顶时间")]
    public float lobDragBonus = 0.4f;

    [Header("轨迹顺滑")]
    [Tooltip("旋转值平滑衰减时间（秒），消除中途突然变直")]
    public float spinSmoothTime = 0.35f;
    [Tooltip("普通传球spin快速衰减倍率（越小直传越干净）")]
    public float straightSpinDecayFactor = 0.3f;

    [Header("拖尾效果")]
    public float trailSpeedThreshold = 8f;
    public Color normalTrailColor = new Color(1, 1, 1, 0.5f);
    public Color fastTrailColor = new Color(1, 0.5f, 0, 1f);
    public Color superFastTrailColor = new Color(1, 0, 0, 1f);
    public Color curveTrailColor = new Color(0.3f, 0.8f, 1f, 0.8f);
    public Color lobTrailColor = new Color(1f, 0.8f, 0.2f, 0.9f);

    [Header("自动重置")]
    public float stuckSpeedThreshold = 0.5f;
    public float stuckResetTime = 3f;
    [Tooltip("高空球卡死检测额外宽容时间")]
    public float lobStuckExtraTime = 2f;

    // === 运行时状态 ===
    private Rigidbody2D rb;
    private TrailRenderer trailRenderer;
    private SpriteRenderer spriteRenderer;
    private Vector2 startPos;
    private PhysicsMaterial2D bouncyMaterial;
    private float stuckTimer = 0f;

    // === 弧线球状态 ===
    private float currentSpin = 0f;
    private float currentVerticalSpin = 0f;
    private bool hasSpin = false;

    // === 增强系统运行时状态 ===
    private PassType currentPassType = PassType.Straight;
    private float defaultGravityScale;
    private float defaultDrag;
    private float spinVelocity;      // SmoothDamp 用
    private float vertSpinVelocity;  // SmoothDamp 用
    private bool lobHasTriggeredDescent = false;  // 高空球是否已进入下降阶段
    public bool isCloneBall = false;              // 是否为分裂球克隆体

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        trailRenderer = GetComponent<TrailRenderer>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        defaultGravityScale = gravityScale;
        defaultDrag = drag;
    }

    void Start()
    {
        startPos = transform.position;

        bouncyMaterial = new PhysicsMaterial2D("BallBounce");
        bouncyMaterial.bounciness = bounciness;
        bouncyMaterial.friction = friction;

        CircleCollider2D col = GetComponent<CircleCollider2D>();
        if (col != null) col.sharedMaterial = bouncyMaterial;

        rb.drag = drag;
        rb.gravityScale = gravityScale;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (trailRenderer != null)
        {
            trailRenderer.startColor = normalTrailColor;
            trailRenderer.endColor = new Color(1, 1, 1, 0);
        }
    }

    void FixedUpdate()
    {
        ApplySpinForces();
        ClampSpeed();
        UpdateTrailEffect();
        UpdateRotation();
        CheckStuck();
    }

    // ==================== 弧线球核心物理 ====================

    /// <summary>
    /// 三种传球模式的差异化弧线物理
    /// FixedUpdate中每帧调用，持续施加旋转力
    /// </summary>
    void ApplySpinForces()
    {
        float speed = rb.velocity.magnitude;

        // 速度太低时不再施加spin力，直接快速归零
        if (speed < 0.5f)
        {
            currentSpin = Mathf.SmoothDamp(currentSpin, 0f, ref spinVelocity, 0.1f);
            currentVerticalSpin = Mathf.SmoothDamp(currentVerticalSpin, 0f, ref vertSpinVelocity, 0.1f);
            hasSpin = false;
            return;
        }

        float speedRatio = Mathf.Clamp01(speed / maxSpeed);

        switch (currentPassType)
        {
            case PassType.Curve:
                ApplyCurvePhysics(speedRatio);
                break;
            case PassType.Lob:
                ApplyLobPhysics(speedRatio);
                break;
            default: // Straight
                ApplyStraightPhysics(speedRatio);
                break;
        }
    }

    /// <summary>
    /// 低平弧线渗透球物理：
    /// 强水平旋转 → 马格努斯效应 → 垂直弯曲力
    /// 球路呈明显S形弧线下坠，但整体平快
    /// </summary>
    void ApplyCurvePhysics(float speedRatio)
    {
        // 平滑衰减旋转值
        currentSpin = Mathf.SmoothDamp(currentSpin, 0f, ref spinVelocity, spinSmoothTime);
        currentVerticalSpin = Mathf.SmoothDamp(currentVerticalSpin, 0f, ref vertSpinVelocity, spinSmoothTime);

        // 马格努斯力：水平旋转变垂直弯曲力
        // 速度越大弯曲越明显，实现"先直后弯"的顺滑弧线
        float magnusForce = -currentSpin * curveLateralForce * speedRatio;
        rb.AddForce(new Vector2(0f, magnusForce), ForceMode2D.Force);

        // 垂直旋转叠加：微调上浮/下坠
        float liftComponent = currentVerticalSpin * spinVerticalEffect * 0.5f;
        rb.AddForce(new Vector2(0f, liftComponent), ForceMode2D.Force);

        hasSpin = true;
    }

    /// <summary>
    /// 高空弧线传中球物理：
    /// 大角度向上+回旋升力 → 高抛物线
    /// 分上升/下降两阶段处理
    /// </summary>
    void ApplyLobPhysics(float speedRatio)
    {
        currentSpin = Mathf.SmoothDamp(currentSpin, 0f, ref spinVelocity, spinSmoothTime);
        currentVerticalSpin = Mathf.SmoothDamp(currentVerticalSpin, 0f, ref vertSpinVelocity, spinSmoothTime * 1.5f);

        // 回旋升力：抵消重力、延长滞空
        float lift = currentVerticalSpin * lobBackspinLift * speedRatio;
        rb.AddForce(new Vector2(0f, lift), ForceMode2D.Force);

        // 水平阻力：减缓球速，给防守方反应时间
        float horizontalDragExtra = rb.velocity.x * (1f - Mathf.Clamp01(1f / (1f + lobDragBonus * 0.5f)));
        rb.AddForce(new Vector2(-horizontalDragExtra, 0f), ForceMode2D.Force);

        // === 阶段管理 ===
        if (rb.velocity.y > 0.5f)
        {
            // 上升阶段：轻重力
            rb.gravityScale = defaultGravityScale * lobGravityScale;
            lobHasTriggeredDescent = false;

            // 高度软限制：接近最高点时逐渐减弱升力
            float heightRatio = Mathf.Clamp01((transform.position.y + 2f) / (lobMaxHeight + 2f));
            if (heightRatio > 0.7f)
            {
                float softCap = 1f - (heightRatio - 0.7f) / 0.3f;
                rb.AddForce(new Vector2(0f, -lift * (1f - softCap) * 0.5f), ForceMode2D.Force);
            }
        }
        else if (rb.velocity.y < -0.5f)
        {
            // 下降阶段：恢复重力快速落地
            if (!lobHasTriggeredDescent)
            {
                lobHasTriggeredDescent = true;
            }
            // 逐渐恢复重力的过渡
            float descentProgress = Mathf.Clamp01((-rb.velocity.y) / 8f);
            rb.gravityScale = Mathf.Lerp(
                defaultGravityScale * lobGravityScale,
                defaultGravityScale,
                descentProgress
            );
        }

        hasSpin = Mathf.Abs(currentSpin) > 0.1f || Mathf.Abs(currentVerticalSpin) > 0.1f;
    }

    /// <summary>
    /// 普通直传物理：
    /// spin快速归零，无额外弧线力
    /// 保留基础手感不变
    /// </summary>
    void ApplyStraightPhysics(float speedRatio)
    {
        currentSpin = Mathf.SmoothDamp(currentSpin, 0f, ref spinVelocity, spinSmoothTime * straightSpinDecayFactor);
        currentVerticalSpin = Mathf.SmoothDamp(currentVerticalSpin, 0f, ref vertSpinVelocity, spinSmoothTime * straightSpinDecayFactor);

        rb.gravityScale = defaultGravityScale;

        hasSpin = Mathf.Abs(currentSpin) > 0.1f || Mathf.Abs(currentVerticalSpin) > 0.1f;
    }

    void ClampSpeed()
    {
        if (rb.velocity.magnitude > maxSpeed)
            rb.velocity = rb.velocity.normalized * maxSpeed;
    }

    // ==================== 踢球接口 ====================

    /// <summary>
    /// 完整踢球接口：带传球类型选择
    /// </summary>
    public void ApplyKick(Vector2 direction, float power, float horizontalSpin, float verticalSpin, PassType passType = PassType.Straight)
    {
        currentPassType = passType;
        Vector2 finalDir = direction.normalized;

        switch (passType)
        {
            case PassType.Curve:
                // 弧线球：方向略向上倾斜 + 强旋转
                finalDir += Vector2.up * curveUpAngle;
                finalDir.Normalize();
                rb.velocity = finalDir * power;
                currentSpin = Mathf.Clamp(horizontalSpin * 2.5f, -curveMaxForce, curveMaxForce);
                currentVerticalSpin = verticalSpin * 0.4f;
                rb.gravityScale = defaultGravityScale;
                rb.drag = defaultDrag;
                break;

            case PassType.Lob:
                // 高空球：大角度向上 + 强制回旋
                finalDir += Vector2.up * lobUpAngle;
                finalDir.Normalize();
                rb.velocity = finalDir * power * 0.85f;
                currentSpin = Mathf.Clamp(horizontalSpin, -curveMaxForce * 0.3f, curveMaxForce * 0.3f);
                currentVerticalSpin = Mathf.Max(verticalSpin, 0.2f) * 2.5f;
                rb.gravityScale = defaultGravityScale * lobGravityScale;
                rb.drag = defaultDrag + lobDragBonus;
                lobHasTriggeredDescent = false;
                break;

            default: // Straight
                rb.velocity = finalDir * power;
                currentSpin = horizontalSpin * 0.2f;
                currentVerticalSpin = verticalSpin * 0.2f;
                rb.gravityScale = defaultGravityScale;
                rb.drag = defaultDrag;
                break;
        }

        // 重置平滑阻尼速度
        spinVelocity = 0f;
        vertSpinVelocity = 0f;
        hasSpin = Mathf.Abs(currentSpin) > 0.01f || Mathf.Abs(currentVerticalSpin) > 0.01f;

        // 根据传球类型更新拖尾颜色
        if (trailRenderer != null)
        {
            if (passType == PassType.Curve)
                trailRenderer.startColor = curveTrailColor;
            else if (passType == PassType.Lob)
                trailRenderer.startColor = lobTrailColor;
            else
                trailRenderer.startColor = normalTrailColor;
        }
    }

    /// <summary>
    /// 简化接口（向后兼容，默认直传）
    /// </summary>
    public void ApplyKick(Vector2 direction, float power, float horizontalSpin, float verticalSpin)
    {
        ApplyKick(direction, power, horizontalSpin, verticalSpin, PassType.Straight);
    }

    /// <summary>
    /// 最简接口：自动计算方向 + 随机小量旋转
    /// </summary>
    public void ApplyKickSimple(Vector2 targetDirection, float power)
    {
        float randomHS = Random.Range(-0.3f, 0.3f);
        float randomVS = Random.Range(-0.3f, 0.3f);
        ApplyKick(targetDirection.normalized, power, randomHS, randomVS, PassType.Straight);
    }

    // ==================== 视觉效果 ====================

    void UpdateTrailEffect()
    {
        if (trailRenderer == null) return;

        float speed = rb.velocity.magnitude;

        if (currentPassType == PassType.Lob && speed > 3f)
        {
            // 高空球专用拖尾：金色/暖色，持续发光
            trailRenderer.startColor = lobTrailColor;
            trailRenderer.widthMultiplier = Mathf.Lerp(trailRenderer.widthMultiplier, 0.5f, Time.fixedDeltaTime * 4f);
        }
        else if (currentPassType == PassType.Curve && hasSpin && speed > 5f)
        {
            // 弧线球拖尾：蓝色弧光
            trailRenderer.startColor = curveTrailColor;
            trailRenderer.widthMultiplier = Mathf.Lerp(trailRenderer.widthMultiplier, 0.6f, Time.fixedDeltaTime * 5f);
        }
        else if (speed > trailSpeedThreshold * 1.5f)
        {
            trailRenderer.startColor = superFastTrailColor;
            trailRenderer.widthMultiplier = Mathf.Lerp(trailRenderer.widthMultiplier, 0.8f, Time.fixedDeltaTime * 5f);
        }
        else if (speed > trailSpeedThreshold)
        {
            trailRenderer.startColor = fastTrailColor;
            trailRenderer.widthMultiplier = Mathf.Lerp(trailRenderer.widthMultiplier, 0.5f, Time.fixedDeltaTime * 5f);
        }
        else
        {
            trailRenderer.startColor = normalTrailColor;
            trailRenderer.widthMultiplier = Mathf.Lerp(trailRenderer.widthMultiplier, 0.2f, Time.fixedDeltaTime * 5f);
        }

        trailRenderer.emitting = speed > 2f;
    }

    void UpdateRotation()
    {
        if (rb.velocity.magnitude > 0.1f)
        {
            float baseRot = rb.velocity.magnitude * 100f;
            float spinRot = currentSpin * spinVisualFactor;
            transform.Rotate(0, 0, (-baseRot + spinRot) * Time.fixedDeltaTime);
        }
    }

    // ==================== 卡死检测 ====================

    void CheckStuck()
    {
        // 分裂球克隆体：停下来后自动销毁
        if (isCloneBall)
        {
            if (rb.velocity.magnitude < stuckSpeedThreshold * 0.5f)
            {
                stuckTimer += Time.fixedDeltaTime;
                if (stuckTimer >= 2f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            else
            {
                stuckTimer = 0f;
            }
            return;
        }

        float threshold = stuckSpeedThreshold;
        float timeout = stuckResetTime;

        // 高空球在上升阶段容易被误判为卡死，增加宽容时间
        if (currentPassType == PassType.Lob)
            timeout += lobStuckExtraTime;

        if (rb.velocity.magnitude < threshold)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer >= timeout)
            {
                ResetBall();
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // ==================== 碰撞 ====================

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            Vector2 normal = collision.GetContact(0).normal;
            rb.AddForce(-normal * 2f, ForceMode2D.Impulse);

            // 碰撞墙壁时 spin 减半
            currentSpin *= 0.5f;
            currentVerticalSpin *= 0.5f;
            if (Mathf.Abs(currentSpin) < 0.1f && Mathf.Abs(currentVerticalSpin) < 0.1f)
                hasSpin = false;
        }

        // 碰地面/天花板也会削减旋转
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Wall"))
        {
            currentSpin *= 0.7f;
            currentVerticalSpin *= 0.7f;
        }
    }

    // ==================== 复位 ====================

    public void ResetBall()
    {
        transform.position = startPos;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
        ClearAllState();
    }

    public void ResetBall(Vector2 position)
    {
        transform.position = position;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.rotation = Quaternion.identity;
        ClearAllState();
        stuckTimer = 0f;
    }

    void ClearAllState()
    {
        currentSpin = 0f;
        currentVerticalSpin = 0f;
        hasSpin = false;
        currentPassType = PassType.Straight;
        spinVelocity = 0f;
        vertSpinVelocity = 0f;
        lobHasTriggeredDescent = false;
        rb.gravityScale = defaultGravityScale;
        rb.drag = defaultDrag;
    }

    public void ClearSpin()
    {
        currentSpin = 0f;
        currentVerticalSpin = 0f;
        hasSpin = false;
    }

    /// <summary>
    /// 分裂球克隆：复制完整的物理/旋转状态
    /// </summary>
    public Ball CloneForSplit(Vector3 position, Vector2 velocity)
    {
        GameObject clone = Instantiate(gameObject, position, Quaternion.identity);
        Ball cloneBall = clone.GetComponent<Ball>();
        Rigidbody2D cloneRb = clone.GetComponent<Rigidbody2D>();

        // 标记为克隆体
        cloneBall.isCloneBall = true;

        // 复制物理状态
        cloneRb.velocity = velocity;
        cloneRb.angularVelocity = rb.angularVelocity;
        cloneRb.gravityScale = rb.gravityScale;
        cloneRb.drag = rb.drag;

        // 复制弧线球运行时状态
        cloneBall.currentSpin = currentSpin;
        cloneBall.currentVerticalSpin = currentVerticalSpin;
        cloneBall.hasSpin = hasSpin;
        cloneBall.currentPassType = currentPassType;
        cloneBall.spinVelocity = spinVelocity;
        cloneBall.vertSpinVelocity = vertSpinVelocity;
        cloneBall.lobHasTriggeredDescent = lobHasTriggeredDescent;
        cloneBall.defaultGravityScale = defaultGravityScale;
        cloneBall.defaultDrag = defaultDrag;
        cloneBall.stuckTimer = 0f;

        // 复制拖尾颜色
        if (cloneBall.trailRenderer != null && trailRenderer != null)
        {
            cloneBall.trailRenderer.startColor = trailRenderer.startColor;
            cloneBall.trailRenderer.widthMultiplier = trailRenderer.widthMultiplier;
            cloneBall.trailRenderer.emitting = trailRenderer.emitting;
        }

        // 自动销毁：最多存活10秒
        Destroy(clone, 10f);

        return cloneBall;
    }

    public float GetSpeed() => rb.velocity.magnitude;
    public float GetCurrentSpin() => currentSpin;
    public float GetCurrentVerticalSpin() => currentVerticalSpin;
    public bool HasSpin() => hasSpin;
    public PassType GetCurrentPassType() => currentPassType;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
    }
}
