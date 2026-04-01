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
    /// Déclenche les effets de tous les modules ET les passiveEffects d'équipements
    /// dont l'EffectData.trigger correspond au trigger demandé.
    /// Également appelé par CombatManager pour les triggers OnFightStart.
    /// </summary>
    public void ApplyModulesWithTrigger(EffectTrigger trigger)
    {
        if (isApplyingModule) return;
        if (RunManager.Instance == null) return;

        CombatManager combat = FindFirstObjectByType<CombatManager>();

        isApplyingModule = true;
        try
        {
            // --- Modules ---
            foreach (ModuleData module in RunManager.Instance.GetModules())
            {
                if (module == null || module.effects == null) continue;

                foreach (EffectData effet in module.effects)
                {
                    if (effet == null) continue;
                    if (effet.trigger != trigger) continue;

                    Debug.Log($"[Module] '{module.moduleName}' déclenché ({trigger})");

                    if (combat != null)
                        combat.ApplyModuleEffect(effet, module.moduleName);
                    else
                        ApplyEffectOutOfCombat(effet, module.moduleName);
                }
            }

            // --- Passifs d'équipement ---
            foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
            {
                EquipmentData equip = RunManager.Instance.GetEquipped(slot);
                if (equip == null || equip.passiveEffects == null) continue;

                foreach (EffectData effet in equip.passiveEffects)
                {
                    if (effet == null) continue;
                    if (effet.trigger != trigger) continue;

                    string nom = $"{equip.equipmentName} — {(string.IsNullOrEmpty(effet.displayName) ? effet.effectID : effet.displayName)}";
                    Debug.Log($"[Équipement passif] '{nom}' déclenché ({trigger})");

                    if (combat != null)
                        combat.ApplyModuleEffect(effet, nom);
                    else
                        ApplyEffectOutOfCombat(effet, nom);
                }
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

            case EffectAction.ModifyStat:
            {
                // Hors combat, ModifyStat ajoute un bonus permanent sur le run
                RunManager.Instance.AddStatBonus(effect.statToModify, effect.value);
                Debug.Log($"[Module] {moduleName} — ModifyStat hors combat : {effect.statToModify} " +
                          $"{(effect.value >= 0 ? "+" : "")}{effect.value}");
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
