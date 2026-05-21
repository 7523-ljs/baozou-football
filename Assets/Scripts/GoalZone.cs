using UnityEngine;

public class GoalZone : MonoBehaviour
{
    [Header("球门设置")]
    public int goalOwnerIndex; // 0 = 左侧球门（P1的球门）, 1 = 右侧球门（P2的球门）
    public Color goalColor = Color.white;

    [Header("特效")]
    public GameObject goalEffectPrefab;

    private SpriteRenderer spriteRenderer;
    private bool goalScored = false;
    private float resetTimer = 0f;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.color = goalColor;
        }
    }

    void Update()
    {
        if (goalScored)
        {
            resetTimer -= Time.deltaTime;
            if (resetTimer <= 0)
            {
                goalScored = false;
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ball") && !goalScored)
        {
            goalScored = true;
            resetTimer = 1f;

            // 谁的球门被进球，对方得分
            int scoringPlayer = goalOwnerIndex == 0 ? 1 : 0;

            // 进球特效
            if (goalEffectPrefab != null)
            {
                Instantiate(goalEffectPrefab, other.transform.position, Quaternion.identity);
            }

            // 通知GameManager
            GameManager.Instance?.OnGoalScored(scoringPlayer);

            Debug.Log($"进球！玩家 {scoringPlayer + 1} 得分！");
        }
    }
}
