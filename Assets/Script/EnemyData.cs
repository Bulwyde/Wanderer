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
    // LISTE D'ACTIONS
    // -----------------------------------------------

    [Header("Actions (dans l'ordre d'exécution)")]
    // Liste ordonnée des actions que l'ennemi peut réaliser.
    // En jeu, l'EnemyAI copie cette liste dans une file et en fait
    // tourner les actions : tête → exécution → queue → tête → ...
    public List<EnemyAction> actions = new List<EnemyAction>();

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    [Header("Loot")]
    [Tooltip("Pièces d'équipement que cet ennemi peut lâcher")]
    public List<EquipmentData> lootPool = new List<EquipmentData>();

    [Tooltip("Nombre de choix proposés au joueur (tiré au hasard dans le lootPool). " +
             "Si le pool contient moins de pièces que ce nombre, toutes sont proposées.")]
    [Range(1, 4)]
    public int lootOfferCount = 2;
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
