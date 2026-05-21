using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("跟随参数")]
    public float followSpeed = 5f;

    [Header("相机设置")]
    public float defaultZoom = 7f;

    [Header("边界")]
    public float minX = -12f;
    public float maxX = 12f;
    public float fixedY = 0f;

    private Camera cam;
    private Transform ball;
    private Transform player1;
    private Transform player2;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = defaultZoom;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.1f);
    }

    void LateUpdate()
    {
        FindTargets();
        if (player1 == null || player2 == null) return;

        // 目标X = 两个玩家和球的加权中点
        float targetX;
        if (ball != null)
            targetX = (player1.position.x + player2.position.x + ball.position.x * 2f) / 4f;
        else
            targetX = (player1.position.x + player2.position.x) / 2f;

        targetX = Mathf.Clamp(targetX, minX, maxX);

        Vector3 targetPos = new Vector3(targetX, fixedY, -10f);
        transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);

        // 固定缩放，不随玩家距离变化
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, defaultZoom, Time.deltaTime * 3f);
    }

    void FindTargets()
    {
        if (ball == null)
        {
            GameObject ballObj = GameObject.FindGameObjectWithTag("Ball");
            if (ballObj != null) ball = ballObj.transform;
        }
        if (player1 == null || player2 == null)
        {
            PlayerController[] players = FindObjectsOfType<PlayerController>();
            foreach (var p in players)
            {
                if (p.playerIndex == 0) player1 = p.transform;
                else player2 = p.transform;
            }
        }
    }

    public void SetBounds(float xMin, float xMax)
    {
        minX = xMin;
        maxX = xMax;
    }
}
