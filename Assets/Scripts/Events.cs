using UnityEngine.SceneManagement;
using UnityEngine;

public class Events : MonoBehaviour
{
    // Start is called before the first frame update
    public void ReplayGame()
    {
        SceneManager.LoadScene("SampleScene");
    }

    // Update is called once per frame
    public void QuitGame()
    {
        SceneManager.LoadScene(0);
    }
}
