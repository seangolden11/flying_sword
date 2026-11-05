using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyRangeSettings
{
    [MenuItem("Flying Sword/Set Enemy Range To 10")]
    static void Apply()
    {
        if (EditorApplication.isPlaying)
        {
            EditorApplication.playModeStateChanged += AfterStop;
            EditorApplication.isPlaying = false;
            return;
        }
        SaveScenes();
    }

    static void AfterStop(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= AfterStop;
        EditorApplication.delayCall += SaveScenes;
    }

    static void SaveScenes()
    {
        string previous = SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        foreach (string name in new[] { "Combat", "Metroidvania" })
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
            foreach (var enemy in Object.FindObjectsByType<EnemySystem>(FindObjectsSortMode.None))
            {
                var settings = new SerializedObject(enemy);
                settings.FindProperty("detectionRange").floatValue = 10;
                settings.ApplyModifiedProperties();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        Debug.Log("Enemy detection range saved as 10 in Combat and Metroidvania.");
    }
}
