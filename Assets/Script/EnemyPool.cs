using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool d'ennemis prédéfini — ScriptableObject réutilisable.
/// Permet de tirer un EnemyData aléatoire parmi une liste.
/// Utilisé par RunManager.EnterRoom() quand la case n'a pas de specificEnemy assigné.
///
/// Utilisation :
///   Créer via clic droit → RPG → Enemy Pool
///   Glisser dans les champs normalEnemyPool / eliteEnemyPool / bossEnemyPool de la MapData
/// </summary>
[CreateAssetMenu(fileName = "New Enemy Pool", menuName = "RPG/Enemy Pool")]
public class EnemyPool : ScriptableObject
{
    [Tooltip("Liste des EnemyData pouvant être tirés au sort.")]
    public List<EnemyData> enemies = new List<EnemyData>();

    // -----------------------------------------------
    // TIRAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne un EnemyData aléatoire parmi la liste.
    /// Retourne null si la liste est vide ou ne contient que des entrées nulles.
    /// </summary>
    public EnemyData PickRandom()
    {
        if (enemies == null || enemies.Count == 0)
        {
            Debug.LogWarning($"[EnemyPool] '{name}' : la liste d'ennemis est vide.");
            return null;
        }

        // Filtre les entrées nulles
        List<EnemyData> valides = new List<EnemyData>();
        foreach (EnemyData e in enemies)
            if (e != null) valides.Add(e);

        if (valides.Count == 0)
        {
            Debug.LogWarning($"[EnemyPool] '{name}' : aucune entrée valide dans la liste.");
            return null;
        }

        EnemyData choisi = valides[Random.Range(0, valides.Count)];
        Debug.Log($"[EnemyPool] '{name}' : '{choisi.enemyName}' tiré parmi {valides.Count} disponible(s).");
        return choisi;
    }
}
