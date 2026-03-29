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
///   GainConsumable  → consumableToGive
///   GainModule      → moduleToGive
///   SetEventFlag    → flagKey + flagValue
/// </summary>
[System.Serializable]
public class EventEffect
{
    public EventEffectType type;

    [Tooltip("Valeur numérique. Positive = gain, négative = perte.\nUtilisé par : ModifyHP, ModifyMaxHP.")]
    public int value;

    [Header("Références optionnelles (selon le type)")]
    [Tooltip("Consommable à donner au joueur.\nUtilisé par : GainConsumable.")]
    public ConsumableData consumableToGive;

    [Tooltip("Mode de distribution du module.\nFromList = donne tous les modules de la liste.\nFromLootTable = tire un module aléatoire dans la loot table (modules déjà possédés exclus).\nUtilisé par : GainModule.")]
    public GainModuleMode gainModuleMode;

    [Tooltip("Liste de modules à donner au joueur (mode FromList).\nTous les modules non encore possédés sont donnés.\nUtilisé par : GainModule → FromList.")]
    public List<ModuleData> modulesToGive = new List<ModuleData>();

    [Tooltip("Loot table depuis laquelle un module aléatoire est tiré (mode FromLootTable).\nLes modules déjà possédés sont exclus du tirage.\nUtilisé par : GainModule → FromLootTable.")]
    public ModuleLootTable moduleLootTable;

    [Tooltip("Clé du flag d'événement à poser dans RunManager.\nUtilisé par : SetEventFlag.")]
    public string flagKey;

    [Tooltip("Valeur du flag (true/false).\nUtilisé par : SetEventFlag.")]
    public bool flagValue;
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
/// Types d'effets disponibles dans les événements Overworld.
/// </summary>
public enum EventEffectType
{
    ModifyHP,       // Soigne ou blesse le joueur (hors combat) — champ : value
    ModifyMaxHP,    // Augmente ou réduit les HP max — champ : value
    HealToFull,     // Soigne le joueur à ses HP max (aucun champ requis)
    GainConsumable, // Donne un consommable si slot libre — champ : consumableToGive
    GainModule,     // Donne un module (relique) — champ : moduleToGive
    SetEventFlag,   // Pose un flag booléen dans RunManager — champs : flagKey + flagValue
}
