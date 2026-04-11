using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Groupe d'ennemis — ScriptableObject définissant une rencontre multi-ennemis.
/// Contient jusqu'à 4 EnemyData et ses propres tables de loot.
///
/// Priorité de résolution au démarrage d'un combat :
///   CellData.specificGroup > EnemyPool.PickRandom() (si renvoie un groupe) > CellData.specificEnemy
///
/// Priorité de loot après victoire :
///   EnemyGroup.lootPool (si Count > 0) > MapData.defaultCombatLootTable > aucun loot équipement
/// </summary>
[CreateAssetMenu(fileName = "New Enemy Group", menuName = "RPG/Enemy Group")]
public class EnemyGroup : ScriptableObject
{
    // -----------------------------------------------
    // IDENTITÉ
    // -----------------------------------------------

    [Header("Identité")]
    public string groupID;
    public string groupName;

    // -----------------------------------------------
    // COMPOSITION DU GROUPE (1 à 4 ennemis)
    // -----------------------------------------------

    [Header("Ennemis (1 à 4)")]
    [Tooltip("Liste ordonnée des ennemis du groupe. Ordre = ordre d'affichage et d'action pendant le tour ennemi.")]
    public List<EnemyData> enemies = new List<EnemyData>();

    // -----------------------------------------------
    // LOOT
    // -----------------------------------------------

    [Header("Loot — Crédits")]
    [Tooltip("Crédits accordés au joueur quand ce groupe est vaincu. Remplace la somme des creditsLoot individuels.")]
    public int creditsLoot = 0;

    [Header("Loot — Équipement")]
    [Tooltip("Pièces d'équipement que ce groupe peut lâcher. Si vide, le fallback MapData.defaultCombatLootTable est utilisé.")]
    public List<EquipmentData> lootPool = new List<EquipmentData>();

    [Tooltip("Nombre de choix proposés au joueur. Si le pool contient moins de pièces, toutes sont proposées.")]
    [Range(1, 4)]
    public int lootOfferCount = 2;

    [Header("Loot — Consommables")]
    [Tooltip("Consommables que ce groupe peut lâcher. Un seul est accordé aléatoirement si le joueur a un slot libre.")]
    public List<ConsumableData> consumableLootPool = new List<ConsumableData>();

    // -----------------------------------------------
    // TAGS
    // -----------------------------------------------

    [Header("Tags")]
    [Tooltip("Tags sémantiques du groupe — utilisés pour les conditions d'effets et les cooldowns de navigation.")]
    public List<TagData> tags = new List<TagData>();
}
