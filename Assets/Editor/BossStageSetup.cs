using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BossStageSetup
{
    [MenuItem("Flying Sword/Apply Boss Stage")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.SaveOpenScenes();
        const string uiPath = "Assets/Game/Prefabs/GameUI.prefab";
        var root = PrefabUtility.LoadPrefabContents(uiPath);
        try
        {
            var manager = root.GetComponent<UIManager>();
            var title = root.transform.Find("Title");
            var tutorial = title.Find("Tutorial stage").GetComponent<RectTransform>();
            var metro = title.Find("Metroidvania stage").GetComponent<RectTransform>();
            tutorial.anchoredPosition = new Vector2(0,80);
            metro.gameObject.SetActive(false);
            var boss = title.Find("Boss stage");
            if (!boss)
            {
                boss = Object.Instantiate(tutorial.gameObject,title).transform;
                boss.name = "Boss stage";
            }
            boss.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-50);
            boss.GetComponentInChildren<Text>().text = "보스전";
            var button = boss.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            UnityEventTools.AddPersistentListener(button.onClick,manager.StartBoss);
            title.Find("Stage hint").GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-330);
            PrefabUtility.SaveAsPrefabAsset(root,uiPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Combat.unity");
        var enemies = Object.FindFirstObjectByType<EnemySystem>();
        var placement = new GameObject("Boss Spawn").AddComponent<EnemyPlacement>();
        placement.transform.position = new Vector2(2,0);
        placement.boss = true;
        placement.spawnOnStart = true;
        placement.health = EnemySystem.BossMaxHealth;
        placement.scale = 3;
        var settings = new SerializedObject(enemies);
        settings.FindProperty("fixedEncounters").boolValue = true;
        settings.FindProperty("detectionRange").floatValue = 10;
        var points = settings.FindProperty("placements");
        points.arraySize = 1;
        points.GetArrayElementAtIndex(0).objectReferenceValue = placement;
        settings.ApplyModifiedPropertiesWithoutUndo();
        var player = Object.FindFirstObjectByType<PlayerController>();
        var playerSettings = new SerializedObject(player);
        playerSettings.FindProperty("spawnPosition").vector2Value = new Vector2(-6,0);
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        player.transform.position = new Vector2(-6,0);
        SetLoadout();
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/Boss.unity");
        var buildScenes = EditorBuildSettings.scenes.ToList();
        if (!buildScenes.Any(item => item.path == "Assets/Scenes/Boss.unity"))
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Boss.unity",true));
        EditorBuildSettings.scenes = buildScenes.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        Debug.Log("Boss stage and title selection saved.");
    }
    [MenuItem("Flying Sword/Set Boss Starting Sword To One")]
    static void SaveLoadout()
    {
        if (EditorApplication.isPlaying) return;
        string previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Boss.unity");
        SetLoadout();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
    }

    static void SetLoadout()
    {
        var definitions = new SwordDefinition[4];
        for (int i = 0; i < definitions.Length; i++)
        {
            string kind = ((SwordKind)i).ToString();
            string path = "Assets/Game/Equipment/Boss" + kind + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<SwordDefinition>(path);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<SwordDefinition>();
                AssetDatabase.CreateAsset(definition,path);
            }
            EditorUtility.CopySerialized(AssetDatabase.LoadAssetAtPath<SwordDefinition>(
                "Assets/Game/Equipment/" + kind + ".asset"),definition);
            definition.name = "Boss" + kind;
            definition.startingCount = i == 0 ? 1 : 0;
            definition.initiallyEquipped = i == 0;
            EditorUtility.SetDirty(definition);
            definitions[i] = definition;
        }
        var settings = new SerializedObject(Object.FindFirstObjectByType<EquipmentSystem>());
        var catalog = settings.FindProperty("definitions");
        catalog.arraySize = definitions.Length;
        for (int i = 0; i < definitions.Length; i++)
            catalog.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
    }

}
