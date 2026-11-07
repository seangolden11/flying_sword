using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneRouter : MonoBehaviour
{
    [SerializeField] bool redirectToTitle;

    void Start()
    {
        if (redirectToTitle)
            Title();
    }

    public static void Title() => Load("Title");
    public static void Combat() => StartStory("Combat");
    public static void Metroidvania() => StartStory("Metroidvania");
    public static void Tutorial() => StartStory("Tutorial");
    public static void Boss() => StartStory("Boss");

    public static void Restart()
    {
        StoryProgress.RestoreStageEntry();
        Load(SceneManager.GetActiveScene().name);
    }

    public static void ContinueTo(string sceneName) => Load(sceneName);

    static void StartStory(string sceneName)
    {
        StoryProgress.StartNewStory();
        Load(sceneName);
    }

    static void Load(string sceneName)
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }
}
