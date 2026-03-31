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
    // Référence directe au ScriptableObject du personnage choisi.
    // Assignée par MainMenuManager (ou la future scène de sélection) via StartNewRun().
    public CharacterData selectedCharacter;

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

        // Notifie les HUDs pour qu'ils rafraîchissent l'affichage des icônes
        ModuleManager.NotifyModulesChanged();
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
    // COMPTEURS DE NAVIGATION
    // -----------------------------------------------

    // Compteurs nommés — usage libre pour les mécaniques de run.
    // Ex : "cles" pour ouvrir des passages, "ferveur" pour un événement conditionnel.
    // Accès via GetCounter / IncrementCounter / SetCounter.
    private Dictionary<string, int> navigationCounters = new Dictionary<string, int>();

    // Bonus de portée de vision accumulés pendant le run.
    // S'ajoute au baseVisionRange défini dans NavigationManager.
    public int visionRangeBonus = 0;

    /// <summary>
    /// Incrémente (ou décrémente) un compteur nommé de `delta`.
    /// Crée le compteur s'il n'existe pas encore (valeur de départ : 0).
    /// </summary>
    public void IncrementCounter(string key, int delta)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!navigationCounters.ContainsKey(key))
            navigationCounters[key] = 0;
        navigationCounters[key] += delta;
        Debug.Log($"[RunManager] Compteur '{key}' : {navigationCounters[key]} ({(delta >= 0 ? "+" : "")}{delta})");
    }

    /// <summary>
    /// Retourne la valeur actuelle d'un compteur nommé (0 si inexistant).
    /// </summary>
    public int GetCounter(string key)
    {
        return string.IsNullOrEmpty(key) ? 0 :
               navigationCounters.TryGetValue(key, out int val) ? val : 0;
    }

    /// <summary>
    /// Pose un compteur à une valeur absolue.
    /// </summary>
    public void SetCounter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        navigationCounters[key] = value;
        Debug.Log($"[RunManager] Compteur '{key}' fixé à {value}");
    }

    // -----------------------------------------------
    // ÉVÉNEMENTS JOUÉS
    // -----------------------------------------------

    // IDs des événements déjà joués pendant la run.
    // Permet d'exclure un event du pool d'une salle s'il a déjà été déclenché.
    private HashSet<string> playedEventIDs = new HashSet<string>();

    /// <summary>
    /// Marque un événement comme joué.
    /// Appelé par EventManager quand le joueur clique sur "Continuer".
    /// </summary>
    public void MarkEventPlayed(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return;
        playedEventIDs.Add(eventID);
        Debug.Log($"[RunManager] Événement '{eventID}' marqué comme joué.");
    }

    /// <summary>
    /// Retourne true si cet événement a déjà été joué pendant la run.
    /// </summary>
    public bool IsEventPlayed(string eventID)
    {
        return !string.IsNullOrEmpty(eventID) && playedEventIDs.Contains(eventID);
    }

    // -----------------------------------------------
    // ÉTAT DE NAVIGATION SAUVEGARDÉ
    // -----------------------------------------------

    // Indique si on a un état de navigation à restaurer.
    // Vaut false au démarrage d'un run (le joueur n'est pas encore parti en combat).
    [Header("État de la run")]
    public bool hasActiveRun = false;

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
    public void StartNewRun(CharacterData character, string missionID)
    {
        selectedCharacter = character;
        currentMissionID  = missionID;
        currentRoomX        = 0;
        currentRoomY        = 0;
        difficultyModifier  = 1.0f;
        eventFlags.Clear();
        clearedRooms.Clear();
        playedEventIDs.Clear();
        equippedItems.Clear();
        activeModules.Clear();
        consumables.Clear();
        maxConsumableSlots = 3;
        startingConsumablesSeeded = false;
        navigationCounters.Clear();
        visionRangeBonus = 0;

        // Réinitialise l'état de navigation : le joueur repart de la case de départ
        hasNavigationState = false;
        savedVisitedCells.Clear();
        savedExploredCells.Clear();

        hasActiveRun = true;

        // Seed l'équipement, le module et les consommables de départ
        // dès le lancement du run, pour que la Navigation les affiche immédiatement.
        SeedDonneesDepart(character);

        Debug.Log($"[RunManager] Nouveau run — Personnage : {character?.characterName ?? "inconnu"} | Mission : {missionID}");
    }

    /// <summary>
    /// Enregistre la salle dans laquelle le joueur entre.
    /// Appelé par NavigationManager juste avant de changer de scène,
    /// pour que la scène de destination sache quelle salle traiter.
    /// </summary>
    public void EnterRoom(CellData cell)
    {
        currentRoomX   = cell.x;
        currentRoomY   = cell.y;
        currentCellType = cell.cellType;
        // currentSpecificEventID est désormais assigné par NavigationManager
        // après le tirage aléatoire dans ChoisirEventAleatoire()

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

    /// <summary>
    /// Termine la run en cours (défaite ou abandon).
    /// Remet hasActiveRun à false — le MainMenu désactivera "Continuer" en conséquence.
    /// </summary>
    /// <summary>
    /// Seed l'équipement, le module et les consommables de départ du personnage.
    /// Appelé depuis StartNewRun() — les guards (IsSlotFree, HasModule, startingConsumablesSeeded)
    /// garantissent que CombatManager ne re-seedera rien lors du premier combat.
    /// </summary>
    private void SeedDonneesDepart(CharacterData character)
    {
        if (character == null) return;

        // Équipement de départ — un slot par slot, seulement si vide
        SeedSlotSiVide(EquipmentSlot.Head,  character.startingHead);
        SeedSlotSiVide(EquipmentSlot.Torso, character.startingTorso);
        SeedSlotSiVide(EquipmentSlot.Legs,  character.startingLegs);
        SeedSlotSiVide(EquipmentSlot.Arm1,  character.startingArm1);
        SeedSlotSiVide(EquipmentSlot.Arm2,  character.startingArm2);

        // Module de départ
        if (character.startingModule != null && !HasModule(character.startingModule))
            AddModule(character.startingModule);

        // Consommables de départ (une seule fois par run)
        if (character.startingConsumables != null && !startingConsumablesSeeded)
        {
            foreach (ConsumableData consumable in character.startingConsumables)
            {
                if (consumable != null)
                    AddConsumable(consumable);
            }
            startingConsumablesSeeded = true;
        }

        // Stats de départ — calculées après le seeding de l'équipement
        // pour inclure les bonus des pièces de départ dans le total.
        InitialiserStats(character);

        Debug.Log($"[RunManager] Données de départ seedées pour {character.characterName}.");
    }

    /// <summary>
    /// Calcule les stats de départ (HP, et à terme toutes les ressources)
    /// à partir du CharacterData et des bonus d'équipement déjà seedés.
    /// Appelé depuis SeedDonneesDepart(), après le seeding de l'équipement.
    /// </summary>
    private void InitialiserStats(CharacterData character)
    {
        // HP max = base + bonus de chaque slot équipé
        int hpMax = character.maxHP;
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = GetEquipped(slot);
            if (equip != null)
                hpMax += equip.bonusHP;
        }

        maxHP     = Mathf.Max(1, hpMax);
        currentHP = maxHP;

        // -----------------------------------------------
        // RESSOURCES FUTURES (or, mana, etc.)
        // Ajouter ici quand CharacterData exposera des valeurs de départ.
        // Exemple : gold = character.startingGold;
        // -----------------------------------------------

        Debug.Log($"[RunManager] Stats initialisées — HP : {currentHP}/{maxHP}");
    }

    private void SeedSlotSiVide(EquipmentSlot slot, EquipmentData starting)
    {
        if (starting != null && IsSlotFree(slot))
            EquipItem(slot, starting);
    }

    public void EndRun()
    {
        hasActiveRun = false;
        Debug.Log("[RunManager] Run terminée.");
    }

    public void AddDifficultyModifier(float delta)
    {
        difficultyModifier = Mathf.Clamp(difficultyModifier + delta, 0.5f, 3.0f);
    }
}
