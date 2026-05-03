using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Définit un statut de combat (Poison, Faiblesse, Force, Brûlure...).
///
/// Un statut existe en tant que ScriptableObject — sa *définition* est ici.
/// Les stacks actifs en combat sont trackés dans CombatManager via des dictionnaires
/// (playerStatuses / enemyStatuses), jamais dans ce ScriptableObject.
///
/// Deux grandes familles de statuts :
///   - StackOnly : aucun effet automatique, les stacks sont simplement consultés
///     par d'autres effets (ex : "inflige X dégâts par stack de Faiblesse").
///   - PerTurnStart : déclenche automatiquement un effet au début du tour
///     de l'entité affectée (ex : Poison inflige des dégâts chaque tour).
/// </summary>
[CreateAssetMenu(fileName = "NewStatus", menuName = "RPG/Status Data")]
public class StatusData : ScriptableObject
{
    // -----------------------------------------------
    // IDENTITÉ
    // -----------------------------------------------

    [Header("Identité")]
    // Identifiant unique — utilisé en interne pour référencer le statut
    public string statusID;

    // Nom affiché au joueur (ex : "Poison", "Faiblesse", "Force")
    public string statusName;

    // Description courte affichée en tooltip — peut mentionner {stacks}
    [TextArea(2, 4)]
    public string description;

    public Sprite icon;

    // -----------------------------------------------
    // COMPORTEMENT
    // -----------------------------------------------

    [Header("Comportement")]
    // Définit si et quand le statut se déclenche automatiquement
    public StatusBehavior behavior;

    // Action exécutée automatiquement (uniquement pour PerTurnStart)
    // Exemple : DealDamage → chaque stack inflige effectPerStack dégâts par tour
    public EffectAction perTurnAction;

    // Valeur par stack pour l'action automatique
    // Exemple : 2f avec DealDamage → chaque stack inflige 2 HP de poison par tour
    public float effectPerStack;

    // -----------------------------------------------
    // DURÉE & DÉCROISSANCE
    // -----------------------------------------------

    [Header("Durée & Décroissance")]
    // Nombre de stacks perdus automatiquement à chaque tour (0 = les stacks ne diminuent jamais)
    public int decayPerTurn;

    // Quand la décroissance a lieu dans le tour de l'entité affectée :
    //   OnTurnStart : au début du tour, avant que l'entité agisse (comportement par défaut — cohérent avec le poison)
    //   OnTurnEnd   : à la fin du tour, après que l'entité a agi (utile pour les debuffs qui durent
    //                 le nombre de tours annoncé — ex. "Affaiblissement 3 tours" = 3 tours complets)
    public StatusDecayTiming decayTiming;

    // Filtre optionnel pour OnSkillUse : ne décroît que si le skill utilisé possède ce tag.
    // Null = décroît sur n'importe quel skill utilisé.
    public TagData decayConditionTag;

    // Plafond de stacks accumulables sur une même entité (0 = illimité)
    public int maxStacks;

    // -----------------------------------------------
    // EFFETS PASSIFS (optionnel)
    // -----------------------------------------------

    [Header("Effets passifs (optionnel)")]
    // Effets déclenchés automatiquement quand ce statut est actif (stacks > 0).
    // Utilise le champ trigger de chaque EffectData pour savoir quand s'activer.
    // Supporte scalingSource = SkillUtilise + comptageTag pour filtrer par tag de skill (OnSkillUsed).
    public List<EffectData> passiveEffects;

    // -----------------------------------------------
    // MODIFICATION DE STAT (behavior == ModifyStat)
    // -----------------------------------------------

    [Header("Modification de stat (behavior = ModifyStat uniquement)")]
    // Stat ciblée par ce statut
    public StatType statToModify;

    // Type de modification : plat (+10 attaque) ou pourcentage (-0.5 = -50% attaque)
    public StatModifierType statModifierType;

    // Si true  : modificateur = effectPerStack x stacks (cas 3 — valeur variable, durée infinie)
    //   Exemple : Force × 5 stacks avec effectPerStack = 10 → +50 attaque
    // Si false : modificateur = effectPerStack fixe, les stacks = durée (cas 2 — valeur constante)
    //   Exemple : Affaiblissement × 3 stacks avec effectPerStack = -0.5 → -50% attaque pendant 3 tours
    public bool valueScalesWithStacks;

    // Optionnel : filtre la modification de stat par tag du skill utilisé.
    // Uniquement pertinent pour les stats combat-temporaires (ArmorGainMultiplier, HealGainMultiplier, DamageGainMultiplier, EnergyCostReduction).
    // Null = s'applique à tous les skills.
    // Exemple : "Ce bonus d'armure ne s'applique que si le skill porte le tag 'Defense'".
    public TagData conditionTagForModifyStat;
}

/// <summary>
/// Quand la décroissance des stacks a lieu dans le tour de l'entité affectée.
/// </summary>
public enum StatusDecayTiming
{
    // Au début du tour, avant que l'entité agisse (défaut — cohérent avec le poison et les effets automatiques)
    OnTurnStart,

    // À la fin du tour, après que l'entité a agi
    // Un Affaiblissement de 3 tours posé au tour T durera T, T+1, T+2 complets
    OnTurnEnd,

    // Décroît quand le joueur utilise une compétence (optionnel : seulement si le skill a decayConditionTag)
    OnSkillUse,

    // Décroît quand l'entité affectée reçoit des dégâts HP
    OnDamageTaken,

    // Décroît quand le joueur gagne de l'armure
    OnArmorGain,

    // Décroît quand le joueur est soigné
    OnHealing,
}

/// <summary>
/// Définit quand (et si) un statut se déclenche automatiquement.
/// </summary>
public enum StatusBehavior
{
    // Aucun effet automatique — les stacks sont uniquement consultés par d'autres effets
    // Exemple : "Faiblesse" — une compétence inflige X dégâts par stack de Faiblesse,
    //           puis consomme les stacks
    StackOnly,

    // Déclenche perTurnAction au début du tour de l'entité affectée
    // Exemple : Poison, Brûlure
    PerTurnStart,

    // Modifie une stat tant que le statut est actif — aucun effet automatique par tour.
    // Le modificateur est lu dynamiquement par CombatManager à chaque calcul de dégâts.
    // Deux modes (voir valueScalesWithStacks) :
    //   false → valeur fixe (effectPerStack), stacks = durée (decayPerTurn > 0)
    //   true  → valeur = effectPerStack × stacks, durée infinie (decayPerTurn = 0)
    ModifyStat,
}
