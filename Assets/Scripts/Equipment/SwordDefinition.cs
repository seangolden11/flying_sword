using UnityEngine;

public enum SwordKind { Normal, Speed, Recall, LockOn }

[CreateAssetMenu(menuName = "Flying Sword/Sword Definition")]
public sealed class SwordDefinition : ScriptableObject
{
    public SwordKind kind;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;
    public Color bladeColor = Color.white;
    [Min(1)] public int damage = 1;
    [Min(0)] public float speedBonus;
    [Min(0)] public float recallBonus;
    public bool marksTarget;
    [Min(0)] public int startingCount = 2;
    public bool initiallyEquipped;
}
