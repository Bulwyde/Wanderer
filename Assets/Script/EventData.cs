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
/// Intentionnellement simple pour l'instant — sera étendu quand
/// l'or, les modules et d'autres systèmes seront en place.
/// </summary>
[System.Serializable]
public class EventEffect
{
    public EventEffectType type;

    [Tooltip("Valeur de l'effet. Positive = gain, négative = perte.")]
    public int value;
}

/// <summary>
/// Types d'effets disponibles dans les événements.
/// </summary>
public enum EventEffectType
{
    ModifyHP,       // Soigne ou blesse le joueur (hors combat)
    // À venir : ModifyGold, GainEquipment, SetEventFlag, RevealRooms...
}
