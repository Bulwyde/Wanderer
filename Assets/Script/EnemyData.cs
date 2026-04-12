using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Données statiques d'un ennemi — définit ses stats et sa liste d'actions.
/// C'est un ScriptableObject : on en crée un asset par type d'ennemi dans le projet.
///
/// Le système d'actions fonctionne comme une file circulaire :
/// - L'ennemi exécute toujours l'action en tête de liste
/// - Après l'avoir exécutée, elle passe en queue (ou disparaît si ses utilisations sont épuisées)
/// - L'ordre dans l'Inspector détermine l'ordre d'exécution en jeu
/// </summary>
[CreateAssetMenu(fileName = "New Enemy", menuName = "RPG/Enemy Data")]
public class EnemyData : ScriptableObject
{
    // -----------------------------------------------
    // IDENTITÉ
    // -----------------------------------------------

    [Header("Identité")]
    public string enemyID;
    public string enemyName;

    // Sprite affiché dans le combat
    public Sprite portrait;

    // -----------------------------------------------
    // STATS DE BASE
    // -----------------------------------------------

    [Header("Stats")]
    public int maxHP     = 30;
    public int attack    = 5;
    public int defense   = 0;

    // -----------------------------------------------
    // TAGS
    // -----------------------------------------------

    [Header("Tags")]
    // Tags sémantiques pour les interactions et les conditions d'effets
    // Ex : Tag_Humain, Tag_Undead, Tag_Feu — créer les assets dans Assets/ScriptableObjects/Tags/
    public List<TagData> tags = new List<TagData>();

    // -----------------------------------------------
    // LISTE D'ACTIONS
    // -----------------------------------------------

    [Header("Actions (dans l'ordre d'exécution)")]
    // Liste ordonnée des actions que l'ennemi peut réaliser.
    // En jeu, l'EnemyAI copie cette liste dans une file et en fait
    // tourner les actions : tête → exécution → queue → tête → ...
    public List<EnemyAction> actions = new List<EnemyAction>();

    // -----------------------------------------------
    // EFFETS SPÉCIAUX
    // -----------------------------------------------

    [Header("Effets — Apparition")]
    [Tooltip("Effets appliqués sur cet ennemi dès l'initialisation du combat (avant le premier tour).")]
    // Ex : conférer un statut, modifier une stat, appliquer de l'armure au départ.
    public List<EffectData> spawnEffects = new List<EffectData>();

    [Header("Effets — Mort")]
    [Tooltip("Effets déclenchés au moment où cet ennemi tombe à 0 HP (avant la vérification de victoire).")]
    // Ex : infliger des dégâts au joueur, soigner un allié, appliquer un statut.
    public List<EffectData> deathEffects = new List<EffectData>();

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    [Header("Loot — Crédits")]
    [Tooltip("Crédits accordés au joueur quand cet ennemi est vaincu. 0 = aucun crédit.")]
    public int creditsLoot = 0;

    [Header("Loot — Équipement")]
    [Tooltip("Pièces d'équipement que cet ennemi peut lâcher")]
    public List<EquipmentData> lootPool = new List<EquipmentData>();

    [Tooltip("Nombre de choix proposés au joueur (tiré au hasard dans le lootPool). " +
             "Si le pool contient moins de pièces que ce nombre, toutes sont proposées.")]
    [Range(1, 4)]
    public int lootOfferCount = 2;

    [Header("Loot — Consommables")]
    [Tooltip("Consommables que cet ennemi peut lâcher. " +
             "Un seul est accordé aléatoirement si le joueur a un slot libre.")]
    public List<ConsumableData> consumableLootPool = new List<ConsumableData>();
}

/// <summary>
/// Une action dans le répertoire d'un ennemi.
/// Contient la compétence à utiliser et un nombre d'utilisations (0 = illimité).
///
/// [System.Serializable] est nécessaire pour qu'Unity puisse afficher
/// cette classe dans l'Inspector à l'intérieur d'une List<>.
/// </summary>
[System.Serializable]
public class EnemyAction
{
    // La compétence associée à cette action (SkillData déjà existant)
    public SkillData skill;

    // Nombre maximum d'utilisations. 0 = illimité (la valeur par défaut)
    // Quand maxUses > 0 et que usesLeft atteint 0, l'action est retirée de la file
    [Tooltip("0 = utilisations illimitées")]
    public int maxUses = 0;
}
