using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton persistant entre les scènes (DontDestroyOnLoad).
/// Stocke tout l'état de la run en cours : personnage, HP, position sur la carte,
/// salle actuelle, difficulté et flags d'événements.
/// </summary>
public class RunManager : MonoBehaviour
{
    // -----------------------------------------------
    // SINGLETON
    // -----------------------------------------------

    public static RunManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------
    // ÉTAT DE LA RUN EN COURS
    // -----------------------------------------------

    [Header("Personnage")]
    public string selectedCharacterID;

    [Header("État du joueur")]
    public int currentHP;
    public int maxHP;

    [Header("Navigation")]
    // Identifiant de la mission (map) en cours
    public string currentMissionID;

    // Position de la salle actuelle sur la carte
    // Stockée avant de changer de scène pour pouvoir y revenir
    public int currentRoomX;
    public int currentRoomY;

    // Type de la salle actuelle (Classic, Boss, Event...)
    // Permet aux autres scènes de savoir dans quel contexte elles s'exécutent
    public CellType currentCellType;

    // ID d'événement spécifique si la salle est de type Event
    public string currentSpecificEventID;

    [Header("Modificateurs de difficulté")]
    public float difficultyModifier = 1.0f;

    [Header("Flags d'événements")]
    public SerializableDictionary<string, bool> eventFlags = new SerializableDictionary<string, bool>();

    // -----------------------------------------------
    // ÉQUIPEMENT PORTÉ
    // -----------------------------------------------

    // Pièces actuellement équipées, indexées par slot.
    // Un slot absent du dictionnaire (ou valeur null) signifie slot vide.
    //
    // Note architecture : si un inventaire est ajouté un jour, ce dictionnaire
    // reste tel quel — on ajoutera simplement une List<EquipmentData> inventory
    // à côté, et EquipItem() y déplacera l'ancienne pièce au lieu de la jeter.
    private Dictionary<EquipmentSlot, EquipmentData> equippedItems
        = new Dictionary<EquipmentSlot, EquipmentData>();

    /// <summary>
    /// Équipe une pièce dans le slot donné.
    /// Remplace silencieusement ce qui était déjà là.
    /// (Futur : déplacer l'ancienne pièce dans l'inventaire au lieu de la supprimer.)
    /// </summary>
    public void EquipItem(EquipmentSlot slot, EquipmentData item)
    {
        equippedItems[slot] = item;
        Debug.Log($"[RunManager] Équipement — {slot} : {item?.equipmentName ?? "aucun"}");
    }

    /// <summary>
    /// Retourne la pièce équipée dans le slot, ou null si le slot est vide.
    /// </summary>
    public EquipmentData GetEquipped(EquipmentSlot slot)
    {
        return equippedItems.TryGetValue(slot, out EquipmentData item) ? item : null;
    }

    /// <summary>
    /// Retourne true si le slot est vide (aucune pièce équipée).
    /// </summary>
    public bool IsSlotFree(EquipmentSlot slot)
    {
        return GetEquipped(slot) == null;
    }

    // -----------------------------------------------
    // MODULES ACTIFS
    // -----------------------------------------------

    // Modules passifs acquis pendant la run (équivalent des reliques dans StS).
    // Le module de départ du personnage est seedé au premier combat via CombatManager.
    private List<ModuleData> activeModules = new List<ModuleData>();

    /// <summary>
    /// Ajoute un module à la liste des modules actifs.
    /// Ignore les doublons — un même module ne peut être ajouté qu'une fois.
    /// </summary>
    public void AddModule(ModuleData module)
    {
        if (module == null || activeModules.Contains(module)) return;
        activeModules.Add(module);
        Debug.Log($"[RunManager] Module acquis : {module.moduleName}");
    }

    /// <summary>
    /// Retourne une copie de la liste des modules actifs.
    /// </summary>
    public List<ModuleData> GetModules() => new List<ModuleData>(activeModules);

    /// <summary>
    /// Retourne true si le module est actuellement actif.
    /// </summary>
    public bool HasModule(ModuleData module) => module != null && activeModules.Contains(module);

    // -----------------------------------------------
    // CONSOMMABLES
    // -----------------------------------------------

    // Nombre de slots disponibles — 3 par défaut, modifiable par modules (max 6)
    public int maxConsumableSlots = 3;

    // Indique si les consommables de départ ont déjà été donnés au joueur ce run.
    // Évite de les redonner si le joueur a tout utilisé et entre dans un nouveau combat.
    public bool startingConsumablesSeeded = false;

    // Consommables actuellement en possession du joueur
    private List<ConsumableData> consumables = new List<ConsumableData>();

    /// <summary>
    /// Ajoute un consommable si un slot est disponible.
    /// Retourne true si l'ajout réussit, false si l'inventaire est plein.
    /// </summary>
    public bool AddConsumable(ConsumableData consumable)
    {
        if (consumable == null) return false;

        int effectiveMax = Mathf.Clamp(maxConsumableSlots, 1, 6);
        if (consumables.Count >= effectiveMax)
        {
            Debug.Log($"[RunManager] Impossible d'ajouter {consumable.consumableName} " +
                      $"— inventaire plein ({consumables.Count}/{effectiveMax})");
            return false;
        }

        consumables.Add(consumable);
        Debug.Log($"[RunManager] Consommable obtenu : {consumable.consumableName} " +
                  $"({consumables.Count}/{effectiveMax})");
        return true;
    }

    /// <summary>
    /// Retire un consommable de l'inventaire (après utilisation).
    /// </summary>
    public void RemoveConsumable(ConsumableData consumable)
    {
        if (consumables.Remove(consumable))
            Debug.Log($"[RunManager] Consommable utilisé : {consumable.consumableName}");
    }

    /// <summary>
    /// Retourne une copie de la liste des consommables actifs.
    /// </summary>
    public List<ConsumableData> GetConsumables() => new List<ConsumableData>(consumables);

    /// <summary>
    /// Retourne true si le joueur a au moins un slot de consommable libre.
    /// </summary>
    public bool HasConsumableSlotFree() => consumables.Count < Mathf.Clamp(maxConsumableSlots, 1, 6);

    // Salles complétées pendant la run — stockées par clé "x,y".
    // HashSet est idéal ici : vérification instantanée, pas de doublons.
    // Ce n'est pas sérialisé car c'est un état purement runtime (réinitialisé à chaque run).
    private HashSet<string> clearedRooms = new HashSet<string>();

    // -----------------------------------------------
    // ÉTAT DE NAVIGATION SAUVEGARDÉ
    // -----------------------------------------------

    // Indique si on a un état de navigation à restaurer.
    // Vaut false au démarrage d'un run (le joueur n'est pas encore parti en combat).
    [Header("Navigation sauvegardée")]
    public bool hasNavigationState = false;

    // Dernière position connue du joueur sur la carte
    public int savedPlayerX;
    public int savedPlayerY;

    // Listes des cases visitées et explorées.
    // On utilise List<Vector2Int> plutôt que HashSet car Unity
    // peut sérialiser les List mais pas les HashSet.
    // NavigationManager les reconvertira en HashSet au chargement.
    public List<Vector2Int> savedVisitedCells  = new List<Vector2Int>();
    public List<Vector2Int> savedExploredCells = new List<Vector2Int>();

    // -----------------------------------------------
    // MÉTHODES
    // -----------------------------------------------

    /// <summary>
    /// Initialise une nouvelle run depuis zéro.
    /// </summary>
    public void StartNewRun(string characterID, string missionID)
    {
        selectedCharacterID = characterID;
        currentMissionID    = missionID;
        currentRoomX        = 0;
        currentRoomY        = 0;
        difficultyModifier  = 1.0f;
        eventFlags.Clear();
        clearedRooms.Clear();
        equippedItems.Clear();
        activeModules.Clear();
        consumables.Clear();
        maxConsumableSlots = 3;
        startingConsumablesSeeded = false;

        // Réinitialise l'état de navigation : le joueur repart de la case de départ
        hasNavigationState = false;
        savedVisitedCells.Clear();
        savedExploredCells.Clear();

        Debug.Log($"Nouveau run — Personnage : {characterID} | Mission : {missionID}");
    }

    /// <summary>
    /// Enregistre la salle dans laquelle le joueur entre.
    /// Appelé par NavigationManager juste avant de changer de scène,
    /// pour que la scène de destination sache quelle salle traiter.
    /// </summary>
    public void EnterRoom(CellData cell)
    {
        currentRoomX            = cell.x;
        currentRoomY            = cell.y;
        currentCellType         = cell.cellType;
        currentSpecificEventID  = cell.specificEventID;

        Debug.Log($"RunManager — Entrée en salle ({cell.x},{cell.y}) — Type : {cell.cellType}");
    }

    /// <summary>
    /// Sauvegarde l'état complet de navigation avant de quitter la scène Navigation.
    /// Appelé par NavigationManager juste avant GoToCombat().
    /// On passe les HashSets qu'on convertit en Lists pour la sérialisation.
    /// </summary>
    public void SaveNavigationState(int playerX, int playerY,
        HashSet<Vector2Int> visitedCells, HashSet<Vector2Int> exploredCells)
    {
        savedPlayerX = playerX;
        savedPlayerY = playerY;

        // Conversion HashSet → List
        savedVisitedCells  = new List<Vector2Int>(visitedCells);
        savedExploredCells = new List<Vector2Int>(exploredCells);

        hasNavigationState = true;
        Debug.Log($"Navigation sauvegardée — Position : ({playerX}, {playerY}), " +
                  $"Visitées : {visitedCells.Count}, Explorées : {exploredCells.Count}");
    }

    /// <summary>
    /// Marque la salle courante (currentRoomX, currentRoomY) comme complétée.
    /// Appelé par CombatManager quand le joueur remporte le combat.
    /// </summary>
    public void ClearCurrentRoom()
    {
        string key = $"{currentRoomX},{currentRoomY}";
        clearedRooms.Add(key);
        Debug.Log($"Salle ({currentRoomX},{currentRoomY}) marquée comme complétée.");
    }

    /// <summary>
    /// Retourne true si la salle à la position (x, y) a déjà été complétée.
    /// Utilisé par MapRenderer pour afficher les salles vidées différemment.
    /// </summary>
    public bool IsRoomCleared(int x, int y)
    {
        return clearedRooms.Contains($"{x},{y}");
    }

    public void SetEventFlag(string key, bool value)
    {
        eventFlags[key] = value;
    }

    public bool GetEventFlag(string key)
    {
        return eventFlags.ContainsKey(key) && eventFlags[key];
    }

    public void AddDifficultyModifier(float delta)
    {
        difficultyModifier = Mathf.Clamp(difficultyModifier + delta, 0.5f, 3.0f);
    }
}
