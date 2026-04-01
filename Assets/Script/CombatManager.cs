using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public enum BattleState { PlayerTurn, EnemyTurn, Victory, Defeat }

/// <summary>
/// Gère la scène de combat : tours, énergie, compétences, effets, fin de combat.
/// </summary>
public class CombatManager : MonoBehaviour
{
    // -----------------------------------------------
    // DONNÉES
    // -----------------------------------------------

    [Header("Données")]
    public CharacterData characterData;
    public EnemyData     enemyData;

    // -----------------------------------------------
    // UI — JOUEUR
    // -----------------------------------------------

    [Header("UI — Joueur")]
    public TextMeshProUGUI playerNameText;
    public TextMeshProUGUI playerHPText;
    public TextMeshProUGUI playerArmorText;  // Affiche "Armure : 5" — peut rester non-assigné
    public TextMeshProUGUI energyText;       // Affiche "Énergie : 2 / 3"

    // -----------------------------------------------
    // UI — ENNEMI
    // -----------------------------------------------

    [Header("UI — Ennemi")]
    public TextMeshProUGUI enemyNameText;
    public TextMeshProUGUI enemyHPText;
    public TextMeshProUGUI enemyArmorText;   // Affiche "Armure : 3" — peut rester non-assigné
    // Affiche la prochaine action de l'ennemi (comme dans Slay the Spire)
    public TextMeshProUGUI enemyNextActionText;

    // -----------------------------------------------
    // UI — COMBAT
    // -----------------------------------------------

    [Header("UI — Combat")]
    public TextMeshProUGUI turnText;
    public TextMeshProUGUI combatLogText;

    // Bouton "Fin du tour" — le joueur l'utilise quand il a fini ses actions
    public Button endTurnButton;

    // Conteneur où les boutons de compétences seront générés dynamiquement.
    // Place-le dans le CombatActivePanel, configure-le avec un Horizontal ou Vertical Layout Group.
    public Transform skillButtonContainer;

    // Préfab d'un bouton de compétence — doit avoir le composant SkillButton
    public GameObject skillButtonPrefab;

    // -----------------------------------------------
    // UI — CONSOMMABLES
    // -----------------------------------------------

    [Header("UI — Consommables")]
    // Conteneur où les boutons de consommables seront générés dynamiquement
    // (Horizontal ou Vertical Layout Group recommandé)
    public Transform consumableButtonContainer;

    // Préfab d'un bouton de consommable — doit avoir le composant ConsumableButton
    public GameObject consumableButtonPrefab;

    // -----------------------------------------------
    // UI — DEBUG
    // -----------------------------------------------

    [Header("Debug / Tests")]
    // Instakill l'ennemi — à retirer quand le vrai système sera complet
    public Button victoireButton;

    // -----------------------------------------------
    // UI — PANNEAUX
    // -----------------------------------------------

    [Header("UI — Panneaux")]
    public GameObject combatActivePanel;
    public GameObject endPanel;
    public TextMeshProUGUI endTitleText;
    public Button          endButton;
    public TextMeshProUGUI endButtonText;

    // -----------------------------------------------
    // UI — LOOT (affiché à la place de endPanel en cas de victoire)
    // -----------------------------------------------

    [Header("UI — Loot")]
    // Panel principal affiché après une victoire
    public GameObject lootPanel;

    // Bouton "Continuer" du panel loot — toujours actif (le joueur peut ignorer le loot)
    public Button lootContinueButton;

    // Contrôleur partagé qui gère l'affichage et la résolution des cartes de loot
    public EquipmentOfferController equipmentOfferController;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private BattleState battleState;

    private int currentPlayerHP;
    private int currentEnemyHP;
    private int currentEnergy;

    // Armure (style Slay the Spire) :
    //   - Absorbe les dégâts directs avant les HP
    //   - N'absorbe PAS le poison
    //   - Se remet à 0 au début du tour de l'entité concernée
    private int currentPlayerArmor;
    private int currentEnemyArmor;

    // Stats effectives du joueur — stats de base + bonus de chaque pièce d'équipement.
    // Calculées une fois au démarrage du combat dans ResolveEquipment().
    // On les sépare des données brutes (CharacterData) pour ne jamais modifier le ScriptableObject.
    private int   effectiveMaxHP;
    private int   effectiveAttack;
    private int   effectiveDefense;
    private int   effectiveMaxEnergy;
    private float effectiveCriticalChance;      // Probabilité de coup critique [0, 1]
    private float effectiveCriticalMultiplier;  // Multiplicateur de dégâts sur un critique
    private int   effectiveRegeneration;        // HP récupérés au début de chaque tour joueur
    private float effectiveLifeSteal;           // Fraction des dégâts convertie en soins [0, 1]

    // Skills disponibles en combat : collectés depuis l'équipement, ou startingSkills en fallback
    private List<SkillData> availableSkills = new List<SkillData>();

    // Instance de l'IA ennemie — gère la file d'actions circulaire
    private EnemyAI enemyAI;

    // Liste des boutons de compétences instanciés — on les garde pour les mettre à jour
    private List<SkillButton> spawnedSkillButtons = new List<SkillButton>();

    // Liste des boutons de consommables instanciés — recréés après chaque utilisation
    private List<ConsumableButton> spawnedConsumableButtons = new List<ConsumableButton>();

    // Cooldowns par compétence : nombre de tours restants avant de pouvoir l'utiliser
    private Dictionary<SkillData, int> skillCooldowns = new Dictionary<SkillData, int>();

    private const float EnemyActionDelay = 1.0f;

    // Vrai uniquement pendant le tout premier tour du combat.
    // Permet d'appliquer les modules Passive après le reset d'armure initial.
    private bool isFirstTurn = true;

    // Statuts actifs en combat — clé = définition du statut, valeur = nombre de stacks
    // Les stacks sont trackés ici, jamais dans les ScriptableObjects
    private Dictionary<StatusData, int> playerStatuses = new Dictionary<StatusData, int>();
    private Dictionary<StatusData, int> enemyStatuses  = new Dictionary<StatusData, int>();

    // Modificateurs de stats temporaires ce combat — appliqués par les skills et consommables.
    // Viennent s'ajouter à effectiveX après les bonus de run (eux-mêmes dans ResolveEquipment).
    // Les statuts de type ModifyStat sont calculés dynamiquement dans GetPlayerStatModifiers().
    private Dictionary<StatType, float> combatStatModifiers = new Dictionary<StatType, float>();


    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        // Résoudre le CharacterData depuis RunManager si une run est en cours.
        // Fallback sur le champ Inspector pour les tests de la scène Combat en isolation.
        if (RunManager.Instance?.selectedCharacter != null)
            characterData = RunManager.Instance.selectedCharacter;
        else
            Debug.Log("[Combat] Pas de CharacterData dans RunManager — utilisation du champ Inspector local.");

        if (endTurnButton      != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (endButton          != null) endButton.onClick.AddListener(OnEndButtonClicked);
        if (victoireButton     != null) victoireButton.onClick.AddListener(OnVictoireCheat);
        if (lootContinueButton != null) lootContinueButton.onClick.AddListener(OnLootContinueClicked);

        if (endPanel           != null) endPanel.SetActive(false);
        if (lootPanel          != null) lootPanel.SetActive(false);
        if (lootContinueButton != null) lootContinueButton.gameObject.SetActive(false);

        InitializeCombat();
    }

    private void InitializeCombat()
    {
        // Reset des modificateurs temporaires de combat
        combatStatModifiers.Clear();

        // Résout l'équipement en premier — les stats effectives et availableSkills
        // sont nécessaires pour tout le reste de l'initialisation
        ResolveEquipment();

        // HP du joueur : on prend ceux du RunManager (persistants entre salles)
        // ou le max effectif (premier combat du run / test direct)
        currentPlayerHP = (RunManager.Instance != null && RunManager.Instance.currentHP > 0)
            ? RunManager.Instance.currentHP
            : effectiveMaxHP;

        if (enemyData != null)
        {
            currentEnemyHP = enemyData.maxHP;
            enemyAI = new EnemyAI(enemyData);
        }

        // Initialise les cooldowns à 0 pour chaque skill disponible
        foreach (SkillData skill in availableSkills)
        {
            if (skill != null)
                skillCooldowns[skill] = 0;
        }

        SpawnSkillButtons();
        SpawnConsumableButtons();
        UpdateUI();
        UpdateEnemyNextActionUI();
        Log($"Combat commencé — {GetPlayerName()} vs {GetEnemyName()}");
        StartPlayerTurn();
    }

    // -----------------------------------------------
    // RÉSOLUTION DE L'ÉQUIPEMENT
    // -----------------------------------------------

    /// <summary>
    /// Lit les 5 emplacements d'équipement du CharacterData et calcule :
    ///   - Les stats effectives (base + somme des bonus de chaque pièce)
    ///   - La liste de skills disponibles (collectés depuis l'équipement)
    ///
    /// Si aucun équipement n'apporte de skills, on replie sur startingSkills
    /// pour pouvoir jouer sans équipement assigné (pratique pendant le développement).
    /// </summary>
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

        // On part des stats de base du personnage
        effectiveMaxHP              = characterData.maxHP;
        effectiveAttack             = characterData.attack;
        effectiveDefense            = characterData.defense;
        effectiveMaxEnergy          = characterData.maxEnergy;
        effectiveCriticalChance     = characterData.criticalChance;
        effectiveCriticalMultiplier = characterData.criticalMultiplier;
        effectiveRegeneration       = characterData.regeneration;
        effectiveLifeSteal          = characterData.lifeSteal;

        availableSkills.Clear();

        // Priorité : pièce sauvegardée dans RunManager (gagnée pendant la run)
        // Fallback  : pièce de départ définie sur le CharacterData (config initiale)
        //
        // Cette logique est prête pour un inventaire : si un jour on ajoute
        // une liste d'objets portés, on interroge RunManager de la même façon.
        List<EquipmentData> equipped = new List<EquipmentData>
        {
            RunManager.Instance?.GetEquipped(EquipmentSlot.Head)  ?? characterData.startingHead,
            RunManager.Instance?.GetEquipped(EquipmentSlot.Torso) ?? characterData.startingTorso,
            RunManager.Instance?.GetEquipped(EquipmentSlot.Legs)  ?? characterData.startingLegs,
            RunManager.Instance?.GetEquipped(EquipmentSlot.Arm1)  ?? characterData.startingArm1,
            RunManager.Instance?.GetEquipped(EquipmentSlot.Arm2)  ?? characterData.startingArm2,
        };

        foreach (EquipmentData equip in equipped)
        {
            if (equip == null) continue;

            // Additionne les bonus de stats
            effectiveMaxHP              += equip.bonusHP;
            effectiveAttack             += equip.bonusAttack;
            effectiveDefense            += equip.bonusDefense;
            effectiveCriticalChance     += equip.bonusCriticalChance;
            effectiveCriticalMultiplier += equip.bonusCriticalMultiplier;
            effectiveRegeneration       += equip.bonusRegeneration;
            effectiveLifeSteal          += equip.bonusLifeSteal;

            // Collecte les skills de cette pièce
            foreach (SkillData skill in equip.skills)
            {
                if (skill != null)
                    availableSkills.Add(skill);
            }
        }

        // Seed RunManager avec l'équipement de départ pour tout slot encore vide.
        // Sans ça, IsSlotFree() renverrait true au premier combat (RunManager est vide),
        // ce qui ferait auto-placer les pièces de bras sans demander au joueur.
        if (RunManager.Instance != null)
        {
            SeedSlotIfFree(EquipmentSlot.Head,  characterData.startingHead);
            SeedSlotIfFree(EquipmentSlot.Torso, characterData.startingTorso);
            SeedSlotIfFree(EquipmentSlot.Legs,  characterData.startingLegs);
            SeedSlotIfFree(EquipmentSlot.Arm1,  characterData.startingArm1);
            SeedSlotIfFree(EquipmentSlot.Arm2,  characterData.startingArm2);

            // Seed le module de départ si le joueur n'en possède pas encore.
            // Même logique que pour l'équipement : on ne le donne qu'une seule fois.
            if (characterData.startingModule != null
                && !RunManager.Instance.HasModule(characterData.startingModule))
            {
                RunManager.Instance.AddModule(characterData.startingModule);
            }

            // Seed les consommables de départ — donnés une seule fois par run, au premier combat.
            // Le flag startingConsumablesSeeded empêche de les redonner si le joueur
            // a tout utilisé et entre dans un nouveau combat pendant le même run.
            if (characterData.startingConsumables != null
                && !RunManager.Instance.startingConsumablesSeeded)
            {
                foreach (ConsumableData consumable in characterData.startingConsumables)
                {
                    if (consumable != null)
                        RunManager.Instance.AddConsumable(consumable);
                }
                RunManager.Instance.startingConsumablesSeeded = true;
            }
        }

        // Bonus de stats permanents du run (events, modules, etc.) — lus depuis RunManager
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

        // Plafonne la chance de critique entre 0 et 1 (les bonus d'équipement et de run peuvent dépasser)
        effectiveCriticalChance = Mathf.Clamp01(effectiveCriticalChance);

        Debug.Log($"[Combat] Équipement résolu — HP: {effectiveMaxHP}, ATK: {effectiveAttack}, " +
                  $"DEF: {effectiveDefense}, Énergie: {effectiveMaxEnergy}, " +
                  $"Crit: {effectiveCriticalChance:P0}, Multi: {effectiveCriticalMultiplier:F1}x, " +
                  $"Regen: {effectiveRegeneration}, LifeSteal: {effectiveLifeSteal:P0}, " +
                  $"Skills: {availableSkills.Count}");
    }

    /// <summary>
    /// Enregistre une pièce de départ dans RunManager uniquement si le slot est vide.
    /// Appelé une fois par combat pour initialiser l'état de l'équipement au premier run.
    /// </summary>
    private void SeedSlotIfFree(EquipmentSlot slot, EquipmentData starting)
    {
        if (starting != null && RunManager.Instance.IsSlotFree(slot))
            RunManager.Instance.EquipItem(slot, starting);
    }

    // -----------------------------------------------
    // GÉNÉRATION DES BOUTONS DE COMPÉTENCES
    // -----------------------------------------------

    /// <summary>
    /// Instancie un bouton par compétence dans le skillButtonContainer.
    /// On génère les boutons une seule fois à l'initialisation —
    /// on les met à jour (grisés, cooldown) à chaque changement d'état.
    /// Après les compétences actives, génère également un bouton passif grisé
    /// pour chaque passiveEffect des pièces de bras (Arm1, Arm2).
    /// </summary>
    private void SpawnSkillButtons()
    {
        if (skillButtonPrefab == null || skillButtonContainer == null) return;

        // Compétences actives (non-navigation)
        foreach (SkillData skill in availableSkills)
        {
            if (skill == null) continue;
            // Les skills de navigation (jambes) n'ont pas de bouton en combat
            if (skill.isNavigationSkill) continue;

            GameObject go = Instantiate(skillButtonPrefab, skillButtonContainer);
            SkillButton sb = go.GetComponent<SkillButton>();
            if (sb == null) continue;

            sb.Setup(skill, UseSkill);
            spawnedSkillButtons.Add(sb);
        }

        // Effets passifs des bras — boutons grisés, non cliquables
        SpawnPassifsBras();
    }

    /// <summary>
    /// Génère un bouton passif grisé pour chaque passiveEffect des pièces Arm1 et Arm2.
    /// Les boutons sont ajoutés à la suite des compétences actives dans le même container.
    /// </summary>
    private void SpawnPassifsBras()
    {
        EquipmentData bras1 = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm1)
                              ?? characterData?.startingArm1;
        EquipmentData bras2 = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm2)
                              ?? characterData?.startingArm2;

        SpawnPassifsEquipement(bras1);
        SpawnPassifsEquipement(bras2);
    }

    /// <summary>
    /// Instancie un bouton passif grisé pour chaque EffectData dans passiveEffects de la pièce.
    /// </summary>
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
            // Les boutons passifs ne sont pas trackés dans spawnedSkillButtons :
            // ils sont toujours grisés, jamais mis à jour par UpdateSkillButtons().
            Debug.Log($"[Combat] Bouton passif généré : {effet.displayName ?? effet.effectID} ({equip.equipmentName})");
        }
    }

    // -----------------------------------------------
    // GÉNÉRATION DES BOUTONS DE CONSOMMABLES
    // -----------------------------------------------

    /// <summary>
    /// Instancie un bouton par consommable utilisable en combat.
    /// Appelé à l'initialisation et après chaque utilisation (pour refléter l'inventaire mis à jour).
    /// </summary>
    private void SpawnConsumableButtons()
    {
        if (consumableButtonPrefab == null || consumableButtonContainer == null) return;

        // Nettoie les anciens boutons avant de recréer
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
            // Non utilisable en combat = affiché mais grisé et réduit
            cb.SetInteractable(consumable.usableInCombat);
            spawnedConsumableButtons.Add(cb);
        }
    }

    // -----------------------------------------------
    // MACHINE À ÉTATS — TRANSITIONS
    // -----------------------------------------------

    private void StartPlayerTurn()
    {
        battleState = BattleState.PlayerTurn;

        // Réinitialise l'énergie au maximum effectif (base + bonus équipement + modificateurs actifs)
        currentEnergy = GetCurrentMaxEnergy();

        // L'armure du joueur se remet à 0 au début de son tour (comme dans Slay the Spire)
        currentPlayerArmor = 0;

        // Modules Passive : appliqués une seule fois au premier tour, après le reset d'armure.
        // Placés ici (et non dans InitializeCombat) pour éviter que le reset efface l'armure passive.
        if (isFirstTurn)
        {
            isFirstTurn = false;
            ModuleManager.Instance?.ApplyModulesWithTrigger(EffectTrigger.OnFightStart);
        }

        // Statuts sur le joueur : effets automatiques (poison, etc.) en début de tour,
        // puis décroissance des statuts dont le timing est OnTurnStart.
        // Les statuts OnTurnEnd décroissent dans OnEndTurn(), après que le joueur a agi.
        ApplyPerTurnEffects(forEnemy: false);
        DecayStatuses(forEnemy: false, StatusDecayTiming.OnTurnStart);

        // Régénération : soigne le joueur au début de son tour (plafonné aux HP max)
        if (effectiveRegeneration > 0)
        {
            int healed = Mathf.Min(effectiveRegeneration, GetPlayerMaxHP() - currentPlayerHP);
            if (healed > 0)
            {
                currentPlayerHP += healed;
                Log($"Régénération — +{healed} HP → {currentPlayerHP}/{GetPlayerMaxHP()}");
            }
        }

        // Décrémente tous les cooldowns d'un tour
        DecrementCooldowns();

        if (endTurnButton != null) endTurnButton.interactable = true;
        if (turnText      != null) turnText.text = "Tour du joueur";

        UpdateSkillButtons();

        // Réactive uniquement les consommables utilisables en combat (les autres restent grisés)
        foreach (ConsumableButton cb in spawnedConsumableButtons)
            cb.SetInteractable(cb.Consumable != null && cb.Consumable.usableInCombat);

        UpdateUI();

        // Notifie les modules abonnés au début du tour joueur
        GameEvents.TriggerPlayerTurnStarted();
    }

    private void StartEnemyTurn()
    {
        battleState = BattleState.EnemyTurn;

        // L'armure de l'ennemi se remet à 0 au début de son tour
        currentEnemyArmor = 0;

        if (endTurnButton != null) endTurnButton.interactable = false;
        if (turnText      != null) turnText.text = "Tour de l'ennemi";

        // Désactive tous les boutons de compétences et consommables pendant le tour ennemi
        foreach (SkillButton sb in spawnedSkillButtons)
            sb.SetInteractable(false);
        foreach (ConsumableButton cb in spawnedConsumableButtons)
            cb.SetInteractable(false);

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(EnemyActionDelay);

        // Statuts sur l'ennemi : effets automatiques (poison, etc.) en début de tour,
        // puis décroissance OnTurnStart. Les statuts OnTurnEnd décroissent après l'action ennemie.
        ApplyPerTurnEffects(forEnemy: true);
        DecayStatuses(forEnemy: true, StatusDecayTiming.OnTurnStart);
        UpdateUI();

        // Si l'ennemi est mort à cause de ses propres statuts, on ne va pas plus loin
        if (currentEnemyHP <= 0)
        {
            EndCombat(victory: true);
            yield break;
        }

        // Récupère et exécute la prochaine action de la file
        // Si la file est vide (toutes les actions épuisées), repli sur une attaque de base
        SkillData nextSkill = enemyAI != null && enemyAI.HasActions
            ? enemyAI.GetAndAdvanceAction()
            : null;

        if (nextSkill != null && nextSkill.effects != null && nextSkill.effects.Count > 0)
        {
            // L'ennemi utilise son skill — on applique les effets depuis son point de vue
            foreach (EffectData eff in nextSkill.effects)
                if (eff != null) ApplyEnemyEffect(eff, nextSkill.skillName);
        }
        else
        {
            // Repli : attaque de base si pas de skill ou pas d'effet défini
            // Formule : attaque de l'ennemi - défense du joueur (pas de valeur de coup de base)
            int enemyAtk  = enemyData != null ? enemyData.attack : 5;
            int rawDamage = Mathf.Max(1, enemyAtk - effectiveDefense);

            int armorAbsorbed  = Mathf.Min(currentPlayerArmor, rawDamage);
            int hpDamage       = rawDamage - armorAbsorbed;
            currentPlayerArmor = Mathf.Max(0, currentPlayerArmor - armorAbsorbed);
            currentPlayerHP    = Mathf.Max(0, currentPlayerHP    - hpDamage);

            string armorInfo = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
            Log($"{GetEnemyName()} attaque — {rawDamage} dégâts{armorInfo} → {currentPlayerHP}/{GetPlayerMaxHP()} HP");
        }

        UpdateUI();
        UpdateEnemyNextActionUI(); // met à jour l'affichage de la prochaine action

        if (currentPlayerHP <= 0)
        {
            EndCombat(victory: false);
            yield break;
        }

        // Décroissance en fin de tour de l'ennemi (debuffs OnTurnEnd sur l'ennemi)
        DecayStatuses(forEnemy: true, StatusDecayTiming.OnTurnEnd);

        StartPlayerTurn();
    }

    // -----------------------------------------------
    // ACTIONS DU JOUEUR
    // -----------------------------------------------

    /// <summary>
    /// Le joueur termine son tour manuellement.
    /// </summary>
    private void OnEndTurn()
    {
        if (battleState != BattleState.PlayerTurn) return;
        Log($"{GetPlayerName()} termine son tour.");

        // Décroissance en fin de tour du joueur (debuffs OnTurnEnd comme Affaiblissement)
        // Placé ici pour que les 3 tours annoncés soient 3 tours complets d'action
        DecayStatuses(forEnemy: false, StatusDecayTiming.OnTurnEnd);

        // Notifie les modules abonnés à la fin du tour joueur
        GameEvents.TriggerPlayerTurnEnded();

        StartEnemyTurn();
    }

    /// <summary>
    /// Utilise une compétence. Vérifie énergie et cooldown avant d'appliquer l'effet.
    /// </summary>
    private void UseSkill(SkillData skill)
    {
        if (battleState != BattleState.PlayerTurn) return;
        if (skill == null) return;

        // Vérifie le cooldown
        if (skillCooldowns.TryGetValue(skill, out int cd) && cd > 0)
        {
            Log($"{skill.skillName} est en cooldown ({cd} tours restants).");
            return;
        }

        // Vérifie l'énergie
        if (currentEnergy < skill.energyCost)
        {
            Log($"Pas assez d'énergie pour {skill.skillName} (coût : {skill.energyCost}).");
            return;
        }

        // Dépense l'énergie
        currentEnergy -= skill.energyCost;

        // Active le cooldown
        if (skill.cooldown > 0)
            skillCooldowns[skill] = skill.cooldown;

        // Applique les effets dans l'ordre
        if (skill.effects != null && skill.effects.Count > 0)
        {
            foreach (EffectData eff in skill.effects)
                if (eff != null) ApplyEffect(eff, skill.skillName);
        }
        else
            Log($"{GetPlayerName()} utilise {skill.skillName} — (aucun effet défini)");

        UpdateUI();
        UpdateSkillButtons();

        // Vérifie si l'ennemi est mort
        if (currentEnemyHP <= 0)
        {
            EndCombat(victory: true);
            return;
        }
    }

    /// <summary>
    /// Utilise un consommable pendant le tour du joueur.
    /// Applique son effet via ApplyConsumableEffect (valeurs brutes, sans stats de combat),
    /// le retire de RunManager, puis recrée les boutons.
    /// </summary>
    private void UseConsumable(ConsumableData consumable)
    {
        if (battleState != BattleState.PlayerTurn) return;
        if (consumable == null || !consumable.usableInCombat) return;

        if (consumable.effects != null && consumable.effects.Count > 0)
        {
            foreach (EffectData eff in consumable.effects)
                if (eff != null) ApplyConsumableEffect(eff, consumable.consumableName);
        }
        else
            Log($"{GetPlayerName()} utilise {consumable.consumableName} — (aucun effet défini)");

        RunManager.Instance?.RemoveConsumable(consumable);

        UpdateUI();

        // Vérifie si l'ennemi est mort (ex : consommable offensif)
        if (currentEnemyHP <= 0)
        {
            EndCombat(victory: true);
            return;
        }

        // Recrée les boutons depuis RunManager (le consommable utilisé n'y est plus)
        SpawnConsumableButtons();
    }

    // -----------------------------------------------
    // RÉSOLUTION DES EFFETS
    // -----------------------------------------------

    /// <summary>
    /// Applique un EffectData en combat.
    /// Pour l'instant, seules les actions DealDamage et Heal sont implémentées.
    /// Les autres (ApplyStatus, ModifyStat...) seront ajoutées progressivement.
    /// </summary>
    private void ApplyEffect(EffectData effect, string sourceName)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // Formule : (valeur du coup + attaque + modificateurs plats) × multiplicateurs - défense ennemie
                // La valeur de compétence est incluse dans la multiplication pour que les malus
                // de stat (ex : Affaiblissement en %) s'appliquent aussi aux dégâts de base.
                int enemyDef  = enemyData != null ? enemyData.defense : 0;
                int rawDamage = Mathf.Max(1, CalculerDegatsJoueur(effect.value) - enemyDef);

                // Mise à l'échelle par stacks (optionnel) — bonus appliqué avant le calcul du critique
                // secondaryValue = bonus de dégâts par stack du scalingStatus sur la cible (l'ennemi ici)
                string stackInfo = "";
                if (effect.scalingStatus != null)
                {
                    int stacks = GetStatusStacks(effect.scalingStatus, onEnemy: true);
                    if (stacks > 0)
                    {
                        int bonus = Mathf.RoundToInt(effect.secondaryValue * stacks);
                        rawDamage += bonus;

                        // "dont +12 Explo×6" → le bonus est inclus dans le total affiché, pas additionné
                        string consumeText = effect.consumeStacks ? " [consommés]" : "";
                        stackInfo = $", dont +{bonus} {effect.scalingStatus.statusName}×{stacks}{consumeText}";

                        if (effect.consumeStacks)
                            enemyStatuses.Remove(effect.scalingStatus);
                    }
                }

                // Coup critique : tirage aléatoire contre la chance de critique effective (avec modificateurs actifs)
                float critChance = GetCurrentCritChance();
                bool isCrit = critChance > 0f && Random.value < critChance;
                if (isCrit)
                    rawDamage = Mathf.RoundToInt(rawDamage * GetCurrentCritMultiplier());

                // Sécurité : les dégâts ne peuvent pas dépasser 9999 ni être négatifs
                rawDamage = Mathf.Clamp(rawDamage, 0, 9999);

                // L'armure absorbe les dégâts directs avant les HP
                int armorAbsorbed = Mathf.Min(currentEnemyArmor, rawDamage);
                int hpDamage      = rawDamage - armorAbsorbed;
                currentEnemyArmor = Mathf.Max(0, currentEnemyArmor - armorAbsorbed);
                currentEnemyHP    = Mathf.Max(0, currentEnemyHP    - hpDamage);

                string critInfo  = isCrit ? " [CRITIQUE !]" : "";
                string armorInfo = armorAbsorbed > 0 ? $", dont {armorAbsorbed} absorbés par l'armure" : "";
                // Le total affiché inclut déjà tous les bonus — stackInfo et armorInfo précisent la composition
                string detailInfo = (stackInfo.Length > 0 || armorInfo.Length > 0)
                    ? $" ({stackInfo.TrimStart(',', ' ')}{armorInfo})" : "";
                string logMsg = $"{GetPlayerName()} utilise {sourceName}{critInfo} — {rawDamage} dégâts{detailInfo} → {currentEnemyHP}/{GetEnemyMaxHP()} HP";

                // Notifie les modules abonnés aux dégâts infligés par le joueur
                if (hpDamage > 0)
                    GameEvents.TriggerPlayerDealtDamage(hpDamage);

                // Vol de vie : soigne le joueur d'une fraction des HP réellement infligés à l'ennemi
                // On ne vole que les HP perdus par l'ennemi, pas les dégâts absorbés par l'armure
                float lifeSteal = GetCurrentLifeSteal();
                if (lifeSteal > 0f && hpDamage > 0)
                {
                    int stolen = Mathf.Max(1, Mathf.RoundToInt(hpDamage * lifeSteal));
                    stolen = Mathf.Min(stolen, GetPlayerMaxHP() - currentPlayerHP);
                    if (stolen > 0)
                    {
                        currentPlayerHP += stolen;
                        logMsg += $" | Vol de vie +{stolen} HP";
                    }
                }

                Log(logMsg);
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
                // Le joueur gagne de l'armure (s'accumule dans le tour, repart à 0 au suivant)
                int armor = Mathf.RoundToInt(effect.value);
                currentPlayerArmor += armor;
                Log($"{GetPlayerName()} utilise {sourceName} — +{armor} armure → {currentPlayerArmor} armure totale");
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null)
                {
                    Log($"{GetPlayerName()} utilise {sourceName} — Aucun statut défini sur cet effet.");
                    break;
                }
                int stacks = Mathf.RoundToInt(effect.value);
                // Self → applique sur le joueur lui-même, sinon sur l'ennemi
                bool toEnemy = effect.target != EffectTarget.Self;
                ApplyStatus(effect.statusToApply, stacks, toEnemy);
                break;
            }

            case EffectAction.ModifyStat:
            {
                // Modificateur temporaire pour ce combat — s'accumule dans combatStatModifiers
                float val = effect.value;
                if (!combatStatModifiers.ContainsKey(effect.statToModify))
                    combatStatModifiers[effect.statToModify] = 0f;
                combatStatModifiers[effect.statToModify] += val;
                Log($"{GetPlayerName()} utilise {sourceName} — {effect.statToModify} " +
                    $"{(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                break;
            }

            case EffectAction.GainEnergy:
            {
                int gain     = Mathf.Max(0, Mathf.RoundToInt(effect.value));
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(gain, maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + gain, maxEnerg);
                Log($"{GetPlayerName()} utilise {sourceName} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            default:
                Log($"{GetPlayerName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté.");
                break;
        }
    }

    /// <summary>
    /// Applique un effet depuis le point de vue de l'ennemi.
    /// DealDamage → blesse le joueur. Heal → soigne l'ennemi.
    /// Séparé de ApplyEffect pour garder chaque méthode lisible et simple.
    /// </summary>
    private void ApplyEnemyEffect(EffectData effect, string sourceName)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // Formule : dégâts = valeur du coup + attaque ennemie - défense effective du joueur (avec modificateurs actifs)
                int enemyAtk  = enemyData != null ? enemyData.attack : 5;
                int rawDamage = Mathf.Max(1, Mathf.RoundToInt(effect.value) + enemyAtk - GetCurrentDefense());

                // Mise à l'échelle par stacks (optionnel) — stacks lus sur le joueur (la cible ici)
                string stackInfo = "";
                if (effect.scalingStatus != null)
                {
                    int stacks = GetStatusStacks(effect.scalingStatus, onEnemy: false);
                    if (stacks > 0)
                    {
                        int bonus = Mathf.RoundToInt(effect.secondaryValue * stacks);
                        rawDamage += bonus;

                        string consumeText = effect.consumeStacks ? " [consommés]" : "";
                        stackInfo = $", dont +{bonus} {effect.scalingStatus.statusName}×{stacks}{consumeText}";

                        if (effect.consumeStacks)
                            playerStatuses.Remove(effect.scalingStatus);
                    }
                }

                // Sécurité : les dégâts ne peuvent pas dépasser 9999 ni être négatifs
                rawDamage = Mathf.Clamp(rawDamage, 0, 9999);

                // L'armure du joueur absorbe les dégâts directs avant les HP
                int armorAbsorbed = Mathf.Min(currentPlayerArmor, rawDamage);
                int hpDamage      = rawDamage - armorAbsorbed;
                currentPlayerArmor = Mathf.Max(0, currentPlayerArmor - armorAbsorbed);
                currentPlayerHP    = Mathf.Max(0, currentPlayerHP    - hpDamage);

                string armorInfo  = armorAbsorbed > 0 ? $", dont {armorAbsorbed} absorbés par l'armure" : "";
                string detailInfo = (stackInfo.Length > 0 || armorInfo.Length > 0)
                    ? $" ({stackInfo.TrimStart(',', ' ')}{armorInfo})" : "";
                Log($"{GetEnemyName()} utilise {sourceName} — {rawDamage} dégâts{detailInfo} → {currentPlayerHP}/{GetPlayerMaxHP()} HP");

                // Notifie les modules abonnés aux dégâts reçus par le joueur
                if (hpDamage > 0)
                    GameEvents.TriggerPlayerDamaged(hpDamage);

                break;
            }

            case EffectAction.Heal:
            {
                int healed = Mathf.Min(Mathf.RoundToInt(effect.value), GetEnemyMaxHP() - currentEnemyHP);
                currentEnemyHP += healed;
                Log($"{GetEnemyName()} utilise {sourceName} — Soin de {healed} HP → {currentEnemyHP}/{GetEnemyMaxHP()}");
                break;
            }

            case EffectAction.AddArmor:
            {
                // L'ennemi gagne de l'armure
                int armor = Mathf.RoundToInt(effect.value);
                currentEnemyArmor += armor;
                Log($"{GetEnemyName()} utilise {sourceName} — +{armor} armure → {currentEnemyArmor} armure totale");
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null)
                {
                    Log($"{GetEnemyName()} utilise {sourceName} — Aucun statut défini sur cet effet.");
                    break;
                }
                int stacks = Mathf.RoundToInt(effect.value);
                // Ici "Self" = l'ennemi lui-même → toEnemy: true
                // "SingleEnemy" = le joueur (la cible de l'ennemi) → toEnemy: false
                bool toEnemy = effect.target == EffectTarget.Self;
                ApplyStatus(effect.statusToApply, stacks, toEnemy);
                break;
            }

            default:
                Log($"{GetEnemyName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté.");
                break;
        }
    }

    // -----------------------------------------------
    // EFFETS DE CONSOMMABLES
    // -----------------------------------------------

    /// <summary>
    /// Applique l'effet d'un consommable avec des valeurs brutes — sans les stats de combat.
    /// Contrairement à ApplyEffect() (compétences), les dégâts ne tiennent pas compte de
    /// l'attaque du joueur ni de la défense ennemie : une bombe fait toujours ses X dégâts,
    /// une potion soigne toujours ses X HP, quelle que soit la build du personnage.
    /// </summary>
    private void ApplyConsumableEffect(EffectData effect, string sourceName)
    {
        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // Dégâts bruts — ni attaque ajoutée, ni défense soustraite, ni critique
                int dmg = Mathf.Clamp(Mathf.RoundToInt(effect.value), 0, 9999);

                int armorAbsorbed = Mathf.Min(currentEnemyArmor, dmg);
                int hpDamage      = dmg - armorAbsorbed;
                currentEnemyArmor = Mathf.Max(0, currentEnemyArmor - armorAbsorbed);
                currentEnemyHP    = Mathf.Max(0, currentEnemyHP    - hpDamage);

                string armorInfo = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                Log($"{GetPlayerName()} utilise {sourceName} — {dmg} dégâts{armorInfo} → {currentEnemyHP}/{GetEnemyMaxHP()} HP");
                break;
            }

            case EffectAction.Heal:
            {
                // Soin brut — plafonné aux HP max
                int healed = Mathf.Min(Mathf.RoundToInt(effect.value), GetPlayerMaxHP() - currentPlayerHP);
                currentPlayerHP += healed;
                Log($"{GetPlayerName()} utilise {sourceName} — Soin de {healed} HP → {currentPlayerHP}/{GetPlayerMaxHP()}");
                break;
            }

            case EffectAction.AddArmor:
            {
                int armor = Mathf.RoundToInt(effect.value);
                currentPlayerArmor += armor;
                Log($"{GetPlayerName()} utilise {sourceName} — +{armor} armure → {currentPlayerArmor} armure totale");
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null)
                {
                    Log($"{GetPlayerName()} utilise {sourceName} — Aucun statut défini sur cet effet.");
                    break;
                }
                int stacks = Mathf.RoundToInt(effect.value);
                // Self → applique sur le joueur lui-même, sinon sur l'ennemi
                bool toEnemy = effect.target != EffectTarget.Self;
                ApplyStatus(effect.statusToApply, stacks, toEnemy);
                break;
            }

            case EffectAction.ModifyStat:
            {
                // Modificateur temporaire pour ce combat
                float val = effect.value;
                if (!combatStatModifiers.ContainsKey(effect.statToModify))
                    combatStatModifiers[effect.statToModify] = 0f;
                combatStatModifiers[effect.statToModify] += val;
                Log($"{GetPlayerName()} utilise {sourceName} — {effect.statToModify} " +
                    $"{(val >= 0 ? "+" : "")}{val:F1} (ce combat)");
                break;
            }

            case EffectAction.GainEnergy:
            {
                int gain     = Mathf.Max(0, Mathf.RoundToInt(effect.value));
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(gain, maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + gain, maxEnerg);
                Log($"{GetPlayerName()} utilise {sourceName} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            default:
                Log($"{GetPlayerName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté.");
                break;
        }
    }

    // -----------------------------------------------
    // EFFETS DE MODULES
    // -----------------------------------------------

    /// <summary>
    /// Applique l'effet d'un module passif ou déclenché.
    /// Appelé par ModuleManager quand un trigger correspondant est reçu.
    ///
    /// La cible est lue depuis EffectData.target :
    ///   - Self                               → cible le joueur
    ///   - SingleEnemy / AllEnemies / Random  → cible l'ennemi
    ///
    /// Les dégâts sont bruts — pas d'ATK ajoutée, pas de critique.
    /// </summary>
    public void ApplyModuleEffect(EffectData effect, string moduleName)
    {
        if (effect == null) return;

        string source    = $"[Module] {moduleName}";
        bool targetsSelf = effect.target == EffectTarget.Self;

        switch (effect.action)
        {
            case EffectAction.DealDamage:
            {
                // DealDamage cible toujours l'ennemi
                int dmg = Mathf.Clamp(Mathf.RoundToInt(effect.value), 0, 9999);

                int armorAbsorbed = Mathf.Min(currentEnemyArmor, dmg);
                int hpDamage      = dmg - armorAbsorbed;
                currentEnemyArmor = Mathf.Max(0, currentEnemyArmor - armorAbsorbed);
                currentEnemyHP    = Mathf.Max(0, currentEnemyHP    - hpDamage);

                string armorInfo = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                Log($"{source} — {dmg} dégâts{armorInfo} → {currentEnemyHP}/{GetEnemyMaxHP()} HP");
                break;
            }

            case EffectAction.Heal:
            {
                if (targetsSelf)
                {
                    int healed = Mathf.Min(Mathf.RoundToInt(effect.value), GetPlayerMaxHP() - currentPlayerHP);
                    if (healed > 0)
                    {
                        currentPlayerHP += healed;
                        Log($"{source} — +{healed} HP joueur → {currentPlayerHP}/{GetPlayerMaxHP()}");
                    }
                }
                else
                {
                    int healed = Mathf.Min(Mathf.RoundToInt(effect.value), GetEnemyMaxHP() - currentEnemyHP);
                    if (healed > 0)
                    {
                        currentEnemyHP += healed;
                        Log($"{source} — +{healed} HP ennemi → {currentEnemyHP}/{GetEnemyMaxHP()}");
                    }
                }
                break;
            }

            case EffectAction.AddArmor:
            {
                int armor = Mathf.RoundToInt(effect.value);
                if (targetsSelf)
                {
                    currentPlayerArmor += armor;
                    Log($"{source} — +{armor} armure joueur → {currentPlayerArmor} armure totale");
                }
                else
                {
                    currentEnemyArmor += armor;
                    Log($"{source} — +{armor} armure ennemi → {currentEnemyArmor} armure totale");
                }
                break;
            }

            case EffectAction.ApplyStatus:
            {
                if (effect.statusToApply == null)
                {
                    Log($"{source} — Aucun statut défini sur cet effet.");
                    break;
                }
                int stacks = Mathf.RoundToInt(effect.value);
                // Self = applique sur le joueur, sinon sur l'ennemi
                ApplyStatus(effect.statusToApply, stacks, toEnemy: !targetsSelf);
                break;
            }

            case EffectAction.GainEnergy:
            {
                int gain     = Mathf.Max(0, Mathf.RoundToInt(effect.value));
                int maxEnerg = GetCurrentMaxEnergy();
                int gained   = Mathf.Min(gain, maxEnerg - currentEnergy);
                currentEnergy = Mathf.Min(currentEnergy + gain, maxEnerg);
                Log($"{source} — +{gained} énergie → {currentEnergy}/{maxEnerg}");
                break;
            }

            case EffectAction.ModifyStat:
            {
                // Les modules ajoutent un bonus PERMANENT sur le run (via RunManager)
                // Le bonus sera intégré aux stats effectives au prochain ResolveEquipment()
                if (RunManager.Instance != null)
                {
                    RunManager.Instance.AddStatBonus(effect.statToModify, effect.value);
                    // Applique aussi immédiatement à la stat effective de ce combat
                    switch (effect.statToModify)
                    {
                        case StatType.Attack:             effectiveAttack             += Mathf.RoundToInt(effect.value); break;
                        case StatType.Defense:            effectiveDefense            += Mathf.RoundToInt(effect.value); break;
                        case StatType.MaxHP:              effectiveMaxHP              += Mathf.RoundToInt(effect.value); break;
                        case StatType.CriticalChance:     effectiveCriticalChance     = Mathf.Clamp01(effectiveCriticalChance + effect.value); break;
                        case StatType.CriticalMultiplier: effectiveCriticalMultiplier += effect.value; break;
                        case StatType.LifeSteal:          effectiveLifeSteal          = Mathf.Clamp01(effectiveLifeSteal + effect.value); break;
                        case StatType.MaxEnergy:          effectiveMaxEnergy          += Mathf.RoundToInt(effect.value); break;
                    }
                    Log($"{source} — {effect.statToModify} {(effect.value >= 0 ? "+" : "")}{effect.value:F1} (permanent run)");
                }
                break;
            }

            default:
                Log($"{source} — Effet '{effect.action}' non encore implémenté pour les modules.");
                break;
        }

        // Met à jour l'UI et vérifie si l'ennemi est mort suite à l'effet du module
        UpdateUI();
        if (currentEnemyHP <= 0 && battleState == BattleState.PlayerTurn)
            EndCombat(victory: true);
    }

    // -----------------------------------------------
    // STATUTS
    // -----------------------------------------------

    /// <summary>
    /// Applique un nombre de stacks d'un statut à une entité (joueur ou ennemi).
    /// Respecte le plafond maxStacks si défini (> 0 dans StatusData).
    /// </summary>
    private void ApplyStatus(StatusData status, int stacks, bool toEnemy)
    {
        if (status == null || stacks <= 0) return;

        Dictionary<StatusData, int> target = toEnemy ? enemyStatuses : playerStatuses;
        string entityName = toEnemy ? GetEnemyName() : GetPlayerName();

        if (!target.ContainsKey(status))
            target[status] = 0;

        target[status] += stacks;

        // Plafonne les stacks si maxStacks est défini
        if (status.maxStacks > 0)
            target[status] = Mathf.Min(target[status], status.maxStacks);

        Log($"{entityName} reçoit {stacks} stack(s) de {status.statusName} " +
            $"→ {target[status]} stack(s) au total");
    }

    /// <summary>
    /// Retourne le nombre de stacks actifs d'un statut sur une entité.
    /// Renvoie 0 si le statut n'est pas présent.
    /// </summary>
    private int GetStatusStacks(StatusData status, bool onEnemy)
    {
        if (status == null) return 0;
        Dictionary<StatusData, int> source = onEnemy ? enemyStatuses : playerStatuses;
        return source.TryGetValue(status, out int stacks) ? stacks : 0;
    }

    /// <summary>
    /// Exécute les effets automatiques de comportement PerTurnStart (poison, soin par tour, etc.)
    /// pour une entité. N'applique PAS la décroissance — voir DecayStatuses().
    ///
    /// Les statuts StackOnly et ModifyStat sont ignorés ici : ils n'ont pas d'effet automatique,
    /// ils sont consultés à la demande (StackOnly) ou calculés dynamiquement (ModifyStat).
    /// </summary>
    private void ApplyPerTurnEffects(bool forEnemy)
    {
        Dictionary<StatusData, int> statuses = forEnemy ? enemyStatuses : playerStatuses;
        if (statuses.Count == 0) return;

        List<StatusData> keys = new List<StatusData>(statuses.Keys);

        foreach (StatusData status in keys)
        {
            if (!statuses.ContainsKey(status)) continue;
            int stacks = statuses[status];
            if (stacks <= 0) continue;
            if (status.behavior != StatusBehavior.PerTurnStart) continue;

            float totalEffect = status.effectPerStack * stacks;
            string entityName = forEnemy ? GetEnemyName() : GetPlayerName();

            switch (status.perTurnAction)
            {
                case EffectAction.DealDamage:
                {
                    // Dégâts bruts — ignorent l'armure (comportement type poison dans Slay the Spire)
                    int dmg = Mathf.Max(1, Mathf.RoundToInt(totalEffect));
                    if (forEnemy)
                        currentEnemyHP = Mathf.Max(0, currentEnemyHP - dmg);
                    else
                        currentPlayerHP = Mathf.Max(0, currentPlayerHP - dmg);

                    Log($"{entityName} subit {dmg} dégâts de {status.statusName} ({stacks} stack(s))");
                    break;
                }

                case EffectAction.Heal:
                {
                    int healed;
                    if (forEnemy)
                    {
                        healed = Mathf.Min(Mathf.RoundToInt(totalEffect), GetEnemyMaxHP() - currentEnemyHP);
                        currentEnemyHP += healed;
                    }
                    else
                    {
                        healed = Mathf.Min(Mathf.RoundToInt(totalEffect), GetPlayerMaxHP() - currentPlayerHP);
                        currentPlayerHP += healed;
                    }
                    Log($"{entityName} récupère {healed} HP grâce à {status.statusName} ({stacks} stack(s))");
                    break;
                }

                default:
                    Log($"{status.statusName} — action automatique '{status.perTurnAction}' non implémentée.");
                    break;
            }
        }
    }

    /// <summary>
    /// Applique la décroissance (decayPerTurn) des statuts d'une entité dont le timing
    /// correspond au paramètre. Appelé deux fois par cycle complet :
    ///   - En début de tour (timing OnTurnStart) : comportement par défaut — pour le poison, etc.
    ///   - En fin de tour  (timing OnTurnEnd)    : pour les debuffs qui durent le nombre de tours annoncé.
    ///
    /// La durée effective d'un statut OnTurnEnd appliqué au tour T est T, T+1, T+2 ... complets.
    /// </summary>
    private void DecayStatuses(bool forEnemy, StatusDecayTiming timing)
    {
        Dictionary<StatusData, int> statuses = forEnemy ? enemyStatuses : playerStatuses;
        if (statuses.Count == 0) return;

        List<StatusData> keys = new List<StatusData>(statuses.Keys);

        foreach (StatusData status in keys)
        {
            if (!statuses.ContainsKey(status)) continue;
            if (status.decayPerTurn <= 0) continue;
            if (status.decayTiming != timing) continue;

            int stacks = statuses[status];
            if (stacks <= 0) continue;

            statuses[status] = Mathf.Max(0, stacks - status.decayPerTurn);
            if (statuses[status] == 0)
            {
                string entityName = forEnemy ? GetEnemyName() : GetPlayerName();
                Log($"{entityName} n'a plus de {status.statusName}");
            }
        }

        // Nettoie les entrées à 0 stacks
        List<StatusData> toRemove = new List<StatusData>();
        foreach (var kvp in statuses)
            if (kvp.Value <= 0) toRemove.Add(kvp.Key);
        foreach (StatusData key in toRemove)
            statuses.Remove(key);
    }

    // -----------------------------------------------
    // STATS DYNAMIQUES (effectives + modificateurs actifs)
    // -----------------------------------------------

    /// <summary>
    /// Calcule les dégâts bruts du joueur (avant défense ennemie et critique) en incluant
    /// la valeur de compétence dans le calcul multiplicatif.
    ///
    /// Formule : (skillValue + effectiveAttack + modificateurs plats) × (1 + modificateurs %)
    ///
    /// Inclure skillValue dans la multiplication garantit que les malus en pourcentage
    /// (ex : Affaiblissement −50%) s'appliquent aussi aux dégâts de base de la compétence,
    /// et pas uniquement au bonus d'attaque.
    /// </summary>
    private int CalculerDegatsJoueur(float skillValue)
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.Attack);
        return Mathf.Max(0, Mathf.RoundToInt((skillValue + effectiveAttack + flat) * (1f + pct)));
    }

    /// <summary>
    /// Calcule les modificateurs plats et en pourcentage actifs pour une stat du joueur.
    /// Somme les combatStatModifiers (skills/consommables) ET les statuts ModifyStat actifs.
    /// </summary>
    private (float flat, float pct) GetPlayerStatModifiers(StatType stat)
    {
        float flat = 0f, pct = 0f;

        // Modificateurs directs (skills, consommables en combat)
        if (combatStatModifiers.TryGetValue(stat, out float directMod))
            flat += directMod;

        // Statuts actifs de type ModifyStat sur le joueur
        foreach (var kvp in playerStatuses)
        {
            StatusData status = kvp.Key;
            int stacks = kvp.Value;
            if (stacks <= 0 || status == null) continue;
            if (status.behavior != StatusBehavior.ModifyStat) continue;
            if (status.statToModify != stat) continue;

            // valueScalesWithStacks = true  → valeur variable (cas 3)
            // valueScalesWithStacks = false → valeur fixe, stacks = durée (cas 2)
            float amount = status.valueScalesWithStacks
                ? status.effectPerStack * stacks
                : status.effectPerStack;

            if (status.statModifierType == StatModifierType.Percentage)
                pct += amount;
            else
                flat += amount;
        }

        return (flat, pct);
    }

    /// <summary>Attaque effective incluant les modificateurs actifs du combat.</summary>
    private int GetCurrentAttack()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.Attack);
        return Mathf.Max(0, Mathf.RoundToInt((effectiveAttack + flat) * (1f + pct)));
    }

    /// <summary>Défense effective incluant les modificateurs actifs du combat.</summary>
    private int GetCurrentDefense()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.Defense);
        return Mathf.Max(0, Mathf.RoundToInt((effectiveDefense + flat) * (1f + pct)));
    }

    /// <summary>Chance de critique effective incluant les modificateurs actifs du combat.</summary>
    private float GetCurrentCritChance()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.CriticalChance);
        return Mathf.Clamp01((effectiveCriticalChance + flat) * (1f + pct));
    }

    /// <summary>Multiplicateur de critique effectif incluant les modificateurs actifs du combat.</summary>
    private float GetCurrentCritMultiplier()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.CriticalMultiplier);
        return Mathf.Max(1f, (effectiveCriticalMultiplier + flat) * (1f + pct));
    }

    /// <summary>Vol de vie effectif incluant les modificateurs actifs du combat.</summary>
    private float GetCurrentLifeSteal()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.LifeSteal);
        return Mathf.Clamp01((effectiveLifeSteal + flat) * (1f + pct));
    }

    /// <summary>Énergie max effective incluant les modificateurs actifs du combat.</summary>
    private int GetCurrentMaxEnergy()
    {
        var (flat, pct) = GetPlayerStatModifiers(StatType.MaxEnergy);
        return Mathf.Max(1, Mathf.RoundToInt((effectiveMaxEnergy + flat) * (1f + pct)));
    }

    // -----------------------------------------------
    // COOLDOWNS
    // -----------------------------------------------

    /// <summary>
    /// Décrémente tous les cooldowns actifs de 1 au début du tour joueur.
    /// </summary>
    private void DecrementCooldowns()
    {
        // On ne peut pas modifier un Dictionary pendant qu'on l'itère —
        // on collecte d'abord les clés, puis on les modifie.
        List<SkillData> keys = new List<SkillData>(skillCooldowns.Keys);
        foreach (SkillData skill in keys)
        {
            if (skillCooldowns[skill] > 0)
                skillCooldowns[skill]--;
        }
    }

    // -----------------------------------------------
    // FIN DE COMBAT
    // -----------------------------------------------

    private void OnVictoireCheat()
    {
        currentEnemyHP = 0;
        UpdateUI();
        Log("(Cheat) Ennemi éliminé instantanément.");
        EndCombat(victory: true);
    }

    private void EndCombat(bool victory)
    {
        battleState = victory ? BattleState.Victory : BattleState.Defeat;

        if (endTurnButton != null) endTurnButton.interactable = false;
        foreach (SkillButton sb in spawnedSkillButtons)
            sb.SetInteractable(false);
        foreach (ConsumableButton cb in spawnedConsumableButtons)
            cb.SetInteractable(false);

        if (combatActivePanel != null) combatActivePanel.SetActive(false);

        if (victory)
        {
            // Notifie les modules abonnés à la mort de l'ennemi
            GameEvents.TriggerEnemyDied();

            // Victoire → panel de loot avant de continuer
            ShowLootPanel();
            Log("Combat terminé — Victoire !");
        }
        else
        {
            // Défaite → panel de fin classique
            if (endPanel      != null) endPanel.SetActive(true);
            if (endTitleText  != null) endTitleText.text  = "Défaite...";
            if (endButtonText != null) endButtonText.text = "Retour au menu";
            Log("Combat terminé — Défaite.");
        }
    }

    // Appelé par le bouton "Continuer" du panel de loot (victoire)
    private void OnLootContinueClicked()
    {
        if (RunManager.Instance != null)
        {
            RunManager.Instance.currentHP = currentPlayerHP;
            RunManager.Instance.maxHP     = GetPlayerMaxHP();
            RunManager.Instance.ClearCurrentRoom();
        }
        SceneLoader.Instance.GoToNavigation();
    }

    // Appelé par le bouton du panel de fin (défaite uniquement)
    private void OnEndButtonClicked()
    {
        if (battleState == BattleState.Defeat)
        {
            // Signaler au RunManager que la run est terminée
            // pour que le MainMenu grise correctement "Continuer"
            RunManager.Instance?.EndRun();
            SceneLoader.Instance.GoToMainMenu();
        }
    }

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    /// <summary>
    /// Affiche le panel de loot avec 2-3 pièces tirées au hasard dans le lootPool de l'ennemi.
    /// Délègue l'affichage des cartes et la résolution à EquipmentOfferController.
    /// Le bouton "Continuer" est toujours actif : le joueur peut ignorer le loot.
    /// </summary>
    private void ShowLootPanel()
    {
        if (lootPanel == null) return;
        lootPanel.SetActive(true);

        // Tente d'accorder un consommable depuis le pool de l'ennemi (si slot libre)
        // Fait avant l'affichage des cartes pour que le log soit cohérent
        TryGrantConsumableLoot();

        // Le bouton "Continuer" est toujours visible et cliquable dès l'ouverture du panel.
        // SetActive(true) est nécessaire en plus de interactable : si le bouton est enfant de
        // EquipmentOfferArea (désactivé par Awake), il reste invisible sans ce SetActive explicite.
        if (lootContinueButton != null)
        {
            lootContinueButton.gameObject.SetActive(true);
            lootContinueButton.interactable = true;
        }

        List<EquipmentData> offers = PickLootOffers();
        if (offers.Count == 0 || equipmentOfferController == null) return;

        // Délègue l'affichage et la résolution au controller partagé
        // Callback null : "Continuer" était déjà actif, aucune action supplémentaire requise
        equipmentOfferController.StartOffresSimultanées(offers, null);
    }

    /// <summary>
    /// Tire aléatoirement lootOfferCount pièces dans le lootPool de l'ennemi.
    /// Si le pool contient moins de pièces que le nombre demandé, toutes sont retournées.
    /// </summary>
    private List<EquipmentData> PickLootOffers()
    {
        if (enemyData == null || enemyData.lootPool == null || enemyData.lootPool.Count == 0)
            return new List<EquipmentData>();

        // Copie du pool pour pouvoir le mélanger sans le modifier
        List<EquipmentData> pool = new List<EquipmentData>(enemyData.lootPool);

        // Mélange Fisher-Yates
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        int count = Mathf.Min(enemyData.lootOfferCount, pool.Count);
        return pool.GetRange(0, count);
    }

    /// <summary>
    /// Accorde automatiquement un consommable aléatoire tiré dans consumableLootPool de l'ennemi,
    /// mais uniquement si le joueur a un slot libre.
    /// Un seul consommable est accordé par victoire — pas de choix proposé au joueur.
    /// </summary>
    private void TryGrantConsumableLoot()
    {
        if (enemyData == null
            || enemyData.consumableLootPool == null
            || enemyData.consumableLootPool.Count == 0) return;

        if (RunManager.Instance == null || !RunManager.Instance.HasConsumableSlotFree()) return;

        // Tire un consommable aléatoire dans le pool
        int index = Random.Range(0, enemyData.consumableLootPool.Count);
        ConsumableData granted = enemyData.consumableLootPool[index];
        if (granted == null) return;

        bool added = RunManager.Instance.AddConsumable(granted);
        if (added)
            Log($"Consommable obtenu : {granted.consumableName}");
    }


    // -----------------------------------------------
    // MISE À JOUR DE L'UI
    // -----------------------------------------------

    private void UpdateUI()
    {
        if (playerNameText  != null) playerNameText.text  = GetPlayerName();
        if (playerHPText    != null) playerHPText.text    = $"HP : {currentPlayerHP} / {GetPlayerMaxHP()}";

        // Armure joueur : affiche uniquement si > 0, ou "Armure : 0" si le champ est assigné
        if (playerArmorText != null) playerArmorText.text = currentPlayerArmor > 0
            ? $"Armure : {currentPlayerArmor}"
            : "";

        if (enemyNameText   != null) enemyNameText.text   = GetEnemyName();
        if (enemyHPText     != null) enemyHPText.text     = $"HP : {currentEnemyHP} / {GetEnemyMaxHP()}";

        if (enemyArmorText  != null) enemyArmorText.text  = currentEnemyArmor > 0
            ? $"Armure : {currentEnemyArmor}"
            : "";

        if (energyText != null) energyText.text = $"Énergie : {currentEnergy} / {GetCurrentMaxEnergy()}";
    }

    /// <summary>
    /// Met à jour l'état de chaque bouton de compétence :
    /// - Désactivé si cooldown > 0 ou énergie insuffisante ou ce n'est pas le tour du joueur
    /// </summary>
    private void UpdateSkillButtons()
    {
        foreach (SkillButton sb in spawnedSkillButtons)
        {
            if (sb.Skill == null) continue;

            int cd = skillCooldowns.TryGetValue(sb.Skill, out int val) ? val : 0;
            sb.SetCooldown(cd);

            bool canUse = battleState == BattleState.PlayerTurn
                       && cd == 0
                       && currentEnergy >= sb.Skill.energyCost;

            sb.SetInteractable(canUse);
        }
    }

    /// <summary>
    /// Affiche la prochaine action que l'ennemi va exécuter.
    /// Utilise PeekNextSkill() pour lire la tête de file sans l'avancer.
    /// </summary>
    private void UpdateEnemyNextActionUI()
    {
        if (enemyNextActionText == null) return;

        SkillData next = enemyAI != null ? enemyAI.PeekNextSkill() : null;
        enemyNextActionText.text = next != null
            ? $"Prochain : {next.skillName}"
            : "Prochain : Attaque de base";
    }

    private void Log(string message)
    {
        Debug.Log($"[Combat] {message}");
        if (combatLogText != null) combatLogText.text = message;
    }

    // -----------------------------------------------
    // UTILITAIRES
    // -----------------------------------------------

    private string GetPlayerName()  => characterData != null ? characterData.characterName : "Joueur";
    private string GetEnemyName()   => enemyData     != null ? enemyData.enemyName         : "Ennemi";
    private int    GetPlayerMaxHP() => effectiveMaxHP;   // stat effective, pas la base brute
    private int    GetEnemyMaxHP()  => enemyData != null ? enemyData.maxHP : 30;
}
