using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StageExpansion
{
    [MenuItem("Flying Sword/Apply Extended Stages")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) return;
        EditorSceneManager.OpenScene("Assets/Scenes/Combat.unity");
        Set(UnityEngine.Object.FindFirstObjectByType<EnemySystem>(), "waveSize", 1);
        Set(UnityEngine.Object.FindFirstObjectByType<EnemySystem>(), "detectionRange", 10);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene("Assets/Scenes/Metroidvania.unity");
        var geometry = UnityEngine.Object.FindFirstObjectByType<StageGeometry>();
        while (geometry.transform.childCount > 0)
            UnityEngine.Object.DestroyImmediate(geometry.transform.GetChild(0).gameObject);
        var old = GameObject.Find("Fixed Encounters");
        if (old) UnityEngine.Object.DestroyImmediate(old);
        old = GameObject.Find("Boss Room");
        if (old) UnityEngine.Object.DestroyImmediate(old);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/TerrainBlock.prefab");
        var surfaces = new List<BoxCollider2D>();
        BoxCollider2D Block(string name, Vector2 position, Vector2 size, Color? color = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, geometry.transform);
            go.name = name;
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1);
            if (color.HasValue) go.GetComponent<SpriteRenderer>().color = color.Value;
            var collider = go.GetComponent<BoxCollider2D>();
            surfaces.Add(collider);
            return collider;
        }
        Block("Floor", new Vector2(0,-7.25f),new Vector2(133,1));
        Block("Ceiling",new Vector2(0,7.25f),new Vector2(133,1));
        Block("West wall",new Vector2(-66.5f,0),new Vector2(1,15));
        Block("East wall",new Vector2(66.5f,0),new Vector2(1,15));
        for (int i = 0; i < 4; i++)
        {
            float x = -53 + i * 23;
            Block("Lower ledge " + i,new Vector2(x,-3.8f),new Vector2(7,1));
            Block("Upper ledge " + i,new Vector2(x+7,3.6f),new Vector2(6,.8f));
            Block("Pillar " + i,new Vector2(x+13,-4.2f),new Vector2(2,4.8f));
            Block("Suspended bridge " + i,new Vector2(x+17,1),new Vector2(5,.8f));
        }
        Block("Boss approach ledge",new Vector2(38,-4),new Vector2(6,1));
        Color bronze = new Color32(115,75,62,255);
        Block("Boss entrance lintel",new Vector2(43,5.4f),new Vector2(1,2.7f),bronze);
        Block("Boss entrance base",new Vector2(43,-5.4f),new Vector2(1,2.7f),bronze);
        var door = Block("Boss gate",new Vector2(43,0),new Vector2(.7f,8.2f),new Color32(225,111,76,255)).gameObject;
        door.SetActive(false);
        Set(geometry,"surfaces",surfaces.ToArray());
        Set(geometry,"size",new Vector2(132,13.6f));
        var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>();
        Set(follow,"arenaSize",new Vector2(132,13.6f));
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        Set(player,"spawnPosition",new Vector2(-60,0));
        player.transform.position = new Vector2(-60,0);
        follow.transform.position = new Vector3(-53,0,-10);
        var group = new GameObject("Fixed Encounters").transform;
        var placements = new List<EnemyPlacement>();
        EnemyPlacement Place(string name, Vector2 position, bool boss = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(group,false);
            go.transform.position = position;
            var placement = go.AddComponent<EnemyPlacement>();
            placement.boss = boss;
            placement.health = boss ? 180 : 30;
            placement.scale = boss ? 2 : 1;
            placements.Add(placement);
            return placement;
        }
        Place("West sentry",new Vector2(-49,0));
        Place("Upper passage guard",new Vector2(-40,4.9f));
        Place("First hall guard",new Vector2(-33,-1));
        Place("Lower passage guard",new Vector2(-23,-5.5f));
        Place("Bridge guard",new Vector2(-17,2.5f));
        Place("Central sentry",new Vector2(-8,0));
        Place("Central upper guard",new Vector2(0,5));
        Place("East hall guard",new Vector2(10,-1));
        Place("East bridge guard",new Vector2(20,5));
        Place("Gate guard",new Vector2(35,-1));
        var boss = Place("Boss - sealed chamber",new Vector2(58,0),true);
        var enemies = UnityEngine.Object.FindFirstObjectByType<EnemySystem>();
        Set(enemies,"detectionRange",10);
        Set(enemies,"fixedEncounters",true);
        Set(enemies,"placements",placements.ToArray());
        var room = new GameObject("Boss Room").AddComponent<BossRoom>();
        Set(room,"player",player);Set(room,"enemies",enemies);
        Set(room,"bossPlacement",boss);Set(room,"entranceDoor",door);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        Debug.Log("Extended stages saved: one enemy per combat wave, 132-unit exploration, ten fixed guards and sealed boss room.");
    }

    static void Set(Object target,string field,object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(field);
        if (value is int count) property.intValue = count;
        else if (value is bool enabled) property.boolValue = enabled;
        else if (value is Vector2 vector) property.vector2Value = vector;
        else if (value is Object item) property.objectReferenceValue = item;
        else if (value is Object[] items)
        {
            property.arraySize = items.Length;
            for(int i=0;i<items.Length;i++) property.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
