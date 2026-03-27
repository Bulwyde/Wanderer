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

    // Description affichée au joueur avec balises de mots-clés
    // Ex : "Inflige 5 de {$weakness} à la cible."
    [TextArea(2, 5)]
    public string description;

    // Mots-clés affichés en tooltip — remplis à la main en cohérence
    // avec les balises {$...} dans la description
    public KeywordData[] keywords;

    [Header("Déclencheur")]
    // Quand cet effet se déclenche-t-il ?
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
}

/// <summary>
/// Quand l'effet se déclenche.
/// </summary>
public enum EffectTrigger
{
    // Combat
    OnPlayerTurnStart,      // Début du tour joueur
    OnPlayerTurnEnd,        // Fin du tour joueur
    OnPlayerDamaged,        // Quand le joueur reçoit des dégâts
    OnPlayerDealtDamage,    // Quand le joueur inflige des dégâts
    OnEnemyDied,            // Quand un ennemi meurt
    OnAllSkillsUsed,        // Quand toutes les compétences ont été utilisées ce tour
    OnArmorDepleted,        // Quand le joueur perd toute son armure
    OnSkillUsed,            // Quand la compétence liée est utilisée

    // Navigation
    OnRoomEntered,          // Quand le joueur entre dans une salle
    OnChestOpened,          // Quand un coffre est ouvert
    OnShopEntered,          // Quand le joueur entre chez un marchand

    // Passif permanent
    Passive                 // Toujours actif, modifie une stat en permanence
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
    AddGold,                // Ajoute des pièces
    RevealRoom,             // Révèle une salle sur la carte
    DisableEnemyPart,       // Désactive une partie d'un ennemi
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