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
    public static void Combat() => Load("Combat");
    public static void Metroidvania() => Load("Metroidvania");
    public static void Restart() => Load(SceneManager.GetActiveScene().name);

    static void Load(string sceneName)
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(sceneName);
    }
}
