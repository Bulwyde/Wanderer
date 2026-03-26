using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Registre centralisé de tous les événements du jeu.
/// EventManager l'interroge avec un eventID pour trouver le bon EventData.
///
/// Usage : créer un asset unique EventDatabase dans le projet,
/// y glisser tous les EventData, puis l'assigner à l'EventManager dans la scène.
/// </summary>
[CreateAssetMenu(fileName = "EventDatabase", menuName = "RPG/Event Database")]
public class EventDatabase : ScriptableObject
{
    [Tooltip("Liste de tous les événements du jeu")]
    public List<EventData> events = new List<EventData>();

    /// <summary>
    /// Retourne l'EventData correspondant à l'ID donné, ou null si introuvable.
    /// </summary>
    public EventData GetByID(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return events.Find(e => e != null && e.eventID == id);
    }
}
