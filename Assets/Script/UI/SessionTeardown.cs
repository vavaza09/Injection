using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    public static class SessionTeardown
    {
        public static void ReturnToTitle(string titleSceneName = "TitleScene")
        {
            Time.timeScale = 1f;
            if (HudCameraBinder.Instance != null)
                Object.Destroy(HudCameraBinder.Instance.gameObject);
            if (RootLifetimeScope.Instance != null)
                Object.Destroy(RootLifetimeScope.Instance.gameObject);
            SceneManager.LoadScene(titleSceneName);
        }
    }
}
