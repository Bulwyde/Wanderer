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

        // Initialise la carte à la première arrivée (résolution des maximums et des cases Aléatoires).
        // Ignoré si on revient d'un combat (carteInitialisee déjà true).
        if (RunManager.Instance != null && !RunManager.Instance.carteInitialisee)
            InitialiserCarte();

        // Évalue les BloqueurLD à chaque chargement de la scène Navigation.
        // Appelé avant le placement du joueur — Start() se charge de UpdateVisibility() ensuite.
        VerifierBloqueurLD();

        // Si RunManager a sauvegardé un état (= on revient d'un combat),
        // on le restaure. Sinon, on place le joueur sur la case de départ.
        if (RunManager.Instance != null && RunManager.Instance.hasNavigationState)
            RestoreNavigationState();
        else
            PlacePlayerOnStart();

        UpdateVisibility();
        mapRenderer.RefreshMap();

        // Applique les NavEffects mis en attente depuis une scène non-Navigation (ex. Event).
        // La liste est vidée après application pour ne pas rejouer les effets à chaque retour.
        if (RunManager.Instance != null && RunManager.Instance.navEffectsEnAttente.Count > 0)
        {
            foreach (NavEffect effetDiffere in RunManager.Instance.navEffectsEnAttente)
            {
                Debug.Log($"[Navigation] Application de l'effet différé : {effetDiffere.type}");
                AppliquerEffetNav(effetDiffere);
            }
            RunManager.Instance.navEffectsEnAttente.Clear();
        }

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
    // INITIALISATION DE CARTE
    // -----------------------------------------------

    /// <summary>
    /// Initialise la carte à la première arrivée du joueur.
    /// Étape 1 : applique les maximums par type (cases en excès → typeDeRemplacement).
    /// Étape 2 : résout les cases Aléatoires via CellAleaPool.
    /// N'est jamais appelée si carteInitialisee est déjà true (retour depuis un combat).
    /// </summary>
    private void InitialiserCarte()
    {
        if (RunManager.Instance == null || mapData == null) return;
        if (mapData.cells == null) return;

        // ── Étape 1 : Maximums par type ─────────────────────────────────────
        // Note design : mapData.typeDeRemplacement ne doit pas lui-même figurer dans
        // maximumsParType — les overrides posés ici ne sont pas recomptés lors du
        // traitement de son propre quota (ils sont dans overridesMaximum, pas dans cell.cellType).
        if (mapData.maximumsParType != null)
        {
            foreach (MaxTypeEntry entree in mapData.maximumsParType)
            {
                if (entree.maximum <= 0) continue;

                // Compter les cases du type concerné (ignorer celles déjà overridées)
                List<CellData> casesType = new List<CellData>();
                foreach (CellData cell in mapData.cells)
                {
                    if (cell == null) continue;
                    if (RunManager.Instance.HasOverrideMaximum(cell.x, cell.y)) continue;
                    if (cell.cellType == entree.type) casesType.Add(cell);
                }

                int exces = casesType.Count - entree.maximum;
                if (exces <= 0) continue;

                // Mélange Fisher-Yates pour choisir aléatoirement les cases en excès
                for (int i = casesType.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    CellData tmp = casesType[i];
                    casesType[i] = casesType[j];
                    casesType[j] = tmp;
                }

                for (int i = 0; i < exces; i++)
                    RunManager.Instance.SetOverrideMaximum(
                        casesType[i].x, casesType[i].y, mapData.typeDeRemplacement);

                Debug.Log($"[Navigation] Maximum dépassé pour {entree.type} — " +
                          $"{exces} case(s) remplacée(s) par {mapData.typeDeRemplacement}");
            }
        }

        // ── Étape 2 : Résolution des cases Aléatoires ───────────────────────
        if (mapData.aleatoirePool == null)
        {
            bool aDesAleatoires = false;
            foreach (CellData cell in mapData.cells)
            {
                if (cell != null && cell.cellType == CellType.Aleatoire)
                { aDesAleatoires = true; break; }
            }
            if (aDesAleatoires)
                Debug.LogWarning("[Navigation] Des cases Aléatoires sont présentes " +
                                 "mais aucune CellAleaPool n'est assignée sur la MapData.");
        }
        else
        {
            // maxOccurrences limite uniquement les cases issues des tirages Aléatoires,
            // pas les cases hardcodées sur la carte — le compteur part donc de zéro.
            Dictionary<CellType, int> comptesActuels = new Dictionary<CellType, int>();

            // Collecter les cases Aléatoires dans un ordre aléatoire (Fisher-Yates)
            List<CellData> casesAleatoires = new List<CellData>();
            foreach (CellData cell in mapData.cells)
            {
                if (cell != null && cell.cellType == CellType.Aleatoire)
                    casesAleatoires.Add(cell);
            }

            for (int i = casesAleatoires.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CellData tmp = casesAleatoires[i];
                casesAleatoires[i] = casesAleatoires[j];
                casesAleatoires[j] = tmp;
            }

            foreach (CellData cell in casesAleatoires)
            {
                CellType resolu = mapData.aleatoirePool.TirerAleatoire(comptesActuels);
                RunManager.Instance.SetResolvedAleatoire(cell.x, cell.y, resolu);

                if (!comptesActuels.ContainsKey(resolu))
                    comptesActuels[resolu] = 0;
                comptesActuels[resolu]++;

                Debug.Log($"[Navigation] Case Aléatoire ({cell.x},{cell.y}) → {resolu}");
            }
        }

        RunManager.Instance.carteInitialisee = true;
        Debug.Log("[Navigation] Carte initialisée.");
    }

    // -----------------------------------------------
    // BLOQUEURS LD
    // -----------------------------------------------

    /// <summary>
    /// Évalue si la condition d'un BloqueurLD est remplie.
    /// Retourne true si le bloqueur doit s'ouvrir (condition satisfaite).
    /// Retourne false si la condition est null, si RunManager est absent,
    /// ou si le compteurID est vide pour un type CompteurNomme.
    /// </summary>
    private bool EvaluerConditionBloqueur(BloqueurCondition condition)
    {
        if (condition == null) return false;
        if (RunManager.Instance == null) return false;

        switch (condition.type)
        {
            case BloqueurConditionType.CompteurNomme:
                if (string.IsNullOrEmpty(condition.compteurID))
                {
                    Debug.LogWarning("[Navigation] EvaluerConditionBloqueur — compteurID vide ou null.");
                    return false;
                }
                return RunManager.Instance.GetCounter(condition.compteurID) >= condition.valeurCible;

            case BloqueurConditionType.CombatsTermines:
                return RunManager.Instance.combatsTermines >= condition.valeurCible;

            case BloqueurConditionType.EventsTermines:
                return RunManager.Instance.eventsTermines >= condition.valeurCible;

            default:
                return false;
        }
    }

    /// <summary>
    /// Parcourt toutes les cases BloqueurLD de la carte et convertit en Empty
    /// celles dont la condition est remplie. Appelle RefreshMap si au moins
    /// un bloqueur a été débloqué. Ne recalcule pas la visibilité —
    /// le appelant doit appeler UpdateVisibility() si nécessaire.
    /// </summary>
    private void VerifierBloqueurLD()
    {
        if (mapData?.cells == null || RunManager.Instance == null) return;

        bool modifie = false;
        foreach (CellData cell in mapData.cells)
        {
            if (cell == null || cell.cellType != CellType.BloqueurLD) continue;

            // Déjà débloqué lors d'une vérification précédente — ignorer
            if (RunManager.Instance.HasPostVisitType(cell.x, cell.y)) continue;

            if (cell.condition == null)
            {
                Debug.LogWarning($"[Navigation] BloqueurLD ({cell.x},{cell.y}) sans condition configurée — ignoré.");
                continue;
            }

            if (EvaluerConditionBloqueur(cell.condition))
            {
                RunManager.Instance.SetPostVisitType(cell.x, cell.y, CellType.Empty);
                modifie = true;
                Debug.Log($"[Navigation] BloqueurLD ({cell.x},{cell.y}) débloqué.");
            }
        }

        if (modifie) mapRenderer.RefreshMap();
    }

    // -----------------------------------------------
    // PLACEMENT DU JOUEUR
    // -----------------------------------------------

    private void PlacePlayerOnStart()
    {
        foreach (CellData cell in mapData.cells)
        {
            // Utiliser GetEffectiveCellType pour ignorer les cases Start overridées
            // par InitialiserCarte() (le SO n'est jamais modifié — cellType brut reste Start).
            CellType typeEffectif = RunManager.Instance != null
                ? RunManager.Instance.GetEffectiveCellType(cell)
                : cell.cellType;

            if (typeEffectif == CellType.Start)
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

        // Vérifie si la case est navigable (en tenant compte des overrides runtime)
        CellData targetCell = mapData.GetCell(targetX, targetY);
        if (targetCell == null)
        {
            Debug.Log("Déplacement impossible — case nulle");
            return;
        }

        CellType typeEffectifCible = RunManager.Instance != null
            ? RunManager.Instance.GetEffectiveCellType(targetCell)
            : targetCell.cellType;

        if (typeEffectifCible == CellType.NonNavigable || typeEffectifCible == CellType.BloqueurLD)
        {
            Debug.Log("Déplacement impossible — case non navigable ou bloqueur");
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
    ///
    /// Algorithme DDA (Digital Differential Analyzer) :
    /// le rayon part du centre de (x1,y1) vers le centre de (x2,y2) et avance
    /// une frontière de grille à la fois — jamais en diagonale d'un seul coup.
    /// À chaque pas, seul le mur cardinal correspondant est vérifié (HasWall
    /// ne connaît que les murs entre cases adjacentes en X ou en Y).
    ///
    /// Cas particulier : si le rayon passe exactement par un coin de grille
    /// (frontières X et Y atteintes simultanément), la vision est bloquée
    /// si l'un ou l'autre des deux murs adjacents est présent.
    /// </summary>
    private bool HasClearLineOfSight(int x1, int y1, int x2, int y2)
    {
        // La case du joueur est toujours visible
        if (x1 == x2 && y1 == y2) return true;

        int dx    = x2 - x1;
        int dy    = y2 - y1;
        int stepX = dx > 0 ? 1 : dx < 0 ? -1 : 0;
        int stepY = dy > 0 ? 1 : dy < 0 ? -1 : 0;

        // tDelta : distance en t entre deux frontières successives sur chaque axe
        // tMax   : t auquel le rayon atteint sa première frontière sur chaque axe
        // On part du centre de la case → distance initiale jusqu'à la frontière = 0.5 case
        float tDeltaX = stepX == 0 ? float.MaxValue : 1f / Mathf.Abs(dx);
        float tDeltaY = stepY == 0 ? float.MaxValue : 1f / Mathf.Abs(dy);
        float tMaxX   = stepX == 0 ? float.MaxValue : 0.5f / Mathf.Abs(dx);
        float tMaxY   = stepY == 0 ? float.MaxValue : 0.5f / Mathf.Abs(dy);

        int curX = x1, curY = y1;

        while (curX != x2 || curY != y2)
        {
            float diff = tMaxX - tMaxY;

            if (Mathf.Abs(diff) < 1e-6f)
            {
                // Le rayon passe exactement par un coin de grille :
                // frontières X et Y atteintes simultanément.
                // Bloque si l'un ou l'autre des murs adjacents est présent.
                if (mapData.HasWall(curX, curY, curX + stepX, curY) ||
                    mapData.HasWall(curX, curY, curX, curY + stepY))
                    return false;
                curX  += stepX;
                curY  += stepY;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }
            else if (tMaxX < tMaxY)
            {
                // Frontière verticale atteinte en premier : vérifie le mur en X
                if (mapData.HasWall(curX, curY, curX + stepX, curY))
                    return false;
                curX  += stepX;
                tMaxX += tDeltaX;
            }
            else
            {
                // Frontière horizontale atteinte en premier : vérifie le mur en Y
                if (mapData.HasWall(curX, curY, curX, curY + stepY))
                    return false;
                curY  += stepY;
                tMaxY += tDeltaY;
            }
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
    /// Utilise GetEffectiveCellType pour respecter les overrides de maximum et les Aléatoires résolus.
    /// </summary>
    private void OnRoomEntered(CellData cell)
    {
        CellType typeEffectif = RunManager.Instance != null
            ? RunManager.Instance.GetEffectiveCellType(cell)
            : cell.cellType;

        switch (typeEffectif)
        {
            case CellType.CombatSimple:
            case CellType.Elite:
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

                // Stocke la MapData pour que ResolveEnemyPool() puisse accéder aux pools d'ennemis
                RunManager.Instance.currentMapData = mapData;
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

            case CellType.Shop:
                // La case marchand n'est jamais "cleared" — on peut y revenir autant de fois que souhaité.
                // L'état (inventaire, prix, articles achetés) est persistant via RunManager.shopStates.
                if (RunManager.Instance == null || SceneLoader.Instance == null)
                {
                    Debug.LogError("RunManager ou SceneLoader introuvable !");
                    break;
                }

                // Détection de première visite : GetShopState renvoie null si l'inventaire
                // n'a pas encore été généré pour cette case → c'est la première fois.
                // On tick avant GoToShop() car ShopManager appellera GetOrCreateShopState()
                // qui créera l'état, empêchant la détection lors de la prochaine visite.
                if (RunManager.Instance.GetShopState(cell.x, cell.y) == null)
                    RunManager.Instance.TickCooldownsDe(NavCooldownType.ShopDecouvert);

                // Stocke la MapData pour que ShopManager puisse accéder à la CellData
                RunManager.Instance.currentMapData = mapData;
                RunManager.Instance.SaveNavigationState(
                    PlayerX, PlayerY, visitedCells, exploredCells);
                RunManager.Instance.EnterRoom(cell);
                SceneLoader.Instance.GoToShop();
                break;

            case CellType.Start:
                Debug.Log($"({PlayerX},{PlayerY}) — Case de départ, pas de transition");
                break;

            case CellType.Empty:
                Debug.Log($"({PlayerX},{PlayerY}) — Case vide, pas de transition");
                break;

            // -----------------------------------------------------------
            // POINT D'INTÉRÊT — événement spécifique curé (specificEvent)
            // -----------------------------------------------------------

            case CellType.PointInteret:
                if (cell.specificEvent == null)
                {
                    Debug.LogWarning($"({PlayerX},{PlayerY}) — PointInteret sans specificEvent assigné, pas de transition.");
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
                RunManager.Instance.currentSpecificEventID = cell.specificEvent.eventID;
                SceneLoader.Instance.GoToEvent();
                break;

            // -----------------------------------------------------------
            // RADAR / COFFRE — événement poolé (ChoisirEventAleatoire)
            // -----------------------------------------------------------

            case CellType.Radar:
            case CellType.Coffre:
            {
                EventData eventPoole = ChoisirEventAleatoire(cell);
                if (eventPoole == null)
                {
                    Debug.Log($"({PlayerX},{PlayerY}) — Tous les events du pool ont été joués, salle ignorée.");
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
                RunManager.Instance.currentSpecificEventID = eventPoole.eventID;
                SceneLoader.Instance.GoToEvent();
                break;
            }

            // -----------------------------------------------------------
            // TÉLÉPORTEUR — specificEvent si assigné, sinon defaultTeleportEvent de la MapData
            // -----------------------------------------------------------

            case CellType.Teleporteur:
            {
                // Priorité 1 : event curé directement sur la case
                // Priorité 2 : fallback global de la MapData (cas Aléatoire résolu → Teleporteur)
                EventData eventTeleporteur = cell.specificEvent ?? mapData.defaultTeleportEvent;

                if (eventTeleporteur == null)
                {
                    Debug.LogWarning($"[Navigation] Teleporteur ({cell.x},{cell.y}) — aucun event configuré " +
                                     $"(specificEvent null et defaultTeleportEvent non assigné sur la MapData).");
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
                RunManager.Instance.currentSpecificEventID = eventTeleporteur.eventID;
                SceneLoader.Instance.GoToEvent();
                break;
            }

            // -----------------------------------------------------------
            // FERRAILLEUR — placeholder [Chantier futur]
            // Mécanique complète à implémenter dans un chantier dédié.
            // Pour l'instant : flux identique à une salle Event poolée.
            // -----------------------------------------------------------

            case CellType.Ferrailleur:
            {
                EventData eventFerrailleur = ChoisirEventAleatoire(cell);
                if (eventFerrailleur == null)
                {
                    Debug.Log($"({PlayerX},{PlayerY}) — Ferrailleur : aucun event disponible, salle ignorée.");
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
                RunManager.Instance.currentSpecificEventID = eventFerrailleur.eventID;
                SceneLoader.Instance.GoToEvent();
                break;
            }

            // -----------------------------------------------------------
            // TYPES POST-VISITE — déjà utilisés, aucune transition
            // -----------------------------------------------------------

            case CellType.FerailleurUtilise:
                Debug.Log($"({PlayerX},{PlayerY}) — Ferrailleur déjà utilisé, pas de transition.");
                break;

            case CellType.TeleporteurUtilise:
                Debug.Log($"({PlayerX},{PlayerY}) — Téléporteur déjà utilisé, pas de transition.");
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

            // Utilisable sur la carte si usableOnMap ET au moins un effet défini
            // (mapEffects OU effects — les deux sont appliqués à l'utilisation)
            bool aMapEffects = conso.mapEffects != null && conso.mapEffects.Count > 0;
            bool aEffects    = conso.effects    != null && conso.effects.Count    > 0;
            bool utilisable  = conso.usableOnMap && (aMapEffects || aEffects);
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

        foreach (SkillSlot slot in jambes.skillSlots)
        {
            if (slot == null) continue;
            if (slot.state != SkillSlot.SlotState.Used &&
                slot.state != SkillSlot.SlotState.LockedInUse) continue;
            SkillData skill = slot.equippedSkill;
            if (skill == null) continue;

            if (!skill.isNavigationSkill) continue;
            if (skill.navEffects == null || skill.navEffects.Count == 0) continue;

            GameObject btn = Instantiate(navSkillPrefab, skillContainer);

            // Affiche le nom du skill sur le bouton
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = skill.skillName;

            // Branche le clic et grise le bouton si le skill est en cooldown
            Button btnComp = btn.GetComponent<Button>();
            if (btnComp != null)
            {
                bool estPret = RunManager.Instance?.IsNavSkillReady(skill.skillID) ?? true;
                btnComp.interactable = estPret;

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

        // Applique les NavEffects (téléportation, révélation, compteur...)
        if (conso.mapEffects != null && conso.mapEffects.Count > 0)
            AppliquerEffetsNav(conso.mapEffects);

        // Applique les EffectData (Heal, AddCredits, ModifyStat...)
        if (conso.effects != null)
        {
            foreach (EffectData effet in conso.effects)
            {
                if (effet == null) continue;
                AppliquerEffetConsommableHorsCombat(effet, conso.consumableName);
            }
        }

        RunManager.Instance?.RemoveConsumable(conso);
        SpawnConsommablesNav(); // Rafraîchit les boutons après consommation
    }

    /// <summary>
    /// Applique un EffectData hors combat depuis la carte (consommable utilisé en navigation).
    /// Seules les actions sensées hors combat sont traitées — les autres sont ignorées avec un log.
    /// </summary>
    private void AppliquerEffetConsommableHorsCombat(EffectData effet, string source)
    {
        if (effet == null || RunManager.Instance == null) return;

        switch (effet.action)
        {
            case EffectAction.Heal:
            {
                int soin = Mathf.Min(
                    Mathf.Max(0, Mathf.RoundToInt(effet.value)),
                    RunManager.Instance.maxHP - RunManager.Instance.currentHP
                );
                if (soin > 0)
                {
                    RunManager.Instance.currentHP += soin;
                    Debug.Log($"[Navigation] {source} — Soin : +{soin} HP " +
                              $"→ {RunManager.Instance.currentHP}/{RunManager.Instance.maxHP}");
                }
                break;
            }

            case EffectAction.AddCredits:
            {
                int montant = Mathf.RoundToInt(effet.value);
                RunManager.Instance.AddCredits(montant);
                string signe = montant >= 0 ? "+" : "";
                Debug.Log($"[Navigation] {source} — {signe}{montant} credits " +
                          $"→ {RunManager.Instance.credits}");
                break;
            }

            case EffectAction.ModifyStat:
            {
                RunManager.Instance.AddStatBonus(effet.statToModify, effet.value);
                Debug.Log($"[Navigation] {source} — ModifyStat : {effet.statToModify} " +
                          $"{(effet.value >= 0 ? "+" : "")}{effet.value}");
                break;
            }

            default:
                Debug.Log($"[Navigation] {source} — Effet '{effet.action}' non applicable hors combat, ignoré.");
                break;
        }
    }

    /// <summary>
    /// Utilise une compétence de navigation des jambes :
    /// applique ses navEffects, puis met le skill en cooldown si un type est configuré.
    /// </summary>
    private void UtiliserSkillJambes(SkillData skill)
    {
        Debug.Log($"[Navigation] Compétence de jambes utilisée : {skill.skillName}");
        AppliquerEffetsNav(skill.navEffects);

        // Met le skill en cooldown si un type est configuré
        if (skill.navCooldownType != NavCooldownType.None && !string.IsNullOrEmpty(skill.skillID))
        {
            RunManager.Instance?.SetNavSkillCooldown(
                skill.skillID, skill.navCooldownType, skill.navCooldownCount, skill.navCooldownTag);
            SpawnSkillsJambes(); // Rafraîchit les boutons (bouton concerné maintenant grisé)
        }
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
