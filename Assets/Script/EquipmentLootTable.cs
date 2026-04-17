using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Loot table d'équipements — ScriptableObject réutilisable.
/// Permet de tirer un équipement aléatoire parmi une liste prédéfinie.
///
/// Contrairement aux modules, les équipements n'ont pas de notion de "déjà possédé"
/// (un slot peut être remplacé à tout moment). GetRandom() tire donc sans filtre
/// parmi toutes les entrées non-nulles de la liste.
///
/// Utilisation :
///   Créer via clic droit → RPG → Equipment Loot Table
///   Glisser dans le champ equipmentLootTable d'un EventEffect
///   (type GainEquipment, mode FromLootTable)
/// </summary>
[CreateAssetMenu(fileName = "New Equipment Loot Table", menuName = "RPG/Equipment Loot Table")]
public class EquipmentLootTable : ScriptableObject
{
    [Tooltip("Liste des équipements pouvant être tirés au sort.\n" +
             "Les entrées nulles sont ignorées automatiquement.")]
    public List<EquipmentData> equipments = new List<EquipmentData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un équipement aléatoire depuis la liste.
    /// Retourne null si la liste est vide ou ne contient que des entrées nulles.
    /// </summary>
    public EquipmentData GetRandom()
    {
        if (equipments == null || equipments.Count == 0)
        {
            Debug.LogWarning($"[EquipmentLootTable] '{name}' : la liste d'équipements est vide.");
            return null;
        }

        // Filtre les entrées nulles éventuelles
        List<EquipmentData> disponibles = new List<EquipmentData>();
        foreach (EquipmentData e in equipments)
        {
            if (e != null) disponibles.Add(e);
        }

        if (disponibles.Count == 0)
        {
            Debug.LogWarning($"[EquipmentLootTable] '{name}' : tous les équipements de la liste sont null.");
            return null;
        }

        EquipmentData tiré = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[EquipmentLootTable] '{name}' : '{tiré.equipmentName}' tiré parmi {disponibles.Count} équipement(s).");
        return tiré;
    }

    /// <summary>
    /// Retourne un équipement aléatoire parmi ceux qui possèdent le tag indiqué.
    /// Si <paramref name="tag"/> est null, délègue à <see cref="GetRandom"/> sans filtre.
    /// Retourne null si aucun équipement ne correspond au tag.
    /// </summary>
    public EquipmentData GetRandomAvecTag(TagData tag)
    {
        if (tag == null) return GetRandom();

        if (equipments == null || equipments.Count == 0)
        {
            Debug.LogWarning($"[EquipmentLootTable] '{name}' : la liste d'équipements est vide.");
            return null;
        }

        List<EquipmentData> filtrés = new List<EquipmentData>();
        foreach (EquipmentData e in equipments)
        {
            if (e != null && e.tags != null && e.tags.Any(t => t != null && t.tagName == tag.tagName))
                filtrés.Add(e);
        }

        if (filtrés.Count == 0)
        {
            Debug.LogWarning($"[EquipmentLootTable] '{name}' : aucun équipement avec le tag '{tag.tagName}'.");
            return null;
        }

        EquipmentData tiré = filtrés[Random.Range(0, filtrés.Count)];
        Debug.Log($"[EquipmentLootTable] '{name}' : '{tiré.equipmentName}' tiré parmi {filtrés.Count} équipement(s) avec le tag '{tag.tagName}'.");
        return tiré;
    }
}
