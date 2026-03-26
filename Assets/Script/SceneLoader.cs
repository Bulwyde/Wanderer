using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadScene(string sceneName)
    {
        Debug.Log($"Chargement de la scène : {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void GoToMainMenu()          => LoadScene("MainMenu");
    public void GoToNavigation()        => LoadScene("Navigation");
    public void GoToCombat()            => LoadScene("Combat");
    public void GoToEvent()             => LoadScene("Event");
}