using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Définit la structure fixe d'une carte.
/// Designée à la main via l'outil d'édition Unity.
/// Le contenu aléatoire des salles est géré par le RunManager.
/// </summary>
[CreateAssetMenu(fileName = "NewMap", menuName = "RPG/Map Data")]
public class MapData : ScriptableObject
{
    [Header("Identité")]
    public string mapID;
    public string mapName;

    // Sprite de fond affiché derrière la grille
    public Sprite backgroundSprite;

    [Header("Dimensions")]
    public int width = 30;
    public int height = 30;

    [Header("Cases")]
    // Liste de toutes les cases de la grille
    // Stockées à plat : l'index d'une case (x,y) = x + y * width
    public List<CellData> cells = new List<CellData>();

    [Header("Murs")]
    // Liste de tous les murs entre cases adjacentes
    public List<WallData> walls = new List<WallData>();

    /// <summary>
    /// Retourne la case à la position (x, y).
    /// </summary>
    public CellData GetCell(int x, int y)
    {
        int index = x + y * width;
        if (index < 0 || index >= cells.Count) return null;
        return cells[index];
    }

    /// <summary>
    /// Vérifie si un mur existe entre deux cases adjacentes.
    /// </summary>
    public bool HasWall(int x1, int y1, int x2, int y2)
    {
        return walls.Exists(w =>
            (w.x1 == x1 && w.y1 == y1 && w.x2 == x2 && w.y2 == y2) ||
            (w.x1 == x2 && w.y1 == y2 && w.x2 == x1 && w.y2 == y1));
    }
}

/// <summary>
/// Représente une case de la grille.
/// </summary>
[System.Serializable]
public class CellData
{
    // Position dans la grille
    public int x;
    public int y;

    // Type de la case
    public CellType cellType;

    // Identifiant optionnel pour les salles à contenu précis
    // Ex : "event_radar_1" pour un événement spécifique designé à la main
    public string specificEventID;
}

/// <summary>
/// Types de cases disponibles dans l'éditeur de cartes.
/// </summary>
public enum CellType
{
    Empty,          // Case vide — le joueur peut passer sans effet
    Start,          // Case de départ du joueur
    Boss,           // Salle du boss
    Classic,        // Salle classique — contenu décidé par la génération
    Event,          // Salle d'événement textuel — specificEventID pointe vers un EventData
    NonNavigable,   // Bloc non navigable — obstacle infranchissable
}

/// <summary>
/// Représente un mur entre deux cases adjacentes.
/// Bloque à la fois le passage et la visibilité.
/// </summary>
[System.Serializable]
public class WallData
{
    // Coordonnées des deux cases séparées par ce mur
    public int x1, y1;
    public int x2, y2;
}