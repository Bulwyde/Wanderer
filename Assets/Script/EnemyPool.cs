using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool de rencontres prédéfini — ScriptableObject réutilisable.
/// Chaque entrée peut être soit un EnemyData (ennemi seul) soit un EnemyGroup (groupe).
/// Permet de mélanger les deux types dans une même pool.
///
/// ⚠ MIGRATION : ce fichier a été modifié pour passer de List<EnemyData> à List<EnemyPoolEntry>.
/// Les pools existantes doivent être ré-assignées dans l'Inspector après ce changement.
///
/// Utilisation :
///   Créer via clic droit → RPG → Enemy Pool
///   Glisser dans les champs normalEnemyPool / eliteEnemyPool / bossEnemyPool de la MapData
/// </summary>
[CreateAssetMenu(fileName = "New Enemy Pool", menuName = "RPG/Enemy Pool")]
public class EnemyPool : ScriptableObject
{
    [Tooltip("Entrées de la pool — chaque entrée est soit un ennemi seul, soit un groupe.")]
    public List<EnemyPoolEntry> entries = new List<EnemyPoolEntry>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne une entrée aléatoire parmi les entrées valides.
    /// Une entrée est valide si elle contient au moins un EnemyData ou un EnemyGroup non-null.
    /// Retourne null si la pool est vide ou ne contient que des entrées invalides.
    /// </summary>
    public EnemyPoolEntry PickRandom()
    {
        if (entries == null || entries.Count == 0)
        {
            Debug.LogWarning($"[EnemyPool] '{name}' : la liste d'entrées est vide.");
            return null;
        }

        // Filtre les entrées invalides
        List<EnemyPoolEntry> valides = new List<EnemyPoolEntry>();
        foreach (EnemyPoolEntry e in entries)
        {
            if (e != null && (e.enemyData != null || e.enemyGroup != null))
                valides.Add(e);
        }

        if (valides.Count == 0)
        {
            Debug.LogWarning($"[EnemyPool] '{name}' : aucune entrée valide (enemyData et enemyGroup tous null).");
            return null;
        }

        // Tirage pondéré si au moins une entrée a un poids > 0, sinon tirage uniforme
        bool useWeights = false;
        float totalWeight = 0f;
        foreach (EnemyPoolEntry e in valides)
        {
            if (e.weight > 0f) { useWeights = true; totalWeight += e.weight; }
        }

        EnemyPoolEntry choisi;
        if (useWeights && totalWeight > 0f)
        {
            float roll = Random.Range(0f, totalWeight);
            float cumul = 0f;
            choisi = valides[valides.Count - 1]; // fallback
            foreach (EnemyPoolEntry e in valides)
            {
                if (e.weight <= 0f) continue;
                cumul += e.weight;
                if (roll <= cumul) { choisi = e; break; }
            }
        }
        else
        {
            choisi = valides[Random.Range(0, valides.Count)];
        }

        string nom = choisi.IsGroup
            ? $"[Groupe] {choisi.enemyGroup.groupName}"
            : choisi.enemyData.enemyName;
        Debug.Log($"[EnemyPool] '{name}' : '{nom}' tiré parmi {valides.Count} entrée(s).");
        return choisi;
    }
}

/// <summary>
/// Une entrée dans une EnemyPool — soit un ennemi seul, soit un groupe.
/// Un seul des deux champs doit être renseigné (si les deux le sont, le groupe a la priorité).
/// </summary>
[System.Serializable]
public class EnemyPoolEntry
{
    [Tooltip("Ennemi seul — laisser null si c'est un groupe.")]
    public EnemyData  enemyData;

    [Tooltip("Groupe d'ennemis — prioritaire sur enemyData si les deux sont renseignés.")]
    public EnemyGroup enemyGroup;

    [Tooltip("Poids de tirage — 0 = tirage uniforme. Si au moins une entrée de la pool a un poids > 0, le tirage pondéré est activé.")]
    public float weight = 0f;

    /// <summary>Retourne true si cette entrée représente un groupe (EnemyGroup non-null).</summary>
    public bool IsGroup => enemyGroup != null;
}
