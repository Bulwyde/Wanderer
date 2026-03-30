using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Données d'un événement textuel (style Slay the Spire).
/// Chaque événement est un ScriptableObject autonome : texte narratif,
/// image de fond, liste de choix avec leurs effets et leur texte d'outcome.
/// </summary>
[CreateAssetMenu(fileName = "New Event", menuName = "RPG/Event Data")]
public class EventData : ScriptableObject
{
    [Header("Identité")]
    // Doit correspondre au specificEventID de la CellData sur la carte
    public string eventID;
    public string title;

    [Header("Contenu")]
    [TextArea(3, 8)]
    public string description;

    // Image affichée en fond d'écran — peut rester null (fond neutre)
    public Sprite backgroundImage;

    [Header("Choix")]
    public List<EventChoice> choices = new List<EventChoice>();
}

/// <summary>
/// Un choix proposé au joueur dans un événement.
/// </summary>
[System.Serializable]
public class EventChoice
{
    // Texte affiché sur le bouton
    public string choiceText;

    // Texte narratif affiché après que le joueur a fait ce choix
    [TextArea(2, 6)]
    public string outcomeText;

    // Conséquences du choix (soins, dégâts, or futur, flags...)
    public List<EventEffect> effects = new List<EventEffect>();
}

/// <summary>
/// Un effet déclenché par un choix d'événement.
/// Chaque type d'effet utilise un ou plusieurs champs ci-dessous.
/// Les champs non pertinents pour le type choisi sont ignorés par le code.
///
/// Correspondances type → champs utilisés :
///   ModifyHP        → value (positif = soin, négatif = dégâts)
///   ModifyMaxHP     → value (positif = gain de max, négatif = réduction)
///   HealToFull      → (aucun champ, soin complet)
///   GainConsumable  → gainConsumableMode + (consumableToGive | consumableLootTable)
///   GainModule      → gainModuleMode + (modulesToGive | moduleLootTable)
///   GainEquipment   → gainEquipmentMode + (equipmentsToGive | equipmentLootTable)
///   SetEventFlag    → flagKey + flagValue
/// </summary>
[System.Serializable]
public class EventEffect
{
    public EventEffectType type;

    [Tooltip("Valeur numérique. Positive = gain, négative = perte.\nUtilisé par : ModifyHP, ModifyMaxHP.")]
    public int value;

    [Tooltip("Mode de distribution du consommable.\nFromList = donne tous les consommables de la liste (si slots disponibles).\nFromLootTable = tire un consommable aléatoire dans consumableLootTable.\nUtilisé par : GainConsumable.")]
    public GainConsumableMode gainConsumableMode;

    [Tooltip("Liste de consommables à donner au joueur (mode FromList).\nChaque consommable est ajouté si un slot est libre.\nUtilisé par : GainConsumable → FromList.")]
    public List<ConsumableData> consumablesToGive = new List<ConsumableData>();

    [Tooltip("Loot table depuis laquelle un consommable aléatoire est tiré (mode FromLootTable).\nUtilisé par : GainConsumable → FromLootTable.")]
    public ConsumableLootTable consumableLootTable;

    [Tooltip("Mode de distribution du module.\nFromList = donne tous les modules de la liste.\nFromLootTable = tire un module aléatoire dans la loot table (modules déjà possédés exclus).\nUtilisé par : GainModule.")]
    public GainModuleMode gainModuleMode;

    [Tooltip("Liste de modules à donner au joueur (mode FromList).\nTous les modules non encore possédés sont donnés.\nUtilisé par : GainModule → FromList.")]
    public List<ModuleData> modulesToGive = new List<ModuleData>();

    [Tooltip("Loot table depuis laquelle un module aléatoire est tiré (mode FromLootTable).\nLes modules déjà possédés sont exclus du tirage.\nUtilisé par : GainModule → FromLootTable.")]
    public ModuleLootTable moduleLootTable;

    [Tooltip("Mode de distribution de l'équipement.\nFromList = équipe toutes les pièces de la liste (chacune dans son slot).\nFromLootTable = tire un équipement aléatoire dans equipmentLootTable.\nUtilisé par : GainEquipment.")]
    public GainEquipmentMode gainEquipmentMode;

    [Tooltip("Liste d'équipements à donner au joueur (mode FromList — chaque pièce est équipée dans son slot).\nUtilisé par : GainEquipment → FromList.")]
    public List<EquipmentData> equipmentsToGive = new List<EquipmentData>();

    [Tooltip("Loot table depuis laquelle un équipement aléatoire est tiré (mode FromLootTable).\nUtilisé par : GainEquipment → FromLootTable.")]
    public EquipmentLootTable equipmentLootTable;

    [Tooltip("Clé du flag d'événement à poser dans RunManager.\nUtilisé par : SetEventFlag.")]
    public string flagKey;

    [Tooltip("Valeur du flag (true/false).\nUtilisé par : SetEventFlag.")]
    public bool flagValue;

    [Tooltip("Effet de navigation déclenché (téléportation, révélation de zone, compteur...).\nUtilisé par : TriggerNavEffect.")]
    public NavEffect navEffect = new NavEffect();
}

/// <summary>
/// Mode de distribution d'un consommable dans un événement.
/// </summary>
public enum GainConsumableMode
{
    FromList,       // Donne tous les consommables de consumablesToGive (si slots disponibles)
    FromLootTable,  // Tire un consommable aléatoire dans consumableLootTable
}

/// <summary>
/// Mode de distribution d'un module dans un événement.
/// </summary>
public enum GainModuleMode
{
    FromList,       // Donne tous les modules de modulesToGive (non encore possédés)
    FromLootTable,  // Tire un module aléatoire dans moduleLootTable (modules possédés exclus)
}

/// <summary>
/// Mode de distribution d'un équipement dans un événement.
/// </summary>
public enum GainEquipmentMode
{
    FromList,       // Équipe toutes les pièces de equipmentsToGive (chacune dans son slot)
    FromLootTable,  // Tire un équipement aléatoire dans equipmentLootTable
}

/// <summary>
/// Types d'effets disponibles dans les événements Overworld.
/// </summary>
public enum EventEffectType
{
    ModifyHP,       // Soigne ou blesse le joueur (hors combat) — champ : value
    ModifyMaxHP,    // Augmente ou réduit les HP max — champ : value
    HealToFull,     // Soigne le joueur à ses HP max (aucun champ requis)
    GainConsumable, // Donne un consommable si slot libre — champs : gainConsumableMode + (consumableToGive ou consumableLootTable)
    GainModule,     // Donne un module (relique) — champs : gainModuleMode + (modulesToGive ou moduleLootTable)
    GainEquipment,  // Équipe une pièce d'équipement — champs : gainEquipmentMode + (equipmentsToGive ou equipmentLootTable)
    SetEventFlag,   // Pose un flag booléen dans RunManager — champs : flagKey + flagValue
    TriggerNavEffect, // Déclenche un effet de navigation (téléportation, révélation, compteur...) — champ : navEffect
}
