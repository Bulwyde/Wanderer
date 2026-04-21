using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class AssociationIconeCase
{
    public CellType typeDCase;
    public Sprite   icone;
}

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

    [Header("Icônes")]
    [SerializeField] private AssociationIconeCase[] iconesParType;

    [Header("Couleurs")]
    public Color colorHidden  = new Color(0.1f, 0.1f, 0.1f);
    public Color colorEmpty   = new Color(0.2f, 0.2f, 0.2f);
    public Color colorStart   = new Color(0.0f, 0.8f, 0.2f);
    public Color colorBoss    = new Color(0.8f, 0.0f, 0.0f);
    public Color colorClassic = new Color(0.3f, 0.5f, 0.8f);
    public Color colorElite   = new Color(0.7f, 0.2f, 0.8f); // violet — élite
    public Color colorEvent   = new Color(1.0f, 0.5f, 0.0f);
    public Color colorShop    = new Color(0.2f, 0.8f, 0.8f); // cyan — marchand
    public Color colorNonNav  = new Color(0.1f, 0.1f, 0.1f);
    public Color colorWall    = new Color(0.9f, 0.7f, 0.1f);
    public Color colorPlayer  = Color.white;
    public Color colorPreview = new Color(0.95f, 0.90f, 0.2f, 0.85f); // jaune vif semi-opaque

    // Références aux images des cases (fond) et aux images d'icônes (calque supérieur)
    private Image[,] cellImages;
    private Image[,] iconeImages;

    // Cases actuellement en surbrillance (mode sélection de zone RevealZoneChoice)
    private HashSet<Vector2Int> cellsEnPreview = new HashSet<Vector2Int>();

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
        iconeImages = new Image[mapData.width, mapData.height];
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

                // Calque icône : GO enfant dédié, par-dessus le fond
                GameObject iconeGO = new GameObject($"Icone_{x}_{y}");
                iconeGO.transform.SetParent(cell.transform, false);

                RectTransform iconeRt = iconeGO.AddComponent<RectTransform>();
                iconeRt.sizeDelta        = new Vector2(cellSize, cellSize);
                iconeRt.anchorMin        = Vector2.zero;
                iconeRt.anchorMax        = Vector2.zero;
                iconeRt.pivot            = Vector2.zero;
                iconeRt.anchoredPosition = Vector2.zero;

                Image iconeImg = iconeGO.AddComponent<Image>();
                iconeImg.color          = Color.clear;
                iconeImg.raycastTarget  = false;
                iconeImages[x, y] = iconeImg;
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
                    AppliquerIcone(x, y, CellType.Empty, false);
                    continue;
                }

                // Les cases NonNavigable sont toujours transparentes :
                // elles ne participent pas au brouillard de guerre.
                if (cell?.cellType == CellType.NonNavigable)
                {
                    cellImages[x, y].color = Color.clear;
                    AppliquerIcone(x, y, CellType.NonNavigable, false);
                    continue;
                }

                if (navigationManager.IsVisible(x, y))
                {
                    // Si la salle a été complétée, on la montre comme vide
                    // même si son type d'origine est Classic ou Boss.
                    // Exception : les cases Shop gardent leur couleur même après visite
                    // (le marchand reste accessible, son état est persistant).
                    bool cleared = RunManager.Instance != null &&
                                   RunManager.Instance.IsRoomCleared(x, y) &&
                                   cell?.cellType != CellType.Shop;

                    CellType displayType = cleared ? CellType.Empty
                                                   : (cell?.cellType ?? CellType.Empty);
                    cellImages[x, y].color = GetCellColor(displayType);
                    AppliquerIcone(x, y, displayType, true);
                }
                else
                {
                    cellImages[x, y].color = colorHidden;
                    AppliquerIcone(x, y, CellType.Empty, false);
                }
            }
        }

        // Met à jour les murs
        foreach (WallEntry entry in wallEntries)
        {
            CellData mC1 = mapData.GetCell(entry.x1, entry.y1);
            CellData mC2 = mapData.GetCell(entry.x2, entry.y2);
            bool c1NonNav = mC1?.cellType == CellType.NonNavigable;
            bool c2NonNav = mC2?.cellType == CellType.NonNavigable;

            // Mur entre deux NonNavigable : transparent (zone sans intérêt)
            if (c1NonNav && c2NonNav)
            {
                entry.image.color = Color.clear;
                continue;
            }

            // Un mur n'apparaît que lorsqu'au moins une de ses cases adjacentes est visible.
            // Non visible = transparent (pas colorHidden) pour ne pas trahir la présence du mur.
            bool cell1Visible = navigationManager.IsVisible(entry.x1, entry.y1);
            bool cell2Visible = navigationManager.IsVisible(entry.x2, entry.y2);

            entry.image.color = (cell1Visible || cell2Visible) ? colorWall : Color.clear;
        }
    }

    // -----------------------------------------------
    // PREVIEW ZONE (RevealZoneChoice)
    // -----------------------------------------------

    /// <summary>
    /// Affiche une surbrillance sur les cases de la zone centrée en (cx, cy).
    /// Ignore les cases NonNavigable. Efface le preview précédent avant d'appliquer.
    /// </summary>
    public void PreviewZone(int cx, int cy, int rayon)
    {
        ClearPreview();

        for (int dy = -rayon; dy <= rayon; dy++)
        {
            for (int dx = -rayon; dx <= rayon; dx++)
            {
                int x = cx + dx;
                int y = cy + dy;
                if (x < 0 || x >= mapData.width || y < 0 || y >= mapData.height) continue;

                CellData cell = mapData.GetCell(x, y);
                if (cell?.cellType == CellType.NonNavigable) continue;

                cellImages[x, y].color = colorPreview;
                cellsEnPreview.Add(new Vector2Int(x, y));
            }
        }
    }

    /// <summary>
    /// Supprime la surbrillance et restaure la couleur normale de chaque case en preview.
    /// </summary>
    public void ClearPreview()
    {
        foreach (Vector2Int pos in cellsEnPreview)
            RefreshSingleCell(pos.x, pos.y);

        cellsEnPreview.Clear();
    }

    /// <summary>
    /// Recalcule et applique la couleur correcte d'une seule case.
    /// Même logique que RefreshMap mais ciblée, pour éviter de tout reconstruire.
    /// </summary>
    private void RefreshSingleCell(int x, int y)
    {
        if (cellImages == null || cellImages[x, y] == null) return;

        CellData cell = mapData.GetCell(x, y);

        if (x == navigationManager.PlayerX && y == navigationManager.PlayerY)
        {
            cellImages[x, y].color = colorPlayer;
            AppliquerIcone(x, y, CellType.Empty, false);
            return;
        }

        if (cell?.cellType == CellType.NonNavigable)
        {
            cellImages[x, y].color = Color.clear;
            AppliquerIcone(x, y, CellType.NonNavigable, false);
            return;
        }

        if (navigationManager.IsVisible(x, y))
        {
            bool cleared = RunManager.Instance != null &&
                           RunManager.Instance.IsRoomCleared(x, y) &&
                           cell?.cellType != CellType.Shop;
            CellType displayType = cleared ? CellType.Empty
                                           : (cell?.cellType ?? CellType.Empty);
            cellImages[x, y].color = GetCellColor(displayType);
            AppliquerIcone(x, y, displayType, true);
        }
        else
        {
            cellImages[x, y].color = colorHidden;
            AppliquerIcone(x, y, CellType.Empty, false);
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

    // --- ICÔNES ---

    /// <summary>
    /// Retourne le sprite associé au type de case donné,
    /// ou null si aucune association n'est configurée dans l'Inspector.
    /// </summary>
    private Sprite ObtenirIcone(CellType type)
    {
        if (iconesParType == null) return null;
        foreach (AssociationIconeCase assoc in iconesParType)
            if (assoc.typeDCase == type) return assoc.icone;
        return null;
    }

    /// <summary>
    /// Assigne le sprite approprié sur l'Image icône de la case (x, y).
    /// Si la case n'est pas visible ou qu'aucun sprite n'est configuré, efface l'icône.
    /// </summary>
    private void AppliquerIcone(int x, int y, CellType displayType, bool visible)
    {
        if (iconeImages == null || iconeImages[x, y] == null) return;
        Image icone   = iconeImages[x, y];
        Sprite sprite = visible ? ObtenirIcone(displayType) : null;
        icone.sprite  = sprite;
        icone.color   = sprite != null ? Color.white : Color.clear;
    }

    private Color GetCellColor(CellType type)
    {
        switch (type)
        {
            case CellType.Start:        return colorStart;
            case CellType.Boss:         return colorBoss;
            case CellType.CombatSimple: return colorClassic;
            case CellType.Elite:        return colorElite;
            case CellType.Event:        return colorEvent;
            case CellType.Shop:         return colorShop;
            case CellType.NonNavigable: return colorNonNav;
            default:                    return colorEmpty;
        }
    }
}