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

    [Header("Tags")]
    // Tags sémantiques pour les interactions et les conditions d'effets
    // Ex : Tag_Donjon, Tag_Foret — créer les assets dans Assets/ScriptableObjects/Tags/
    public List<TagData> tags = new List<TagData>();

    [Header("Cases")]
    // Liste de toutes les cases de la grille
    // Stockées à plat : l'index d'une case (x,y) = x + y * width
    public List<CellData> cells = new List<CellData>();

    [Header("Murs")]
    // Liste de tous les murs entre cases adjacentes
    public List<WallData> walls = new List<WallData>();

    [Header("Marchand — défaut")]
    // ShopData utilisé pour toutes les cases Marchand qui n'ont pas
    // de ShopData assigné individuellement.
    public ShopData defaultShopData;

    [Header("Pools d'ennemis")]
    // Piochés aléatoirement quand une case n'a pas de specificEnemy/specificGroup assigné.
    // La pool correspondant au type de la case est utilisée (Normal / Elite / Boss).
    // Chaque entrée peut être un EnemyData (solo) ou un EnemyGroup (multi).
    public EnemyPool normalEnemyPool;
    public EnemyPool eliteEnemyPool;
    public EnemyPool bossEnemyPool;

    [Header("Loot combat — défaut")]
    // Table de loot utilisée si ni l'ennemi/groupe ne définit de lootPool.
    // Sert de fallback global par map pour éviter les combats sans récompense.
    public EquipmentLootTable defaultCombatLootTable;

    [Tooltip("Nombre de choix proposés au joueur quand le loot vient du defaultCombatLootTable.")]
    [Range(1, 4)]
    public int defaultLootOfferCount = 2;

    // Table de skills donnés après combat (fallback global — null = pas de skill loot).
    public SkillLootTable defaultCombatSkillLootTable;

    [Header("Aléatoire")]
    [SerializeField] public CellAleaPool aleatoirePool;

    [Header("Maximums par type")]
    [SerializeField] public List<MaxTypeEntry> maximumsParType;
    [SerializeField] public CellType typeDeRemplacement; // Type utilisé quand un maximum est dépassé

    [Header("Événements par défaut")]
    [Tooltip("Event déclenché par un Teleporteur sans specificEvent — ex : cas issu d'une résolution Aléatoire.")]
    public EventData defaultTeleportEvent;

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
    /// Retourne true si au moins un des 8 voisins (cardinaux + diagonales) de la case (x, y)
    /// est d'un type différent de NonNavigable.
    /// Utilisé pour filtrer l'affichage et la révélation des cases NonNavigable isolées.
    /// </summary>
    public bool AUnVoisinNavigable(int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                CellData voisin = GetCell(x + dx, y + dy);
                if (voisin != null && voisin.cellType != CellType.NonNavigable)
                    return true;
            }
        }
        return false;
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

    // ── Champs Combat (CellType.CombatSimple / CellType.Boss / CellType.Elite) ──────

    // Ennemi solo à affronter dans cette salle.
    // Si null et specificGroup est null, fallback sur la pool de la MapData.
    public EnemyData specificEnemy;

    // Groupe d'ennemis à affronter dans cette salle — prioritaire sur specificEnemy.
    // Utiliser pour des rencontres multi-ennemis spécifiques à cette case.
    public EnemyGroup specificGroup;

    // ── Champs Marchand (CellType.Shop) ─────────────────────────────────────

    // Configuration du marchand pour cette case spécifique.
    // Si null, la MapData.defaultShopData est utilisée en fallback.
    public ShopData shopData;

    // ── Champs PointInteret / Teleporteur ────────────────────────────────────

    // EventData spécifique à cette case (PointInteret, Teleporteur).
    public EventData specificEvent;

    // ── Champs BloqueurLD ────────────────────────────────────────────────────

    // Condition de déblocage du bloqueur.
    public BloqueurCondition condition;
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
    Empty           = 0,    // Case vide — le joueur peut passer sans effet
    Start           = 1,    // Case de départ du joueur
    Boss            = 2,    // Salle du boss
    CombatSimple    = 3,    // Salle de combat classique — contenu décidé par la génération
    Event           = 4,    // Salle d'événement textuel — eventPool contient les EventData disponibles
    NonNavigable    = 5,    // Bloc non navigable — obstacle infranchissable
    Shop            = 6,    // Salle marchand — inventaire persistant généré à la première visite
    Elite           = 7,    // Salle élite — ennemi plus difficile qu'un classique
    BloqueurLD      = 8,    // Bloqueur de ligne de direction — débloqué par condition
    PointInteret    = 9,    // Point d'intérêt — déclenche un EventData spécifique
    Ferrailleur     = 10,   // Ferrailleur — marchand d'équipement usagé
    Radar           = 11,   // Radar — révèle une zone de la carte
    Coffre          = 12,   // Coffre — récompense de loot directe
    Teleporteur     = 13,   // Téléporteur — déplace le joueur vers une autre case
    Aleatoire       = 14,   // Case aléatoire — type tiré depuis CellAleaPool au runtime
    FerailleurUtilise  = 15, // Ferrailleur déjà visité
    TeleporteurUtilise = 16, // Téléporteur déjà utilisé
}

/// <summary>
/// Condition de déblocage d'une case BloqueurLD.
/// </summary>
[System.Serializable]
public class BloqueurCondition
{
    public BloqueurConditionType type;
    public string compteurID;   // Pour CompteurNomme
    public int valeurCible;
}

/// <summary>
/// Type de condition évaluée pour débloquer un BloqueurLD.
/// </summary>
public enum BloqueurConditionType
{
    CompteurNomme    = 0,   // Variable nommée dans RunManager
    CombatsTermines  = 1,   // RunManager.combatsTermines
    EventsTermines   = 2,   // RunManager.eventsTermines
}

/// <summary>
/// Entrée de la liste de maximums par type sur une MapData.
/// </summary>
[System.Serializable]
public class MaxTypeEntry
{
    public CellType type;
    public int maximum;
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
