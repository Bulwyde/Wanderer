using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Fenêtre d'édition de cartes Unity.
/// Accessible via le menu : RPG > Map Editor
/// </summary>
public class MapEditorWindow : EditorWindow
{
    // -----------------------------------------------
    // RÉFÉRENCES
    // -----------------------------------------------

    // La MapData en cours d'édition
    private MapData currentMap;

    // -----------------------------------------------
    // PARAMÈTRES D'AFFICHAGE
    // -----------------------------------------------

    // Permet de bien placer le hover de la souris
    private float gridOffsetY;

    // Taille d'une case en pixels
    private float cellSize = 20f;

    // Épaisseur de l'espace entre les cases (pour les murs)
    private float wallThickness = 6f;

    // Décalage du pan dans l'éditeur
    private Vector2 panOffset = Vector2.zero;

    // État du pan
    private bool    isPanning        = false;
    private Vector2 lastPanMousePos;

    // Hauteur cumulée des contrôles en haut de la fenêtre.
    // Stockée lors du Repaint et réutilisée pour tous les autres events
    // (MouseDown, Layout, etc.) afin que la grille soit toujours positionnée correctement.
    private float headerEndY = 80f;

    // -----------------------------------------------
    // COULEURS PAR TYPE DE CASE (configurables)
    // -----------------------------------------------

    private Color colorEmpty       = new Color(0.2f, 0.2f, 0.2f);
    private Color colorStart       = new Color(0.0f, 0.8f, 0.2f);
    private Color colorBoss        = new Color(0.8f, 0.0f, 0.0f);
    private Color colorClassic     = new Color(0.3f, 0.5f, 0.8f);
    private Color colorEvent       = new Color(0.8f, 0.5f, 0.0f);
    private Color colorNonNav      = new Color(0.1f, 0.1f, 0.1f);
    private Color colorWall        = new Color(0.9f, 0.7f, 0.1f);


    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------


    // Afficher ou non le panneau de configuration des couleurs
    private bool showColorSettings = false;

    // Case actuellement sélectionnée (clic gauche) — affiche ses propriétés
    private CellData selectedCell = null;

    // -----------------------------------------------
    // OUVERTURE DE LA FENÊTRE
    // -----------------------------------------------

    [MenuItem("RPG/Map Editor")]
    public static void OpenWindow()
    {
        MapEditorWindow window = GetWindow<MapEditorWindow>("Map Editor");
        window.minSize = new Vector2(400, 400);
        window.Show();
    }

    // -----------------------------------------------
    // INTERFACE PRINCIPALE
    // -----------------------------------------------

    private void OnGUI()
    {
        DrawToolbar();

        if (currentMap == null)
        {
            EditorGUILayout.HelpBox(
                "Sélectionne ou crée une MapData pour commencer.",
                MessageType.Info);
            return;
        }

        DrawColorSettings();
        DrawZoomControls();
        DrawCellProperties();

        // Un séparateur de 2px sert de marqueur de position :
        // après lui, GUILayoutUtility.GetLastRect().yMax nous donne
        // la coordonnée Y exacte où s'arrêtent tous les contrôles.
        // On ne stocke cette valeur que pendant le Repaint, car c'est
        // le seul event où le layout est finalisé et fiable.
        EditorGUILayout.Space(2f);
        if (Event.current.type == EventType.Repaint)
            headerEndY = GUILayoutUtility.GetLastRect().yMax;

        DrawGrid();
    }

    // -----------------------------------------------
    // BARRE D'OUTILS
    // -----------------------------------------------

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        // Sélection de la MapData
        currentMap = (MapData)EditorGUILayout.ObjectField(
            "Map", currentMap, typeof(MapData), false);

        // Bouton pour créer une nouvelle MapData
        if (GUILayout.Button("Nouvelle Map", EditorStyles.toolbarButton, GUILayout.Width(100)))
            CreateNewMap();

        // Bouton pour initialiser les cases
        if (currentMap != null &&
            GUILayout.Button("Initialiser", EditorStyles.toolbarButton, GUILayout.Width(80)))
            InitializeCells();

        GUILayout.FlexibleSpace();

        // Bouton de sauvegarde
        if (currentMap != null &&
            GUILayout.Button("Sauvegarder", EditorStyles.toolbarButton, GUILayout.Width(90)))
            SaveMap();

        EditorGUILayout.EndHorizontal();
    }

    // -----------------------------------------------
    // PANNEAU DE CONFIGURATION DES COULEURS
    // -----------------------------------------------

    private void DrawColorSettings()
    {
        showColorSettings = EditorGUILayout.Foldout(showColorSettings, "Couleurs des cases");

        if (!showColorSettings) return;

        EditorGUI.indentLevel++;
        colorEmpty   = EditorGUILayout.ColorField("Vide",          colorEmpty);
        colorStart   = EditorGUILayout.ColorField("Départ",        colorStart);
        colorBoss    = EditorGUILayout.ColorField("Boss",          colorBoss);
        colorClassic = EditorGUILayout.ColorField("Classique",     colorClassic);
        colorEvent   = EditorGUILayout.ColorField("Événement",     colorEvent);
        colorNonNav  = EditorGUILayout.ColorField("Non navigable", colorNonNav);
        colorWall    = EditorGUILayout.ColorField("Mur",           colorWall);
        EditorGUI.indentLevel--;

        EditorGUILayout.Space(5);
    }

    // -----------------------------------------------
    // CONTRÔLES DE ZOOM
    // -----------------------------------------------

    private void DrawZoomControls()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Zoom :", GUILayout.Width(45));

        // Slider de zoom entre 10px et 50px par case
        cellSize = EditorGUILayout.Slider(cellSize, 10f, 50f);

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    // -----------------------------------------------
    // PANNEAU DE PROPRIÉTÉS DE LA CASE SÉLECTIONNÉE
    // -----------------------------------------------

    /// <summary>
    /// Affiche les propriétés de la case sélectionnée (clic gauche sur une case).
    /// Permet notamment de saisir le specificEventID pour les cases Event.
    /// </summary>
    private void DrawCellProperties()
    {
        if (selectedCell == null)
        {
            EditorGUILayout.LabelField("Clic gauche sur une case pour voir ses propriétés.",
                EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField(
            $"Case ({selectedCell.x}, {selectedCell.y}) — {selectedCell.cellType}",
            EditorStyles.boldLabel);

        // Changement de type via dropdown
        CellType newType = (CellType)EditorGUILayout.EnumPopup("Type", selectedCell.cellType);
        if (newType != selectedCell.cellType)
        {
            selectedCell.cellType = newType;
            EditorUtility.SetDirty(currentMap);
        }

        // Configuration des événements — uniquement pour les cases de type Event
        if (selectedCell.cellType == CellType.Event)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Événements", EditorStyles.boldLabel);

            // Dropdown pour choisir le mode
            EventCellMode newMode = (EventCellMode)EditorGUILayout.EnumPopup("Mode", selectedCell.eventCellMode);
            if (newMode != selectedCell.eventCellMode)
            {
                selectedCell.eventCellMode = newMode;
                EditorUtility.SetDirty(currentMap);
            }

            EditorGUILayout.Space(2);

            if (selectedCell.eventCellMode == EventCellMode.ManualList)
            {
                // Liste manuelle d'EventData
                for (int i = 0; i < selectedCell.eventList.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    EventData newEntry = (EventData)EditorGUILayout.ObjectField(
                        selectedCell.eventList[i], typeof(EventData), false);
                    if (newEntry != selectedCell.eventList[i])
                    {
                        selectedCell.eventList[i] = newEntry;
                        EditorUtility.SetDirty(currentMap);
                    }
                    if (GUILayout.Button("-", GUILayout.Width(22)))
                    {
                        selectedCell.eventList.RemoveAt(i);
                        EditorUtility.SetDirty(currentMap);
                        EditorGUILayout.EndHorizontal(); // fermer avant de sortir de la boucle
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ Ajouter un Event"))
                {
                    selectedCell.eventList.Add(null);
                    EditorUtility.SetDirty(currentMap);
                }

                bool listeVide = selectedCell.eventList.Count == 0 ||
                                 selectedCell.eventList.TrueForAll(e => e == null);
                if (listeVide)
                    EditorGUILayout.HelpBox(
                        "La liste est vide. Ajoute au moins un EventData.",
                        MessageType.Warning);
            }
            else // FromPool
            {
                // Référence vers un EventPool ScriptableObject
                EventPool newPool = (EventPool)EditorGUILayout.ObjectField(
                    "Event Pool", selectedCell.eventPool, typeof(EventPool), false);
                if (newPool != selectedCell.eventPool)
                {
                    selectedCell.eventPool = newPool;
                    EditorUtility.SetDirty(currentMap);
                }

                if (selectedCell.eventPool == null)
                    EditorGUILayout.HelpBox(
                        "Aucun EventPool assigné. Crée-en un via RPG → Event Pool.",
                        MessageType.Warning);
            }
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // -----------------------------------------------
    // AFFICHAGE DE LA GRILLE
    // -----------------------------------------------

    private void DrawGrid()
    {
        // On utilise headerEndY (stocké lors du dernier Repaint) pour calculer
        // le rect de la grille. Cela garantit que la grille commence
        // toujours sous les contrôles, quelle que soit leur taille.
        float gridHeight = position.height - headerEndY;
        if (gridHeight <= 0f) return;

        Rect gridRect = new Rect(0f, headerEndY, position.width, gridHeight);

        if (Event.current.type == EventType.Repaint)
        {
            // BeginClip restreint le rendu à la zone grille :
            // les cases ne peuvent plus déborder au-dessus des contrôles header.
            // À l'intérieur du clip les coordonnées sont locales (0,0 = coin haut-gauche du gridRect).
            GUI.BeginClip(gridRect);
            DrawCells(new Rect(0f, 0f, gridRect.width, gridRect.height));
            GUI.EndClip();
        }

        HandleMouseEvents(gridRect);
    }

    // -----------------------------------------------
    // DESSIN DES CASES ET DES MURS
    // -----------------------------------------------

    private void DrawCells(Rect gridRect)
{
    for (int y = 0; y < currentMap.height; y++)
    {
        for (int x = 0; x < currentMap.width; x++)
        {
            Rect cellRect = GetCellRect(gridRect, x, y);
            CellData cell = currentMap.GetCell(x, y);

            Color cellColor = GetCellColor(cell?.cellType ?? CellType.Empty);
            EditorGUI.DrawRect(cellRect, cellColor);

            GUI.Label(cellRect, GetCellLabel(cell?.cellType ?? CellType.Empty),
                new GUIStyle(EditorStyles.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize  = Mathf.RoundToInt(cellSize * 0.35f),
                    normal    = { textColor = Color.white }
                });

            DrawWallSpaces(gridRect, x, y);
        }
    }
}

    private void DrawWallSpaces(Rect gridRect, int x, int y)
    {
        // Mur à droite de la case (x, y) — entre (x,y) et (x+1,y)
        if (x < currentMap.width - 1)
        {
            Rect wallRect = GetHorizontalWallRect(gridRect, x, y);
            bool hasWall  = currentMap.HasWall(x, y, x + 1, y);


            Color wallColor = hasWall ? colorWall :
                              new Color(0.15f, 0.15f, 0.15f);
            EditorGUI.DrawRect(wallRect, wallColor);
        }

        // Mur en dessous de la case (x, y) — entre (x,y) et (x,y+1)
        if (y < currentMap.height - 1)
        {
            Rect wallRect = GetVerticalWallRect(gridRect, x, y);
            bool hasWall  = currentMap.HasWall(x, y, x, y + 1);

            Color wallColor = hasWall ? colorWall :
                              new Color(0.15f, 0.15f, 0.15f);
            EditorGUI.DrawRect(wallRect, wallColor);
        }
    }

    // -----------------------------------------------
    // GESTION DES ÉVÉNEMENTS SOURIS
    // -----------------------------------------------

private void HandleMouseEvents(Rect gridRect)
{
    Event e = Event.current;

    // ── Pan avec clic du milieu ──────────────────────
    if (e.type == EventType.MouseDown && e.button == 2)
    {
        isPanning       = true;
        lastPanMousePos = e.mousePosition;
        e.Use();
    }
    if (e.type == EventType.MouseUp && e.button == 2)
    {
        isPanning = false;
        e.Use();
    }
    if (e.type == EventType.MouseDrag && isPanning)
    {
        panOffset      += e.mousePosition - lastPanMousePos;
        lastPanMousePos = e.mousePosition;
        Repaint();
        e.Use();
        return;
    }

    // ── Clic droit — change le type de case ─────────
    if (e.type == EventType.MouseDown && e.button == 1)
    {
        for (int y = 0; y < currentMap.height; y++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                if (GetCellRect(gridRect, x, y).Contains(e.mousePosition))
                {
                    CycleCell(x, y);
                    e.Use();
                    return;
                }
            }
        }
    }

    // ── Clic gauche — sélectionne une case OU pose/retire un mur ──
    if (e.type == EventType.MouseDown && e.button == 0)
    {
        for (int y = 0; y < currentMap.height; y++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                // Clic sur la case elle-même → sélection
                if (GetCellRect(gridRect, x, y).Contains(e.mousePosition))
                {
                    selectedCell = currentMap.GetCell(x, y);
                    Repaint();
                    e.Use();
                    return;
                }

                // Clic sur un espace mur horizontal → toggle mur
                if (x < currentMap.width - 1 &&
                    GetHorizontalWallRect(gridRect, x, y).Contains(e.mousePosition))
                {
                    ToggleWall(x, y, x + 1, y);
                    e.Use();
                    return;
                }

                // Clic sur un espace mur vertical → toggle mur
                if (y < currentMap.height - 1 &&
                    GetVerticalWallRect(gridRect, x, y).Contains(e.mousePosition))
                {
                    ToggleWall(x, y, x, y + 1);
                    e.Use();
                    return;
                }
            }
        }
    }

    if (gridRect.Contains(e.mousePosition))
        Repaint();
}

    // -----------------------------------------------
    // LOGIQUE DE MODIFICATION
    // -----------------------------------------------

    // Cycle le type de case au clic droit
    private void CycleCell(int x, int y)
    {
        CellData cell = currentMap.GetCell(x, y);
        if (cell == null) return;

        int next = ((int)cell.cellType + 1) % System.Enum.GetValues(typeof(CellType)).Length;
        cell.cellType = (CellType)next;
        EditorUtility.SetDirty(currentMap);
    }

    // Pose ou retire un mur entre deux cases
    private void ToggleWall(int x1, int y1, int x2, int y2)
    {
        if (currentMap.HasWall(x1, y1, x2, y2))
        {
            currentMap.walls.RemoveAll(w =>
                (w.x1 == x1 && w.y1 == y1 && w.x2 == x2 && w.y2 == y2) ||
                (w.x1 == x2 && w.y1 == y2 && w.x2 == x1 && w.y2 == y1));
        }
        else
        {
            currentMap.walls.Add(new WallData { x1 = x1, y1 = y1, x2 = x2, y2 = y2 });
        }
        EditorUtility.SetDirty(currentMap);
    }

    // -----------------------------------------------
    // UTILITAIRES DE POSITION
    // -----------------------------------------------

private Rect GetCellRect(Rect grid, int x, int y)
{
    float step = cellSize + wallThickness;
    return new Rect(
        grid.x + panOffset.x + wallThickness + x * step,
        grid.y + panOffset.y + wallThickness + y * step,
        cellSize, cellSize);
}

private Rect GetHorizontalWallRect(Rect grid, int x, int y)
{
    float step = cellSize + wallThickness;
    return new Rect(
        grid.x + panOffset.x + wallThickness + x * step + cellSize,
        grid.y + panOffset.y + wallThickness + y * step,
        wallThickness, cellSize);
}

private Rect GetVerticalWallRect(Rect grid, int x, int y)
{
    float step = cellSize + wallThickness;
    return new Rect(
        grid.x + panOffset.x + wallThickness + x * step,
        grid.y + panOffset.y + wallThickness + y * step + cellSize,
        cellSize, wallThickness);
}

    // -----------------------------------------------
    // COULEURS ET LABELS
    // -----------------------------------------------

    private Color GetCellColor(CellType type)
    {
        switch (type)
        {
            case CellType.Start:         return colorStart;
            case CellType.Boss:          return colorBoss;
            case CellType.Classic:       return colorClassic;
            case CellType.Event:         return colorEvent;
            case CellType.NonNavigable:  return colorNonNav;
            default:                     return colorEmpty;
        }
    }

    private string GetCellLabel(CellType type)
    {
        switch (type)
        {
            case CellType.Start:         return "S";
            case CellType.Boss:          return "B";
            case CellType.Classic:       return "C";
            case CellType.Event:         return "E";
            case CellType.NonNavigable:  return "X";
            default:                     return "";
        }
    }

    // -----------------------------------------------
    // CRÉATION ET SAUVEGARDE
    // -----------------------------------------------

    private void CreateNewMap()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Nouvelle MapData", "NewMap", "asset", "Choisir un emplacement");
        if (string.IsNullOrEmpty(path)) return;

        MapData newMap = CreateInstance<MapData>();
        AssetDatabase.CreateAsset(newMap, path);
        AssetDatabase.SaveAssets();

        currentMap = newMap;
        InitializeCells();
    }

    private void InitializeCells()
    {
        currentMap.cells.Clear();
        for (int y = 0; y < currentMap.height; y++)
        {
            for (int x = 0; x < currentMap.width; x++)
            {
                currentMap.cells.Add(new CellData
                {
                    x = x, y = y,
                    cellType = CellType.NonNavigable
                });
            }
        }
        EditorUtility.SetDirty(currentMap);
        Debug.Log($"Grille {currentMap.width}x{currentMap.height} initialisée.");
    }

    private void SaveMap()
    {
        EditorUtility.SetDirty(currentMap);
        AssetDatabase.SaveAssets();
        Debug.Log($"Map '{currentMap.mapName}' sauvegardée.");
    }
}