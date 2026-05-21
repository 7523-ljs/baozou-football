using UnityEngine;

public class PlayerAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    public float frameInterval = 0.12f;
    public float kickDuration = 0.25f;

    private SpriteRenderer sr;

    // 动画帧
    private Sprite idleSprite;
    private Sprite[] runFrames;
    private Sprite kickSprite;
    private Sprite attackKickSprite;
    private Sprite jumpSprite;
    private Sprite hurtSprite;
    private Sprite fallSprite;

    // 状态
    private enum AnimState { Idle, Run, Kick, Jump, Hurt, Fall }
    private AnimState currentState = AnimState.Idle;
    private int runFrame;
    private float frameTimer;
    private float kickTimer;
    private bool isMoving;
    private bool characterLoaded;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public void LoadCharacter(CharacterData data)
    {
        if (data == null) return;

        idleSprite = LoadSingleSprite(data.idlePath);
        runFrames = LoadPoseSequence(data.runPaths);
        kickSprite = LoadSingleSprite(data.kickPath);
        attackKickSprite = LoadSingleSprite(data.attackKickPath);
        jumpSprite = LoadSingleSprite(data.jumpPath);
        hurtSprite = LoadSingleSprite(data.hurtPath);
        fallSprite = LoadSingleSprite(data.fallPath);

        characterLoaded = true;

        if (idleSprite != null && sr != null)
            sr.sprite = idleSprite;

        currentState = AnimState.Idle;
        runFrame = 0;
    }

    void Update()
    {
        if (!characterLoaded || sr == null) return;

        switch (currentState)
        {
            case AnimState.Idle:
                if (idleSprite != null) sr.sprite = idleSprite;
                break;

            case AnimState.Run:
                UpdateRun();
                break;

            case AnimState.Kick:
                UpdateKick();
                break;

            case AnimState.Jump:
                if (jumpSprite != null) sr.sprite = jumpSprite;
                break;

            case AnimState.Hurt:
                if (hurtSprite != null) sr.sprite = hurtSprite;
                break;

            case AnimState.Fall:
                if (fallSprite != null) sr.sprite = fallSprite;
                break;
        }
    }

    void UpdateRun()
    {
        if (runFrames == null || runFrames.Length == 0) return;

        frameTimer -= Time.deltaTime;
        if (frameTimer <= 0)
        {
            frameTimer = frameInterval;
            runFrame = (runFrame + 1) % runFrames.Length;
            sr.sprite = runFrames[runFrame];
        }
    }

    void UpdateKick()
    {
        kickTimer -= Time.deltaTime;
        if (kickTimer <= 0)
        {
            currentState = isMoving ? AnimState.Run : AnimState.Idle;
            if (isMoving) frameTimer = 0;
        }
    }

    // === 公开方法 ===

    public void PlayIdle()
    {
        if (!characterLoaded) return;
        if (currentState == AnimState.Kick) { isMoving = false; return; }
        if (currentState == AnimState.Idle && !isMoving) return;
        isMoving = false;
        currentState = AnimState.Idle;
    }

    public void PlayRun()
    {
        if (!characterLoaded) return;
        if (currentState == AnimState.Kick) { isMoving = true; return; }
        if (currentState == AnimState.Run && isMoving) return;
        isMoving = true;
        currentState = AnimState.Run;
        runFrame = 0;
        frameTimer = 0;
        if (runFrames != null && runFrames.Length > 0)
            sr.sprite = runFrames[0];
    }

    public void PlayKick()
    {
        if (!characterLoaded) return;
        currentState = AnimState.Kick;
        kickTimer = kickDuration;
        if (kickSprite != null) sr.sprite = kickSprite;
    }

    public void PlayAttackKick()
    {
        if (!characterLoaded) return;
        currentState = AnimState.Kick;
        kickTimer = kickDuration;
        if (attackKickSprite != null) sr.sprite = attackKickSprite;
        else if (kickSprite != null) sr.sprite = kickSprite;
    }

    public void PlayJump()
    {
        if (!characterLoaded) return;
        if (currentState == AnimState.Kick) return;
        currentState = AnimState.Jump;
        if (jumpSprite != null) sr.sprite = jumpSprite;
    }

    public void PlayHurt()
    {
        if (!characterLoaded) return;
        currentState = AnimState.Hurt;
        if (hurtSprite != null) sr.sprite = hurtSprite;
    }

    public void PlayFall()
    {
        if (!characterLoaded) return;
        if (currentState == AnimState.Kick) return;
        currentState = AnimState.Fall;
        if (fallSprite != null) sr.sprite = fallSprite;
    }

    public void SetFlipX(bool flip)
    {
        if (sr != null) sr.flipX = flip;
    }

    public void SetColor(Color color)
    {
        if (sr != null) sr.color = color;
    }

    public void ResetToIdle()
    {
        characterLoaded = characterLoaded && idleSprite != null;
        currentState = AnimState.Idle;
        isMoving = false;
        if (idleSprite != null && sr != null)
            sr.sprite = idleSprite;
    }

    public bool IsLoaded() => characterLoaded;

    // === 静态素材加载工具 ===

    public static Sprite LoadSingleSprite(string path)
    {
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
            return null;

        byte[] fileData = System.IO.File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        if (!tex.LoadImage(fileData))
            return null;

        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 64f);
    }

    static Sprite[] LoadPoseSequence(string[] paths)
    {
        if (paths == null) return null;
        Sprite[] frames = new Sprite[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            frames[i] = LoadSingleSprite(paths[i]);
        return frames;
    }
}
