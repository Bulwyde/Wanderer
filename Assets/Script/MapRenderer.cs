using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MapRenderer : MonoBehaviour
{
    [Header("Références")]
    public MapData mapData;
    public NavigationManager navigationManager;
    public RectTransform mapContainer;
    public MapCameraController cameraController;

    [Header("Dimensions")]
    public float cellSize      = 30f;
    public float wallThickness = 6f;

    [Header("Couleurs")]
    public Color colorHidden  = new Color(0.1f, 0.1f, 0.1f);
    public Color colorEmpty   = new Color(0.2f, 0.2f, 0.2f);
    public Color colorStart   = new Color(0.0f, 0.8f, 0.2f);
    public Color colorBoss    = new Color(0.8f, 0.0f, 0.0f);
    public Color colorClassic = new Color(0.3f, 0.5f, 0.8f);
    public Color colorEvent   = new Color(1.0f, 0.5f, 0.0f);
    public Color colorNonNav  = new Color(0.1f, 0.1f, 0.1f);
    public Color colorWall    = new Color(0.9f, 0.7f, 0.1f);
    public Color colorPlayer  = Color.white;

    // Références aux images des cases
    private Image[,] cellImages;

    // Chaque mur stocké avec ses deux cases adjacentes
    private struct WallEntry
    {
        public Image  image;
        public int    x1, y1; // Première case adjacente
        public int    x2, y2; // Deuxième case adjacente
    }
    private List<WallEntry> wallEntries = new List<WallEntry>();

    void Start()
    {
        GenerateGrid();
        RefreshMap();

        // Attend une frame que le Canvas soit prêt avant de centrer
        StartCoroutine(CenterAfterFrame());
    }

    // -----------------------------------------------
    // GÉNÉRATION
    // -----------------------------------------------

    private void GenerateGrid()
    {
        foreach (Transform child in mapContainer)
            Destroy(child.gameObject);

        cellImages  = new Image[mapData.width, mapData.height];
        wallEntries = new List<WallEntry>();
        float step  = cellSize + wallThickness;

        for (int y = 0; y < mapData.height; y++)
        {
            for (int x = 0; x < mapData.width; x++)
            {
                GameObject cell = new GameObject($"Cell_{x}_{y}");
                cell.transform.SetParent(mapContainer, false);

                RectTransform rt = cell.AddComponent<RectTransform>();
                rt.sizeDelta   = new Vector2(cellSize, cellSize);
                rt.anchorMin   = Vector2.zero;
                rt.anchorMax   = Vector2.zero;
                rt.pivot       = Vector2.zero;

                int flippedY   = mapData.height - 1 - y;
                rt.anchoredPosition = new Vector2(
                    wallThickness + x        * step,
                    wallThickness + flippedY * step);

                Image img = cell.AddComponent<Image>();
                img.color = colorHidden;
                cellImages[x, y] = img;
            }
        }

        GenerateWalls();
    }

    private void GenerateWalls()
    {
        float step = cellSize + wallThickness;

        foreach (WallData wall in mapData.walls)
        {
            GameObject wallObj = new GameObject($"Wall_{wall.x1}{wall.y1}_{wall.x2}{wall.y2}");
            wallObj.transform.SetParent(mapContainer, false);

            RectTransform rt = wallObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;

            // Mur entre cases de même Y — apparaît verticalement
            if (wall.y1 == wall.y2)
            {
                int leftX    = Mathf.Min(wall.x1, wall.x2);
                int flippedY = mapData.height - 1 - wall.y1;

                rt.sizeDelta        = new Vector2(wallThickness, cellSize);
                rt.anchoredPosition = new Vector2(
                    wallThickness + leftX    * step + cellSize,
                    wallThickness + flippedY * step);
            }
            // Mur entre cases de même X — apparaît horizontalement
            else
            {
                int topY     = Mathf.Min(wall.y1, wall.y2);
                int flippedY = mapData.height - 1 - topY;

                rt.sizeDelta        = new Vector2(cellSize, wallThickness);
                rt.anchoredPosition = new Vector2(
                    wallThickness + wall.x1 * step,
                    wallThickness + flippedY * step - wallThickness);
            }

            Image img   = wallObj.AddComponent<Image>();
            img.color   = colorHidden; // Caché par défaut

            // Stocke le mur avec ses cases adjacentes pour la visibilité
            wallEntries.Add(new WallEntry
            {
                image = img,
                x1    = wall.x1, y1 = wall.y1,
                x2    = wall.x2, y2 = wall.y2
            });
        }
    }

    // -----------------------------------------------
    // AFFICHAGE
    // -----------------------------------------------

    public void RefreshMap()
    {
        // Met à jour les cases
        for (int y = 0; y < mapData.height; y++)
        {
            for (int x = 0; x < mapData.width; x++)
            {
                CellData cell = mapData.GetCell(x, y);

                if (x == navigationManager.PlayerX && y == navigationManager.PlayerY)
                {
                    cellImages[x, y].color = colorPlayer;
                    continue;
                }

                if (navigationManager.IsVisible(x, y))
                {
                    // Si la salle a été complétée, on la montre comme vide
                    // même si son type d'origine est Classic ou Boss.
                    bool cleared = RunManager.Instance != null &&
                                   RunManager.Instance.IsRoomCleared(x, y);

                    CellType displayType = cleared ? CellType.Empty
                                                   : (cell?.cellType ?? CellType.Empty);
                    cellImages[x, y].color = GetCellColor(displayType);
                }
                else
                {
                    cellImages[x, y].color = colorHidden;
                }
            }
        }

        // Met à jour les murs
        // Un mur est visible si au moins une de ses deux cases adjacentes est visible
        foreach (WallEntry entry in wallEntries)
        {
            bool cell1Visible = navigationManager.IsVisible(entry.x1, entry.y1);
            bool cell2Visible = navigationManager.IsVisible(entry.x2, entry.y2);

            entry.image.color = (cell1Visible || cell2Visible) ? colorWall : colorHidden;
        }
    }

    // -----------------------------------------------
    // CENTRAGE DE LA CAMÉRA
    // -----------------------------------------------

    /// <summary>
    /// Attend une frame que le Canvas soit initialisé,
    /// puis centre la vue sur le joueur.
    /// </summary>
private IEnumerator CenterAfterFrame()
{
    // Attend deux frames pour s'assurer que le Canvas est pleinement initialisé
    yield return null;
    yield return null;

    CenterCameraOnPlayer();
}

public void CenterCameraOnPlayer()
{
    if (cameraController == null) return;

    float step   = cellSize + wallThickness;
    int flippedY = mapData.height - 1 - navigationManager.PlayerY;

    Vector2 playerUIPos = new Vector2(
        wallThickness + navigationManager.PlayerX * step + cellSize * 0.5f,
        wallThickness + flippedY                  * step + cellSize * 0.5f);

    Canvas canvas            = mapContainer.GetComponentInParent<Canvas>();
    RectTransform canvasRect = canvas.GetComponent<RectTransform>();
    Vector2 canvasCenter     = canvasRect.rect.size * 0.5f;

    mapContainer.anchoredPosition = canvasCenter - playerUIPos;

    Debug.Log($"CanvasCenter : {canvasCenter} | PlayerUIPos : {playerUIPos} | Result : {mapContainer.anchoredPosition}");
}

    private Color GetCellColor(CellType type)
    {
        switch (type)
        {
            case CellType.Start:        return colorStart;
            case CellType.Boss:         return colorBoss;
            case CellType.Classic:      return colorClassic;
            case CellType.Event:        return colorEvent;
            case CellType.NonNavigable: return colorNonNav;
            default:                    return colorEmpty;
        }
    }
}