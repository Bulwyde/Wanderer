using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Définit un équipement du jeu.
/// Stocke les stats, effets passifs et compétences actives.
/// La génération aléatoire du contenu est gérée par le système de loot.
/// </summary>
[CreateAssetMenu(fileName = "NewEquipment", menuName = "RPG/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    [Header("Identité")]
    public string equipmentID;
    public string equipmentName;
    public Sprite icon;

    [Header("Description")]
    [TextArea(2, 5)]
    public string description;
    public KeywordData[] keywords;

    [Header("Type d'équipement")]
    // Décrit la nature de la pièce (tête, torse, bras...).
    // Pour les bras, le choix du slot gauche/droit se fait au moment de l'équipement —
    // la pièce elle-même n'a pas de main assignée.
    public EquipmentType equipmentType;

    [Header("Niveau")]
    // Niveau actuel de l'équipement — influence les stats et compétences disponibles
    public int level = 1;

    [Header("Stats")]
    // Stats que cet équipement apporte au joueur
    public int bonusHP;
    public int bonusAttack;
    public int bonusDefense;
    [Range(0f, 1f)] public float bonusCriticalChance;
    public float bonusCriticalMultiplier;
    public int bonusRegeneration;
    [Range(0f, 1f)] public float bonusLifeSteal;

    [Header("Effets passifs")]
    // Effets toujours actifs tant que l'équipement est porté
    // Maximum 3 effets passifs (tous emplacements)
    public List<EffectData> passiveEffects;

    [Header("Compétences actives")]
    // Emplacements configurables pour équiper des skills (états : Available / Used / Unavailable / LockedInUse).
    // Bras : 1 à 4 slots | Autres emplacements : jusqu'à 3 slots.
    public List<SkillSlot> skillSlots = new List<SkillSlot>();

    [Header("Tags")]
    // Tags sémantiques pour les interactions et la gestion du loot
    // Ex : Tag_Épée, Tag_Feu, Tag_Maudit — créer les assets dans Assets/ScriptableObjects/Tags/
    public List<TagData> tags = new List<TagData>();

    [Header("Set")]
    // Identifiant du set auquel appartient cet équipement (vide si aucun)
    public string setID;
}

/// <summary>
/// Type d'une pièce d'équipement — décrit ce qu'elle est, pas où elle est portée.
/// Utilisé sur EquipmentData pour définir la nature de la pièce.
/// Pour les bras, une pièce est simplement de type Arm : c'est au moment de
/// l'équipement qu'on choisit le slot gauche (Arm1) ou droit (Arm2).
/// </summary>
public enum EquipmentType
{
    Head,   // Tête
    Torso,  // Torse
    Legs,   // Jambes
    Arm     // Bras (gauche ou droit, choisi à l'équipement)
}

/// <summary>
/// Les emplacements physiques du joueur — utilisés par le RunManager pour tracker
/// ce qui est porté. Arm1 = bras gauche, Arm2 = bras droit.
/// </summary>
public enum EquipmentSlot
{
    Head,   // Tête
    Torso,  // Torse
    Legs,   // Jambes
    Arm1,   // Bras gauche
    Arm2,   // Bras droit
    Arm3,   // Bras 3
    Arm4    // Bras 4
}