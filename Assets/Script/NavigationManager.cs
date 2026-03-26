using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Gère la navigation du joueur sur la carte.
/// Calcule la visibilité des cases selon la position du joueur
/// et les murs qui bloquent la ligne de vue.
/// </summary>
public class NavigationManager : MonoBehaviour
{
    [Header("Références")]
    public MapData mapData;
    public MapRenderer mapRenderer;

    [Header("Visibilité")]
    // Portée de vision du joueur en nombre de cases
    public int visionRange = 1;

    // Position actuelle du joueur — accessibles par MapRenderer
    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }

    // Cases actuellement visibles
    private HashSet<Vector2Int> visibleCells = new HashSet<Vector2Int>();

    // Cases déjà visitées (restent révélées même hors de la vision)
    private HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

    // Cases déjà vues — restent révélées même hors champ de vision
    private HashSet<Vector2Int> exploredCells = new HashSet<Vector2Int>();

    void Start()
    {
        if (mapData == null)
        {
            Debug.LogError("NavigationManager : aucune MapData assignée !");
            return;
        }

        // Si RunManager a sauvegardé un état (= on revient d'un combat),
        // on le restaure. Sinon, on place le joueur sur la case de départ.
        if (RunManager.Instance != null && RunManager.Instance.hasNavigationState)
            RestoreNavigationState();
        else
            PlacePlayerOnStart();

        UpdateVisibility();
        mapRenderer.RefreshMap();
        mapRenderer.CenterCameraOnPlayer();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))    TryMove(0, -1);
        if (Input.GetKeyDown(KeyCode.DownArrow))  TryMove(0, 1);
        if (Input.GetKeyDown(KeyCode.LeftArrow))  TryMove(-1, 0);
        if (Input.GetKeyDown(KeyCode.RightArrow)) TryMove(1, 0);
    }

    // -----------------------------------------------
    // PLACEMENT DU JOUEUR
    // -----------------------------------------------

    private void PlacePlayerOnStart()
    {
        foreach (CellData cell in mapData.cells)
        {
            if (cell.cellType == CellType.Start)
            {
                PlayerX = cell.x;
                PlayerY = cell.y;
                visitedCells.Add(new Vector2Int(PlayerX, PlayerY));
                Debug.Log($"Joueur placé en ({PlayerX}, {PlayerY}) — Case de départ");
                return;
            }
        }

        Debug.LogWarning("Aucune case de départ trouvée — placement en (0,0)");
        PlayerX = 0;
        PlayerY = 0;
    }

    /// <summary>
    /// Restaure la position du joueur et le brouillard de guerre
    /// depuis les données sauvegardées dans RunManager.
    /// Les Lists sont reconverties en HashSets pour les recherches rapides.
    /// </summary>
    private void RestoreNavigationState()
    {
        PlayerX = RunManager.Instance.savedPlayerX;
        PlayerY = RunManager.Instance.savedPlayerY;

        // Conversion List → HashSet
        visitedCells  = new HashSet<Vector2Int>(RunManager.Instance.savedVisitedCells);
        exploredCells = new HashSet<Vector2Int>(RunManager.Instance.savedExploredCells);

        // Le joueur est toujours sur une case visitée
        visitedCells.Add(new Vector2Int(PlayerX, PlayerY));

        Debug.Log($"Navigation restaurée — Position : ({PlayerX}, {PlayerY})");
    }

    // -----------------------------------------------
    // DÉPLACEMENT
    // -----------------------------------------------

    private void TryMove(int dx, int dy)
    {
        int targetX = PlayerX + dx;
        int targetY = PlayerY + dy;

        // Vérifie les limites de la carte
        if (targetX < 0 || targetX >= mapData.width ||
            targetY < 0 || targetY >= mapData.height)
        {
            Debug.Log("Déplacement impossible — bord de la carte");
            return;
        }

        // Vérifie si un mur bloque le passage
        if (mapData.HasWall(PlayerX, PlayerY, targetX, targetY))
        {
            Debug.Log($"Mur entre ({PlayerX},{PlayerY}) et ({targetX},{targetY})");
            return;
        }

        // Vérifie si la case est navigable
        CellData targetCell = mapData.GetCell(targetX, targetY);
        if (targetCell == null || targetCell.cellType == CellType.NonNavigable)
        {
            Debug.Log("Déplacement impossible — case non navigable");
            return;
        }

        // Déplace le joueur
        PlayerX = targetX;
        PlayerY = targetY;
        visitedCells.Add(new Vector2Int(PlayerX, PlayerY));

        // Met à jour la visibilité et l'affichage
        UpdateVisibility();
        mapRenderer.RefreshMap();

        OnRoomEntered(targetCell);
    }

    // -----------------------------------------------
    // VISIBILITÉ
    // -----------------------------------------------

private void UpdateVisibility()
{
    visibleCells.Clear();

    for (int dy = -visionRange; dy <= visionRange; dy++)
    {
        for (int dx = -visionRange; dx <= visionRange; dx++)
        {
            int checkX = PlayerX + dx;
            int checkY = PlayerY + dy;

            if (checkX < 0 || checkX >= mapData.width ||
                checkY < 0 || checkY >= mapData.height)
                continue;

            // Distance de Tchebychev remplacée par distance euclidienne
            // Une case diagonale à visionRange=1 aura distance ≈ 1.41 → non visible
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > visionRange) continue;

            if (HasClearLineOfSight(PlayerX, PlayerY, checkX, checkY))
            {
                Vector2Int pos = new Vector2Int(checkX, checkY);
                visibleCells.Add(pos);
                exploredCells.Add(pos);
            }
        }
    }
}

    /// <summary>
    /// Vérifie si la ligne de vue entre deux cases est dégagée.
    /// Pour l'instant, vérifie uniquement les murs directs entre cases adjacentes.
    /// </summary>
    private bool HasClearLineOfSight(int x1, int y1, int x2, int y2)
    {
        // La case du joueur est toujours visible
        if (x1 == x2 && y1 == y2) return true;

        // Pour les cases adjacentes, on vérifie juste le mur direct
        if (Mathf.Abs(x2 - x1) + Mathf.Abs(y2 - y1) == 1)
            return !mapData.HasWall(x1, y1, x2, y2);

        // Pour les cases plus lointaines, on trace un chemin case par case
        // et on vérifie les murs à chaque étape
        int stepX = (x2 > x1) ? 1 : (x2 < x1) ? -1 : 0;
        int stepY = (y2 > y1) ? 1 : (y2 < y1) ? -1 : 0;

        int currentX = x1;
        int currentY = y1;

        while (currentX != x2 || currentY != y2)
        {
            int nextX = currentX + stepX;
            int nextY = currentY + stepY;

            if (mapData.HasWall(currentX, currentY, nextX, nextY))
                return false;

            currentX = nextX;
            currentY = nextY;
        }

        return true;
    }

    /// <summary>
    /// Retourne true si une case est visible OU a déjà été visitée.
    /// </summary>
    public bool IsVisible(int x, int y)
    {
        Vector2Int pos = new Vector2Int(x, y);
        return visibleCells.Contains(pos)  ||
               visitedCells.Contains(pos)  ||
               exploredCells.Contains(pos);
    }

    // -----------------------------------------------
    // ARRIVÉE DANS UNE SALLE
    // -----------------------------------------------

    /// <summary>
    /// Appelé à chaque fois que le joueur entre dans une nouvelle case.
    /// Notifie le RunManager de la salle courante, puis déclenche
    /// la transition de scène adaptée au type de salle.
    /// </summary>
    private void OnRoomEntered(CellData cell)
    {
        switch (cell.cellType)
        {
            case CellType.Classic:
            case CellType.Boss:
                if (RunManager.Instance != null &&
                    RunManager.Instance.IsRoomCleared(cell.x, cell.y))
                {
                    Debug.Log($"({PlayerX},{PlayerY}) — Salle déjà complétée, pas de transition");
                    break;
                }

                if (RunManager.Instance == null || SceneLoader.Instance == null)
                {
                    Debug.LogError("RunManager ou SceneLoader introuvable !");
                    break;
                }

                RunManager.Instance.SaveNavigationState(
                    PlayerX, PlayerY, visitedCells, exploredCells);
                RunManager.Instance.EnterRoom(cell);
                SceneLoader.Instance.GoToCombat();
                break;

            case CellType.Event:
                if (RunManager.Instance != null &&
                    RunManager.Instance.IsRoomCleared(cell.x, cell.y))
                {
                    Debug.Log($"({PlayerX},{PlayerY}) — Événement déjà complété, pas de transition");
                    break;
                }

                if (RunManager.Instance == null || SceneLoader.Instance == null)
                {
                    Debug.LogError("RunManager ou SceneLoader introuvable !");
                    break;
                }

                RunManager.Instance.SaveNavigationState(
                    PlayerX, PlayerY, visitedCells, exploredCells);
                RunManager.Instance.EnterRoom(cell);
                SceneLoader.Instance.GoToEvent();
                break;

            case CellType.Start:
                Debug.Log($"({PlayerX},{PlayerY}) — Case de départ, pas de transition");
                break;

            case CellType.Empty:
                Debug.Log($"({PlayerX},{PlayerY}) — Case vide, pas de transition");
                break;
        }
    }
}