using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Pool de types de cases pour la résolution des cases Aleatoire au runtime.
/// Définit les types possibles, leur poids de tirage et leur maximum d'occurrences par carte.
/// </summary>
[CreateAssetMenu(fileName = "NewCellAleaPool", menuName = "RPG/Cell Alea Pool")]
public class CellAleaPool : ScriptableObject
{
    public List<CellAleaEntry> entrees;

    /// <summary>
    /// Tire un type au hasard depuis la pool en tenant compte des poids et des maxOccurrences.
    /// Les entrées avec poids <= 0 sont ignorées.
    /// Les entrées dont maxOccurrences > 0 ET comptesActuels[type] >= maxOccurrences sont exclues.
    /// Retourne CellType.Empty si aucune entrée n'est disponible.
    /// </summary>
    public CellType TirerAleatoire(Dictionary<CellType, int> comptesActuels)
    {
        if (entrees == null || entrees.Count == 0) return CellType.Empty;

        List<CellAleaEntry> disponibles = new List<CellAleaEntry>();
        foreach (CellAleaEntry entree in entrees)
        {
            if (entree.poids <= 0) continue;

            if (entree.maxOccurrences > 0)
            {
                int compteActuel = 0;
                if (comptesActuels != null)
                    comptesActuels.TryGetValue(entree.type, out compteActuel);
                if (compteActuel >= entree.maxOccurrences) continue;
            }

            disponibles.Add(entree);
        }

        if (disponibles.Count == 0) return CellType.Empty;

        int totalPoids = 0;
        foreach (CellAleaEntry e in disponibles)
            totalPoids += e.poids;

        int tirage = Random.Range(0, totalPoids);
        int cumul = 0;
        foreach (CellAleaEntry e in disponibles)
        {
            cumul += e.poids;
            if (tirage < cumul) return e.type;
        }

        return disponibles[disponibles.Count - 1].type;
    }
}

/// <summary>
/// Entrée d'une CellAleaPool : type tiré, poids et limite d'occurrences.
/// </summary>
[System.Serializable]
public class CellAleaEntry
{
    public CellType type;
    public int poids;           // Poids de tirage (0 = ignoré)
    public int maxOccurrences;  // Max fois que ce type peut sortir sur cette carte (0 = illimité)
}
