using System;
using UnityEngine;

/// <summary>
/// Bus d'événements global du jeu.
/// Centralise tous les événements auxquels les systèmes peuvent s'abonner.
/// Utilisation :
///   - S'abonner   : GameEvents.OnChestOpened += MaFonction;
///   - Se désabonner : GameEvents.OnChestOpened -= MaFonction;
///   - Déclencher  : GameEvents.OnChestOpened?.Invoke();
/// </summary>
public static class GameEvents
{
    // -----------------------------------------------
    // ÉVÉNEMENTS DE COMBAT
    // -----------------------------------------------

    /// <summary>Déclenché quand le joueur reçoit des dégâts.</summary>
    public static event Action<int> OnPlayerDamaged;

    /// <summary>Déclenché quand le joueur inflige des dégâts.</summary>
    public static event Action<int> OnPlayerDealtDamage;

    /// <summary>Déclenché quand un ennemi meurt.</summary>
    public static event Action OnEnemyDied;

    /// <summary>Déclenché au début du tour du joueur.</summary>
    public static event Action OnPlayerTurnStarted;

    /// <summary>Déclenché à la fin du tour du joueur.</summary>
    public static event Action OnPlayerTurnEnded;

    /// <summary>Déclenché quand le joueur perd toute son armure.</summary>
    public static event Action OnArmorDepleted;

    /// <summary>Déclenché quand le joueur a utilisé toutes ses compétences ce tour.</summary>
    public static event Action OnAllSkillsUsed;

    /// <summary>Déclenché quand le joueur utilise un skill (après application de ses effets).</summary>
    public static event Action<SkillData> OnSkillUsed;

    // -----------------------------------------------
    // ÉVÉNEMENTS DE NAVIGATION
    // -----------------------------------------------

    /// <summary>Déclenché quand le joueur entre dans une nouvelle salle.</summary>
    public static event Action<int> OnRoomEntered;

    /// <summary>Déclenché quand un coffre est ouvert.</summary>
    public static event Action OnChestOpened;

    /// <summary>Déclenché quand le joueur entre chez un marchand.</summary>
    public static event Action OnShopEntered;

    // -----------------------------------------------
    // MÉTHODES DE DÉCLENCHEMENT
    // Encapsulent l'appel Invoke avec la vérification null
    // -----------------------------------------------

    public static void TriggerPlayerDamaged(int damage)
        => OnPlayerDamaged?.Invoke(damage);

    public static void TriggerPlayerDealtDamage(int damage)
        => OnPlayerDealtDamage?.Invoke(damage);

    public static void TriggerEnemyDied()
        => OnEnemyDied?.Invoke();

    public static void TriggerPlayerTurnStarted()
        => OnPlayerTurnStarted?.Invoke();

    public static void TriggerPlayerTurnEnded()
        => OnPlayerTurnEnded?.Invoke();

    public static void TriggerArmorDepleted()
        => OnArmorDepleted?.Invoke();

    public static void TriggerAllSkillsUsed()
        => OnAllSkillsUsed?.Invoke();

    public static void TriggerSkillUsed(SkillData skill)
        => OnSkillUsed?.Invoke(skill);

    public static void TriggerRoomEntered(int roomIndex)
        => OnRoomEntered?.Invoke(roomIndex);

    public static void TriggerChestOpened()
        => OnChestOpened?.Invoke();

    public static void TriggerShopEntered()
        => OnShopEntered?.Invoke();
}