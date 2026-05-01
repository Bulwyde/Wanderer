using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public enum BattleState { PlayerTurn, EnemyTurn, Victory, Defeat }

/// <summary>
/// Gère la scène de combat : tours, énergie, compétences, effets, fin de combat.
/// Supporte les combats contre 1 à 4 ennemis simultanément.
///
/// Résolution de la rencontre au démarrage :
///   RunManager.currentEnemyGroup (groupe) > RunManager.currentEnemyData (solo) > fallback Inspector
///
/// Ciblage :
///   Skills SingleEnemy → mode sélection de cible (flèche joueur → souris → clic sur ennemi)
///   Skills AllEnemies / RandomEnemy → résolution automatique, pas de sélection
/// </summary>
public class CombatManager : MonoBehaviour
{
    // -----------------------------------------------
    // DONNÉES
    // -----------------------------------------------

    [Header("Données")]
    public CharacterData characterData;
    // Fallbacks Inspector pour les tests en scène isolée (écrasés par RunManager en jeu normal)
    // Priorité : enemyGroup > enemyData (même logique qu'en jeu via RunManager)
    public EnemyGroup    enemyGroup;
    public EnemyData     enemyData;

    // -----------------------------------------------
    // UI — JOUEUR
    // -----------------------------------------------

    [Header("UI — Joueur")]
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerArmorText;
    public TextMeshProUGUI energyText;
    public TextMeshProUGUI creditsText;
    // Sprite du joueur — sert aussi de point d'ancrage pour la flèche de ciblage
    public RectTransform   playerSpriteTransform;

    // -----------------------------------------------
    // UI — ENNEMIS (dynamique)
    // -----------------------------------------------

    [Header("UI — Ennemis")]
    // Conteneur dans lequel les blocs UI ennemis sont générés dynamiquement (HorizontalLayoutGroup)
    public Transform   enemiesContainer;
    // Prefab d'un bloc ennemi — structure attendue :
    //   Root (Button + EnemyUIBlock si besoin)
    //     ├── EnemySprite (Image) ← sprite + Animator pour les futures animations
    //     ├── EnemyNameText (TMP)
    //     ├── EnemyHPText (TMP)
    //     ├── EnemyArmorText (TMP)
    //     └── EnemyNextActionText (TMP)
    public GameObject  enemyUIPrefab;

    // -----------------------------------------------
    // UI — CIBLAGE (flèche style Slay the Spire)
    // -----------------------------------------------

    [Header("UI — Ciblage")]
    // RectTransform de la flèche — pivot (0, 0.5), s'étire de playerSpriteTransform vers la souris
    // Doit avoir un composant Image avec le sprite de la flèche (ou une couleur de remplissage)
    public RectTransform arrowTransform;

    // -----------------------------------------------
    // UI — COMBAT
    // -----------------------------------------------

    [Header("UI — Combat")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI combatLogText;
    public Button          endTurnButton;
    public Transform       skillButtonContainer;
    public GameObject      skillButtonPrefab;

    // -----------------------------------------------
    // UI — CONSOMMABLES
    // -----------------------------------------------

    [Header("UI — Consommables")]
    public Transform  consumableButtonContainer;
    public GameObject consumableButtonPrefab;

    // -----------------------------------------------
    // UI — DEBUG
    // -----------------------------------------------

    [Header("Mort d'ennemi")]
    [Tooltip("Durée du fade visuel après la mort d'un ennemi (secondes)")]
    public float fadeDureeMort = 3.5f;

    [Header("Debug / Tests")]
    public Button victoireButton;

    // -----------------------------------------------
    // UI — PANNEAUX
    // -----------------------------------------------

    [Header("UI — Panneaux")]
    public GameObject      combatActivePanel;
    public GameObject      endPanel;
    public TextMeshProUGUI endTitleText;
    public Button          endButton;
    public TextMeshProUGUI endButtonText;

    // -----------------------------------------------
    // UI — LOOT
    // -----------------------------------------------

    [Header("UI — Loot")]
    public GameObject              lootPanel;
    public Button                  lootContinueButton;
    public EquipmentOfferController equipmentOfferController;

    // Équipements injectés en cours de combat (via DonnerEquipement) — ajoutés aux offres du loot panel.
    private List<EquipmentData> equipementsLootDifféré = new List<EquipmentData>();

    // -----------------------------------------------
    // UI — STATUTS JOUEUR
    // -----------------------------------------------

    [Header("UI — Statuts joueur")]
    // Container des icônes — doit avoir un GridLayoutGroup (Constraint = Fixed Column Count)
    // Le constraintCount est écrasé au runtime par statusIconsPerRow
    public Transform   statusIconContainer;
    // Prefab d'une icône — doit avoir le composant StatusIcon (Image + StackText TMP)
    public GameObject  statusIconPrefab;
    [Tooltip("Nombre d'icônes de statut par ligne avant retour à la ligne")]
    public int         statusIconsPerRow = 5;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private BattleState battleState;

    // Groupe résolu depuis RunManager (null si combat solo)
    private EnemyGroup currentGroup;

    // Liste des ennemis actifs — chaque EnemyInstance porte toutes ses données runtime
    private List<EnemyInstance> enemies = new List<EnemyInstance>();

    // Icônes de statuts joueur actuellement affichées
    private List<StatusIcon> _spawnedStatusIcons = new List<StatusIcon>();

    // Canvas racine — mis en cache pour la conversion pixels écran → unités canvas (flèche ciblage)
    private Canvas _rootCanvas;

    // Contexte d'execution du skill en attente (mode selection de cible)
    private struct ContexteExecutionSkill
    {
        public float         multiplicateurDegatsBase;   // applique a skill.value avant CalculerDegatsJoueur
        public float         multiplicateurDegatsFinal;  // delta applique aux degats finaux
        public float         bonusCrit;                  // ajout flat a la crit chance
        public EffectTarget? overrideTarget;             // si non-null, force ce target sur les DealDamage et ApplyStatus
        public int           repetitions;                // nb total d'executions (1 = pas de repetition)
        public int           bonusStacksStatut;          // stacks supplementaires sur tout ApplyStatus
        public int           coutEnergieSupplementaire;  // s'ajoute a skill.energyCost
        public EquipmentData sourceEquipment;            // equipement source du skill (pour SkillEquipeSurCetObjet)

        public static ContexteExecutionSkill Default => new ContexteExecutionSkill
        {
            multiplicateurDegatsBase  = 0f,
            multiplicateurDegatsFinal = 0f,
            bonusCrit                 = 0f,
            overrideTarget            = null,
            repetitions               = 1,
            bonusStacksStatut         = 0,
            coutEnergieSupplementaire = 0,
            sourceEquipment           = null,
        };
    }

    // Compétence en attente d'une cible (mode sélection de cible)
    private SkillData   pendingSkill;
    private ContexteExecutionSkill _pendingCtx;
    private int         _pendingCoutEffectif;
    private bool        isSelectingTarget;

    // Vrai si EndCombat a déjà été appelé ce combat (protection multi-appel)
    private bool combatEnded;

    private int currentPlayerHP;
    private int currentEnergy;
    private int currentPlayerArmor;

    private int   effectiveMaxHP;
    private int   effectiveAttack;
    private int   effectiveDefense;
    private int   effectiveMaxEnergy;
    private float effectiveCriticalChance;
    private float effectiveCriticalMultiplier;
    private int   effectiveRegeneration;
    private float effectiveLifeSteal;

    private List<SkillData>        availableSkills          = new List<SkillData>();
    private List<EquipmentData>    _availableSkillSources   = new List<EquipmentData>();
    private List<SkillButton>      spawnedSkillButtons      = new List<SkillButton>();
    private List<ConsumableButton> spawnedConsumableButtons = new List<ConsumableButton>();
    private Dictionary<SkillData, int> skillCooldowns       = new Dictionary<SkillData, int>();

    private const float EnemyActionDelay = 1.0f;

    private bool isFirstTurn = true;

    private Dictionary<StatusData, int> playerStatuses = new Dictionary<StatusData, int>();

    private Dictionary<StatType, float> combatStatModifiers = new Dictionary<StatType, float>();

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        if (RunManager.Instance?.selectedCharacter != null)
            characterData = RunManager.Instance.selectedCharacter;
        else
            Debug.Log("[Combat] Pas de CharacterData dans RunManager — utilisation du champ Inspector local.");

        // Résout la rencontre (groupe > solo > fallback Inspector)
        if (RunManager.Instance?.currentEnemyGroup != null)
        {
            currentGroup = RunManager.Instance.currentEnemyGroup;
        }
        else if (RunManager.Instance?.currentEnemyData != null)
        {
            enemyData = RunManager.Instance.currentEnemyData;
            currentGroup = null;
        }
        else
        {
            // Fallback Inspector — priorité groupe > solo
            currentGroup = enemyGroup;
            Debug.Log("[Combat] Pas de rencontre dans RunManager — utilisation du fallback Inspector.");
        }

        if (endTurnButton      != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (endButton          != null) endButton.onClick.AddListener(OnEndButtonClicked);
        if (victoireButton     != null) victoireButton.onClick.AddListener(OnVictoireCheat);
        if (lootContinueButton != null) lootContinueButton.onClick.AddListener(OnLootContinueClicked);

        if (endPanel           != null) endPanel.SetActive(false);
        if (lootPanel          != null) lootPanel.SetActive(false);
        if (lootContinueButton != null) lootContinueButton.gameObject.SetActive(false);
        if (arrowTransform != null)
        {
            arrowTransform.gameObject.SetActive(false);
            // La flèche ne doit jamais intercepter les clics — sinon elle bloque les boutons ennemis
            Image arrowImage = arrowTransform.GetComponent<Image>();
            if (arrowImage != null) arrowImage.raycastTarget = false;
        }

        // Mise en cache du Canvas racine pour la conversion pixels écran ↔ unités canvas.
        // Cherché depuis arrowTransform (enfant du Canvas) et non depuis this (qui peut être
        // sur un GO séparé hors hiérarchie Canvas — GetComponentInParent retournerait null).
        if (arrowTransform != null)
        {
            _rootCanvas = arrowTransform.GetComponentInParent<Canvas>();
            if (_rootCanvas != null) _rootCanvas = _rootCanvas.rootCanvas;
        }

        InitializeCombat();
    }

    void Update()
    {
        if (!isSelectingTarget) return;

        UpdateTargetingArrow();

        if (Input.GetMouseButtonDown(0))
        {
            // Détection manuelle sur le rect du sprite (et non sur le root élargi par le LayoutGroup).
            // Le Button.onClick utilisait le root qui s'étire pour remplir le conteneur,
            // causant un décalage d'un ennemi par rapport au sprite visible.
            foreach (EnemyInstance e in enemies)
            {
                if (!e.IsAlive || e.spriteImage == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(
                        e.spriteImage.rectTransform, Input.mousePosition, null))
                {
                    OnEnemyCibleClique(e);
                    return;
                }
            }
            // Clic en dehors de tout sprite ennemi → on laisse la flèche active
        }

        // Annulation avec clic droit ou Échap
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            CancelTargetSelection();
    }

    private void InitializeCombat()
    {
        combatEnded = false;
        combatStatModifiers.Clear();
        equipementsLootDifféré.Clear();
        ResolveEquipment();

        currentPlayerHP    = (RunManager.Instance != null && RunManager.Instance.currentHP > 0)
            ? RunManager.Instance.currentHP
            : effectiveMaxHP;

        // Construit la liste des EnemyInstance
        enemies.Clear();
        BuildEnemyList();

        // Applique les effets d'apparition de chaque ennemi (spawnEffects)
        // — exécutés depuis le point de vue de l'ennemi, avant le premier tour.
        foreach (EnemyInstance instance in enemies)
        {
            if (instance.data?.spawnEffects == null) continue;
            foreach (EffectData eff in instance.data.spawnEffects)
                if (eff != null) ApplyEnemyEffect(eff, $"{instance.GetName()} (apparition)", instance);
        }

        foreach (SkillData skill in availableSkills)
            if (skill != null) skillCooldowns[skill] = 0;

        SpawnSkillButtons();
        SpawnConsumableButtons();
        UpdatePlayerUI();
        Log($"Combat commencé — {GetPlayerName()} vs {GetRencontreName()}");
        InventoryUIManager.Instance?.SetDragDropEnabled(false);
        StartPlayerTurn();
    }

    // -----------------------------------------------
    // CONSTRUCTION DE LA LISTE D'ENNEMIS
    // -----------------------------------------------

    /// <summary>
    /// Construit la List<EnemyInstance> depuis currentGroup ou enemyData (fallback solo).
    /// Génère les blocs UI correspondants dans enemiesContainer.
    /// </summary>
    private void BuildEnemyList()
    {
        List<EnemyData> dataList = new List<EnemyData>();

        if (currentGroup != null && currentGroup.enemies != null)
        {
            foreach (EnemyData d in currentGroup.enemies)
                if (d != null) dataList.Add(d);
        }
        else if (enemyData != null)
        {
            dataList.Add(enemyData);
        }

        // Plafonne à 4
        if (dataList.Count > 4) dataList = dataList.GetRange(0, 4);

        foreach (EnemyData data in dataList)
        {
            EnemyInstance instance = new EnemyInstance(data);
            SpawnEnemyUI(instance);
            enemies.Add(instance);
        }

        Debug.Log($"[Combat] {enemies.Count} ennemi(s) initialisé(s).");
    }

    /// <summary>
    /// Génère le bloc UI d'un EnemyInstance dans enemiesContainer.
    /// Remplit les références UI dans l'instance.
    /// </summary>
    private void SpawnEnemyUI(EnemyInstance instance)
    {
        if (enemyUIPrefab == null || enemiesContainer == null) return;

        GameObject root = Instantiate(enemyUIPrefab, enemiesContainer);
        instance.uiRoot = root;

        // CanvasGroup — permet de passer blocksRaycasts à false hors ciblage
        // pour que les blocs ennemis ne bloquent pas les autres boutons (ex : Fin de tour)
        // ⚠️ Ne pas utiliser ?? avec GetComponent : Unity null ≠ C# null, ?? ne le détecte pas
        instance.canvasGroup = root.GetComponent<CanvasGroup>();
        if (instance.canvasGroup == null) instance.canvasGroup = root.AddComponent<CanvasGroup>();
        instance.canvasGroup.blocksRaycasts = false;

        // Sprite — cherché par nom exact "EnemySprite" pour éviter de récupérer
        // le fond blanc du Button sur le root (GetComponentInChildren inclut le root lui-même)
        Transform spriteChild = root.transform.Find("EnemySprite");
        if (spriteChild != null)
        {
            instance.spriteImage = spriteChild.GetComponent<Image>();
            if (instance.spriteImage != null)
            {
                // Respecte les proportions d'origine du sprite (pas d'étirement)
                instance.spriteImage.preserveAspect = true;
                if (instance.data.portrait != null)
                    instance.spriteImage.sprite = instance.data.portrait;
            }
            // Animator — trigger "Death" déclenché à la mort (fonctionnel dès que l'Animator est configuré)
            instance.animator = spriteChild.GetComponent<Animator>();
        }

        // Textes TMP — cherchés par nom dans les enfants
        foreach (TextMeshProUGUI tmp in root.GetComponentsInChildren<TextMeshProUGUI>())
        {
            switch (tmp.gameObject.name)
            {
                case "EnemyHPText":         instance.hpText         = tmp; break;
                case "EnemyArmorText":      instance.armorText      = tmp; break;
                case "EnemyNextActionText": instance.nextActionText = tmp; break;
            }
        }

        // Container d'icônes de statuts — cherché par nom exact "EnemyStatusContainer"
        Transform statusContainerChild = root.transform.Find("EnemyStatusContainer");
        if (statusContainerChild != null)
        {
            instance.statusIconContainer = statusContainerChild;
            // Configure le GridLayoutGroup dès l'instanciation pour refléter statusIconsPerRow
            GridLayoutGroup grid = statusContainerChild.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, statusIconsPerRow);
            }
        }

        // Bouton sur le root — gardé comme référence mais plus utilisé pour le ciblage.
        // La détection de clic se fait dans Update() via RectTransformContainsScreenPoint
        // sur spriteImage, ce qui correspond à ce que l'œil voit (pas au root élargi par le LayoutGroup).
        instance.targetButton = root.GetComponent<Button>();
        if (instance.targetButton != null)
            instance.targetButton.interactable = false;

        UpdateEnemyUI(instance);
    }

    // -----------------------------------------------
    // RÉSOLUTION DE L'ÉQUIPEMENT
    // -----------------------------------------------

    private void ResolveEquipment()
    {
        if (characterData == null)
        {
            effectiveMaxHP              = 100;
            effectiveAttack             = 10;
            effectiveDefense            = 0;
            effectiveMaxEnergy          = 3;
            effectiveCriticalChance     = 0f;
            effectiveCriticalMultiplier = 2f;
            effectiveRegeneration       = 0;
            effectiveLifeSteal          = 0f;
            return;
        }

        effectiveMaxHP              = characterData.maxHP;
        effectiveAttack             = characterData.attack;
        effectiveDefense            = characterData.defense;
        effectiveMaxEnergy          = characterData.maxEnergy;
        effectiveCriticalChance     = characterData.criticalChance;
        effectiveCriticalMultiplier = characterData.criticalMultiplier;
        effectiveRegeneration       = characterData.regeneration;
        effectiveLifeSteal          = characterData.lifeSteal;

        availableSkills.Clear();
        _availableSkillSources.Clear();

        List<EquipmentData> equipped = new List<EquipmentData>();

        if (RunManager.Instance != null)
        {
            // Run normale : tous les slots via l'enum — couvre Head, Torso, Legs, Arm1..Arm4.
            // Slot vide = null → ignoré. Pas de fallback SO (évite les skills fantômes mutés).
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                EquipmentData equip = RunManager.Instance.GetEquipped(slot);
                if (equip != null) equipped.Add(equip);
            }
        }
        else
        {
            // Test direct de scène sans RunManager : fallback sur les SO de départ.
            if (characterData.startingHead  != null) equipped.Add(characterData.startingHead);
            if (characterData.startingTorso != null) equipped.Add(characterData.startingTorso);
            if (characterData.startingLegs  != null) equipped.Add(characterData.startingLegs);
            if (characterData.startingArm1  != null) equipped.Add(characterData.startingArm1);
            if (characterData.startingArm2  != null) equipped.Add(characterData.startingArm2);
            if (characterData.maxEquippedArms >= 3 && characterData.startingArm3 != null)
                equipped.Add(characterData.startingArm3);
            if (characterData.maxEquippedArms >= 4 && characterData.startingArm4 != null)
                equipped.Add(characterData.startingArm4);
        }

        foreach (EquipmentData equip in equipped)
        {
            if (equip == null) continue;
            effectiveMaxHP              += equip.bonusHP;
            effectiveAttack             += equip.bonusAttack;
            effectiveDefense            += equip.bonusDefense;
            effectiveCriticalChance     += equip.bonusCriticalChance;
            effectiveCriticalMultiplier += equip.bonusCriticalMultiplier;
            effectiveRegeneration       += equip.bonusRegeneration;
            effectiveLifeSteal          += equip.bonusLifeSteal;
            foreach (SkillSlot slot in equip.skillSlots)
            {
                if (slot == null) continue;
                if (slot.state != SkillSlot.SlotState.Used &&
                    slot.state != SkillSlot.SlotState.LockedInUse) continue;
                if (slot.equippedSkill != null)
                {
                    availableSkills.Add(slot.equippedSkill);
                    _availableSkillSources.Add(equip);
                }
            }
        }

        if (RunManager.Instance != null)
        {
            if (characterData.startingModule != null
                && !RunManager.Instance.HasModule(characterData.startingModule))
                RunManager.Instance.AddModule(characterData.startingModule);

            if (characterData.startingConsumables != null
                && !RunManager.Instance.startingConsumablesSeeded)
            {
                foreach (ConsumableData c in characterData.startingConsumables)
                    if (c != null) RunManager.Instance.AddConsumable(c);
                RunManager.Instance.startingConsumablesSeeded = true;
            }
        }

        if (RunManager.Instance != null)
        {
            effectiveMaxHP              += Mathf.RoundToInt(RunManager.Instance.GetStatBonus(StatType.MaxHP));
            effectiveAttack             += Mathf.RoundToInt(RunManager.Instance.GetStatBonus(StatType.Attack));
            effectiveDefense            += Mathf.RoundToInt(RunManager.Instance.GetStatBonus(StatType.Defense));
            effectiveCriticalChance     += RunManager.Instance.GetStatBonus(StatType.CriticalChance);
            effectiveCriticalMultiplier += RunManager.Instance.GetStatBonus(StatType.CriticalMultiplier);
            effectiveLifeSteal          += RunManager.Instance.GetStatBonus(StatType.LifeSteal);
            effectiveMaxEnergy          += Mathf.RoundToInt(RunManager.Instance.GetStatBonus(StatType.MaxEnergy));
        }

        effectiveCriticalChance = Mathf.Clamp01(effectiveCriticalChance);

        Debug.Log($"[Combat] Équipement résolu — HP: {effectiveMaxHP}, ATK: {effectiveAttack}, " +
                  $"DEF: {effectiveDefense}, Énergie: {effectiveMaxEnergy}, " +
                  $"Crit: {effectiveCriticalChance:P0}, Skills: {availableSkills.Count}");
    }

    // -----------------------------------------------
    // GÉNÉRATION DES BOUTONS DE COMPÉTENCES
    // -----------------------------------------------

    private void SpawnSkillButtons()
    {
        if (skillButtonPrefab == null || skillButtonContainer == null) return;

        for (int i = 0; i < availableSkills.Count; i++)
        {
            SkillData     skill = availableSkills[i];
            EquipmentData equip = (i < _availableSkillSources.Count) ? _availableSkillSources[i] : null;

            if (skill == null || skill.isNavigationSkill) continue;

            int effectiveCost = skill.energyCost
                + ObtenirContexteExecution(skill, equip).coutEnergieSupplementaire;

            GameObject go = Instantiate(skillButtonPrefab, skillButtonContainer);
            SkillButton sb = go.GetComponent<SkillButton>();
            if (sb == null) continue;
            sb.Setup(skill, equip, effectiveCost, UseSkill);
            spawnedSkillButtons.Add(sb);
        }

        SpawnPassifsBras();
    }

    private void SpawnPassifsBras()
    {
        if (RunManager.Instance != null)
        {
            // Run normale : tous les slots arm via l'enum.
            // Slot vide = null → SpawnPassifsEquipement gère le null gracieusement.
            SpawnPassifsEquipement(RunManager.Instance.GetEquipped(EquipmentSlot.Arm1));
            SpawnPassifsEquipement(RunManager.Instance.GetEquipped(EquipmentSlot.Arm2));
            SpawnPassifsEquipement(RunManager.Instance.GetEquipped(EquipmentSlot.Arm3));
            SpawnPassifsEquipement(RunManager.Instance.GetEquipped(EquipmentSlot.Arm4));
        }
        else
        {
            // Test direct de scène sans RunManager.
            SpawnPassifsEquipement(characterData?.startingArm1);
            SpawnPassifsEquipement(characterData?.startingArm2);
            if (characterData != null && characterData.maxEquippedArms >= 3)
                SpawnPassifsEquipement(characterData.startingArm3);
            if (characterData != null && characterData.maxEquippedArms >= 4)
                SpawnPassifsEquipement(characterData.startingArm4);
        }
    }

    private void SpawnPassifsEquipement(EquipmentData equip)
    {
        if (equip == null || equip.passiveEffects == null) return;
        foreach (EffectData effet in equip.passiveEffects)
        {
            if (effet == null) continue;
            GameObject go = Instantiate(skillButtonPrefab, skillButtonContainer);
            SkillButton sb = go.GetComponent<SkillButton>();
            if (sb == null) continue;
            sb.SetupPassif(effet);
            Debug.Log($"[Combat] Bouton passif généré : {effet.displayName ?? effet.effectID} ({equip.equipmentName})");
        }
    }

    // -----------------------------------------------
    // GÉNÉRATION DES BOUTONS DE CONSOMMABLES
    // -----------------------------------------------

    private void SpawnConsumableButtons()
    {
        if (consumableButtonPrefab == null || consumableButtonContainer == null) return;

        foreach (ConsumableButton cb in spawnedConsumableButtons)
            if (cb != null) Destroy(cb.gameObject);
        spawnedConsumableButtons.Clear();

        List<ConsumableData> consumables = RunManager.Instance != null
            ? RunManager.Instance.GetConsumables()
            : new List<ConsumableData>();

        foreach (ConsumableData consumable in consumables)
        {
            if (consumable == null) continue;
            GameObject go = Instantiate(consumableButtonPrefab, consumableButtonContainer);
            ConsumableButton cb = go.GetComponent<ConsumableButton>();
            if (cb == null) continue;
            cb.Setup(consumable, UseConsumable);
            cb.SetInteractable(consumable.usableInCombat);
            spawnedConsumableButtons.Add(cb);
        }
    }

    // -----------------------------------------------
    // MACHINE À ÉTATS — TRANSITIONS
    // -----------------------------------------------

    private void StartPlayerTurn()
    {
        battleState    = BattleState.PlayerTurn;
        currentEnergy  = GetCurrentMaxEnergy();
        currentPlayerArmor = 0;

        if (isFirstTurn)
        {
            isFirstTurn = false;
            ModuleManager.Instance?.ApplyModulesWithTrigger(EffectTrigger.OnFightStart);
        }

        ApplyPlayerPerTurnEffects();
        DecayPlayerStatuses(StatusDecayTiming.OnTurnStart);

        if (effectiveRegeneration > 0)
        {
            int healed = Mathf.Min(effectiveRegeneration, GetPlayerMaxHP() - currentPlayerHP);
            if (healed > 0) { currentPlayerHP += healed; Log($"Régénération — +{healed} HP"); }
        }

        DecrementCooldowns();

        if (endTurnButton != null) endTurnButton.interactable = true;
        if (turnText      != null) turnText.text = "Tour du joueur";

        UpdateSkillButtons();
        foreach (ConsumableButton cb in spawnedConsumableButtons)
            cb.SetInteractable(cb.Consumable != null && cb.Consumable.usableInCombat);

        UpdatePlayerUI();
        GameEvents.TriggerPlayerTurnStarted();
    }

    private void StartEnemyTurn()
    {
        battleState = BattleState.EnemyTurn;

        if (endTurnButton != null) endTurnButton.interactable = false;
        if (turnText      != null) turnText.text = "Tour des ennemis";

        foreach (SkillButton   sb in spawnedSkillButtons)      sb.SetInteractable(false);
        foreach (ConsumableButton cb in spawnedConsumableButtons) cb.SetInteractable(false);

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(EnemyActionDelay);

        foreach (EnemyInstance ennemi in enemies)
        {
            if (!ennemi.IsAlive) continue;
            if (combatEnded) yield break;

            // Reset d'armure au début du tour de cet ennemi
            ennemi.currentArmor = 0;

            // Effets de statuts + décroissance pour cet ennemi
            ApplyEnemyPerTurnEffects(ennemi);
            DecayEnemyStatuses(ennemi, StatusDecayTiming.OnTurnStart);
            UpdateEnemyUI(ennemi);

            // L'ennemi a pu mourir à cause de ses propres statuts
            if (!ennemi.IsAlive)
            {
                CheckEnemyDeath(ennemi);
                if (combatEnded) yield break;
                continue;
            }

            // L'ennemi exécute son action
            SkillData nextSkill = ennemi.ai != null && ennemi.ai.HasActions
                ? ennemi.ai.GetAndAdvanceAction()
                : null;

            if (nextSkill != null && nextSkill.effects != null && nextSkill.effects.Count > 0)
            {
                foreach (EffectData eff in nextSkill.effects)
                    if (eff != null) ApplyEnemyEffect(eff, nextSkill.skillName, ennemi);
            }
            else
            {
                // Attaque de base — passe par GetEnemyAttack pour tenir compte des buffs/debuffs runtime
                int atk      = GetEnemyAttack(ennemi);
                int rawDmg   = Mathf.Max(1, atk - GetCurrentDefense());
                AppliquerDegatsAuJoueur(rawDmg, ennemi.GetName(), "Attaque de base");
            }

            UpdatePlayerUI();

            if (currentPlayerHP <= 0)
            {
                EndCombat(victory: false);
                yield break;
            }

            DecayEnemyStatuses(ennemi, StatusDecayTiming.OnTurnEnd);
            UpdateEnemyUI(ennemi);

            // Délai entre les actions de chaque ennemi
            yield return new WaitForSeconds(EnemyActionDelay);
        }

        if (!combatEnded)
            StartPlayerTurn();
    }

    // -----------------------------------------------
    // ACTIONS DU JOUEUR
    // -----------------------------------------------

    /// <summary>
    /// Retourne l'EquipmentData portant ce skill dans ses skillSlots (etat Used ou LockedInUse).
    /// Retourne null si introuvable ou RunManager indisponible.
    /// </summary>
    private EquipmentData ObtenirEquipementDuSkill(SkillData skill)
    {
        if (skill == null || RunManager.Instance == null) return null;

        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = RunManager.Instance.GetEquipped(slot);
            if (equip == null || equip.skillSlots == null) continue;

            foreach (SkillSlot skillSlot in equip.skillSlots)
            {
                if (skillSlot == null) continue;
                if (skillSlot.state != SkillSlot.SlotState.Used &&
                    skillSlot.state != SkillSlot.SlotState.LockedInUse) continue;
                if (skillSlot.equippedSkill == skill)
                    return equip;
            }
        }

        return null;
    }

    /// <summary>
    /// Construit le ContexteExecutionSkill depuis les skillModifiers de l'equipement source.
    /// Retourne Default si equip est null ou sans modificateurs.
    /// </summary>
    private ContexteExecutionSkill ObtenirContexteExecution(SkillData skill, EquipmentData equip)
    {
        ContexteExecutionSkill ctx = ContexteExecutionSkill.Default;
        ctx.sourceEquipment = equip;

        if (equip == null || equip.skillModifiers == null || equip.skillModifiers.Count == 0)
            return ctx;

        foreach (SkillModifier m in equip.skillModifiers)
        {
            if (m == null) continue;

            // Verifie la condition tag si elle existe
            if (m.conditionTag != null)
            {
                bool hasTag = false;
                if (skill != null && skill.tags != null)
                {
                    foreach (TagData tag in skill.tags)
                    {
                        if (tag != null && tag.tagName == m.conditionTag.tagName)
                        {
                            hasTag = true;
                            break;
                        }
                    }
                }
                if (!hasTag) continue;
            }

            // Applique selon le type
            switch (m.type)
            {
                case SkillModifierType.BaseDamageMultiplier:
                    ctx.multiplicateurDegatsBase += m.value;
                    break;
                case SkillModifierType.DamageMultiplier:
                    ctx.multiplicateurDegatsFinal += m.value;
                    break;
                case SkillModifierType.CritChanceBonus:
                    ctx.bonusCrit += m.value;
                    break;
                case SkillModifierType.ForceAoE:
                    ctx.overrideTarget = EffectTarget.AllEnemies;
                    break;
                case SkillModifierType.RepeatExecution:
                    ctx.repetitions += Mathf.RoundToInt(m.value);
                    break;
                case SkillModifierType.EnergyCostModifier:
                    ctx.coutEnergieSupplementaire += Mathf.RoundToInt(m.value);
                    break;
                case SkillModifierType.BonusStatusStacks:
                    ctx.bonusStacksStatut += Mathf.RoundToInt(m.value);
                    break;
            }
        }

        ctx.repetitions = Mathf.Max(1, ctx.repetitions);
        return ctx;
    }

    /// <summary>
    /// Execute tous les effets du skill avec le contexte donne.
    /// explicitTarget : ennemi selectionne manuellement, null pour resolution automatique.
    /// </summary>
    private void ExecuterEffetsSkill(SkillData skill, EnemyInstance explicitTarget,
                                      ContexteExecutionSkill ctx)
    {
        if (skill?.effects == null) return;
        foreach (EffectData eff in skill.effects)
            if (eff != null) ApplyEffect(eff, skill.skillName, explicitTarget, skill, ctx);
    }

    private void OnEndTurn()
    {
        if (battleState != BattleState.PlayerTurn) return;

        // Annule le ciblage si en cours
        if (isSelectingTarget) CancelTargetSelection();

        Log($"{GetPlayerName()} termine son tour.");
        DecayPlayerStatuses(StatusDecayTiming.OnTurnEnd);
        GameEvents.TriggerPlayerTurnEnded();
        StartEnemyTurn();
    }

    private void UseSkill(SkillData skill, EquipmentData equipSource)
    {
        if (battleState != BattleState.PlayerTurn) return;
        if (skill == null) return;

        if (skillCooldowns.TryGetValue(skill, out int cd) && cd > 0)
        {
            Log($"{skill.skillName} est en cooldown ({cd} tours restants).");
            return;
        }

        // Construit le contexte d'execution depuis l'equipement source transmis par le bouton
        ContexteExecutionSkill ctx   = ObtenirContexteExecution(skill, equipSource);
        int coutEffectif             = skill.energyCost + ctx.coutEnergieSupplementaire;

        if (currentEnergy < coutEffectif)
        {
            Log($"Pas assez d'énergie pour {skill.skillName} (coût : {coutEffectif}).");
            return;
        }

        // Si le skill nécessite un ciblage explicite et qu'il y a plusieurs ennemis vivants
        if (!ctx.overrideTarget.HasValue && RequiertCiblage(skill))
        {
            _pendingCtx          = ctx;
            _pendingCoutEffectif = coutEffectif;
            pendingSkill         = skill;
            currentEnergy       -= coutEffectif;
            if (skill.cooldown > 0) skillCooldowns[skill] = skill.cooldown;
            UpdateSkillButtons();
            UpdatePlayerUI();
            EnterTargetSelectionMode();
            return;
        }

        // Execution directe (AllEnemies, RandomEnemy, Self, ou seul ennemi vivant)
        currentEnergy -= coutEffectif;
        if (skill.cooldown > 0) skillCooldowns[skill] = skill.cooldown;

        ExecuterEffetsSkill(skill, null, ctx);
        for (int i = 1; i < ctx.repetitions && !combatEnded; i++)
            ExecuterEffetsSkill(skill, null, ctx);

        GameEvents.TriggerSkillUsed(skill);
        UpdatePlayerUI();
        UpdateSkillButtons();
    }

    private void UseConsumable(ConsumableData consumable)
    {
        if (battleState != BattleState.PlayerTurn) return;
        if (consumable == null || !consumable.usableInCombat) return;

        if (consumable.effects != null && consumable.effects.Count > 0)
            foreach (EffectData eff in consumable.effects)
                if (eff != null) ApplyConsumableEffect(eff, consumable.consumableName);
        else
            Log($"{GetPlayerName()} utilise {consumable.consumableName} — (aucun effet défini)");

        RunManager.Instance?.RemoveConsumable(consumable);
        UpdatePlayerUI();
        SpawnConsumableButtons();
    }

    // -----------------------------------------------
    // CIBLAGE (style Slay the Spire)
    // -----------------------------------------------

    /// <summary>
    /// Retourne true si le skill contient un effet SingleEnemy ET qu'il y a plus d'un ennemi vivant.
    /// Si un seul ennemi est vivant, le ciblage est automatique (pas de sélection requise).
    /// </summary>
    private bool RequiertCiblage(SkillData skill)
    {
        if (skill?.effects == null) return false;
        if (GetAliveEnemies().Count <= 1) return false;

        foreach (EffectData eff in skill.effects)
            if (eff != null && eff.target == EffectTarget.SingleEnemy) return true;
        return false;
    }

    /// <summary>
    /// Active le mode sélection de cible : affiche la flèche, rend les boutons ennemis cliquables.
    /// </summary>
    private void EnterTargetSelectionMode()
    {
        isSelectingTarget = true;

        if (arrowTransform != null) arrowTransform.gameObject.SetActive(true);

        // Autorise les raycasts sur les ennemis vivants (bloque les clics vers l'arrière-plan)
        foreach (EnemyInstance e in enemies)
            if (e.canvasGroup != null) e.canvasGroup.blocksRaycasts = e.IsAlive;

        if (turnText != null) turnText.text = "Choisir une cible...";
        Log($"{GetPlayerName()} vise avec {pendingSkill?.skillName} — cliquez sur un ennemi.");
    }

    /// <summary>
    /// Annule la sélection de cible en cours et rembourse énergie + cooldown.
    /// </summary>
    private void CancelTargetSelection()
    {
        if (!isSelectingTarget) return;
        isSelectingTarget = false;

        if (arrowTransform != null) arrowTransform.gameObject.SetActive(false);

        // Rembourse l'énergie et remet le cooldown à 0
        if (pendingSkill != null)
        {
            currentEnergy += _pendingCoutEffectif;
            skillCooldowns[pendingSkill] = 0;
            pendingSkill = null;
        }

        // Désactive les boutons de ciblage ennemis + bloque plus les raycasts
        foreach (EnemyInstance e in enemies)
        {
            if (e.targetButton != null) e.targetButton.interactable = false;
            if (e.canvasGroup  != null) e.canvasGroup.blocksRaycasts = false;
        }

        if (turnText != null) turnText.text = "Tour du joueur";
        UpdatePlayerUI();
        UpdateSkillButtons();
    }

    /// <summary>
    /// Appelé quand le joueur clique sur le bloc UI d'un ennemi en mode sélection.
    /// </summary>
    private void OnEnemyCibleClique(EnemyInstance cible)
    {
        if (!isSelectingTarget || cible == null || !cible.IsAlive) return;

        isSelectingTarget = false;
        if (arrowTransform != null) arrowTransform.gameObject.SetActive(false);

        // Désactive les boutons de ciblage ennemis
        foreach (EnemyInstance e in enemies)
        {
            if (e.targetButton != null) e.targetButton.interactable = false;
            if (e.canvasGroup  != null) e.canvasGroup.blocksRaycasts = false;
        }

        if (turnText != null) turnText.text = "Tour du joueur";

        ContexteExecutionSkill ctx = _pendingCtx;
        SkillData skill = pendingSkill;
        pendingSkill = null;

        ExecuterEffetsSkill(skill, cible, ctx);
        for (int i = 1; i < ctx.repetitions && !combatEnded; i++)
        {
            if (skill.targetType == SkillTargetType.SingleEnemy && !cible.IsAlive) break;
            // SingleEnemy : meme cible. Random/AoE/Self : null (resolution auto dans ApplyEffect).
            EnemyInstance cibleRepeat = (skill.targetType == SkillTargetType.SingleEnemy) ? cible : null;
            ExecuterEffetsSkill(skill, cibleRepeat, ctx);
        }

        if (skill != null) GameEvents.TriggerSkillUsed(skill);
        UpdatePlayerUI();
        UpdateSkillButtons();
    }

    /// <summary>
    /// Met à jour la flèche de ciblage à chaque frame : pointe vers la souris depuis le sprite joueur.
    /// </summary>
    private void UpdateTargetingArrow()
    {
        if (arrowTransform == null || playerSpriteTransform == null) return;

        Vector2 origin    = playerSpriteTransform.position;  // pixels écran
        Vector2 mousePos  = Input.mousePosition;              // pixels écran
        Vector2 direction = mousePos - origin;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        arrowTransform.position = origin;
        arrowTransform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);

        // direction.magnitude est en pixels écran, mais sizeDelta est en unités canvas.
        // Le scaleFactor convertit : unités canvas × scaleFactor = pixels écran.
        // → on divise pour que la pointe de la flèche arrive exactement sur la souris.
        float scaleFactor = (_rootCanvas != null) ? _rootCanvas.scaleFactor : 1f;
        arrowTransform.sizeDelta = new Vector2(direction.magnitude / scaleFactor, arrowTransform.sizeDelta.y);
    }

    // -----------------------------------------------
    // RÉSOLUTION DES EFFETS JOUEUR → ENNEMI(S)
    // -----------------------------------------------

    /// <summary>
    /// Applique un EffectData du joueur (surcharge courte).
    /// Appelée depuis les modules, consommables, effets ennemis.
    /// Injecte automatiquement ContexteExecutionSkill.Default.
    /// </summary>
    private void ApplyEffect(EffectData effect, string sourceName, EnemyInstance explicitTarget, SkillData sourceSkill = null)
        => ApplyEffect(effect, sourceName, explicitTarget, sourceSkill, ContexteExecutionSkill.Default);

    /// <summary>
    /// Applique un EffectData du joueur (surcharge complète).
    /// explicitTarget : ennemi sélectionné explicitement (null = résolution automatique).
    /// sourceSkill    : skill à l'origine de l'appel — utilisé pour résoudre les bonus
    ///                  stacks des modules (SkillUtilise). null = pas de bonus stacks.
    /// ctx            : contexte d'execution du skill (modificateurs, repetitions, etc).
    /// </summary>
    private void ApplyEffect(EffectData effect, string sourceName, EnemyInstance explicitTarget, SkillData sourceSkill, ContexteExecutionSkill ctx)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                EffectTarget targetEffectif = ctx.overrideTarget ?? effect.target;

                float critChance = Mathf.Clamp01(GetCurrentCritChance() + ctx.bonusCrit);
                bool  isCrit     = critChance > 0f && Random.value < critChance;

                foreach (EnemyInstance cible in GetEffectTargets(targetEffectif, explicitTarget))
                {
                    float multiplicateur = 1f;
                    int   bonusFlat      = 0;

                    // Bonus conditionnel de tag sur l'ennemi ciblé
                    if (effect.conditionCible == ConditionCible.EnnemiCible && effect.conditionTag != null)
                    {
                        bool aLeTag = cible.data != null
                                   && cible.data.tags != null
                                   && cible.data.tags.Contains(effect.conditionTag);

                        if (aLeTag && effect.bonusConditionnel != 0f)
                        {
                            if (effect.typeBonusConditionnel == TypeBonusConditionnel.Pourcentage)
                                multiplicateur = 1f + effect.bonusConditionnel;
                            else
                                bonusFlat = Mathf.RoundToInt(effect.bonusConditionnel);
                        }
                    }

                    AppliquerDegatsEnnemi(cible, effect, sourceName, isCrit, multiplicateur, bonusFlat, ctx);
                    CheckEnemyDeath(cible);
                    if (combatEnded) return;
                }
                break;
            }

            case EffectAction.Heal:
            {
                var (healFlat, healPct) = GetPlayerStatModifiers(StatType.HealGainMultiplier);
                int healed = Mathf.RoundToInt(effect.value * (1f + healPct) + healFlat);
                healed = Mathf.Min(healed, GetPlayerMaxHP() - currentPlayerHP);
                if (healed > 0) { currentPlayerHP += healed; }
                Log($"{GetPlayerName()} utilise {sourceName} — Soin de {healed} HP → {currentPlayerHP}/{GetPlayerMaxHP()}");
                break;
            }

            case EffectAction.AddArmor:
            {
                var (armorFlat, armorPct) = GetPlayerStatModifiers(StatType.ArmorGainMultiplier);
                int armor = Mathf.Max(0, Mathf.RoundToInt(effect.value * (1f + armorPct) + armorFlat));
                currentPlayerArmor += armor;
                Log($"{GetPlayerName()} utilise {sourceName} — +{armor} armure → {currentPlayerArmor} armure totale");
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null) break;
                int stacks = Mathf.RoundToInt(effect.value);
                int bonusStacks = ObtenirBonusStacksModules(effect.statusToApply, sourceSkill)
                                + ctx.bonusStacksStatut;
                if (bonusStacks > 0)
                    Log($"Bonus stacks : +{bonusStacks} {effect.statusToApply.statusName}");
                stacks += bonusStacks;
                if (effect.target == EffectTarget.Self)
                {
                    ApplyStatusAuJoueur(effect.statusToApply, stacks);
                }
                else
                {
                    foreach (EnemyInstance cible in GetEffectTargets(ctx.overrideTarget ?? effect.target, explicitTarget))
                        ApplyStatusAEnnemi(cible, effect.statusToApply, stacks);
                }
                break;
            }

            case EffectAction.ModifyStat:
            {
                float val = effect.value;
                if (effect.scalingSource == EffectScalingSource.EquipementEquipe && effect.comptageTag != null)
                    val = effect.value * CompterEquipementsAvecTag(effect.comptageTag);
                else if (effect.scalingSource == EffectScalingSource.SkillEquipeSurCetObjet && effect.comptageTag != null)
                    val = effect.value * CompterSkillsAvecTagSurEquipement(ctx.sourceEquipment, effect.comptageTag);
                if (!combatStatModifiers.ContainsKey(effect.statToModify))
                    combatStatModifiers[effect.statToModify] = 0f;
                combatStatModifiers[effect.statToModify] += val;
                Log($"{GetPlayerName()} utilise {sourceName} — {effect.statToModify} " +
                    $"{(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                break;
            }

            case EffectAction.GainEnergy:
            {
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(Mathf.Max(0, Mathf.RoundToInt(effect.value)), maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + Mathf.RoundToInt(effect.value), maxEnerg);
                Log($"{GetPlayerName()} utilise {sourceName} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            case EffectAction.AddCredits:
            {
                int montant = Mathf.RoundToInt(effect.value);
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddCredits(montant);
                    Log($"{GetPlayerName()} utilise {sourceName} — {(montant >= 0 ? "+" : "")}{montant} credits");
                }
                break;
            }

            case EffectAction.DonnerConsommable:
            {
                if (effect.consommableLootTable == null)
                {
                    Log($"[CombatManager] DonnerConsommable — consommableLootTable non assignée sur '{sourceName}'.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                ConsumableData consommable = effect.consommableLootTable.GetRandomAvecTag(filtre);
                if (consommable == null) break;
                if (RunManager.Instance != null)
                {
                    bool ajouté = RunManager.Instance.AddConsumable(consommable);
                    if (ajouté)
                    {
                        Log($"{GetPlayerName()} obtient un consommable via {sourceName} : {consommable.consumableName}");
                        SpawnConsumableButtons();
                    }
                    else
                        Log($"[CombatManager] DonnerConsommable — inventaire plein, '{consommable.consumableName}' ignoré.");
                }
                break;
            }

            case EffectAction.DonnerModule:
            {
                if (effect.moduleLootTable == null)
                {
                    Log($"[CombatManager] DonnerModule — moduleLootTable non assignée sur '{sourceName}'.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                ModuleData module = effect.moduleLootTable.GetRandomAvecTag(filtre);
                if (module == null) break;
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddModule(module);
                    Log($"{GetPlayerName()} obtient un module via {sourceName} : {module.moduleName}");
                }
                break;
            }

            case EffectAction.DonnerEquipement:
            {
                if (effect.equipementLootTable == null)
                {
                    Log($"[CombatManager] DonnerEquipement — equipementLootTable non assignée sur '{sourceName}'.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                EquipmentData equipement = effect.equipementLootTable.GetRandomAvecTag(filtre);
                if (equipement == null) break;
                equipementsLootDifféré.Add(equipement);
                Log($"{GetPlayerName()} va recevoir un équipement via {sourceName} : {equipement.equipmentName} (proposé après le combat)");
                break;
            }

            default:
                Log($"{GetPlayerName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté.");
                break;
        }
    }

    /// <summary>
    /// Cumule les bonus stacks provenant des modules et passifs d'équipement
    /// configurés avec action == ApplyStatus, scalingSource == SkillUtilise,
    /// statusToApply == status, et dont le tag correspond au skill source.
    /// Retourne 0 si sourceSkill == null, RunManager indisponible, ou aucun bonus.
    /// Comparaison du tag par tagName (jamais par référence objet).
    /// </summary>
    private int ObtenirBonusStacksModules(StatusData status, SkillData sourceSkill)
    {
        if (sourceSkill == null || RunManager.Instance == null) return 0;

        int total = 0;

        // --- Modules ---
        foreach (ModuleData module in RunManager.Instance.GetModules())
        {
            if (module == null || module.effects == null) continue;
            foreach (EffectData effet in module.effects)
            {
                if (effet == null) continue;
                if (effet.action         != EffectAction.ApplyStatus)              continue;
                if (effet.scalingSource  != EffectScalingSource.SkillUtilise)      continue;
                if (effet.statusToApply  != status)                                continue;
                if (effet.comptageTag    == null)                                  continue;
                if (sourceSkill.tags     == null)                                  continue;
                if (!sourceSkill.tags.Any(t => t != null && t.tagName == effet.comptageTag.tagName)) continue;
                total += Mathf.RoundToInt(effet.value);
            }
        }

        // --- Passifs d'équipement ---
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = RunManager.Instance.GetEquipped(slot);
            if (equip == null || equip.passiveEffects == null) continue;
            foreach (EffectData effet in equip.passiveEffects)
            {
                if (effet == null) continue;
                if (effet.action         != EffectAction.ApplyStatus)              continue;
                if (effet.scalingSource  != EffectScalingSource.SkillUtilise)      continue;
                if (effet.statusToApply  != status)                                continue;
                if (effet.comptageTag    == null)                                  continue;
                if (sourceSkill.tags     == null)                                  continue;
                if (!sourceSkill.tags.Any(t => t != null && t.tagName == effet.comptageTag.tagName)) continue;
                total += Mathf.RoundToInt(effet.value);
            }
        }

        return total;
    }

    /// <summary>
    /// Compte les équipements portés qui possèdent le tag indiqué (comparaison par tagName).
    /// Retourne 0 si tag == null ou RunManager non disponible.
    /// </summary>
    private int CompterEquipementsAvecTag(TagData tag)
    {
        if (tag == null || RunManager.Instance == null) return 0;
        int total = 0;
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = RunManager.Instance.GetEquipped(slot);
            if (equip != null && equip.tags != null
                && equip.tags.Any(t => t != null && t.tagName == tag.tagName))
                total++;
        }
        return total;
    }

    private int CompterSkillsAvecTagSurEquipement(EquipmentData equip, TagData tag)
    {
        if (equip == null || tag == null || equip.skillSlots == null) return 0;
        return equip.skillSlots.Count(s =>
            s != null && s.state != SkillSlot.SlotState.Available &&
            s.equippedSkill != null && s.equippedSkill.tags != null &&
            s.equippedSkill.tags.Any(t => t != null && t.tagName == tag.tagName));
    }

    /// <summary>
    /// Résout le tag de filtre à utiliser pour la distribution d'un item.
    /// Si <c>filtreParTagHero</c> est activé et que le héros a au moins un tag, retourne <c>tags[0]</c>.
    /// Sinon retourne <c>effect.filtreTag</c> (peut être null — pas de filtre).
    /// </summary>
    private TagData ObtenirFiltreTag(EffectData effect)
    {
        if (effect.filtreParTagHero)
        {
            CharacterData héros = RunManager.Instance?.selectedCharacter;
            if (héros != null && héros.tags != null && héros.tags.Count > 0)
                return héros.tags[0];
        }
        return effect.filtreTag;
    }

    /// <summary>
    /// Applique des dégâts du joueur sur un ennemi spécifique (helpers pour ApplyEffect).
    /// </summary>
    private void AppliquerDegatsEnnemi(EnemyInstance ennemi, EffectData effect, string sourceName, bool isCrit, float multiplicateur = 1f, int bonusFlat = 0, ContexteExecutionSkill? ctx = null)
    {
        ContexteExecutionSkill contextEffectif = ctx ?? ContexteExecutionSkill.Default;

        int enemyDef  = ennemi.data != null ? ennemi.data.defense : 0;
        float baseValue = effect.value * (1f + contextEffectif.multiplicateurDegatsBase);
        int rawDamage = Mathf.Max(1, CalculerDegatsJoueur(baseValue) - enemyDef);

        // Mise à l'échelle par stacks (scalingStatus sur l'ennemi)
        string stackInfo = "";
        if (effect.scalingStatus != null)
        {
            int stacks = GetEnemyStatusStacks(ennemi, effect.scalingStatus);
            if (stacks > 0)
            {
                int bonus = Mathf.RoundToInt(effect.secondaryValue * stacks);
                rawDamage += bonus;
                string consumeText = effect.consumeStacks ? " [consommés]" : "";
                stackInfo = $", dont +{bonus} {effect.scalingStatus.statusName}×{stacks}{consumeText}";
                if (effect.consumeStacks) ennemi.statuses.Remove(effect.scalingStatus);
            }
        }

        // Bonus conditionnel de tag (pourcentage puis flat, dans cet ordre)
        if (multiplicateur != 1f)
            rawDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * multiplicateur));
        if (bonusFlat != 0)
            rawDamage = Mathf.Max(1, rawDamage + bonusFlat);

        if (isCrit) rawDamage = Mathf.RoundToInt(rawDamage * GetCurrentCritMultiplier());
        var (dmgFlat, dmgPct) = GetPlayerStatModifiers(StatType.DamageGainMultiplier);
        if (dmgPct != 0f || dmgFlat != 0f)
            rawDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f + dmgPct) + dmgFlat));
        if (contextEffectif.multiplicateurDegatsFinal != 0f)
            rawDamage = Mathf.Max(1, Mathf.RoundToInt(rawDamage * (1f + contextEffectif.multiplicateurDegatsFinal)));
        rawDamage = Mathf.Clamp(rawDamage, 0, 9999);

        int armorAbsorbed  = Mathf.Min(ennemi.currentArmor, rawDamage);
        int hpDamage       = rawDamage - armorAbsorbed;
        ennemi.currentArmor = Mathf.Max(0, ennemi.currentArmor - armorAbsorbed);
        ennemi.currentHP    = Mathf.Max(0, ennemi.currentHP    - hpDamage);

        string critInfo   = isCrit ? " [CRITIQUE !]" : "";
        string armorInfo  = armorAbsorbed > 0 ? $", dont {armorAbsorbed} absorbés par l'armure" : "";
        string detailInfo = (stackInfo.Length > 0 || armorInfo.Length > 0)
            ? $" ({stackInfo.TrimStart(',', ' ')}{armorInfo})" : "";
        Log($"{GetPlayerName()} utilise {sourceName} sur {ennemi.GetName()}{critInfo} — {rawDamage} dégâts{detailInfo} → {ennemi.currentHP}/{ennemi.MaxHP} HP");

        if (hpDamage > 0)
            GameEvents.TriggerPlayerDealtDamage(hpDamage);

        // Vol de vie
        float lifeSteal = GetCurrentLifeSteal();
        if (lifeSteal > 0f && hpDamage > 0)
        {
            int stolen = Mathf.Max(1, Mathf.RoundToInt(hpDamage * lifeSteal));
            stolen = Mathf.Min(stolen, GetPlayerMaxHP() - currentPlayerHP);
            if (stolen > 0)
            {
                currentPlayerHP += stolen;
                Log($"Vol de vie +{stolen} HP → {currentPlayerHP}/{GetPlayerMaxHP()}");
            }
        }

        UpdateEnemyUI(ennemi);
    }

    // -----------------------------------------------
    // RÉSOLUTION DES EFFETS ENNEMI → JOUEUR
    // -----------------------------------------------

    /// <summary>
    /// Applique un EffectData depuis le point de vue d'un ennemi.
    /// </summary>
    private void ApplyEnemyEffect(EffectData effect, string sourceName, EnemyInstance attacker)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // Passe par GetEnemyAttack pour tenir compte des buffs/debuffs runtime
                int atk      = GetEnemyAttack(attacker);
                int rawDmg   = Mathf.Max(1, Mathf.RoundToInt(effect.value) + atk - GetCurrentDefense());

                // Mise à l'échelle par stacks (scalingStatus sur le joueur)
                string stackInfo = "";
                if (effect.scalingStatus != null)
                {
                    int stacks = GetPlayerStatusStacks(effect.scalingStatus);
                    if (stacks > 0)
                    {
                        int bonus = Mathf.RoundToInt(effect.secondaryValue * stacks);
                        rawDmg += bonus;
                        string consumeText = effect.consumeStacks ? " [consommés]" : "";
                        stackInfo = $", dont +{bonus} {effect.scalingStatus.statusName}×{stacks}{consumeText}";
                        if (effect.consumeStacks) playerStatuses.Remove(effect.scalingStatus);
                    }
                }

                rawDmg = Mathf.Clamp(rawDmg, 0, 9999);
                AppliquerDegatsAuJoueur(rawDmg, attacker.GetName(), sourceName, stackInfo);
                break;
            }

            case EffectAction.Heal:
            {
                int healed = Mathf.Min(Mathf.RoundToInt(effect.value), attacker.MaxHP - attacker.currentHP);
                if (healed > 0)
                {
                    attacker.currentHP += healed;
                    Log($"{attacker.GetName()} utilise {sourceName} — Soin de {healed} HP → {attacker.currentHP}/{attacker.MaxHP}");
                    UpdateEnemyUI(attacker);
                }
                break;
            }

            case EffectAction.AddArmor:
            {
                int armor = Mathf.RoundToInt(effect.value);
                attacker.currentArmor += armor;
                Log($"{attacker.GetName()} utilise {sourceName} — +{armor} armure");
                UpdateEnemyUI(attacker);
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null) break;
                int stacks = Mathf.RoundToInt(effect.value);
                // Self (depuis l'ennemi) = l'ennemi lui-même, SingleEnemy = le joueur
                if (effect.target == EffectTarget.Self)
                    ApplyStatusAEnnemi(attacker, effect.statusToApply, stacks);
                else
                    ApplyStatusAuJoueur(effect.statusToApply, stacks);
                break;
            }

            case EffectAction.ModifyStat:
            {
                // Self = modifie les stats de l'ennemi lui-même (ex : +attaque au spawn)
                // Toute autre cible = modifie les stats du joueur (ex : debuff)
                float val = effect.value;
                if (effect.target == EffectTarget.Self)
                {
                    if (!attacker.combatStatBonuses.ContainsKey(effect.statToModify))
                        attacker.combatStatBonuses[effect.statToModify] = 0f;
                    attacker.combatStatBonuses[effect.statToModify] += val;
                    Log($"{attacker.GetName()} utilise {sourceName} — {effect.statToModify} {(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                }
                else
                {
                    if (!combatStatModifiers.ContainsKey(effect.statToModify))
                        combatStatModifiers[effect.statToModify] = 0f;
                    combatStatModifiers[effect.statToModify] += val;
                    Log($"{attacker.GetName()} utilise {sourceName} — {GetPlayerName()} : {effect.statToModify} {(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                }
                break;
            }

            default:
                Log($"{attacker.GetName()} utilise {sourceName} — Effet '{effect.action}' non implémenté pour les ennemis.");
                break;
        }
    }

    /// <summary>
    /// Applique des dégâts bruts au joueur (armure + HP), avec logs.
    /// </summary>
    private void AppliquerDegatsAuJoueur(int rawDmg, string sourceName, string skillName, string stackInfo = "")
    {
        int armorAbsorbed  = Mathf.Min(currentPlayerArmor, rawDmg);
        int hpDamage       = rawDmg - armorAbsorbed;
        currentPlayerArmor = Mathf.Max(0, currentPlayerArmor - armorAbsorbed);
        currentPlayerHP    = Mathf.Max(0, currentPlayerHP    - hpDamage);

        string armorInfo  = armorAbsorbed > 0 ? $", dont {armorAbsorbed} absorbés par l'armure" : "";
        string detailInfo = (stackInfo.Length > 0 || armorInfo.Length > 0)
            ? $" ({stackInfo.TrimStart(',', ' ')}{armorInfo})" : "";
        Log($"{sourceName} utilise {skillName} — {rawDmg} dégâts{detailInfo} → {currentPlayerHP}/{GetPlayerMaxHP()} HP");

        if (hpDamage > 0)
            GameEvents.TriggerPlayerDamaged(hpDamage);
    }

    // -----------------------------------------------
    // EFFETS DE CONSOMMABLES
    // -----------------------------------------------

    /// <summary>
    /// Applique l'effet d'un consommable — valeurs brutes, sans stats combat.
    /// DealDamage cible le premier ennemi vivant (consommables offensifs).
    /// </summary>
    private void ApplyConsumableEffect(EffectData effect, string sourceName)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // Les consommables DealDamage sont bruts — pas de stats, pas de crit
                int dmg = Mathf.Clamp(Mathf.RoundToInt(effect.value), 0, 9999);
                List<EnemyInstance> cibles = GetEffectTargets(effect.target, GetDefaultTarget());
                foreach (EnemyInstance cible in cibles)
                {
                    int armorAbsorbed  = Mathf.Min(cible.currentArmor, dmg);
                    int hpDamage       = dmg - armorAbsorbed;
                    cible.currentArmor = Mathf.Max(0, cible.currentArmor - armorAbsorbed);
                    cible.currentHP    = Mathf.Max(0, cible.currentHP    - hpDamage);
                    string armorInfo   = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                    Log($"{GetPlayerName()} utilise {sourceName} sur {cible.GetName()} — {dmg} dégâts{armorInfo}");
                    UpdateEnemyUI(cible);
                    CheckEnemyDeath(cible);
                    if (combatEnded) return;
                }
                break;
            }

            case EffectAction.Heal:
            {
                int healed = Mathf.Min(Mathf.RoundToInt(effect.value), GetPlayerMaxHP() - currentPlayerHP);
                currentPlayerHP += healed;
                Log($"{GetPlayerName()} utilise {sourceName} — Soin de {healed} HP → {currentPlayerHP}/{GetPlayerMaxHP()}");
                break;
            }

            case EffectAction.AddArmor:
            {
                int armor = Mathf.RoundToInt(effect.value);
                currentPlayerArmor += armor;
                Log($"{GetPlayerName()} utilise {sourceName} — +{armor} armure");
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null) break;
                int stacks = Mathf.RoundToInt(effect.value);
                if (effect.target == EffectTarget.Self)
                    ApplyStatusAuJoueur(effect.statusToApply, stacks);
                else
                {
                    foreach (EnemyInstance cible in GetEffectTargets(effect.target, GetDefaultTarget()))
                        ApplyStatusAEnnemi(cible, effect.statusToApply, stacks);
                }
                break;
            }

            case EffectAction.ModifyStat:
            {
                float val = effect.value;
                if (effect.scalingSource == EffectScalingSource.EquipementEquipe && effect.comptageTag != null)
                    val = effect.value * CompterEquipementsAvecTag(effect.comptageTag);
                if (!combatStatModifiers.ContainsKey(effect.statToModify))
                    combatStatModifiers[effect.statToModify] = 0f;
                combatStatModifiers[effect.statToModify] += val;
                Log($"{GetPlayerName()} utilise {sourceName} — {effect.statToModify} {(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                break;
            }

            case EffectAction.GainEnergy:
            {
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(Mathf.Max(0, Mathf.RoundToInt(effect.value)), maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + Mathf.RoundToInt(effect.value), maxEnerg);
                Log($"{GetPlayerName()} utilise {sourceName} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            case EffectAction.AddCredits:
            {
                int montant = Mathf.RoundToInt(effect.value);
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddCredits(montant);
                    Log($"{GetPlayerName()} utilise {sourceName} — {(montant >= 0 ? "+" : "")}{montant} credits");
                }
                break;
            }

            default:
                Log($"{GetPlayerName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté (consommable).");
                break;
        }
    }

    // -----------------------------------------------
    // EFFETS DE MODULES
    // -----------------------------------------------

    /// <summary>
    /// Applique l'effet d'un module passif ou déclenché.
    /// DealDamage cible l'ennemi par défaut (premier vivant).
    /// </summary>
    public void ApplyModuleEffect(EffectData effect, string moduleName, EquipmentData sourceEquipment = null)
    {
        if (effect == null) return;

        string source    = $"[Module] {moduleName}";
        bool targetsSelf = effect.target == EffectTarget.Self;

        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                int dmg = Mathf.Clamp(Mathf.RoundToInt(effect.value), 0, 9999);
                List<EnemyInstance> cibles = GetEffectTargets(effect.target, GetDefaultTarget());
                foreach (EnemyInstance cible in cibles)
                {
                    int armorAbsorbed  = Mathf.Min(cible.currentArmor, dmg);
                    int hpDamage       = dmg - armorAbsorbed;
                    cible.currentArmor = Mathf.Max(0, cible.currentArmor - armorAbsorbed);
                    cible.currentHP    = Mathf.Max(0, cible.currentHP    - hpDamage);
                    string armorInfo   = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                    Log($"{source} — {dmg} dégâts{armorInfo} sur {cible.GetName()} → {cible.currentHP}/{cible.MaxHP} HP");
                    UpdateEnemyUI(cible);
                    CheckEnemyDeath(cible);
                    if (combatEnded) return;
                }
                break;
            }

            case EffectAction.Heal:
            {
                if (targetsSelf)
                {
                    var (healFlat, healPct) = GetPlayerStatModifiers(StatType.HealGainMultiplier);
                    int healed = Mathf.RoundToInt(effect.value * (1f + healPct) + healFlat);
                    healed = Mathf.Min(healed, GetPlayerMaxHP() - currentPlayerHP);
                    if (healed > 0) { currentPlayerHP += healed; Log($"{source} — +{healed} HP joueur"); }
                }
                else
                {
                    EnemyInstance cible = GetDefaultTarget();
                    if (cible != null)
                    {
                        int healed = Mathf.Min(Mathf.RoundToInt(effect.value), cible.MaxHP - cible.currentHP);
                        if (healed > 0) { cible.currentHP += healed; Log($"{source} — +{healed} HP {cible.GetName()}"); UpdateEnemyUI(cible); }
                    }
                }
                break;
            }

            case EffectAction.AddArmor:
            {
                if (targetsSelf)
                {
                    var (armorFlat, armorPct) = GetPlayerStatModifiers(StatType.ArmorGainMultiplier);
                    int armor = Mathf.Max(0, Mathf.RoundToInt(effect.value * (1f + armorPct) + armorFlat));
                    currentPlayerArmor += armor;
                    Log($"{source} — +{armor} armure joueur");
                }
                else
                {
                    int armor = Mathf.RoundToInt(effect.value);
                    EnemyInstance cible = GetDefaultTarget();
                    if (cible != null) { cible.currentArmor += armor; Log($"{source} — +{armor} armure {cible.GetName()}"); UpdateEnemyUI(cible); }
                }
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null) break;
                int stacks = Mathf.RoundToInt(effect.value);
                if (targetsSelf)
                    ApplyStatusAuJoueur(effect.statusToApply, stacks);
                else
                {
                    foreach (EnemyInstance cible in GetEffectTargets(effect.target, GetDefaultTarget()))
                        ApplyStatusAEnnemi(cible, effect.statusToApply, stacks);
                }
                break;
            }

            case EffectAction.GainEnergy:
            {
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(Mathf.Max(0, Mathf.RoundToInt(effect.value)), maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + Mathf.RoundToInt(effect.value), maxEnerg);
                Log($"{source} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            case EffectAction.ModifyStat:
            {
                float val = effect.value;
                if (effect.scalingSource == EffectScalingSource.EquipementEquipe && effect.comptageTag != null)
                    val = effect.value * CompterEquipementsAvecTag(effect.comptageTag);
                else if (effect.scalingSource == EffectScalingSource.SkillEquipeSurCetObjet && effect.comptageTag != null)
                    val = effect.value * CompterSkillsAvecTagSurEquipement(sourceEquipment, effect.comptageTag);

                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddStatBonus(effect.statToModify, val);
                    switch (effect.statToModify)
                    {
                        case StatType.Attack:             effectiveAttack             += Mathf.RoundToInt(val); break;
                        case StatType.Defense:            effectiveDefense            += Mathf.RoundToInt(val); break;
                        case StatType.MaxHP:              effectiveMaxHP              += Mathf.RoundToInt(val); break;
                        case StatType.CriticalChance:     effectiveCriticalChance     = Mathf.Clamp01(effectiveCriticalChance + val); break;
                        case StatType.CriticalMultiplier: effectiveCriticalMultiplier += val; break;
                        case StatType.LifeSteal:          effectiveLifeSteal          = Mathf.Clamp01(effectiveLifeSteal + val); break;
                        case StatType.MaxEnergy:          effectiveMaxEnergy          += Mathf.RoundToInt(val); break;
                        case StatType.ArmorGainMultiplier:
                        case StatType.HealGainMultiplier:
                        case StatType.DamageGainMultiplier:
                            // Pas de champ effective* — lues dynamiquement via GetPlayerStatModifiers
                            break;
                    }
                    Log($"{source} — {effect.statToModify} {(val >= 0 ? "+" : "")}{val:F1} (permanent run)");
                }
                break;
            }

            case EffectAction.AddCredits:
            {
                int montant = Mathf.RoundToInt(effect.value);
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddCredits(montant);
                    Log($"{source} — {(montant >= 0 ? "+" : "")}{montant} credits");
                }
                break;
            }

            case EffectAction.DonnerConsommable:
            {
                if (effect.consommableLootTable == null)
                {
                    Log($"{source} — DonnerConsommable : consommableLootTable non assignée.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                ConsumableData consommable = effect.consommableLootTable.GetRandomAvecTag(filtre);
                if (consommable == null) break;
                if (RunManager.Instance != null)
                {
                    bool ajouté = RunManager.Instance.AddConsumable(consommable);
                    if (ajouté)
                    {
                        Log($"{source} — consommable obtenu : {consommable.consumableName}");
                        SpawnConsumableButtons();
                    }
                    else
                        Log($"{source} — DonnerConsommable : inventaire plein, '{consommable.consumableName}' ignoré.");
                }
                break;
            }

            case EffectAction.DonnerModule:
            {
                if (effect.moduleLootTable == null)
                {
                    Log($"{source} — DonnerModule : moduleLootTable non assignée.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                ModuleData module = effect.moduleLootTable.GetRandomAvecTag(filtre);
                if (module == null) break;
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddModule(module);
                    Log($"{source} — module obtenu : {module.moduleName}");
                }
                break;
            }

            case EffectAction.DonnerEquipement:
            {
                if (effect.equipementLootTable == null)
                {
                    Log($"{source} — DonnerEquipement : equipementLootTable non assignée.");
                    break;
                }
                TagData filtre = ObtenirFiltreTag(effect);
                EquipmentData equipement = effect.equipementLootTable.GetRandomAvecTag(filtre);
                if (equipement == null) break;
                equipementsLootDifféré.Add(equipement);
                Log($"{source} — équipement réservé : {equipement.equipmentName} (proposé après le combat)");
                break;
            }

            default:
                Log($"{source} — Effet '{effect.action}' non encore implémenté pour les modules.");
                break;
        }

        UpdatePlayerUI();
    }

    // -----------------------------------------------
    // HELPERS DE CIBLAGE
    // -----------------------------------------------

    /// <summary>
    /// Retourne la liste des EnemyInstance à cibler selon le EffectTarget.
    /// explicitTarget : ennemi sélectionné manuellement (pour SingleEnemy).
    /// </summary>
    private List<EnemyInstance> GetEffectTargets(EffectTarget target, EnemyInstance explicitTarget)
    {
        List<EnemyInstance> vivants = GetAliveEnemies();
        switch (target)
        {
            case EffectTarget.SingleEnemy:
                // Cible explicite si fournie, sinon premier vivant (cas 1 ennemi restant)
                EnemyInstance cible = (explicitTarget != null && explicitTarget.IsAlive)
                    ? explicitTarget
                    : (vivants.Count > 0 ? vivants[0] : null);
                return cible != null ? new List<EnemyInstance> { cible } : new List<EnemyInstance>();

            case EffectTarget.AllEnemies:
                return vivants;

            case EffectTarget.RandomEnemy:
                if (vivants.Count == 0) return new List<EnemyInstance>();
                return new List<EnemyInstance> { vivants[Random.Range(0, vivants.Count)] };

            default: // Self, etc.
                return new List<EnemyInstance>();
        }
    }

    /// <summary>
    /// Retourne le premier ennemi vivant (cible par défaut pour modules et consommables).
    /// </summary>
    private EnemyInstance GetDefaultTarget()
    {
        foreach (EnemyInstance e in enemies)
            if (e.IsAlive) return e;
        return null;
    }

    /// <summary>Retourne la liste des ennemis encore en vie.</summary>
    private List<EnemyInstance> GetAliveEnemies()
    {
        List<EnemyInstance> vivants = new List<EnemyInstance>();
        foreach (EnemyInstance e in enemies)
            if (e.IsAlive) vivants.Add(e);
        return vivants;
    }

    // -----------------------------------------------
    // STATUTS
    // -----------------------------------------------

    private void ApplyStatusAuJoueur(StatusData status, int stacks)
    {
        if (status == null || stacks <= 0) return;
        if (!playerStatuses.ContainsKey(status)) playerStatuses[status] = 0;
        playerStatuses[status] += stacks;
        if (status.maxStacks > 0) playerStatuses[status] = Mathf.Min(playerStatuses[status], status.maxStacks);
        Log($"{GetPlayerName()} reçoit {stacks} stack(s) de {status.statusName} → {playerStatuses[status]} total");
    }

    private void ApplyStatusAEnnemi(EnemyInstance ennemi, StatusData status, int stacks)
    {
        if (status == null || stacks <= 0 || ennemi == null) return;
        if (!ennemi.statuses.ContainsKey(status)) ennemi.statuses[status] = 0;
        ennemi.statuses[status] += stacks;
        if (status.maxStacks > 0) ennemi.statuses[status] = Mathf.Min(ennemi.statuses[status], status.maxStacks);
        Log($"{ennemi.GetName()} reçoit {stacks} stack(s) de {status.statusName} → {ennemi.statuses[status]} total");
        // Rafraîchit immédiatement l'UI ennemi pour que l'icône apparaisse dès l'application
        // (sans ça, l'icône n'apparaît qu'au prochain UpdateEnemyUI déclenché par les dégâts/tour ennemi)
        UpdateEnemyUI(ennemi);
    }

    private int GetPlayerStatusStacks(StatusData status)
    {
        if (status == null) return 0;
        return playerStatuses.TryGetValue(status, out int s) ? s : 0;
    }

    private int GetEnemyStatusStacks(EnemyInstance ennemi, StatusData status)
    {
        if (status == null || ennemi == null) return 0;
        return ennemi.statuses.TryGetValue(status, out int s) ? s : 0;
    }

    private void ApplyPlayerPerTurnEffects()
    {
        if (playerStatuses.Count == 0) return;
        List<StatusData> keys = new List<StatusData>(playerStatuses.Keys);
        foreach (StatusData status in keys)
        {
            if (!playerStatuses.ContainsKey(status)) continue;
            int stacks = playerStatuses[status];
            if (stacks <= 0 || status.behavior != StatusBehavior.PerTurnStart) continue;

            float totalEffect = status.effectPerStack * stacks;
            switch (status.perTurnAction)
            {
                case EffectAction.DealDamage:
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(totalEffect));
                    currentPlayerHP = Mathf.Max(0, currentPlayerHP - dmg);
                    Log($"{GetPlayerName()} subit {dmg} dégâts de {status.statusName} ({stacks} stack(s))");
                    break;
                case EffectAction.ModifyStat:
                    if (!combatStatModifiers.ContainsKey(status.statToModify))
                        combatStatModifiers[status.statToModify] = 0f;
                    combatStatModifiers[status.statToModify] += totalEffect;
                    Log($"{GetPlayerName()} — {status.statusName} : {status.statToModify} {(totalEffect >= 0 ? "+" : "")}{totalEffect:F1} (ce combat)");
                    break;
                case EffectAction.Heal:
                    int healed = Mathf.Min(Mathf.RoundToInt(totalEffect), GetPlayerMaxHP() - currentPlayerHP);
                    currentPlayerHP += healed;
                    Log($"{GetPlayerName()} récupère {healed} HP grâce à {status.statusName}");
                    break;
            }
        }
    }

    private void ApplyEnemyPerTurnEffects(EnemyInstance ennemi)
    {
        if (ennemi.statuses.Count == 0) return;
        List<StatusData> keys = new List<StatusData>(ennemi.statuses.Keys);
        foreach (StatusData status in keys)
        {
            if (!ennemi.statuses.ContainsKey(status)) continue;
            int stacks = ennemi.statuses[status];
            if (stacks <= 0 || status.behavior != StatusBehavior.PerTurnStart) continue;

            float totalEffect = status.effectPerStack * stacks;
            switch (status.perTurnAction)
            {
                case EffectAction.DealDamage:
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(totalEffect));
                    ennemi.currentHP = Mathf.Max(0, ennemi.currentHP - dmg);
                    Log($"{ennemi.GetName()} subit {dmg} dégâts de {status.statusName} ({stacks} stack(s))");
                    break;
                case EffectAction.ModifyStat:
                    if (!ennemi.combatStatBonuses.ContainsKey(status.statToModify))
                        ennemi.combatStatBonuses[status.statToModify] = 0f;
                    ennemi.combatStatBonuses[status.statToModify] += totalEffect;
                    Log($"{ennemi.GetName()} — {status.statusName} : {status.statToModify} {(totalEffect >= 0 ? "+" : "")}{totalEffect:F1} (ce combat)");
                    break;
                case EffectAction.Heal:
                    int healed = Mathf.Min(Mathf.RoundToInt(totalEffect), ennemi.MaxHP - ennemi.currentHP);
                    ennemi.currentHP += healed;
                    Log($"{ennemi.GetName()} récupère {healed} HP grâce à {status.statusName}");
                    break;
            }
        }
    }

    private void DecayPlayerStatuses(StatusDecayTiming timing)
    {
        if (playerStatuses.Count == 0) return;
        List<StatusData> keys = new List<StatusData>(playerStatuses.Keys);
        foreach (StatusData status in keys)
        {
            if (!playerStatuses.ContainsKey(status) || status.decayPerTurn <= 0) continue;
            if (status.decayTiming != timing) continue;
            playerStatuses[status] = Mathf.Max(0, playerStatuses[status] - status.decayPerTurn);
            if (playerStatuses[status] == 0) Log($"{GetPlayerName()} n'a plus de {status.statusName}");
        }
        List<StatusData> toRemove = new List<StatusData>();
        foreach (var kvp in playerStatuses) if (kvp.Value <= 0) toRemove.Add(kvp.Key);
        foreach (StatusData key in toRemove) playerStatuses.Remove(key);
    }

    private void DecayEnemyStatuses(EnemyInstance ennemi, StatusDecayTiming timing)
    {
        if (ennemi.statuses.Count == 0) return;
        List<StatusData> keys = new List<StatusData>(ennemi.statuses.Keys);
        foreach (StatusData status in keys)
        {
            if (!ennemi.statuses.ContainsKey(status) || status.decayPerTurn <= 0) continue;
            if (status.decayTiming != timing) continue;
            ennemi.statuses[status] = Mathf.Max(0, ennemi.statuses[status] - status.decayPerTurn);
            if (ennemi.statuses[status] == 0) Log($"{ennemi.GetName()} n'a plus de {status.statusName}");
        }
        List<StatusData> toRemove = new List<StatusData>();
        foreach (var kvp in ennemi.statuses) if (kvp.Value <= 0) toRemove.Add(kvp.Key);
        foreach (StatusData key in toRemove) ennemi.statuses.Remove(key);
    }

    // -----------------------------------------------
    // MORT D'UN ENNEMI
    // -----------------------------------------------

    /// <summary>
    /// Appelé après chaque modification des HP d'un ennemi.
    /// Gère les événements de mort et vérifie si le combat est terminé.
    /// </summary>
    private void CheckEnemyDeath(EnemyInstance ennemi)
    {
        if (combatEnded || ennemi == null || ennemi.currentHP > 0) return;

        Log($"{ennemi.GetName()} est vaincu !");

        // Exclut immédiatement l'ennemi du ciblage et de tout flux de combat
        if (ennemi.targetButton != null) ennemi.targetButton.interactable = false;
        if (ennemi.canvasGroup  != null)
        {
            ennemi.canvasGroup.interactable  = false;
            ennemi.canvasGroup.blocksRaycasts = false;
        }

        // Déclenche les événements de mort pour les modules
        GameEvents.TriggerEnemyDied();

        // Recharge les cooldowns de nav liés aux tags de l'ennemi
        if (ennemi.data?.tags != null && ennemi.data.tags.Count > 0 && RunManager.Instance != null)
            RunManager.Instance.TickCooldownsAvecTag(ennemi.data.tags);

        // Effets à la mort — ex : explosion infligeant des dégâts au joueur, soin d'un allié, etc.
        // Exécutés depuis le point de vue de l'ennemi, avant la vérification de victoire.
        if (ennemi.data?.deathEffects != null)
        {
            foreach (EffectData eff in ennemi.data.deathEffects)
                if (eff != null) ApplyEnemyEffect(eff, $"{ennemi.GetName()} (mort)", ennemi);
            // Un death effect peut tuer le joueur → combat déjà terminé, on sort immédiatement
            if (combatEnded) return;
        }

        // Lance le fade visuel en parallèle — n'interrompt pas le flux de combat
        StartCoroutine(MortEnnemiRoutine(ennemi));

        // Tous les ennemis morts → victoire
        if (AllEnemiesDead())
            EndCombat(victory: true);
    }

    /// <summary>
    /// Coroutine de mort visuelle : animation → fade du CanvasGroup → bloc invisible mais dans le layout.
    /// SetActive(false) évité intentionnellement pour ne pas redistribuer les positions des survivants.
    /// </summary>
    private IEnumerator MortEnnemiRoutine(EnemyInstance ennemi)
    {
        // Déclenche l'animation de mort (no-op si pas d'Animator ou de paramètre "Death" configuré)
        if (ennemi.animator != null)
            ennemi.animator.SetTrigger("Death");

        // Courte pause — laisse le coup fatal "impacter" avant que le fade ne démarre
        yield return new WaitForSeconds(0.3f);

        // Fade progressif via CanvasGroup.alpha (sprite + textes ensemble)
        if (ennemi.canvasGroup != null)
        {
            float elapsed = 0f;
            float alphaDepart = ennemi.canvasGroup.alpha;
            while (elapsed < fadeDureeMort)
            {
                elapsed += Time.deltaTime;
                ennemi.canvasGroup.alpha = Mathf.Lerp(alphaDepart, 0f, elapsed / fadeDureeMort);
                yield return null;
            }
            ennemi.canvasGroup.alpha = 0f;
        }

        // Le bloc reste dans le HorizontalLayoutGroup (SetActive(false) évité) :
        // les ennemis survivants conservent leurs positions d'origine.
    }

    private bool AllEnemiesDead()
    {
        foreach (EnemyInstance e in enemies)
            if (e.IsAlive) return false;
        return true;
    }

    // -----------------------------------------------
    // STATS DYNAMIQUES
    // -----------------------------------------------

    private int CalculerDegatsJoueur(float skillValue)
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.Attack);
        return Mathf.Max(0, Mathf.RoundToInt((skillValue + effectiveAttack + flat) * (1f + pct)));
    }

    private (float flat, float pct) GetPlayerStatModifiers(StatType stat)
    {
        float flat = 0f, pct = 0f;
        if (combatStatModifiers.TryGetValue(stat, out float directMod))
            flat += directMod;
        foreach (var kvp in playerStatuses)
        {
            StatusData status = kvp.Key;
            int stacks = kvp.Value;
            if (stacks <= 0 || status == null || status.behavior != StatusBehavior.ModifyStat) continue;
            if (status.statToModify != stat) continue;
            float amount = status.valueScalesWithStacks ? status.effectPerStack * stacks : status.effectPerStack;
            if (status.statModifierType == StatModifierType.Percentage) pct += amount;
            else flat += amount;
        }
        return (flat, pct);
    }

    private int   GetCurrentAttack()        { var (f,p) = GetPlayerStatModifiers(StatType.Attack);             return Mathf.Max(0, Mathf.RoundToInt((effectiveAttack + f) * (1f + p))); }
    private int   GetCurrentDefense()       { var (f,p) = GetPlayerStatModifiers(StatType.Defense);            return Mathf.Max(0, Mathf.RoundToInt((effectiveDefense + f) * (1f + p))); }
    private float GetCurrentCritChance()    { var (f,p) = GetPlayerStatModifiers(StatType.CriticalChance);     return Mathf.Clamp01((effectiveCriticalChance + f) * (1f + p)); }
    private float GetCurrentCritMultiplier(){ var (f,p) = GetPlayerStatModifiers(StatType.CriticalMultiplier); return Mathf.Max(1f, (effectiveCriticalMultiplier + f) * (1f + p)); }
    private float GetCurrentLifeSteal()     { var (f,p) = GetPlayerStatModifiers(StatType.LifeSteal);          return Mathf.Clamp01((effectiveLifeSteal + f) * (1f + p)); }
    private int   GetCurrentMaxEnergy()     { var (f,p) = GetPlayerStatModifiers(StatType.MaxEnergy);          return Mathf.Max(1, Mathf.RoundToInt((effectiveMaxEnergy + f) * (1f + p))); }

    /// <summary>
    /// Retourne les modificateurs de stats cumulés d'un ennemi pour le combat en cours.
    /// Miroir de GetPlayerStatModifiers : combine combatStatBonuses (effets directs)
    /// et les statuts ModifyStat passifs portés par l'ennemi.
    /// </summary>
    private (float flat, float pct) GetEnemyStatModifiers(EnemyInstance ennemi, StatType stat)
    {
        float flat = 0f, pct = 0f;

        // Bonus directs accumulés (skills ennemis, spawnEffects, perTurnAction ModifyStat…)
        if (ennemi.combatStatBonuses.TryGetValue(stat, out float directMod))
            flat += directMod;

        // Statuts ModifyStat passifs — actifs tant que stacks > 0
        foreach (var kvp in ennemi.statuses)
        {
            StatusData status = kvp.Key;
            int stacks = kvp.Value;
            if (stacks <= 0 || status == null || status.behavior != StatusBehavior.ModifyStat) continue;
            if (status.statToModify != stat) continue;
            float amount = status.valueScalesWithStacks ? status.effectPerStack * stacks : status.effectPerStack;
            if (status.statModifierType == StatModifierType.Percentage) pct += amount;
            else flat += amount;
        }

        return (flat, pct);
    }

    /// <summary>
    /// Retourne l'attaque effective d'un ennemi en tenant compte de ses modificateurs de combat.
    /// Utiliser à la place de ennemi.data.attack pour respecter les buffs/debuffs en cours.
    /// </summary>
    private int GetEnemyAttack(EnemyInstance ennemi)
    {
        int baseAtk = ennemi.data != null ? ennemi.data.attack : 5;
        var (flat, pct) = GetEnemyStatModifiers(ennemi, StatType.Attack);
        return Mathf.Max(0, Mathf.RoundToInt((baseAtk + flat) * (1f + pct)));
    }

    // -----------------------------------------------
    // COOLDOWNS
    // -----------------------------------------------

    private void DecrementCooldowns()
    {
        List<SkillData> keys = new List<SkillData>(skillCooldowns.Keys);
        foreach (SkillData skill in keys)
            if (skillCooldowns[skill] > 0) skillCooldowns[skill]--;
    }

    // -----------------------------------------------
    // FIN DE COMBAT
    // -----------------------------------------------

    private void OnVictoireCheat()
    {
        foreach (EnemyInstance e in enemies) e.currentHP = 0;
        foreach (EnemyInstance e in enemies) UpdateEnemyUI(e);
        Log("(Cheat) Tous les ennemis éliminés instantanément.");
        EndCombat(victory: true);
    }

    private void EndCombat(bool victory)
    {
        if (combatEnded) return;
        combatEnded = true;

        // Annule le ciblage si en cours
        if (isSelectingTarget)
        {
            isSelectingTarget = false;
            if (arrowTransform != null) arrowTransform.gameObject.SetActive(false);
            foreach (EnemyInstance e in enemies)
            {
                if (e.targetButton != null) e.targetButton.interactable = false;
                if (e.canvasGroup  != null) e.canvasGroup.blocksRaycasts = false;
            }
        }

        battleState = victory ? BattleState.Victory : BattleState.Defeat;

        if (endTurnButton != null) endTurnButton.interactable = false;
        foreach (SkillButton   sb in spawnedSkillButtons)      sb.SetInteractable(false);
        foreach (ConsumableButton cb in spawnedConsumableButtons) cb.SetInteractable(false);

        if (combatActivePanel != null) combatActivePanel.SetActive(false);

        if (victory)
        {
            // Crédits de loot
            int totalCredits = currentGroup != null
                ? currentGroup.creditsLoot
                : enemies.Sum(e => e.data?.creditsLoot ?? 0);

            if (RunManager.Instance != null && totalCredits > 0)
            {
                RunManager.Instance.AddCredits(totalCredits);
                Log($"Loot — +{totalCredits} credits → {RunManager.Instance.credits} total");
            }

            ShowLootPanel();
            Log("Combat terminé — Victoire !");
        }
        else
        {
            if (endPanel      != null) endPanel.SetActive(true);
            if (endTitleText  != null) endTitleText.text  = "Défaite...";
            if (endButtonText != null) endButtonText.text = "Retour au menu";
            Log("Combat terminé — Défaite.");
        }
    }

    private void OnLootContinueClicked()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.currentHP = currentPlayerHP;
            RunManager.Instance.maxHP     = GetPlayerMaxHP();

            if (RunManager.Instance.currentCellType == CellType.Boss)
            {
                Log("Boss vaincu — fin de la run.");
                RunManager.Instance.TickCooldownsDe(NavCooldownType.MondeTermine);
                RunManager.Instance.EndRun();
                SceneLoader.Instance.GoToMainMenu();
                return;
            }

            RunManager.Instance.combatsTermines++;
            RunManager.Instance.TickCooldownsDe(NavCooldownType.CombatsTermines);
            RunManager.Instance.ClearCurrentRoom();
        }
        SceneLoader.Instance.GoToNavigation();
    }

    private void OnEndButtonClicked()
    {
        if (battleState == BattleState.Defeat)
        {
            RunManager.Instance?.EndRun();
            SceneLoader.Instance.GoToMainMenu();
        }
    }

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    private void ShowLootPanel()
    {
        if (lootPanel == null) return;
        lootPanel.SetActive(true);

        InventoryUIManager.Instance?.SetDragDropEnabled(true);
        TryGrantConsumableLoot();
        TryGrantSkillLoot();

        if (lootContinueButton != null)
        {
            lootContinueButton.gameObject.SetActive(true);
            lootContinueButton.interactable = true;
        }

        List<EquipmentData> offers = PickLootOffers();
        if (offers.Count == 0 || equipmentOfferController == null) return;
        equipmentOfferController.StartOffresSimultanées(offers, null);
    }

    private void TryGrantSkillLoot()
    {
        SkillLootTable table = RunManager.Instance?.currentMapData?.defaultCombatSkillLootTable;
        if (table == null) return;

        SkillData skill = table.GetRandom();
        if (skill == null) return;

        bool ajouté = RunManager.Instance.AddSkillToInventory(skill);
        if (ajouté)
            Log($"Loot — Skill '{skill.skillName}' ajouté à l'inventaire.");
        else
            Log($"Loot — Inventaire skills plein, '{skill.skillName}' perdu.");
    }

    /// <summary>
    /// Résout les offres d'équipement : offres standard + équipements injectés via DonnerEquipement.
    /// Vide <c>equipementsLootDifféré</c> après la fusion.
    /// </summary>
    private List<EquipmentData> PickLootOffers()
    {
        List<EquipmentData> offresBase = BuildLootOffresBase();

        // Fusionne les équipements injectés par DonnerEquipement pendant le combat
        foreach (EquipmentData e in equipementsLootDifféré)
        {
            if (e != null && !offresBase.Contains(e))
                offresBase.Add(e);
        }
        equipementsLootDifféré.Clear();

        // Clone chaque équipement pour garantir l'indépendance des instances runtime
        List<EquipmentData> clones = new List<EquipmentData>(offresBase.Count);
        foreach (EquipmentData e in offresBase)
        {
            if (e != null)
                clones.Add(RunManager.Instance.CloneEquipmentForLoot(e));
        }
        return clones;
    }

    /// <summary>
    /// Construit la liste d'offres d'équipement standard dans l'ordre de priorité :
    ///   EnemyGroup.lootPool > EnemyData.lootPool > MapData.defaultCombatLootTable
    /// </summary>
    private List<EquipmentData> BuildLootOffresBase()
    {
        List<EquipmentData> pool       = null;
        int                 offerCount = 2;

        if (currentGroup != null && currentGroup.lootPool != null && currentGroup.lootPool.Count > 0)
        {
            pool       = new List<EquipmentData>(currentGroup.lootPool);
            offerCount = currentGroup.lootOfferCount;
        }
        else if (enemies.Count == 1 && enemies[0].data?.lootPool != null && enemies[0].data.lootPool.Count > 0)
        {
            pool       = new List<EquipmentData>(enemies[0].data.lootPool);
            offerCount = enemies[0].data.lootOfferCount;
        }
        else if (RunManager.Instance?.currentMapData?.defaultCombatLootTable != null)
        {
            // Fallback MapData : on tire offerCount fois depuis la table
            EquipmentLootTable table = RunManager.Instance.currentMapData.defaultCombatLootTable;
            int count = RunManager.Instance.currentMapData.defaultLootOfferCount;
            List<EquipmentData> fallbackOffers = new List<EquipmentData>();
            for (int i = 0; i < count; i++)
            {
                EquipmentData item = table.GetRandom();
                if (item != null && !fallbackOffers.Contains(item))
                    fallbackOffers.Add(item);
            }
            return fallbackOffers;
        }

        if (pool == null || pool.Count == 0) return new List<EquipmentData>();

        // Mélange Fisher-Yates
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, Mathf.Min(offerCount, pool.Count));
    }

    private void TryGrantConsumableLoot()
    {
        if (RunManager.Instance == null || !RunManager.Instance.HasConsumableSlotFree()) return;

        List<ConsumableData> consPool = null;
        if (currentGroup != null && currentGroup.consumableLootPool != null && currentGroup.consumableLootPool.Count > 0)
            consPool = currentGroup.consumableLootPool;
        else if (enemies.Count == 1 && enemies[0].data?.consumableLootPool != null && enemies[0].data.consumableLootPool.Count > 0)
            consPool = enemies[0].data.consumableLootPool;

        if (consPool == null || consPool.Count == 0) return;

        ConsumableData granted = consPool[Random.Range(0, consPool.Count)];
        if (granted == null) return;
        if (RunManager.Instance.AddConsumable(granted))
            Log($"Consommable obtenu : {granted.consumableName}");
    }

    // -----------------------------------------------
    // MISE À JOUR DE L'UI
    // -----------------------------------------------

    private void UpdatePlayerUI()
    {
        if (playerHPText    != null) playerHPText.text    = $"HP : {currentPlayerHP} / {GetPlayerMaxHP()}";
        if (playerArmorText != null) playerArmorText.text = currentPlayerArmor > 0 ? $"Armure : {currentPlayerArmor}" : "";
        if (energyText      != null) energyText.text      = $"Énergie : {currentEnergy} / {GetCurrentMaxEnergy()}";
        if (creditsText     != null && RunManager.Instance != null)
            creditsText.text = $"Credits : {RunManager.Instance.credits}";

        RefreshPlayerStatusIcons();
    }

    /// <summary>
    /// Recrée les icônes de statuts du joueur depuis playerStatuses.
    /// Appelée automatiquement par UpdatePlayerUI à chaque changement d'état joueur.
    /// Le GridLayoutGroup sur statusIconContainer gère le retour à la ligne automatiquement.
    /// </summary>
    private void RefreshPlayerStatusIcons()
    {
        if (statusIconPrefab == null || statusIconContainer == null) return;

        // Met à jour le constraintCount du GridLayoutGroup pour refléter la valeur Inspector
        GridLayoutGroup grid = statusIconContainer.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = Mathf.Max(1, statusIconsPerRow);
        }

        // Détruit les icônes existantes — SetActive(false) d'abord pour retrait immédiat du layout (Piège #3)
        foreach (StatusIcon icon in _spawnedStatusIcons)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
        }
        _spawnedStatusIcons.Clear();

        // Recrée une icône par statut actif (stacks > 0)
        foreach (var kvp in playerStatuses)
        {
            StatusData status = kvp.Key;
            int        stacks = kvp.Value;
            if (status == null || stacks <= 0) continue;

            GameObject go   = Instantiate(statusIconPrefab, statusIconContainer);
            StatusIcon icon = go.GetComponent<StatusIcon>();
            if (icon != null)
            {
                icon.Setup(status, stacks);
                _spawnedStatusIcons.Add(icon);
            }
        }
    }

    private void UpdateEnemyUI(EnemyInstance ennemi)
    {
        if (ennemi == null) return;
        if (ennemi.hpText         != null) ennemi.hpText.text         = $"HP : {ennemi.currentHP} / {ennemi.MaxHP}";
        if (ennemi.armorText      != null) ennemi.armorText.text      = ennemi.currentArmor > 0 ? $"Armure : {ennemi.currentArmor}" : "";
        if (ennemi.nextActionText != null)
        {
            SkillData next = ennemi.ai?.PeekNextSkill();
            ennemi.nextActionText.text = next != null ? $"Prochain : {next.skillName}" : "Prochain : Attaque de base";
        }
        RefreshEnemyStatusIcons(ennemi);
    }

    /// <summary>
    /// Recrée les icônes de statuts d'un ennemi depuis son dictionnaire statuses.
    /// Appelée automatiquement par UpdateEnemyUI. Utilise le même statusIconPrefab que le joueur.
    /// Le container "EnemyStatusContainer" doit exister dans l'EnemyUIPrefab.
    /// </summary>
    private void RefreshEnemyStatusIcons(EnemyInstance ennemi)
    {
        if (statusIconPrefab == null || ennemi.statusIconContainer == null) return;

        // Détruit les icônes existantes — SetActive(false) d'abord pour retrait immédiat du layout (Piège #3)
        foreach (StatusIcon icon in ennemi.spawnedStatusIcons)
        {
            if (icon != null)
            {
                icon.gameObject.SetActive(false);
                Destroy(icon.gameObject);
            }
        }
        ennemi.spawnedStatusIcons.Clear();

        // Recrée une icône par statut actif (stacks > 0)
        foreach (var kvp in ennemi.statuses)
        {
            StatusData status = kvp.Key;
            int        stacks = kvp.Value;
            if (status == null || stacks <= 0) continue;

            GameObject go   = Instantiate(statusIconPrefab, ennemi.statusIconContainer);
            StatusIcon icon = go.GetComponent<StatusIcon>();
            if (icon != null)
            {
                icon.Setup(status, stacks);
                ennemi.spawnedStatusIcons.Add(icon);
            }
        }
    }

    private void UpdateSkillButtons()
    {
        foreach (SkillButton sb in spawnedSkillButtons)
        {
            if (sb.Skill == null) continue;
            int cd = skillCooldowns.TryGetValue(sb.Skill, out int val) ? val : 0;
            sb.SetCooldown(cd);
            bool canUse = battleState == BattleState.PlayerTurn
                       && !isSelectingTarget
                       && cd == 0
                       && currentEnergy >= sb.EffectiveCost;
            sb.SetInteractable(canUse);
        }
    }

    private void Log(string message)
    {
        Debug.Log($"[Combat] {message}");
        if (combatLogText != null) combatLogText.text = message;
    }

    // -----------------------------------------------
    // UTILITAIRES
    // -----------------------------------------------

    private string GetPlayerName()    => characterData != null ? characterData.characterName : "Joueur";
    private string GetRencontreName() => currentGroup != null  ? currentGroup.groupName
                                       : enemies.Count > 0     ? enemies[0].GetName()
                                       : "Ennemi";
    private int    GetPlayerMaxHP()   => effectiveMaxHP;

    // -----------------------------------------------
    // CLASSE INTERNE — ÉTAT RUNTIME D'UN ENNEMI
    // -----------------------------------------------

    /// <summary>
    /// Encapsule l'état runtime d'un ennemi pendant le combat.
    /// Séparé du ScriptableObject EnemyData pour ne jamais modifier l'asset.
    /// </summary>
    private class EnemyInstance
    {
        public EnemyData data;
        public int       currentHP;
        public int       currentArmor;
        public Dictionary<StatusData, int>  statuses         = new Dictionary<StatusData, int>();
        // Modificateurs de stats temporaires accumulés pendant ce combat (skills, perTurnAction, spawnEffects…)
        // Miroir du combatStatModifiers du joueur — jamais modifié sur le ScriptableObject.
        public Dictionary<StatType, float>  combatStatBonuses = new Dictionary<StatType, float>();
        public EnemyAI   ai;

        // Références UI (peuplées par SpawnEnemyUI)
        public GameObject      uiRoot;
        public CanvasGroup     canvasGroup;   // contrôle blocksRaycasts pour ne pas bloquer les autres boutons
        public Image           spriteImage;
        public TextMeshProUGUI hpText;
        public TextMeshProUGUI armorText;
        public TextMeshProUGUI nextActionText;
        public Button          targetButton;
        public Animator        animator;      // pour le trigger "Death" (branché quand l'Animator sera prêt)

        // Icônes de statuts — container trouvé par nom "EnemyStatusContainer" dans SpawnEnemyUI
        public Transform       statusIconContainer;
        public List<StatusIcon> spawnedStatusIcons = new List<StatusIcon>();

        public bool IsAlive => currentHP > 0;
        public int  MaxHP   => data != null ? data.maxHP : 0;
        public string GetName() => data != null ? data.enemyName : "Ennemi";

        public EnemyInstance(EnemyData d)
        {
            data       = d;
            currentHP  = d != null ? d.maxHP : 0;
            currentArmor = 0;
            ai         = d != null ? new EnemyAI(d) : null;
        }
    }
}
