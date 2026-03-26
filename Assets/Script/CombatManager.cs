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

    // Conteneur des cartes d'équipement (ex : Horizontal Layout Group)
    public Transform lootCardContainer;

    // Préfab d'une carte — doit avoir le composant LootCard
    public GameObject lootCardPrefab;

    // Bouton "Continuer" du panel loot — actif dès qu'une pièce est choisie (ou si aucun loot)
    public Button lootContinueButton;

    // Sous-panel de choix du slot bras (visible uniquement quand les deux bras sont occupés)
    public GameObject armSelectionPanel;
    public Button     armSelectArm1Button; // "Bras gauche"
    public Button     armSelectArm2Button; // "Bras droit"

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
    private int effectiveMaxHP;
    private int effectiveAttack;
    private int effectiveDefense;
    private int effectiveMaxEnergy;

    // Skills disponibles en combat : collectés depuis l'équipement, ou startingSkills en fallback
    private List<SkillData> availableSkills = new List<SkillData>();

    // Instance de l'IA ennemie — gère la file d'actions circulaire
    private EnemyAI enemyAI;

    // Liste des boutons de compétences instanciés — on les garde pour les mettre à jour
    private List<SkillButton> spawnedSkillButtons = new List<SkillButton>();

    // Cooldowns par compétence : nombre de tours restants avant de pouvoir l'utiliser
    private Dictionary<SkillData, int> skillCooldowns = new Dictionary<SkillData, int>();

    private const float EnemyActionDelay = 1.0f;

    // Cartes de loot instanciées — on les garde pour gérer la sélection visuelle
    private List<LootCard> spawnedLootCards = new List<LootCard>();

    // La pièce que le joueur a sélectionnée (avant confirmation du slot pour les bras)
    private EquipmentData pendingLootChoice;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        if (endTurnButton      != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (endButton          != null) endButton.onClick.AddListener(OnEndButtonClicked);
        if (victoireButton     != null) victoireButton.onClick.AddListener(OnVictoireCheat);
        if (lootContinueButton != null) lootContinueButton.onClick.AddListener(OnLootContinueClicked);
        if (armSelectArm1Button != null)
            armSelectArm1Button.onClick.AddListener(() => ConfirmArmPlacement(EquipmentSlot.Arm1));
        if (armSelectArm2Button != null)
            armSelectArm2Button.onClick.AddListener(() => ConfirmArmPlacement(EquipmentSlot.Arm2));

        if (endPanel          != null) endPanel.SetActive(false);
        if (lootPanel         != null) lootPanel.SetActive(false);
        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);

        InitializeCombat();
    }

    private void InitializeCombat()
    {
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
            effectiveMaxHP     = 100;
            effectiveAttack    = 10;
            effectiveDefense   = 0;
            effectiveMaxEnergy = 3;
            return;
        }

        // On part des stats de base du personnage
        effectiveMaxHP     = characterData.maxHP;
        effectiveAttack    = characterData.attack;
        effectiveDefense   = characterData.defense;
        effectiveMaxEnergy = characterData.maxEnergy;

        availableSkills.Clear();
        bool hasEquipmentSkills = false;

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
            effectiveMaxHP   += equip.bonusHP;
            effectiveAttack  += equip.bonusAttack;
            effectiveDefense += equip.bonusDefense;
            // Les autres bonus (crit, regen, lifesteal...) seront exploités plus tard

            // Collecte les skills de cette pièce
            foreach (SkillData skill in equip.skills)
            {
                if (skill != null)
                {
                    availableSkills.Add(skill);
                    hasEquipmentSkills = true;
                }
            }
        }

        // Fallback : si aucun équipement ne fournit de skills,
        // on utilise les startingSkills directement définis sur le CharacterData
        if (!hasEquipmentSkills)
        {
            foreach (SkillData skill in characterData.startingSkills)
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
        }

        Debug.Log($"[Combat] Équipement résolu — HP: {effectiveMaxHP}, ATK: {effectiveAttack}, " +
                  $"DEF: {effectiveDefense}, Énergie: {effectiveMaxEnergy}, " +
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
    /// </summary>
    private void SpawnSkillButtons()
    {
        if (skillButtonPrefab == null || skillButtonContainer == null) return;

        foreach (SkillData skill in availableSkills)
        {
            if (skill == null) continue;

            GameObject go = Instantiate(skillButtonPrefab, skillButtonContainer);
            SkillButton sb = go.GetComponent<SkillButton>();
            if (sb == null) continue;

            sb.Setup(skill, UseSkill);
            spawnedSkillButtons.Add(sb);
        }
    }

    // -----------------------------------------------
    // MACHINE À ÉTATS — TRANSITIONS
    // -----------------------------------------------

    private void StartPlayerTurn()
    {
        battleState = BattleState.PlayerTurn;

        // Réinitialise l'énergie au maximum effectif (base + bonus équipement)
        currentEnergy = effectiveMaxEnergy;

        // L'armure du joueur se remet à 0 au début de son tour (comme dans Slay the Spire)
        currentPlayerArmor = 0;

        // Décrémente tous les cooldowns d'un tour
        DecrementCooldowns();

        if (endTurnButton != null) endTurnButton.interactable = true;
        if (turnText      != null) turnText.text = "Tour du joueur";

        UpdateSkillButtons();
        UpdateUI();
    }

    private void StartEnemyTurn()
    {
        battleState = BattleState.EnemyTurn;

        // L'armure de l'ennemi se remet à 0 au début de son tour
        currentEnemyArmor = 0;

        if (endTurnButton != null) endTurnButton.interactable = false;
        if (turnText      != null) turnText.text = "Tour de l'ennemi";

        // Désactive tous les boutons de compétences pendant le tour ennemi
        foreach (SkillButton sb in spawnedSkillButtons)
            sb.SetInteractable(false);

        StartCoroutine(EnemyTurnRoutine());
    }

    private IEnumerator EnemyTurnRoutine()
    {
        yield return new WaitForSeconds(EnemyActionDelay);

        // Récupère et exécute la prochaine action de la file
        // Si la file est vide (toutes les actions épuisées), repli sur une attaque de base
        SkillData nextSkill = enemyAI != null && enemyAI.HasActions
            ? enemyAI.GetAndAdvanceAction()
            : null;

        if (nextSkill != null && nextSkill.effect != null)
        {
            // L'ennemi utilise son skill — on applique l'effet depuis son point de vue
            ApplyEnemyEffect(nextSkill.effect, nextSkill.skillName);
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

        // Applique l'effet
        if (skill.effect != null)
            ApplyEffect(skill.effect, skill.skillName);
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
                // Formule : dégâts = valeur du coup + attaque du lanceur - défense de la cible
                int enemyDef = enemyData != null ? enemyData.defense : 0;
                int rawDamage = Mathf.Max(1, Mathf.RoundToInt(effect.value) + effectiveAttack - enemyDef);

                // L'armure absorbe les dégâts directs avant les HP
                int armorAbsorbed = Mathf.Min(currentEnemyArmor, rawDamage);
                int hpDamage      = rawDamage - armorAbsorbed;
                currentEnemyArmor = Mathf.Max(0, currentEnemyArmor - armorAbsorbed);
                currentEnemyHP    = Mathf.Max(0, currentEnemyHP    - hpDamage);

                string armorInfo = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                Log($"{GetPlayerName()} utilise {sourceName} — {rawDamage} dégâts{armorInfo} → {currentEnemyHP}/{GetEnemyMaxHP()} HP");
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
                // Formule : dégâts = valeur du coup + attaque du lanceur - défense de la cible
                int enemyAtk  = enemyData != null ? enemyData.attack : 5;
                int rawDamage = Mathf.Max(1, Mathf.RoundToInt(effect.value) + enemyAtk - effectiveDefense);

                // L'armure du joueur absorbe les dégâts directs avant les HP
                int armorAbsorbed = Mathf.Min(currentPlayerArmor, rawDamage);
                int hpDamage      = rawDamage - armorAbsorbed;
                currentPlayerArmor = Mathf.Max(0, currentPlayerArmor - armorAbsorbed);
                currentPlayerHP    = Mathf.Max(0, currentPlayerHP    - hpDamage);

                string armorInfo = armorAbsorbed > 0 ? $" (dont {armorAbsorbed} absorbés par l'armure)" : "";
                Log($"{GetEnemyName()} utilise {sourceName} — {rawDamage} dégâts{armorInfo} → {currentPlayerHP}/{GetPlayerMaxHP()} HP");
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

            default:
                Log($"{GetEnemyName()} utilise {sourceName} — Effet '{effect.action}' non encore implémenté.");
                break;
        }
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

        if (combatActivePanel != null) combatActivePanel.SetActive(false);

        if (victory)
        {
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
            SceneLoader.Instance.GoToMainMenu();
        }
    }

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    /// <summary>
    /// Affiche le panel de loot avec 2-3 pièces tirées au hasard dans le lootPool de l'ennemi.
    /// Si le pool est vide, active directement le bouton "Continuer".
    /// </summary>
    private void ShowLootPanel()
    {
        if (lootPanel == null) return;
        lootPanel.SetActive(true);

        // Nettoie les cartes précédentes (au cas où)
        foreach (LootCard card in spawnedLootCards)
            if (card != null) Destroy(card.gameObject);
        spawnedLootCards.Clear();
        pendingLootChoice = null;

        // Tire les pièces à proposer
        List<EquipmentData> offers = PickLootOffers();

        if (offers.Count == 0)
        {
            // Aucun loot disponible — on laisse le joueur continuer directement
            if (lootContinueButton != null) lootContinueButton.interactable = true;
            return;
        }

        // Le bouton "Continuer" commence désactivé : le joueur doit faire un choix
        // (ou on peut le laisser actif si on veut permettre de passer le loot)
        if (lootContinueButton != null) lootContinueButton.interactable = false;

        // Instancie une carte par offre
        foreach (EquipmentData equip in offers)
        {
            if (lootCardPrefab == null || lootCardContainer == null) break;

            GameObject go = Instantiate(lootCardPrefab, lootCardContainer);
            LootCard card = go.GetComponent<LootCard>();
            if (card == null) continue;

            card.Setup(equip, OnLootCardChosen);
            spawnedLootCards.Add(card);
        }
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
    /// Appelé quand le joueur clique sur une carte de loot.
    /// - Pièce non-bras : équipe immédiatement dans son slot.
    /// - Pièce bras + au moins un slot libre : équipe dans le slot libre.
    /// - Pièce bras + les deux slots occupés : affiche le sous-panel de choix.
    /// </summary>
    private void OnLootCardChosen(EquipmentData chosen)
    {
        // Mise à jour visuelle : sélectionne la carte cliquée, déselectionne les autres
        foreach (LootCard card in spawnedLootCards)
            card.SetSelected(card.Equipment == chosen);

        pendingLootChoice = chosen;

        bool isArm = chosen.equipmentType == EquipmentType.Arm;

        if (!isArm)
        {
            // Slot unique (Tête, Torse, Jambes) → équipe directement dans le slot correspondant
            FinalizeEquip(EquipmentTypeToSlot(chosen.equipmentType), chosen);
        }
        else
        {
            // Pièce de bras : cherche un slot libre en priorité
            bool arm1Free = RunManager.Instance == null || RunManager.Instance.IsSlotFree(EquipmentSlot.Arm1);
            bool arm2Free = RunManager.Instance == null || RunManager.Instance.IsSlotFree(EquipmentSlot.Arm2);

            if (arm1Free)
                FinalizeEquip(EquipmentSlot.Arm1, chosen);
            else if (arm2Free)
                FinalizeEquip(EquipmentSlot.Arm2, chosen);
            else
                ShowArmSelectionPanel(); // les deux bras sont occupés → demander au joueur
        }
    }

    /// <summary>
    /// Affiche le sous-panel permettant de choisir quel bras remplacer.
    /// </summary>
    private void ShowArmSelectionPanel()
    {
        if (armSelectionPanel == null) return;
        armSelectionPanel.SetActive(true);

        // Affiche le nom de la pièce actuellement équipée sur chaque bouton
        if (armSelectArm1Button != null)
        {
            var label = armSelectArm1Button.GetComponentInChildren<TextMeshProUGUI>();
            EquipmentData current1 = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm1);
            if (label != null)
                label.text = $"Bras gauche\n({current1?.equipmentName ?? "vide"})";
        }

        if (armSelectArm2Button != null)
        {
            var label = armSelectArm2Button.GetComponentInChildren<TextMeshProUGUI>();
            EquipmentData current2 = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm2);
            if (label != null)
                label.text = $"Bras droit\n({current2?.equipmentName ?? "vide"})";
        }
    }

    /// <summary>
    /// Appelé quand le joueur choisit un slot de bras dans le sous-panel.
    /// </summary>
    private void ConfirmArmPlacement(EquipmentSlot slot)
    {
        if (pendingLootChoice == null) return;

        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);
        FinalizeEquip(slot, pendingLootChoice);
    }

    /// <summary>
    /// Convertit un EquipmentType (nature de la pièce) en EquipmentSlot (emplacement physique).
    /// Ne doit pas être appelé pour EquipmentType.Arm — le slot bras est choisi par le joueur.
    /// </summary>
    private static EquipmentSlot EquipmentTypeToSlot(EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => EquipmentSlot.Head,
            EquipmentType.Torso => EquipmentSlot.Torso,
            EquipmentType.Legs  => EquipmentSlot.Legs,
            _ => throw new System.ArgumentException($"Impossible de convertir {type} en slot unique.")
        };
    }

    /// <summary>
    /// Sauvegarde l'équipement dans le RunManager et active le bouton "Continuer".
    /// </summary>
    private void FinalizeEquip(EquipmentSlot slot, EquipmentData item)
    {
        RunManager.Instance?.EquipItem(slot, item);
        pendingLootChoice = null;

        // Verrouille toutes les cartes — le choix est définitif, on ne peut plus changer
        foreach (LootCard card in spawnedLootCards)
            card.SetInteractable(false);

        if (lootContinueButton != null) lootContinueButton.interactable = true;

        Log($"Équipement choisi : {item.equipmentName} → slot {slot}");
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

        if (energyText != null) energyText.text = $"Énergie : {currentEnergy} / {effectiveMaxEnergy}";
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
