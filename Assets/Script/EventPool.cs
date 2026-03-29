using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool d'événements prédéfini — ScriptableObject réutilisable.
/// Permet de tirer un EventData aléatoire parmi une liste,
/// en excluant automatiquement les événements déjà joués pendant la run.
///
/// Utilisation :
///   Créer via clic droit → RPG → Event Pool
///   Glisser dans le champ eventPool d'une CellData de type Event (mode FromPool)
/// </summary>
[CreateAssetMenu(fileName = "New Event Pool", menuName = "RPG/Event Pool")]
public class EventPool : ScriptableObject
{
    [Tooltip("Liste des EventData pouvant être tirés au sort.\nLes événements déjà joués pendant la run sont automatiquement exclus.")]
    public List<EventData> events = new List<EventData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un EventData aléatoire parmi ceux qui n'ont pas encore été joués.
    /// Retourne null si la liste est vide ou si tous les événements ont été joués.
    /// </summary>
    public EventData GetRandom()
    {
        if (events == null || events.Count == 0)
        {
            Debug.LogWarning($"[EventPool] '{name}' : la liste d'événements est vide.");
            return null;
        }

        List<EventData> disponibles = new List<EventData>();
        foreach (EventData ev in events)
        {
            if (ev == null) continue;
            if (RunManager.Instance != null && RunManager.Instance.IsEventPlayed(ev.eventID)) continue;
            disponibles.Add(ev);
        }

        if (disponibles.Count == 0)
        {
            Debug.Log($"[EventPool] '{name}' : tous les événements ont déjà été joués.");
            return null;
        }

        EventData choisi = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[EventPool] '{name}' : '{choisi.eventID}' tiré parmi {disponibles.Count} disponible(s).");
        return choisi;
    }
}
