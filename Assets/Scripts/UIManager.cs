using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

// Opening menu: Play / Character select / Quit. No account needed, the high score is stored on the device.
public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Screens")]
    public GameObject mainMenuUI;
    public GameObject characterSelectUI;

    [Header("Main Menu")]
    [SerializeField] private TMP_Text highScoreText;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != null)
        {
            Debug.Log("Instance already exists, destroying object!");
            Destroy(this);
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        MainMenuScreen();
    }

    public void ClearScreen() //Turn off all screens
    {
        SetActive(mainMenuUI, false);
        SetActive(characterSelectUI, false);
    }

    public void MainMenuScreen() //Opening screen / back to menu
    {
        ClearScreen();
        SetActive(mainMenuUI, true);
        if (highScoreText != null)
        {
            highScoreText.SetText("EN YÜKSEK SKOR: {0:0}", Score.HighScore);
        }
    }

    public void CharacterSelectScreen() //Character select button
    {
        ClearScreen();
        SetActive(characterSelectUI, true);
    }

    public void PlayGame() //Play button
    {
        HeroManager.gameOver = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame() //Quit button
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private static void SetActive(GameObject screen, bool active)
    {
        if (screen != null)
        {
            screen.SetActive(active);
        }
    }
}
