using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class SlowMotion : MonoBehaviour, ISlowMotionController
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
    private Coroutine _hitstopCoroutine;
    private bool _hitstopActive;
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

        // Safety net: never let slowed time leak across a room transition / reload,
        // regardless of which code path was driving the slow-motion.
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnSceneUnloaded(Scene scene)
    {
        ResetImmediate();
    }

    /// <summary>
    /// �Դ��ҹ Slow Motion
    /// </summary>
    /// <param name="timeScale">����˹ѡ�ͧ���ҷ���ͧ���˹�ǧ (0.0 - 1.0) �� 0.5 = ���ŧ 50%</param>
    /// <param name="duration">�������ҷ���ͧ���˹�ǧ (�Թҷ�)</param>
    public void StartSlowMotion(float timeScale, float duration)
    {
        // Callers hold this via the ISlowMotionController interface, which bypasses Unity's
        // overloaded null check — so guard against use after the object's native side is
        // destroyed (e.g. play-mode exit teardown) where Stop/StartCoroutine would throw.
        if (this == null) return;

        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        _slowMotionCoroutine = StartCoroutine(SlowMotionCoroutine(timeScale, duration));
    }

    /// <summary>
    /// �Դ��ҹ Slow Motion ����� Ease In/Out
    /// </summary>
    /// <param name="timeScale">����˹ѡ�ͧ���ҷ���ͧ���˹�ǧ (0.0 - 1.0)</param>
    /// <param name="duration">�������ҷ���ͧ���˹�ǧ (�Թҷ�)</param>
    /// <param name="easeInDuration">����㹡�� Fade In (�Թҷ�)</param>
    /// <param name="easeOutDuration">����㹡�� Fade Out (�Թҷ�)</param>
    public void StartSlowMotionSmooth(float timeScale, float duration, float easeInDuration = 0.2f, float easeOutDuration = 0.2f)
    {
        if (this == null) return;

        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        _slowMotionCoroutine = StartCoroutine(SlowMotionSmoothCoroutine(timeScale, duration, easeInDuration, easeOutDuration));
    }

    /// <summary>
    /// High-priority slow-motion for hit reactions. Cannot be cancelled by StopSlowMotion().
    /// </summary>
    public void StartHitstop(float timeScale, float duration)
    {
        if (this == null) return;

        if (_hitstopCoroutine != null) StopCoroutine(_hitstopCoroutine);
        if (_slowMotionCoroutine != null) { StopCoroutine(_slowMotionCoroutine); _slowMotionCoroutine = null; }
        _hitstopCoroutine = StartCoroutine(HitstopCoroutine(timeScale, duration));
    }

    private IEnumerator HitstopCoroutine(float timeScale, float duration)
    {
        _hitstopActive = true;
        Time.timeScale = Mathf.Clamp01(timeScale);
        Time.fixedDeltaTime = _defaultFixedDeltaTime * Time.timeScale;

        yield return new WaitForSecondsRealtime(duration);

        _hitstopActive = false;
        _hitstopCoroutine = null;
        ResetTimeScale();
    }

    /// <summary>
    /// ��ش Slow Motion �ѹ�� (does not cancel an active hitstop)
    /// </summary>
    public void StopSlowMotion()
    {
        if (this == null) return;
        if (_hitstopActive) return;

        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
        }

        ResetTimeScale();
    }

    /// <summary>
    /// Hard reset: cancels slow-motion AND hitstop, then restores normal time.
    /// Bypasses the hitstop guard so lifecycle events (death, scene unload) always win.
    /// </summary>
    public void ResetImmediate()
    {
        if (this == null) return;

        if (_slowMotionCoroutine != null)
        {
            StopCoroutine(_slowMotionCoroutine);
            _slowMotionCoroutine = null;
        }

        if (_hitstopCoroutine != null)
        {
            StopCoroutine(_hitstopCoroutine);
            _hitstopCoroutine = null;
        }

        _hitstopActive = false;
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
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        ResetTimeScale();
    }
}
