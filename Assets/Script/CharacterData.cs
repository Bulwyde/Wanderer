using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Définit les données de base d'un personnage jouable.
/// Ce ScriptableObject sert de "fiche de personnage" immuable.
/// Les valeurs qui changent pendant un run (HP actuels, etc.) sont gérées par le RunManager.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "RPG/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identité")]
    // Identifiant unique utilisé par le RunManager pour référencer ce personnage
    public string characterID;
    public string characterName;
    public Sprite portrait;

    [Header("Débloqué")]
    // Si false, le personnage apparaît dans la sélection mais ne peut pas être choisi
    public bool isUnlocked;

    [Header("Stats de base")]
    public int maxHP;
    public int attack;       // S'additionne aux dégâts des compétences
    public int defense;

    [Header("Stats avancées")]
    // Probabilité de coup critique (0 = jamais, 1 = toujours)
    [Range(0f, 1f)]
    public float criticalChance;

    // Multiplicateur de dégâts sur un critique (2 = dégâts x2)
    public float criticalMultiplier = 2f;

    // HP récupérés automatiquement au début de chaque tour
    public int regeneration;

    // Fraction des dégâts infligés convertie en soins (0.1 = 10%)
    [Range(0f, 1f)]
    public float lifeSteal;

    [Header("Énergie")]
    // Énergie disponible chaque tour pour utiliser des compétences.
    // Quand le système d'équipement sera en place, cette valeur pourra être
    // modifiée par des bonus d'équipement ou des effets passifs.
    public int maxEnergy = 3;

    [Header("Compétences de départ")]
    // Liste des compétences disponibles en combat.
    // Placeholder : à terme, elles proviendront de l'équipement équipé.
    // On les assigne ici directement pour pouvoir tester sans équipement.
    public List<SkillData> startingSkills = new List<SkillData>();

    [Header("Équipement de départ")]
    // Les 5 emplacements d'équipement — peuvent être null si vides au départ
    public EquipmentData startingHead;
    public EquipmentData startingTorso;
    public EquipmentData startingLegs;
    public EquipmentData startingArm1;
    public EquipmentData startingArm2;
}