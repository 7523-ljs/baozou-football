using UnityEngine;

/// <summary>
/// 技能系统 — 负责技能冷却管理、冷却UI绑定
/// 每个玩家一个 SkillSystem 实例，由 PlayerController 引用
/// </summary>
public class SkillSystem : MonoBehaviour
{
    [Header("技能设置")]
    [Tooltip("技能冷却时间（秒）")]
    public float cooldownDuration = 5f;

    // 冷却状态
    public float cooldownTimer { get; private set; } = 0f;

    // 当前技能类型
    public SkillType currentSkill { get; set; } = SkillType.PowerShot;

    void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    /// <summary>技能是否就绪（冷却结束）</summary>
    public bool IsReady() => cooldownTimer <= 0f;

    /// <summary>使用技能 → 开始冷却</summary>
    public void Use()
    {
        cooldownTimer = cooldownDuration;
    }

    /// <summary>获取冷却进度（0~1，1=就绪）</summary>
    public float GetProgress()
    {
        if (cooldownTimer <= 0f) return 1f;
        return 1f - (cooldownTimer / cooldownDuration);
    }

    /// <summary>获取剩余冷却秒数</summary>
    public float GetRemainingTime()
    {
        return Mathf.Max(0f, cooldownTimer);
    }

    /// <summary>重置冷却</summary>
    public void ResetCooldown()
    {
        cooldownTimer = 0f;
    }

    /// <summary>获取技能名称</summary>
    public string GetSkillName()
    {
        switch (currentSkill)
        {
            case SkillType.PowerShot:   return "POWER SHOT!";
            case SkillType.SpeedBoost:  return "SPEED BOOST!";
            case SkillType.Shield:      return "SHIELD!";
            default:                    return "SKILL!";
        }
    }
}
