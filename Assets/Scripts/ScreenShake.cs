using UnityEngine;

public class ScreenShake : MonoBehaviour
{
    public static ScreenShake Instance { get; private set; }

    private float shakeDuration = 0f;
    private float shakeIntensity = 0f;
    private Vector3 originalPosition;
    private bool isShaking = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        originalPosition = transform.localPosition;
    }

    void Update()
    {
        if (shakeDuration > 0)
        {
            if (!isShaking)
            {
                originalPosition = transform.localPosition;
                isShaking = true;
            }

            // 随机偏移
            float offsetX = Random.Range(-1f, 1f) * shakeIntensity;
            float offsetY = Random.Range(-1f, 1f) * shakeIntensity;

            transform.localPosition = originalPosition + new Vector3(offsetX, offsetY, 0);

            shakeDuration -= Time.deltaTime;

            // 逐渐减弱
            shakeIntensity = Mathf.Lerp(shakeIntensity, 0, Time.deltaTime * 3f);
        }
        else if (isShaking)
        {
            transform.localPosition = originalPosition;
            isShaking = false;
        }
    }

    public void Shake(float duration, float intensity)
    {
        shakeDuration = duration;
        shakeIntensity = intensity;
    }

    public void ShakeBig()
    {
        Shake(0.5f, 0.3f);
    }
}
