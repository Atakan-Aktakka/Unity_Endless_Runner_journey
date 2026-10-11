using System.Collections;
using System.Text;
using UnityEngine;
using TMPro;

// Arcade style in-game UI: pause popup, game over popup with bounce, dropping title,
// rolling score, high score badge.
// Everything animates in unscaled time because Time.timeScale is 0 while a popup is open.
// Time.timeScale is only changed here for pause / resume; game over freezing stays in HeroManager,
// scene loading (and its timeScale reset) stays in Events.
public class GameUIController : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private GameObject pauseButton;
    [Tooltip("More in-game HUD objects hidden while a popup is open (score counter, fire button...).")]
    [SerializeField] private GameObject[] hudObjects;

    [Header("Pause")]
    [SerializeField] private CanvasGroup pausePanel;
    [SerializeField] private RectTransform pauseWindow;

    [Header("Game Over")]
    [SerializeField] private CanvasGroup gameOverPanel;
    [SerializeField] private RectTransform gameOverWindow;
    [Tooltip("'OYUN BİTTİ' title, drops in from above after the window opened.")]
    [SerializeField] private RectTransform gameOverTitle;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text highScoreText;
    [Tooltip("'YENİ REKOR!' badge, hidden unless the high score was beaten.")]
    [SerializeField] private RectTransform highScoreBadge;
    [SerializeField] private TMP_Text highScoreBadgeText;

    [Header("Buttons")]
    [Tooltip("Restart / Menu go through Events (ads + timeScale reset before the scene load).")]
    [SerializeField] private Events sceneEvents;
    [Tooltip("Real-time delay so the button press animation is visible before the scene loads.")]
    [SerializeField] private float buttonActionDelay = 0.15f;

    [Header("Popup Bounce")]
    [SerializeField] private float popupDuration = 0.45f;
    [Tooltip("x = time 0..1, y = scale. Overshoot above 1 gives the bounce.")]
    [SerializeField] private AnimationCurve popupCurve = new AnimationCurve(
        new Keyframe(0f, 0f), new Keyframe(0.6f, 1.15f), new Keyframe(0.8f, 0.95f), new Keyframe(1f, 1f));
    [SerializeField] private float fadeDuration = 0.15f;

    [Header("Title Drop")]
    [SerializeField] private float titleDropHeight = 160f;
    [SerializeField] private float titleDropDuration = 0.5f;
    [SerializeField] private float titleStartScale = 1.6f;
    [Tooltip("EaseOutBack overshoot amount (1.70158 = classic).")]
    [SerializeField] private float titleBackOvershoot = 1.70158f;
    [SerializeField] private float titlePunchScale = 0.15f;
    [SerializeField] private float titlePunchDuration = 0.25f;

    [Header("Score Text")]
    [SerializeField] private string scorePrefix = "SKOR: ";
    [SerializeField] private string bestPrefix = "EN İYİ: ";
    [SerializeField] private char thousandsSeparator = ',';
    [SerializeField] private float scoreRollDelay = 0.1f;
    [SerializeField] private float scoreRollDuration = 1.2f;

    [Header("High Score Badge")]
    [SerializeField] private float badgePulseSpeed = 6f;
    [SerializeField] private float badgePulseScale = 0.12f;
    [SerializeField] private Color badgeColorA = new Color(1f, 0.9f, 0f);
    [SerializeField] private Color badgeColorB = Color.white;

    private readonly StringBuilder textBuilder = new StringBuilder(32);
    private Vector2 titleRestPosition;
    private bool isPaused;
    private bool isGameOver;
    private bool buttonBusy;
    private Coroutine pauseRoutine;

    public bool IsPaused { get { return isPaused; } }

    private void Awake()
    {
        if (gameOverTitle != null)
        {
            titleRestPosition = gameOverTitle.anchoredPosition;
        }
        HidePanel(pausePanel, pauseWindow);
        HidePanel(gameOverPanel, gameOverWindow);
        if (highScoreBadge != null)
        {
            highScoreBadge.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        // Escape / Android back button toggles pause
        if (!isGameOver && Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused) Resume(); else Pause();
        }
    }

    // ---------------------------------------------------------------- pause

    public void Pause()
    {
        // Without a pause panel there would be no way back: never freeze the game then
        if (pausePanel == null || isPaused || isGameOver || HeroManager.gameOver) return;

        isPaused = true;
        Time.timeScale = 0f;
        SetHudVisible(false);
        if (pauseRoutine != null) StopCoroutine(pauseRoutine);
        pauseRoutine = StartCoroutine(ShowPopup(pausePanel, pauseWindow));
    }

    public void Resume()
    {
        if (!isPaused) return;

        isPaused = false;
        // Never unfreeze a finished run
        Time.timeScale = HeroManager.gameOver ? 0f : 1f;
        SetHudVisible(true);
        if (pauseRoutine != null) StopCoroutine(pauseRoutine);
        HidePanel(pausePanel, pauseWindow);
    }

    // Pause automatically when the app goes to the background
    private void OnApplicationPause(bool paused)
    {
        if (paused) Pause();
    }

    // ---------------------------------------------------------------- game over

    // Called by HeroManager after it froze the game and stored the score
    public void ShowGameOver(int score, int highScore, bool newRecord)
    {
        if (isGameOver) return;
        isGameOver = true;

        if (isPaused)
        {
            isPaused = false;
            HidePanel(pausePanel, pauseWindow);
        }

        SetHudVisible(false);
        StartCoroutine(GameOverSequence(score, highScore, newRecord));
    }

    private IEnumerator GameOverSequence(int score, int highScore, bool newRecord)
    {
        SetScoreText(finalScoreText, scorePrefix, 0);
        SetScoreText(highScoreText, bestPrefix, newRecord ? 0 : highScore);

        // Title stays hidden until the window has popped in
        if (gameOverTitle != null)
        {
            gameOverTitle.localScale = Vector3.zero;
        }

        yield return ShowPopup(gameOverPanel, gameOverWindow);

        if (gameOverTitle != null)
        {
            yield return TitleDrop();
        }

        yield return new WaitForSecondsRealtime(scoreRollDelay);

        // Rolling score: ease-out count from 0 to the final score
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, scoreRollDuration);
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            int shown = Mathf.RoundToInt(score * eased);
            SetScoreText(finalScoreText, scorePrefix, shown);
            if (newRecord) SetScoreText(highScoreText, bestPrefix, shown);
            yield return null;
        }

        SetScoreText(finalScoreText, scorePrefix, score);
        SetScoreText(highScoreText, bestPrefix, newRecord ? score : highScore);

        if (newRecord && highScoreBadge != null)
        {
            yield return BadgeRoutine();
        }
    }

    // Falls from above with EaseOutBack (position + scale), then a short scale punch on landing
    private IEnumerator TitleDrop()
    {
        Vector2 from = titleRestPosition + Vector2.up * titleDropHeight;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, titleDropDuration);
            float e = EaseOutBack(Mathf.Clamp01(t), titleBackOvershoot);
            gameOverTitle.anchoredPosition = Vector2.LerpUnclamped(from, titleRestPosition, e);
            gameOverTitle.localScale = Vector3.one * Mathf.LerpUnclamped(titleStartScale, 1f, e);
            yield return null;
        }
        gameOverTitle.anchoredPosition = titleRestPosition;

        // Punch: 1 -> 1 + punch -> 1, damped sine
        t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, titlePunchDuration);
            float c = Mathf.Clamp01(t);
            float punch = Mathf.Sin(c * Mathf.PI * 2f) * (1f - c) * titlePunchScale;
            gameOverTitle.localScale = Vector3.one * (1f + punch);
            yield return null;
        }
        gameOverTitle.localScale = Vector3.one;
    }

    private IEnumerator BadgeRoutine()
    {
        highScoreBadge.gameObject.SetActive(true);

        // Stamp in, then pulse + color flash until the scene is left
        yield return ScalePop(highScoreBadge, popupDuration * 0.8f);

        while (true)
        {
            float wave = (Mathf.Sin(Time.unscaledTime * badgePulseSpeed) + 1f) * 0.5f;
            highScoreBadge.localScale = Vector3.one * (1f + wave * badgePulseScale);
            if (highScoreBadgeText != null)
            {
                highScoreBadgeText.color = Color.Lerp(badgeColorA, badgeColorB, wave);
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------------- buttons

    public void OnRestartButton()
    {
        RunButtonAction(true);
    }

    public void OnMenuButton()
    {
        RunButtonAction(false);
    }

    private void RunButtonAction(bool restart)
    {
        if (buttonBusy) return;
        if (sceneEvents == null)
        {
            Debug.LogError("GameUIController: Events reference is missing.", this);
            return;
        }
        buttonBusy = true;
        StartCoroutine(DelayedButtonAction(restart));
    }

    private IEnumerator DelayedButtonAction(bool restart)
    {
        // Lets UIButtonPunch play before the scene changes
        yield return new WaitForSecondsRealtime(buttonActionDelay);
        if (restart) sceneEvents.ReplayGame();
        else sceneEvents.QuitGame();
    }

    // ---------------------------------------------------------------- text

    // "SKOR: 1,420" without per-frame string allocations
    private void SetScoreText(TMP_Text target, string prefix, int value)
    {
        if (target == null) return;

        textBuilder.Length = 0;
        textBuilder.Append(prefix);
        AppendGrouped(textBuilder, value, thousandsSeparator);
        target.SetText(textBuilder);
    }

    private static void AppendGrouped(StringBuilder sb, int value, char separator)
    {
        if (value < 0)
        {
            sb.Append('-');
            value = -value;
        }
        if (value < 1000)
        {
            sb.Append(value);
            return;
        }
        AppendGrouped(sb, value / 1000, separator);
        sb.Append(separator);
        int rest = value % 1000;
        if (rest < 100) sb.Append('0');
        if (rest < 10) sb.Append('0');
        sb.Append(rest);
    }

    // ---------------------------------------------------------------- animation helpers

    private static float EaseOutBack(float t, float overshoot)
    {
        float c3 = overshoot + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + overshoot * x * x;
    }

    private IEnumerator ShowPopup(CanvasGroup panel, RectTransform window)
    {
        if (panel == null) yield break;

        panel.gameObject.SetActive(true);
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = true;

        // Background fade and window bounce run in parallel
        Coroutine pop = window != null ? StartCoroutine(ScalePop(window, popupDuration)) : null;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration);
            panel.alpha = Mathf.Clamp01(t);
            yield return null;
        }

        if (pop != null) yield return pop;
        panel.interactable = true;
    }

    private IEnumerator ScalePop(RectTransform target, float duration)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / Mathf.Max(0.01f, duration);
            target.localScale = Vector3.one * popupCurve.Evaluate(Mathf.Clamp01(t));
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private static void HidePanel(CanvasGroup panel, RectTransform window)
    {
        if (panel == null) return;
        panel.alpha = 0f;
        panel.interactable = false;
        panel.blocksRaycasts = false;
        panel.gameObject.SetActive(false);
        if (window != null) window.localScale = Vector3.one;
    }

    private void SetHudVisible(bool visible)
    {
        if (hudRoot != null) hudRoot.SetActive(visible);
        if (pauseButton != null) pauseButton.SetActive(visible);
        if (hudObjects == null) return;
        for (int i = 0; i < hudObjects.Length; i++)
        {
            if (hudObjects[i] != null) hudObjects[i].SetActive(visible);
        }
    }
}
