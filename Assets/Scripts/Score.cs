using UnityEngine;
using TMPro;

// Run score + device-local high score (PlayerPrefs "HighScore")
public class Score : MonoBehaviour
{
    public const string HighScoreKey = "HighScore";

    public static float scoreValue = 0;
    [SerializeField] private TMP_Text score;
    [Tooltip("Points per second; frame-rate independent so scores are comparable between devices.")]
    [SerializeField] private float pointsPerSecond = 60f;

    public static int CurrentScore { get { return Mathf.FloorToInt(scoreValue); } }
    public static int HighScore { get { return PlayerPrefs.GetInt(HighScoreKey, 0); } }

    // Stores the finished run on the device; returns true for a new record
    public static bool SubmitScore()
    {
        int current = CurrentScore;
        if (current <= HighScore)
        {
            return false;
        }
        PlayerPrefs.SetInt(HighScoreKey, current);
        PlayerPrefs.Save();
        return true;
    }

    // Bonus points (e.g. destroyed obstacles); ignored after game over
    public static void AddPoints(int points)
    {
        if (HeroManager.gameOver) return;
        scoreValue += points;
    }

    void Start()
    {
        scoreValue = 0;
    }

    void Update()
    {
        if (HeroManager.gameOver)
        {
            return;
        }

        scoreValue += Time.deltaTime * pointsPerSecond;
        if (score != null)
        {
            score.SetText("SKOR: {0:0}", CurrentScore);
        }
    }
}
