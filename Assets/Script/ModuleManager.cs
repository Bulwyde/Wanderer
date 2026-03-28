using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Singleton persistant (DontDestroyOnLoad) qui écoute le bus d'événements GameEvents
/// et déclenche les effets des modules actifs du joueur au bon moment.
///
/// Le trigger de chaque module est défini sur son EffectData.trigger (EffectTrigger).
/// ModuleManager fait le lien entre les événements de jeu et ces triggers.
///
/// Placement : un seul GameObject "ModuleManager" dans la scène de démarrage.
/// Il persistera automatiquement dans toutes les scènes.
/// </summary>
public class ModuleManager : MonoBehaviour
{
    // -----------------------------------------------
    // SINGLETON
    // -----------------------------------------------

    public static ModuleManager Instance { get; private set; }

    /// <summary>
    /// Déclenché quand la liste des modules actifs change (ajout ou retrait).
    /// Les ModuleHUDManager s'y abonnent pour rafraîchir l'affichage.
    /// </summary>
    public static event Action OnModulesChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------
    // ABONNEMENTS AUX ÉVÉNEMENTS
    // -----------------------------------------------

    void OnEnable()
    {
        GameEvents.OnPlayerTurnStarted  += HandlePlayerTurnStarted;
        GameEvents.OnPlayerTurnEnded    += HandlePlayerTurnEnded;
        GameEvents.OnPlayerDamaged      += HandlePlayerDamaged;
        GameEvents.OnPlayerDealtDamage  += HandlePlayerDealtDamage;
        GameEvents.OnEnemyDied          += HandleEnemyDied;
    }

    void OnDisable()
    {
        GameEvents.OnPlayerTurnStarted  -= HandlePlayerTurnStarted;
        GameEvents.OnPlayerTurnEnded    -= HandlePlayerTurnEnded;
        GameEvents.OnPlayerDamaged      -= HandlePlayerDamaged;
        GameEvents.OnPlayerDealtDamage  -= HandlePlayerDealtDamage;
        GameEvents.OnEnemyDied          -= HandleEnemyDied;
    }

    // -----------------------------------------------
    // ANTI-RÉCURSION
    // -----------------------------------------------

    // Empêche qu'un effet de module déclenche un autre événement qui relancerait les modules.
    // Ex : un module DealDamage ne doit pas re-déclencher OnPlayerDealtDamage.
    private bool isApplyingModule = false;

    // -----------------------------------------------
    // GESTIONNAIRES D'ÉVÉNEMENTS
    // -----------------------------------------------

    private void HandlePlayerTurnStarted()
        => ApplyModulesWithTrigger(EffectTrigger.OnPlayerTurnStart);

    private void HandlePlayerTurnEnded()
        => ApplyModulesWithTrigger(EffectTrigger.OnPlayerTurnEnd);

    private void HandlePlayerDamaged(int damage)
        => ApplyModulesWithTrigger(EffectTrigger.OnPlayerDamaged);

    private void HandlePlayerDealtDamage(int damage)
        => ApplyModulesWithTrigger(EffectTrigger.OnPlayerDealtDamage);

    private void HandleEnemyDied()
        => ApplyModulesWithTrigger(EffectTrigger.OnEnemyDied);

    // -----------------------------------------------
    // APPLICATION DES EFFETS DE MODULES
    // -----------------------------------------------

    /// <summary>
    /// Déclenche l'effet de tous les modules dont l'EffectData.trigger correspond.
    /// Également appelé par CombatManager pour les modules Passive au démarrage du combat.
    /// </summary>
    public void ApplyModulesWithTrigger(EffectTrigger trigger)
    {
        if (isApplyingModule) return;
        if (RunManager.Instance == null) return;

        List<ModuleData> modules = RunManager.Instance.GetModules();
        if (modules.Count == 0) return;

        CombatManager combat = FindFirstObjectByType<CombatManager>();

        isApplyingModule = true;
        try
        {
            foreach (ModuleData module in modules)
            {
                if (module == null || module.effect == null) continue;
                if (module.effect.trigger != trigger) continue;

                Debug.Log($"[Module] '{module.moduleName}' déclenché ({trigger})");

                if (combat != null)
                    combat.ApplyModuleEffect(module.effect, module.moduleName);
                else
                    ApplyEffectOutOfCombat(module.effect, module.moduleName);
            }
        }
        finally
        {
            isApplyingModule = false;
        }
    }

    /// <summary>
    /// Applique un effet de module hors combat (navigation, événements).
    /// Respecte EffectTarget : Self = joueur, sinon ignoré hors combat.
    /// </summary>
    private void ApplyEffectOutOfCombat(EffectData effect, string moduleName)
    {
        if (RunManager.Instance == null || effect == null) return;

        switch (effect.action)
        {
            case EffectAction.Heal:
            {
                // Hors combat, Heal cible toujours le joueur (Self)
                int amount  = Mathf.RoundToInt(effect.value);
                int maxHP   = RunManager.Instance.maxHP;
                int current = RunManager.Instance.currentHP;
                int healed  = Mathf.Min(amount, maxHP - current);

                if (healed > 0)
                {
                    RunManager.Instance.currentHP += healed;
                    Debug.Log($"[Module] {moduleName} — Soin hors combat : +{healed} HP " +
                              $"→ {RunManager.Instance.currentHP}/{maxHP}");
                }
                break;
            }

            default:
                Debug.Log($"[Module] {moduleName} — Effet '{effect.action}' " +
                          $"non pris en charge hors combat.");
                break;
        }
    }

    // -----------------------------------------------
    // NOTIFICATION DE CHANGEMENT
    // -----------------------------------------------

    /// <summary>
    /// Notifie tous les HUDs qu'un module a été ajouté ou retiré.
    /// Appelé depuis RunManager.AddModule().
    /// </summary>
    public static void NotifyModulesChanged()
    {
        OnModulesChanged?.Invoke();
    }
}
