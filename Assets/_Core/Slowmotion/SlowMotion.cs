using UnityEngine;
using System.Collections;

public class SlowMotion : MonoBehaviour
{
    private static SlowMotion _instance;
    public static SlowMotion Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new GameObject("SlowMotion");
                _instance = obj.AddComponent<SlowMotion>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    private Coroutine _slowMotionCoroutine;
    private float _defaultFixedDeltaTime;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _defaultFixedDeltaTime = Time.fixedDeltaTime;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// เปิดใช้งาน Slow Motion
    /// </summary>
    /// <param name="timeScale">ความหนักของเวลาที่ต้องการหน่วง (0.0 - 1.0) เช่น 0.5 = ช้าลง 50%</param>
    /// <param name="duration">ระยะเวลาที่ต้องการหน่วง (วินาที)</param>
    public void StartSlowMotion(float timeScale, float duration)
    {
        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        _slowMotionCoroutine = StartCoroutine(SlowMotionCoroutine(timeScale, duration));
    }

    /// <summary>
    /// เปิดใช้งาน Slow Motion พร้อม Ease In/Out
    /// </summary>
    /// <param name="timeScale">ความหนักของเวลาที่ต้องการหน่วง (0.0 - 1.0)</param>
    /// <param name="duration">ระยะเวลาที่ต้องการหน่วง (วินาที)</param>
    /// <param name="easeInDuration">เวลาในการ Fade In (วินาที)</param>
    /// <param name="easeOutDuration">เวลาในการ Fade Out (วินาที)</param>
    public void StartSlowMotionSmooth(float timeScale, float duration, float easeInDuration = 0.2f, float easeOutDuration = 0.2f)
    {
        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        _slowMotionCoroutine = StartCoroutine(SlowMotionSmoothCoroutine(timeScale, duration, easeInDuration, easeOutDuration));
    }

    /// <summary>
    /// หยุด Slow Motion ทันที
    /// </summary>
    public void StopSlowMotion()
    {
        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        ResetTimeScale();
    }

    private IEnumerator SlowMotionCoroutine(float timeScale, float duration)
    {
        timeScale = Mathf.Clamp01(timeScale);

        Time.timeScale = timeScale;
        Time.fixedDeltaTime = _defaultFixedDeltaTime * timeScale;

        yield return new WaitForSecondsRealtime(duration);

        ResetTimeScale();

        _slowMotionCoroutine = null;
    }

    private IEnumerator SlowMotionSmoothCoroutine(float timeScale, float duration, float easeInDuration, float easeOutDuration)
    {
        timeScale = Mathf.Clamp01(timeScale);

        // Ease In
        float elapsed = 0f;
        float startTimeScale = Time.timeScale;

        while (elapsed < easeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / easeInDuration;
            float currentTimeScale = Mathf.Lerp(startTimeScale, timeScale, t);
            Time.timeScale = currentTimeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * currentTimeScale;
            yield return null;
        }

        Time.timeScale = timeScale;
        Time.fixedDeltaTime = _defaultFixedDeltaTime * timeScale;

        // Hold
        yield return new WaitForSecondsRealtime(duration);

        // Ease Out
        elapsed = 0f;
        startTimeScale = Time.timeScale;

        while (elapsed < easeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / easeOutDuration;
            float currentTimeScale = Mathf.Lerp(startTimeScale, 1f, t);
            Time.timeScale = currentTimeScale;
            Time.fixedDeltaTime = _defaultFixedDeltaTime * currentTimeScale;
            yield return null;
        }

        ResetTimeScale();

        _slowMotionCoroutine = null;
    }

    private void ResetTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = _defaultFixedDeltaTime;
    }

    private void OnDestroy()
    {
        ResetTimeScale();
    }
}
