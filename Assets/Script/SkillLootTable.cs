using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Loot table de skills — ScriptableObject réutilisable.
/// Permet de tirer un skill aléatoire parmi une liste prédéfinie.
///
/// Contrairement aux modules, les skills n'ont pas de notion de "déjà possédé".
/// GetRandom() tire donc sans filtre parmi toutes les entrées non-nulles de la liste.
///
/// Utilisation :
///   Créer via clic droit → RPG → Skill Loot Table
///   Glisser dans le champ skillLootTable d'un EventEffect ou d'un loot pool
/// </summary>
[CreateAssetMenu(fileName = "New Skill Loot Table", menuName = "RPG/Skill Loot Table")]
public class SkillLootTable : ScriptableObject
{
    [Tooltip("Liste des skills pouvant être tirés au sort.\n" +
             "Les entrées nulles sont ignorées automatiquement.")]
    public List<SkillData> skills = new List<SkillData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un skill aléatoire depuis la liste.
    /// Retourne null si la liste est vide ou ne contient que des entrées nulles.
    /// </summary>
    public SkillData GetRandom()
    {
        if (skills == null || skills.Count == 0)
        {
            Debug.LogWarning($"[SkillLootTable] '{name}' : la liste de skills est vide.");
            return null;
        }

        List<SkillData> disponibles = new List<SkillData>();
        foreach (SkillData s in skills)
        {
            if (s != null) disponibles.Add(s);
        }

        if (disponibles.Count == 0)
        {
            Debug.LogWarning($"[SkillLootTable] '{name}' : tous les skills de la liste sont null.");
            return null;
        }

        SkillData tiré = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[SkillLootTable] '{name}' : '{tiré.skillName}' tiré parmi {disponibles.Count} skill(s).");
        return tiré;
    }

    /// <summary>
    /// Retourne un skill aléatoire parmi ceux qui possèdent le tag indiqué.
    /// Si <paramref name="tag"/> est null, délègue à <see cref="GetRandom"/> sans filtre.
    /// Retourne null si aucun skill ne correspond au tag.
    /// </summary>
    public SkillData GetRandomAvecTag(TagData tag)
    {
        if (tag == null) return GetRandom();

        if (skills == null || skills.Count == 0)
        {
            Debug.LogWarning($"[SkillLootTable] '{name}' : la liste de skills est vide.");
            return null;
        }

        List<SkillData> filtrés = new List<SkillData>();
        foreach (SkillData s in skills)
        {
            if (s != null && s.tags != null && s.tags.Any(t => t != null && t.tagName == tag.tagName))
                filtrés.Add(s);
        }

        if (filtrés.Count == 0)
        {
            Debug.LogWarning($"[SkillLootTable] '{name}' : aucun skill avec le tag '{tag.tagName}'.");
            return null;
        }

        SkillData tiré = filtrés[Random.Range(0, filtrés.Count)];
        Debug.Log($"[SkillLootTable] '{name}' : '{tiré.skillName}' tiré parmi {filtrés.Count} skill(s) avec le tag '{tag.tagName}'.");
        return tiré;
    }
}
