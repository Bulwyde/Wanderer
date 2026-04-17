using UnityEngine;

/// <summary>
/// Définit un effet du jeu : quand il se déclenche, ce qu'il fait, sur qui.
/// Utilisé par les modules, les enchantements d'équipements et les compétences.
/// </summary>
[CreateAssetMenu(fileName = "NewEffect", menuName = "RPG/Effect Data")]
public class EffectData : ScriptableObject
{
    [Header("Identité")]
    // Identifiant unique de l'effet
    public string effectID;

    // Nom court affiché sur les boutons passifs (ex : "Empoisonnement au contact").
    // Si vide, le bouton utilise effectID comme fallback.
    public string displayName;

    // Description affichée au joueur avec balises de mots-clés
    // Ex : "Inflige 5 de {$weakness} à la cible."
    [TextArea(2, 5)]
    public string description;

    // Mots-clés affichés en tooltip — remplis à la main en cohérence
    // avec les balises {$...} dans la description
    public KeywordData[] keywords;

    [Header("Déclencheur")]
    // Quand cet effet se déclenche-t-il ?
    // • Compétences  → laisser sur OnSkillUsed (géré automatiquement par CombatManager)
    // • Consommables → laisser sur OnSkillUsed (géré automatiquement à l'utilisation)
    // • Modules      → choisir le trigger voulu (OnPlayerTurnStart, OnEnemyDied, Passive…)
    // • Équipement passiveEffects → Passive pour un bonus permanent, ou un trigger de combat
    [Tooltip("Compétences / Consommables : laisser sur OnSkillUsed.\nModules : choisir quand l'effet doit se déclencher (OnPlayerTurnStart, OnEnemyDied, OnFightStart…).\nOnFightStart = au premier tour du combat, après le reset d'armure.")]
    public EffectTrigger trigger;

    [Header("Action")]
    // Que fait cet effet ?
    public EffectAction action;

    [Header("Valeurs")]
    // Valeur principale de l'effet (ex : montant de dégâts, HP soignés, stacks appliqués...)
    public float value;

    // Valeur secondaire — bonus appliqué par stack du scalingStatus (voir section ci-dessous)
    // Exemple : 3f → chaque stack de Faiblesse ajoute 3 dégâts supplémentaires
    public float secondaryValue;

    [Header("Statut (pour ApplyStatus)")]
    // Statut à appliquer sur la cible — value = nombre de stacks infligés
    // Uniquement utilisé si action == ApplyStatus
    public StatusData statusToApply;

    [Header("Mise à l'échelle par stacks (optionnel)")]
    // Si renseigné, l'effet est amplifié selon les stacks actifs de ce statut sur la cible
    // La valeur bonus par stack est définie dans secondaryValue
    // Exemple : scalingStatus = Faiblesse, secondaryValue = 5 → +5 dégâts par stack de Faiblesse
    public StatusData scalingStatus;

    // Si true, tous les stacks du scalingStatus sont consommés (retirés) après l'effet
    // Si false, les stacks sont simplement lus sans être modifiés
    public bool consumeStacks;

    [Header("Cible")]
    public EffectTarget target;

    [Header("Modification de stat (pour ModifyStat)")]
    // Stat ciblée par cet effet — uniquement utilisé si action == ModifyStat.
    // Pour les modules et passifs : bonus permanent sur le run (via RunManager).
    // Pour les skills et consommables en combat : bonus temporaire jusqu'à la fin du combat.
    public StatType statToModify;

    [Header("Distribution d'item (DonnerConsommable / DonnerEquipement / DonnerModule)")]
    // Loot table source — seule celle correspondant à l'action est utilisée.
    [SerializeField] public ConsumableLootTable consommableLootTable;
    [SerializeField] public EquipmentLootTable  equipementLootTable;
    [SerializeField] public ModuleLootTable     moduleLootTable;

    // Filtre appliqué lors du tirage (null = pas de filtre).
    // Mutuellement exclusifs : si filtreParTagHero == true, filtreTag est ignoré.
    [SerializeField] public TagData filtreTag;
    [SerializeField] public bool    filtreParTagHero; // Utilise tags[0] du héros sélectionné

    [Header("Condition de tag (optionnel)")]
    // Tag requis sur la cible ou le contexte pour que l'effet s'applique (ou soit amplifié).
    // null = pas de condition.
    [SerializeField] public TagData conditionTag;

    // Sur quoi vérifier le tag.
    [SerializeField] public ConditionCible conditionCible;

    // Valeur du bonus appliqué si la condition de tag est remplie.
    // Pourcentage : ex. 0.5 → +50%, -0.3 → -30% | Flat : valeur brute ajoutée aux dégâts.
    [SerializeField] public float bonusConditionnel;

    // Interprétation de bonusConditionnel : pourcentage ou valeur plate.
    [SerializeField] public TypeBonusConditionnel typeBonusConditionnel;
}

/// <summary>
/// Quand l'effet se déclenche.
/// </summary>
public enum EffectTrigger
{
    // Valeur par défaut — aucun déclenchement automatique.
    // À utiliser pour les compétences et consommables (gérés implicitement par le code).
    None = 0,

    // Combat
    OnPlayerTurnStart    = 1,   // Début du tour joueur
    OnPlayerTurnEnd      = 2,   // Fin du tour joueur
    OnPlayerDamaged      = 3,   // Quand le joueur reçoit des dégâts
    OnPlayerDealtDamage  = 4,   // Quand le joueur inflige des dégâts
    OnEnemyDied          = 5,   // Quand un ennemi meurt
    OnAllSkillsUsed      = 6,   // Quand toutes les compétences ont été utilisées ce tour
    OnArmorDepleted      = 7,   // Quand le joueur perd toute son armure
    OnSkillUsed          = 8,   // Quand la compétence liée est utilisée

    // Début de combat
    OnFightStart         = 9,   // Au premier tour du combat, après le reset d'armure initial

    // Navigation (hors combat)
    OnChestOpened        = 10,  // Quand un coffre est ouvert
    OnShopEntered        = 11,  // Quand le joueur entre chez un marchand
}

/// <summary>
/// Ce que l'effet fait concrètement.
/// </summary>
public enum EffectAction
{
    DealDamage,             // Inflige des dégâts
    Heal,                   // Soigne des HP
    ApplyStatus,            // Applique un statut (Faiblesse, Poison...)
    ModifyStat,             // Modifie une stat (attaque, défense...)
    AddArmor,               // Ajoute de l'armure
    GainEnergy,             // Restaure de l'énergie courante (plafonné à l'énergie max du tour)
    AddCredits,             // Modifie les crédits du joueur (positif = gain, négatif = dépense)
    RevealRoom,             // Révèle une salle sur la carte
    DisableEnemyPart,       // Désactive une partie d'un ennemi
    DonnerConsommable,      // Tire un consommable depuis une loot table et l'ajoute à l'inventaire
    DonnerEquipement,       // Tire un équipement depuis une loot table (offre UI post-combat)
    DonnerModule,           // Tire un module depuis une loot table et l'active immédiatement
}

/// <summary>
/// Stat ciblée par un effet ModifyStat ou un statut de type ModifyStat.
/// Partagée entre EffectData, StatusData, EventData et RunManager.
/// </summary>
public enum StatType
{
    MaxHP,               // Points de vie maximum
    Attack,              // Attaque
    Defense,             // Défense
    CriticalChance,      // Probabilité de critique [0..1]
    CriticalMultiplier,  // Multiplicateur de dégâts sur un critique
    LifeSteal,           // Fraction des dégâts convertie en soins [0..1]
    MaxEnergy,           // Énergie maximale par tour
}

/// <summary>
/// Type de modificateur pour ModifyStat : plat ou pourcentage.
/// </summary>
public enum StatModifierType
{
    Flat,       // Valeur additive (ex : +10 → attaque + 10)
    Percentage, // Valeur multiplicative (ex : -0.5 → stat × 0,5 ; +0.2 → stat × 1,2)
}

/// <summary>
/// Sur qui l'effet s'applique.
/// </summary>
public enum EffectTarget
{
    Self,                   // Le joueur lui-même
    SingleEnemy,            // Un ennemi ciblé
    AllEnemies,             // Tous les ennemis
    RandomEnemy,            // Un ennemi aléatoire
}

/// <summary>
/// Interprétation de bonusConditionnel dans un EffectData.
/// </summary>
public enum TypeBonusConditionnel
{
    Pourcentage, // bonusConditionnel = fraction (ex : 0.5 = +50%, -0.3 = -30%)
    Flat,        // bonusConditionnel = valeur brute ajoutée aux dégâts
}

/// <summary>
/// Sur quoi vérifier la condition de tag d'un EffectData.
/// </summary>
public enum ConditionCible
{
    Aucune       = 0,       // Pas de condition — l'effet s'applique toujours
    EnnemiCible,            // L'ennemi ciblé doit avoir le conditionTag
    CarteActuelle,          // La carte courante doit avoir le conditionTag (réservé — non implémenté)
}