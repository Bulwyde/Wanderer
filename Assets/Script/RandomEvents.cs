using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject de fallback pour les salles Event sans événement configuré.
/// Contient une liste de paires [MapData → EventPool] : pour chaque carte,
/// un pool d'événements génériques est utilisé quand une salle Event n'a
/// ni ManualList valide ni FromPool assigné (ou lorsqu'ils sont épuisés).
///
/// Setup :
///   - Créer via RPG → Random Events
///   - Assigner dans l'Inspector de NavigationManager (champ "randomEvents")
///   - Ajouter une entrée par MapData avec son EventPool de fallback associé
/// </summary>
[CreateAssetMenu(fileName = "RandomEvents", menuName = "RPG/Random Events")]
public class RandomEvents : ScriptableObject
{
    // -----------------------------------------------
    // STRUCTURE
    // -----------------------------------------------

    /// <summary>
    /// Associe une MapData à un EventPool de fallback.
    /// </summary>
    [Serializable]
    public class EntreeFallback
    {
        public MapData map;
        public EventPool eventPool;
    }

    // Liste des associations map → pool de fallback
    public List<EntreeFallback> entrees = new List<EntreeFallback>();

    // -----------------------------------------------
    // ACCÈS
    // -----------------------------------------------

    /// <summary>
    /// Retourne le pool de fallback associé à la MapData donnée.
    /// Retourne null si aucune entrée ne correspond.
    /// </summary>
    public EventPool GetPoolPourMap(MapData map)
    {
        if (map == null) return null;

        foreach (EntreeFallback entree in entrees)
        {
            if (entree.map == map)
                return entree.eventPool;
        }

        return null;
    }
}
