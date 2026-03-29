using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Loot table de modules — ScriptableObject réutilisable.
/// Permet de tirer un module aléatoire parmi une liste prédéfinie,
/// en excluant automatiquement les modules déjà possédés par le joueur.
///
/// Utilisation :
///   Créer via clic droit → RPG → Module Loot Table
///   Glisser dans le champ moduleLootTable d'un EventEffect (type GainModule, mode FromLootTable)
/// </summary>
[CreateAssetMenu(fileName = "New Module Loot Table", menuName = "RPG/Module Loot Table")]
public class ModuleLootTable : ScriptableObject
{
    [Tooltip("Liste des modules pouvant être tirés au sort.\nLes modules déjà possédés par le joueur sont automatiquement exclus du tirage.")]
    public List<ModuleData> modules = new List<ModuleData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un module aléatoire parmi ceux que le joueur ne possède pas encore.
    /// Retourne null si la liste est vide ou si le joueur possède déjà tous les modules.
    /// </summary>
    public ModuleData GetRandom()
    {
        if (modules == null || modules.Count == 0)
        {
            Debug.LogWarning($"[ModuleLootTable] '{name}' : la liste de modules est vide.");
            return null;
        }

        // Filtre les modules déjà possédés
        List<ModuleData> disponibles = new List<ModuleData>();
        foreach (ModuleData module in modules)
        {
            if (module == null) continue;
            if (RunManager.Instance != null && RunManager.Instance.HasModule(module)) continue;
            disponibles.Add(module);
        }

        if (disponibles.Count == 0)
        {
            Debug.Log($"[ModuleLootTable] '{name}' : le joueur possède déjà tous les modules de cette table.");
            return null;
        }

        // Tirage aléatoire parmi les modules disponibles
        ModuleData tiré = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[ModuleLootTable] '{name}' : '{tiré.moduleName}' tiré parmi {disponibles.Count} module(s) disponible(s).");
        return tiré;
    }
}
