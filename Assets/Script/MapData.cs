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

    // Mode de sélection des événements pour les cases de type Event.
    // ManualList  → liste saisie à la main directement dans la case
    // FromPool    → EventPool ScriptableObject réutilisable
    public EventCellMode eventCellMode;

    // Mode ManualList : liste d'EventData saisie directement dans l'éditeur de carte.
    // Un event aléatoire (non encore joué) est tiré à chaque visite.
    public List<EventData> eventList = new List<EventData>();

    // Mode FromPool : référence vers un EventPool ScriptableObject prédéfini.
    public EventPool eventPool;
}

/// <summary>
/// Mode de sélection des événements pour une case de type Event.
/// </summary>
public enum EventCellMode
{
    ManualList, // Liste d'EventData saisie à la main dans la case
    FromPool,   // EventPool ScriptableObject réutilisable
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
    Event,          // Salle d'événement textuel — eventPool contient les EventData disponibles
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