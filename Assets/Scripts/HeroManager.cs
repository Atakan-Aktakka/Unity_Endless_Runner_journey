using UnityEngine;
using TMPro;

public class HeroManager : MonoBehaviour
{
    public static bool gameOver;
    public GameObject gameOverPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private TMP_Text highScoreText;
    [Tooltip("Optional: animated game over popup. Empty = the plain gameOverPanel above is used.")]
    [SerializeField] private GameUIController gameUI;

    private bool gameOverHandled;

    void Awake()
    {
        // gameOver is static and timeScale is global, both survive a scene reload: reset them first
        gameOver = false;
        Time.timeScale = 1f;
        // Android defaults to 30 fps; a runner needs 60
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!gameOver || gameOverHandled)
        {
            return;
        }

        gameOverHandled = true;
        Time.timeScale = 0f;

        bool newRecord = Score.SubmitScore();
        if (gameUI != null)
        {
            gameUI.ShowGameOver(Score.CurrentScore, Score.HighScore, newRecord);
            return;
        }

        if (finalScoreText != null)
        {
            finalScoreText.SetText("SKOR: {0:0}", Score.CurrentScore);
        }
        if (highScoreText != null)
        {
            highScoreText.text = newRecord ? "YENİ REKOR!" : "EN YÜKSEK: " + Score.HighScore;
        }
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
    }
}
