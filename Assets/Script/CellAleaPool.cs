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
