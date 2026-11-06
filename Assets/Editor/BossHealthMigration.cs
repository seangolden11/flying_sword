using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class BossHealthMigration
{
    static BossHealthMigration() => EditorApplication.delayCall += Apply;

    static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged += AfterStop;
            return;
        }
        string[] paths = { "Assets/Scenes/Boss.unity", "Assets/Scenes/Metroidvania.unity" };
        foreach (string path in paths)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            bool changed = false;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var placement in root.GetComponentsInChildren<EnemyPlacement>(true))
                {
                    if (!placement.boss || placement.health == EnemySystem.BossMaxHealth) continue;
                    var data = new SerializedObject(placement);
                    data.FindProperty("health").intValue = EnemySystem.BossMaxHealth;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (opened) EditorSceneManager.CloseScene(scene,true);
        }
    }

    static void AfterStop(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        EditorApplication.playModeStateChanged -= AfterStop;
        EditorApplication.delayCall += Apply;
    }
}
