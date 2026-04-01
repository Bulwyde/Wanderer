using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Gère la navigation du joueur sur la carte.
/// Calcule la visibilité des cases selon la position du joueur
/// et les murs qui bloquent la ligne de vue.
///
/// Fournit aussi :
///   - L'exécution des NavEffects (AppliquerEffetNav / AppliquerEffetsNav)
///   - Le mode de sélection de zone par clic (RevealZoneChoice)
///   - L'UI des consommables et compétences de jambes utilisables sur la carte
///
/// Setup scène recommandé :
///   Canvas
///   ├── mapContainer (RectTransform, déplacé par MapCameraController)
///   └── NavigationHUD (enfant direct du Canvas)
///       ├── HPText (TextMeshPro)
///       ├── ConsommableContainer (HorizontalLayoutGroup) ← optionnel
///       └── SkillContainer (HorizontalLayoutGroup)       ← optionnel
/// </summary>
public class NavigationManager : MonoBehaviour
{
    [Header("Références")]
    public MapData mapData;
    public MapRenderer mapRenderer;
    // Fallback CharacterData pour les tests de la scène Navigation en isolation.
    // En jeu normal, le CharacterData vient de RunManager.selectedCharacter.
    public CharacterData characterData;

    [Header("Visibilité")]
    // La portée effective = characterData.baseVisionRange + RunManager.visionRangeBonus.

    // Position actuelle du joueur — accessibles par MapRenderer
    public int PlayerX { get; private set; }
    public int PlayerY { get; private set; }

    // Cases actuellement visibles
    private HashSet<Vector2Int> visibleCells = new HashSet<Vector2Int>();

    // Cases déjà visitées (restent révélées même hors de la vision)
    private HashSet<Vector2Int> visitedCells = new HashSet<Vector2Int>();

    // Cases déjà vues — restent révélées même hors champ de vision
    private HashSet<Vector2Int> exploredCells = new HashSet<Vector2Int>();

    // -----------------------------------------------
    // MODE SÉLECTION DE ZONE (RevealZoneChoice)
    // -----------------------------------------------

    // Activé quand un effet RevealZoneChoice est en attente du clic du joueur.
    // Les déplacements clavier sont bloqués pendant ce mode.
    private bool modeSelectionZone = false;
    private int selectionRayon = 1;

    // Dernière case survolée en mode sélection — évite de recalculer le preview à chaque frame
    private Vector2Int? derniereCellSurvol = null;

    // -----------------------------------------------
    // UI NAVIGATION
    // -----------------------------------------------

    [Header("Fallback événements")]
    // Pool de fallback par map — utilisé quand une salle Event n'a aucun event configurable
    public RandomEvents randomEvents;

    [Header("UI — Consommables sur la carte")]
    // Container des boutons de consommables (optionnel — si null, l'UI est ignorée)
    public Transform consommableContainer;
    // Prefab de bouton consommable — doit avoir un composant ConsumableButton
    public GameObject consommablePrefab;

    [Header("UI — Compétences de jambes")]
    // Container des boutons de compétences de navigation (optionnel)
    public Transform skillContainer;
    // Prefab de bouton skill de navigation — doit avoir un Button + TextMeshProUGUI enfant
    public GameObject navSkillPrefab;

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

        // Génère les boutons UI (consommables + skills jambes)
        RafraichirUINavigation();
    }

    void Update()
    {
        // En mode sélection de zone : bloquer navigation clavier, attendre un clic
        if (modeSelectionZone)
        {
            GererSelectionZone();
            return;
        }

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

    private int PorteeVisionEffective
    {
        get
        {
            // Priorité : CharacterData issu de RunManager (run en cours)
            // Fallback : champ Inspector local (tests en isolation)
            CharacterData cd = RunManager.Instance?.selectedCharacter ?? characterData;
            int baseRange = cd != null ? cd.baseVisionRange : 1;
            int bonus     = RunManager.Instance != null ? RunManager.Instance.visionRangeBonus : 0;
            return baseRange + bonus;
        }
    }

    private void UpdateVisibility()
    {
        visibleCells.Clear();
        int portee = PorteeVisionEffective;

        for (int dy = -portee; dy <= portee; dy++)
        {
            for (int dx = -portee; dx <= portee; dx++)
            {
                int checkX = PlayerX + dx;
                int checkY = PlayerY + dy;

                if (checkX < 0 || checkX >= mapData.width ||
                    checkY < 0 || checkY >= mapData.height)
                    continue;

                // Distance euclidienne — les diagonales à portée = 1 auront distance ≈ 1.41 → non visibles
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > portee) continue;

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
            // On ne fait avancer un axe que s'il n'a pas encore atteint sa cible.
            // Sans ce garde, un chemin non carré (ex : dx=2, dy=1) ferait dépasser
            // l'un des axes, rendant la condition de sortie inatteignable → boucle infinie.
            int nextX = (currentX == x2) ? currentX : currentX + stepX;
            int nextY = (currentY == y2) ? currentY : currentY + stepY;

            if (mapData.HasWall(currentX, currentY, nextX, nextY))
                return false;

            currentX = nextX;
            currentY = nextY;
        }

        return true;
    }

    // -----------------------------------------------
    // TIRAGE D'ÉVÉNEMENT
    // -----------------------------------------------

    /// <summary>
    /// Tire aléatoirement un EventData depuis le pool de la case,
    /// en excluant les events déjà joués pendant ce run.
    /// Si aucun event n'est disponible (liste vide, pool null ou épuisé),
    /// tente un fallback via RandomEvents pour la MapData courante.
    /// Retourne null si tout est épuisé.
    /// </summary>
    private EventData ChoisirEventAleatoire(CellData cell)
    {
        EventData resultat = null;

        switch (cell.eventCellMode)
        {
            case EventCellMode.ManualList:
                resultat = ChoisirDepuisListe(cell);
                break;

            case EventCellMode.FromPool:
                if (cell.eventPool == null)
                    Debug.LogWarning($"[Navigation] Salle ({cell.x},{cell.y}) : mode FromPool mais aucun EventPool assigné.");
                else
                    resultat = cell.eventPool.GetRandom();
                break;
        }

        // Fallback : si aucun event disponible, cherche dans RandomEvents pour cette map
        if (resultat == null)
        {
            if (randomEvents == null)
            {
                Debug.LogWarning($"[Navigation] Salle ({cell.x},{cell.y}) : aucun event disponible et aucun RandomEvents assigné.");
                return null;
            }

            EventPool fallbackPool = randomEvents.GetPoolPourMap(mapData);

            if (fallbackPool == null)
            {
                Debug.LogWarning($"[Navigation] Salle ({cell.x},{cell.y}) : aucun fallback RandomEvents pour la map '{mapData?.mapName}'.");
                return null;
            }

            resultat = fallbackPool.GetRandom();

            if (resultat != null)
                Debug.Log($"[Navigation] Salle ({cell.x},{cell.y}) : fallback RandomEvents — event '{resultat.eventID}' tiré.");
            else
                Debug.LogWarning($"[Navigation] Salle ({cell.x},{cell.y}) : fallback RandomEvents aussi épuisé pour la map '{mapData?.mapName}'.");
        }

        return resultat;
    }

    /// <summary>
    /// Tire aléatoirement un EventData depuis la liste manuelle de la case,
    /// en excluant les events déjà joués pendant ce run.
    /// </summary>
    private EventData ChoisirDepuisListe(CellData cell)
    {
        if (cell.eventList == null || cell.eventList.Count == 0)
        {
            Debug.LogWarning($"[Navigation] Salle ({cell.x},{cell.y}) : mode ManualList mais eventList vide.");
            return null;
        }

        List<EventData> disponibles = new List<EventData>();
        foreach (EventData ev in cell.eventList)
        {
            if (ev == null) continue;
            if (RunManager.Instance != null && RunManager.Instance.IsEventPlayed(ev.eventID)) continue;
            disponibles.Add(ev);
        }

        if (disponibles.Count == 0)
        {
            Debug.Log($"[Navigation] Salle ({cell.x},{cell.y}) : tous les events de la liste ont déjà été joués.");
            return null;
        }

        EventData choisi = disponibles[Random.Range(0, disponibles.Count)];
        Debug.Log($"[Navigation] Salle ({cell.x},{cell.y}) : event '{choisi.eventID}' tiré parmi {disponibles.Count} disponible(s).");
        return choisi;
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
                    Debug.Log($"({PlayerX},{PlayerY}) — Salle d'événement déjà complétée, pas de transition");
                    break;
                }

                if (RunManager.Instance == null || SceneLoader.Instance == null)
                {
                    Debug.LogError("RunManager ou SceneLoader introuvable !");
                    break;
                }

                EventData eventChoisi = ChoisirEventAleatoire(cell);

                if (eventChoisi == null)
                {
                    // Tous les events du pool ont été joués — la salle devient vide
                    Debug.Log($"({PlayerX},{PlayerY}) — Tous les events du pool ont été joués, salle ignorée");
                    break;
                }

                RunManager.Instance.SaveNavigationState(
                    PlayerX, PlayerY, visitedCells, exploredCells);
                RunManager.Instance.EnterRoom(cell);
                // On écrase currentSpecificEventID avec l'event tiré au sort
                RunManager.Instance.currentSpecificEventID = eventChoisi.eventID;
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

    // -----------------------------------------------
    // EFFETS DE NAVIGATION
    // -----------------------------------------------

    /// <summary>
    /// Applique une liste d'effets de navigation dans l'ordre.
    /// Si plusieurs effets sont présents, ils s'exécutent séquentiellement —
    /// sauf RevealZoneChoice qui bloque jusqu'au clic du joueur.
    /// </summary>
    public void AppliquerEffetsNav(List<NavEffect> effects)
    {
        if (effects == null) return;
        foreach (NavEffect effect in effects)
            AppliquerEffetNav(effect);
    }

    /// <summary>
    /// Applique un seul effet de navigation.
    /// </summary>
    public void AppliquerEffetNav(NavEffect effect)
    {
        if (effect == null) return;

        switch (effect.type)
        {
            case NavEffectType.TeleportRandom:
                AppliquerTeleportRandom(effect.allowedCellTypes);
                break;

            case NavEffectType.RevealZoneRandom:
                AppliquerRevealZoneRandom(effect.value);
                break;

            case NavEffectType.RevealZoneChoice:
                // Active le mode sélection — le joueur choisit la zone par clic
                DemarrerSelectionZone(effect.value);
                break;

            case NavEffectType.IncreaseVisionRange:
                AppliquerBonusVision(effect.value);
                break;

            case NavEffectType.IncrementCounter:
                RunManager.Instance?.IncrementCounter(effect.counterKey, effect.value);
                break;
        }
    }

    // -----------------------------------------------
    // IMPLÉMENTATION DES EFFETS
    // -----------------------------------------------

    /// <summary>
    /// Téléporte le joueur vers une case aléatoire (filtrée par allowedCellTypes).
    /// Si la liste est vide, toutes les cases navigables sont acceptées.
    /// La case actuelle du joueur est exclue.
    /// </summary>
    private void AppliquerTeleportRandom(List<CellType> allowedTypes)
    {
        List<CellData> valides = new List<CellData>();
        foreach (CellData cell in mapData.cells)
        {
            if (cell == null) continue;
            if (cell.cellType == CellType.NonNavigable) continue;
            // Filtre par types si une liste est fournie
            if (allowedTypes != null && allowedTypes.Count > 0 &&
                !allowedTypes.Contains(cell.cellType)) continue;
            // Exclure la position actuelle
            if (cell.x == PlayerX && cell.y == PlayerY) continue;
            valides.Add(cell);
        }

        if (valides.Count == 0)
        {
            Debug.LogWarning("[Navigation] Téléportation impossible — aucune case valide trouvée.");
            return;
        }

        CellData cible = valides[Random.Range(0, valides.Count)];
        TeleporterJoueur(cible.x, cible.y);
    }

    /// <summary>
    /// Révèle une zone autour d'une case choisie aléatoirement parmi les cases navigables.
    /// </summary>
    private void AppliquerRevealZoneRandom(int rayon)
    {
        List<CellData> navigables = new List<CellData>();
        foreach (CellData cell in mapData.cells)
        {
            if (cell != null && cell.cellType != CellType.NonNavigable)
                navigables.Add(cell);
        }

        if (navigables.Count == 0) return;

        CellData centre = navigables[Random.Range(0, navigables.Count)];
        RevelerZone(centre.x, centre.y, rayon);
        Debug.Log($"[Navigation] Zone aléatoire révélée autour de ({centre.x},{centre.y}), rayon {rayon}.");
    }

    /// <summary>
    /// Augmente la portée de vision du joueur de `delta` cases (permanent pour le run).
    /// Met à jour la visibilité immédiatement.
    /// </summary>
    private void AppliquerBonusVision(int delta)
    {
        if (RunManager.Instance != null)
            RunManager.Instance.visionRangeBonus += delta;

        UpdateVisibility();
        mapRenderer.RefreshMap();
        Debug.Log($"[Navigation] Portée de vision +{delta} (effective : {PorteeVisionEffective}).");
    }

    // -----------------------------------------------
    // TÉLÉPORTATION ET RÉVÉLATION
    // -----------------------------------------------

    /// <summary>
    /// Téléporte le joueur vers la case (targetX, targetY) sans déclencher OnRoomEntered.
    /// Met à jour la visibilité, l'affichage et centre la caméra.
    /// </summary>
    public void TeleporterJoueur(int targetX, int targetY)
    {
        PlayerX = targetX;
        PlayerY = targetY;
        visitedCells.Add(new Vector2Int(PlayerX, PlayerY));

        UpdateVisibility();
        mapRenderer.RefreshMap();
        mapRenderer.CenterCameraOnPlayer();
        Debug.Log($"[Navigation] Joueur téléporté en ({targetX}, {targetY}).");
    }

    /// <summary>
    /// Révèle toutes les cases dans un carré (2*rayon+1)×(2*rayon+1) centré en (centreX, centreY).
    /// Les cases révélées rejoignent exploredCells — elles restent visibles même hors vision directe.
    /// </summary>
    public void RevelerZone(int centreX, int centreY, int rayon)
    {
        for (int dy = -rayon; dy <= rayon; dy++)
        {
            for (int dx = -rayon; dx <= rayon; dx++)
            {
                int x = centreX + dx;
                int y = centreY + dy;
                if (x < 0 || x >= mapData.width || y < 0 || y >= mapData.height) continue;

                // Ne pas révéler les cases NonNavigable sans voisin navigable :
                // elles sont en plein milieu d'une zone inutile, l'effet de révélation
                // ne doit pas être "gaspillé" sur elles.
                CellData cellReveler = mapData.GetCell(x, y);
                if (cellReveler != null &&
                    cellReveler.cellType == CellType.NonNavigable &&
                    !mapData.AUnVoisinNavigable(x, y))
                    continue;

                exploredCells.Add(new Vector2Int(x, y));
            }
        }

        mapRenderer.RefreshMap();
        Debug.Log($"[Navigation] Zone {rayon * 2 + 1}x{rayon * 2 + 1} révélée autour de ({centreX},{centreY}).");
    }

    // -----------------------------------------------
    // SÉLECTION DE ZONE PAR CLIC (RevealZoneChoice)
    // -----------------------------------------------

    /// <summary>
    /// Active le mode sélection de zone.
    /// Le joueur doit cliquer sur la carte pour choisir le centre de la révélation.
    /// Les déplacements clavier sont bloqués jusqu'au clic.
    /// </summary>
    public void DemarrerSelectionZone(int rayon)
    {
        modeSelectionZone = true;
        selectionRayon    = rayon;
        Debug.Log($"[Navigation] Sélection de zone activée — cliquez sur la carte pour révéler " +
                  $"une zone {rayon * 2 + 1}x{rayon * 2 + 1}.");
    }

    /// <summary>
    /// Convertit la position de la souris en coordonnées de case sur la carte.
    /// Retourne false si la souris est hors du mapContainer ou hors des limites de la carte.
    /// </summary>
    private bool ObtenirCaseDepuisSouris(out int cellX, out int cellY)
    {
        cellX = cellY = -1;

        RectTransform mapContainerRect = mapRenderer.mapContainer;
        Canvas canvas = mapContainerRect.GetComponentInParent<Canvas>();
        Camera canvasCamera = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                mapContainerRect, Input.mousePosition, canvasCamera, out Vector2 localPos))
            return false;

        float step = mapRenderer.cellSize + mapRenderer.wallThickness;
        cellX = Mathf.FloorToInt((localPos.x - mapRenderer.wallThickness) / step);
        int flippedY = Mathf.FloorToInt((localPos.y - mapRenderer.wallThickness) / step);
        cellY = mapData.height - 1 - flippedY;

        return cellX >= 0 && cellX < mapData.width &&
               cellY >= 0 && cellY < mapData.height;
    }

    /// <summary>
    /// Met à jour la surbrillance de preview selon la position de la souris.
    /// Appelée chaque frame en mode sélection de zone.
    /// </summary>
    private void ActualiserPreviewZone()
    {
        if (!ObtenirCaseDepuisSouris(out int cellX, out int cellY))
        {
            // Souris hors carte : effacer le preview
            if (derniereCellSurvol.HasValue)
            {
                mapRenderer.ClearPreview();
                derniereCellSurvol = null;
            }
            return;
        }

        CellData cellSurvol = mapData.GetCell(cellX, cellY);
        if (cellSurvol == null || cellSurvol.cellType == CellType.NonNavigable)
        {
            // Case invalide : effacer le preview
            if (derniereCellSurvol.HasValue)
            {
                mapRenderer.ClearPreview();
                derniereCellSurvol = null;
            }
            return;
        }

        // Même case que le frame précédent : rien à faire
        Vector2Int nouvellePos = new Vector2Int(cellX, cellY);
        if (derniereCellSurvol == nouvellePos) return;

        // Nouvelle case valide : mettre à jour le preview
        derniereCellSurvol = nouvellePos;
        mapRenderer.PreviewZone(cellX, cellY, selectionRayon);
    }

    /// <summary>
    /// Gère le hover (preview) et le clic du joueur en mode sélection de zone.
    /// </summary>
    private void GererSelectionZone()
    {
        // Mise à jour du preview à chaque frame selon la position de la souris
        ActualiserPreviewZone();

        if (!Input.GetMouseButtonDown(0)) return;

        if (!ObtenirCaseDepuisSouris(out int cellX, out int cellY))
        {
            Debug.Log("[Navigation] Clic hors de la carte — veuillez cliquer sur une case valide.");
            return;
        }

        // Refuser les cases NonNavigable comme centre de révélation
        CellData cellCible = mapData.GetCell(cellX, cellY);
        if (cellCible == null || cellCible.cellType == CellType.NonNavigable)
        {
            Debug.Log("[Navigation] Clic sur une case non navigable — choisissez une case navigable.");
            return; // Reste en mode sélection
        }

        // Clic valide : effacer le preview et confirmer la révélation
        mapRenderer.ClearPreview();
        derniereCellSurvol = null;
        modeSelectionZone = false;
        RevelerZone(cellX, cellY, selectionRayon);
        Debug.Log($"[Navigation] Case ({cellX},{cellY}) choisie — zone révélée.");
    }

    // -----------------------------------------------
    // UI NAVIGATION (consommables + compétences jambes)
    // -----------------------------------------------

    /// <summary>
    /// Recrée tous les boutons de l'UI de navigation.
    /// Appelé au Start et après utilisation d'un consommable.
    /// </summary>
    public void RafraichirUINavigation()
    {
        SpawnConsommablesNav();
        SpawnSkillsJambes();
        SpawnPassifsJambes();
    }

    /// <summary>
    /// Génère les boutons pour les consommables utilisables sur la carte.
    /// Un bouton est créé pour chaque consommable avec usableOnMap = true
    /// et au moins un mapEffect défini.
    /// </summary>
    private void SpawnConsommablesNav()
    {
        if (consommableContainer == null || consommablePrefab == null) return;

        foreach (Transform child in consommableContainer)
            Destroy(child.gameObject);

        if (RunManager.Instance == null) return;

        foreach (ConsumableData conso in RunManager.Instance.GetConsumables())
        {
            GameObject btn = Instantiate(consommablePrefab, consommableContainer);
            ConsumableButton btnScript = btn.GetComponent<ConsumableButton>();
            if (btnScript == null) continue;

            ConsumableData consoRef = conso; // capture pour le lambda
            btnScript.Setup(conso, (c) => UtiliserConsommableNav(c));

            // Utilisable sur la carte uniquement si usableOnMap et au moins un mapEffect défini
            bool utilisable = conso.usableOnMap &&
                              conso.mapEffects != null &&
                              conso.mapEffects.Count > 0;
            btnScript.SetInteractable(utilisable);
        }
    }

    /// <summary>
    /// Génère les boutons pour les compétences de navigation portées par les jambes.
    /// Un bouton est créé pour chaque SkillData avec isNavigationSkill = true.
    /// </summary>
    private void SpawnSkillsJambes()
    {
        if (skillContainer == null || navSkillPrefab == null) return;

        foreach (Transform child in skillContainer)
            Destroy(child.gameObject);

        if (RunManager.Instance == null) return;

        EquipmentData jambes = RunManager.Instance.GetEquipped(EquipmentSlot.Legs);
        if (jambes == null) return;

        foreach (SkillData skill in jambes.skills)
        {
            if (!skill.isNavigationSkill) continue;
            if (skill.navEffects == null || skill.navEffects.Count == 0) continue;

            GameObject btn = Instantiate(navSkillPrefab, skillContainer);

            // Affiche le nom du skill sur le bouton
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = skill.skillName;

            // Branche le clic
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null)
            {
                SkillData skillRef = skill; // capture pour le lambda
                btnComp.onClick.AddListener(() => UtiliserSkillJambes(skillRef));
            }
        }
    }

    /// <summary>
    /// Utilise un consommable depuis la carte :
    /// applique ses mapEffects, le retire de l'inventaire, rafraîchit l'UI.
    /// </summary>
    private void UtiliserConsommableNav(ConsumableData conso)
    {
        Debug.Log($"[Navigation] Consommable utilisé sur la carte : {conso.consumableName}");
        AppliquerEffetsNav(conso.mapEffects);
        RunManager.Instance?.RemoveConsumable(conso);
        SpawnConsommablesNav(); // Rafraîchit les boutons après consommation
    }

    /// <summary>
    /// Utilise une compétence de navigation des jambes :
    /// applique ses navEffects.
    /// Note : pas de cooldown géré hors combat pour l'instant.
    /// </summary>
    private void UtiliserSkillJambes(SkillData skill)
    {
        Debug.Log($"[Navigation] Compétence de jambes utilisée : {skill.skillName}");
        AppliquerEffetsNav(skill.navEffects);
    }

    /// <summary>
    /// Génère les boutons passifs grisés pour les passiveEffects des jambes équipées.
    /// Ajoutés à la suite des compétences actives dans le même skillContainer.
    /// Utilise le même navSkillPrefab (Button + TextMeshProUGUI), avec le bouton désactivé.
    /// </summary>
    private void SpawnPassifsJambes()
    {
        if (skillContainer == null || navSkillPrefab == null) return;
        if (RunManager.Instance == null) return;

        EquipmentData jambes = RunManager.Instance.GetEquipped(EquipmentSlot.Legs);
        if (jambes == null || jambes.passiveEffects == null) return;

        foreach (EffectData effet in jambes.passiveEffects)
        {
            if (effet == null) continue;

            GameObject btn = Instantiate(navSkillPrefab, skillContainer);

            // Affiche le nom de l'effet (displayName ou effectID en fallback)
            string nom = (!string.IsNullOrEmpty(effet.displayName)) ? effet.displayName : effet.effectID;
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = $"{nom} (Passif)";

            // Désactive le bouton — passif, non cliquable
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null) btnComp.interactable = false;

            Debug.Log($"[Navigation] Bouton passif jambes généré : {nom} ({jambes.equipmentName})");
        }
    }
}
