using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;

// Game over panel buttons
public class Events : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private string menuSceneName = "ScoreMenuScene";
    [Tooltip("Optional: an interstitial is shown first, the scene loads after it closes.")]
    [SerializeField] private UnityAds ads;
    [Tooltip("Real-time seconds to wait for the ad to close before loading anyway (a stuck ad must never freeze the game).")]
    [SerializeField] private float adTimeout = 30f;

    private bool loading;

    public void ReplayGame()
    {
        StartLoad(gameSceneName);
    }

    public void QuitGame()
    {
        StartLoad(menuSceneName);
    }

    private void StartLoad(string sceneName)
    {
        if (loading)
        {
            return;
        }
        loading = true;
        StartCoroutine(LoadAfterAd(sceneName));
    }

    // Runs while Time.timeScale is 0, so only real time is used
    private IEnumerator LoadAfterAd(string sceneName)
    {
        bool adClosed = false;
        bool adShown = false;
#if !UNITY_EDITOR
        // In the Editor Unity Ads only shows a placeholder that blocks until closed by hand
        adShown = ads != null && ads.ShowInterstitialAd(() => adClosed = true);
#endif
        float deadline = Time.realtimeSinceStartup + adTimeout;
        while (adShown && !adClosed && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }
        LoadScene(sceneName);
    }

    private static void LoadScene(string sceneName)
    {
        // gameOver is static and timeScale is global: both must be reset before the reload
        HeroManager.gameOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(sceneName);
    }
}
