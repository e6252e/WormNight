using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSceneManager : AudioSingleton<GameSceneManager>
{
    public void LoadScene(SceneType sceneType)
    {
        string sceneName = AudioSceneName.GetSceneName(sceneType);
        if (string.IsNullOrEmpty(sceneName))
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    public void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
