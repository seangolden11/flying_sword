using System.Collections.Generic;
using UnityEngine;

public static class GameArt
{
    static readonly Dictionary<string, Sprite> Cache = new();
    public static float WorldScale(Sprite sprite, float size) => sprite == null ? size :
        size / Mathf.Max(.0001f, Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));
    public static Sprite Get(string name)
    {
        if (!Cache.TryGetValue(name, out Sprite sprite))
            Cache[name] = sprite = Resources.Load<Sprite>("Art/" + name);
        return sprite;
    }
    public static Sprite Weapon(TurnActors.WeaponKind kind) => Get(kind switch {
        TurnActors.WeaponKind.Sword => "sword", TurnActors.WeaponKind.Shard => "shard",
        TurnActors.WeaponKind.Trash => "trash", TurnActors.WeaponKind.Table => "table",
        TurnActors.WeaponKind.Monitor => "monitor", TurnActors.WeaponKind.Fencing => "fencing",
        TurnActors.WeaponKind.Washer => "washer", TurnActors.WeaponKind.Workshop => "synergy_a", _ => "synergy_b"
    });
}
