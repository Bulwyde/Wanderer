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

    [Header("Module de départ")]
    // Module passif donné au joueur au début de chaque run avec ce personnage.
    // Équivalent d'une relique de départ (comme dans Slay the Spire).
    // Peut être null si le personnage n'a pas de module de départ.
    public ModuleData startingModule;

    [Header("Équipement de départ")]
    // Les 5 emplacements d'équipement — peuvent être null si vides au départ
    public EquipmentData startingHead;
    public EquipmentData startingTorso;
    public EquipmentData startingLegs;
    public EquipmentData startingArm1;
    public EquipmentData startingArm2;

    [Header("Consommables de départ")]
    // Consommables donnés au joueur au début de chaque run avec ce personnage.
    // Limités par maxConsumableSlots du RunManager (3 par défaut).
    public List<ConsumableData> startingConsumables;
}