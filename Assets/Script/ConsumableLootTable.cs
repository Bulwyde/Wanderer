using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Loot table de consommables — ScriptableObject réutilisable.
/// Permet de tirer un consommable aléatoire parmi une liste prédéfinie.
///
/// Différence clé avec ModuleLootTable : les consommables peuvent être obtenus
/// en plusieurs exemplaires, donc aucun filtre "déjà possédé" n'est appliqué.
/// Si l'inventaire est plein au moment de l'ajout, RunManager.AddConsumable()
/// le signale et retourne false — géré dans EventManager.
///
/// Utilisation :
///   Créer via clic droit → RPG → Consumable Loot Table
///   Glisser dans le champ consumableLootTable d'un EventEffect
///   (type GainConsumable, mode FromLootTable)
/// </summary>
[CreateAssetMenu(fileName = "New Consumable Loot Table", menuName = "RPG/Consumable Loot Table")]
public class ConsumableLootTable : ScriptableObject
{
    [Tooltip("Liste des consommables pouvant être tirés au sort.\n" +
             "Les entrées nulles sont ignorées automatiquement.")]
    public List<ConsumableData> consumables = new List<ConsumableData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un consommable aléatoire depuis la liste.
    /// Retourne null si la liste est vide ou ne contient que des entrées nulles.
    /// </summary>
    public ConsumableData GetRandom()
    {
        if (consumables == null || consumables.Count == 0)
        {
            Debug.LogWarning($"[ConsumableLootTable] '{name}' : la liste de consommables est vide.");
            return null;
        }

        // Filtre les entrées nulles éventuelles
        List<ConsumableData> disponibles = new List<ConsumableData>();
        foreach (ConsumableData c in consumables)
        {
            if (c != null) disponibles.Add(c);
        }

        if (disponibles.Count == 0)
        {
            Debug.LogWarning($"[ConsumableLootTable] '{name}' : tous les consommables de la liste sont null.");
            return null;
        }

        ConsumableData tiré = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[ConsumableLootTable] '{name}' : '{tiré.consumableName}' tiré parmi {disponibles.Count} consommable(s).");
        return tiré;
    }
}
