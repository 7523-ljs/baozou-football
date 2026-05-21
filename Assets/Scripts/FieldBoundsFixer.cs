using UnityEngine;

/// <summary>
/// 场景边界修复器 — 运行时自动检测并修补物理边界漏洞
/// 在球门后方添加拦网碰撞体，防止角色/足球掉出场景
///
/// 使用方式：挂载到 GameManager 或任意场景中的 GameObject 上即可
/// </summary>
public class FieldBoundsFixer : MonoBehaviour
{
    [Header("边界参数（应与 SceneSetupEditor 中的值一致）")]
    public float fieldWidth = 20f;
    public float fieldHeight = 12f;
    public float wallThickness = 0.5f;
    public float goalWidth = 3f;
    public float goalDepth = 1.5f;

    [Header("调试")]
    public bool showGizmos = true;

    // 创建的拦网引用
    private GameObject leftBackstop;
    private GameObject rightBackstop;

    void Awake()
    {
        // 在 GameManager 初始化之前添加拦网
        AddBackstopWalls();
    }

    void AddBackstopWalls()
    {
        // 查找场地容器
        GameObject field = GameObject.Find("Field");
        if (field == null)
        {
            Debug.LogWarning("[FieldBoundsFixer] 未找到 Field 容器，将创建独立的拦网");
            // 创建一个容器来放拦网
            GameObject walls = new GameObject("BackstopWalls");
            walls.transform.position = Vector3.zero;
            CreateBackstops(walls.transform);
            return;
        }

        // 检查是否已存在拦网
        Transform existing = field.transform.Find("LeftBackstop");
        if (existing != null)
        {
            Debug.Log("[FieldBoundsFixer] 拦网已存在，跳过创建");
            return;
        }

        CreateBackstops(field.transform);
        Debug.Log("[FieldBoundsFixer] 球门后方拦网已添加");
    }

    void CreateBackstops(Transform parent)
    {
        float backstopY = -fieldHeight / 2f + goalWidth / 2f;
        float backstopH = goalWidth + wallThickness;

        // 左球门拦网
        leftBackstop = CreateBackstopWall("LeftBackstop", parent,
            new Vector3(-fieldWidth / 2f - goalDepth - wallThickness / 2f, backstopY, 0),
            new Vector2(wallThickness, backstopH));

        // 右球门拦网
        rightBackstop = CreateBackstopWall("RightBackstop", parent,
            new Vector3(fieldWidth / 2f + goalDepth + wallThickness / 2f, backstopY, 0),
            new Vector2(wallThickness, backstopH));
    }

    GameObject CreateBackstopWall(string name, Transform parent, Vector3 position, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        wall.tag = "Wall";

        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size;

        // 可选：添加半透明视觉方便调试
        SpriteRenderer sr = wall.AddComponent<SpriteRenderer>();
        sr.sprite = CreateSquareSprite();
        sr.color = new Color(1f, 1f, 1f, 0.15f); // 几乎透明，只隐约可见
        sr.sortingOrder = -5;

        return wall;
    }

    static Sprite CreateSquareSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        Color[] colors = new Color[16];
        for (int i = 0; i < 16; i++) colors[i] = Color.white;
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
    }

    void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

        // 绘制预期的拦网位置
        float backstopY = -fieldHeight / 2f + goalWidth / 2f;
        float backstopH = goalWidth + wallThickness;

        // 左
        float lx = -fieldWidth / 2f - goalDepth - wallThickness / 2f;
        Gizmos.DrawWireCube(new Vector3(lx, backstopY, 0), new Vector3(wallThickness, backstopH, 0.1f));

        // 右
        float rx = fieldWidth / 2f + goalDepth + wallThickness / 2f;
        Gizmos.DrawWireCube(new Vector3(rx, backstopY, 0), new Vector3(wallThickness, backstopH, 0.1f));
    }
}
