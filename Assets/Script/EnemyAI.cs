using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gère la file d'actions circulaire d'un ennemi pendant un combat.
/// C'est une classe C# simple (pas un MonoBehaviour) — elle n'a pas besoin
/// d'être attachée à un GameObject, CombatManager l'instancie directement.
///
/// Fonctionnement de la file :
///   - Au démarrage, on copie les actions de l'EnemyData dans une liste ordonnée
///   - Chaque tour, l'ennemi exécute l'action en tête de liste
///   - Après exécution, elle repasse en queue (sauf si ses utilisations sont épuisées)
///   - Une action à 0 utilisations restantes est supprimée définitivement
///   - Si la file est vide, l'ennemi fait une attaque de base (fallback)
/// </summary>
public class EnemyAI
{
    // File d'actions à exécuter (ordre = ordre d'exécution)
    private List<RuntimeAction> queue = new List<RuntimeAction>();

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Construit la file à partir des actions définies dans l'EnemyData.
    /// L'ordre de la liste dans l'Inspector = ordre d'exécution en jeu.
    /// </summary>
    public EnemyAI(EnemyData data)
    {
        if (data == null || data.actions == null) return;

        foreach (EnemyAction action in data.actions)
        {
            // On ignore les actions sans skill assigné
            if (action != null && action.skill != null)
                queue.Add(new RuntimeAction(action));
        }

        Debug.Log($"[EnemyAI] File initialisée — {queue.Count} action(s) pour {data.enemyName}");
    }

    // -----------------------------------------------
    // CONSULTATION DE LA FILE
    // -----------------------------------------------

    /// <summary>
    /// True si la file contient encore au moins une action.
    /// False si toutes les actions à utilisations limitées ont été épuisées.
    /// </summary>
    public bool HasActions => queue.Count > 0;

    /// <summary>
    /// Retourne le skill de la prochaine action sans avancer la file.
    /// Utile pour afficher à l'avance ce que l'ennemi va faire (comme dans Slay the Spire).
    /// Retourne null si la file est vide.
    /// </summary>
    public SkillData PeekNextSkill()
    {
        return queue.Count > 0 ? queue[0].skill : null;
    }

    // -----------------------------------------------
    // AVANCEMENT DE LA FILE
    // -----------------------------------------------

    /// <summary>
    /// Retourne le skill de la prochaine action ET fait avancer la file.
    ///
    /// Règles d'avancement :
    /// - maxUses == 0 (illimité) : l'action repasse en queue après exécution
    /// - maxUses > 0 (limité)    : on décrémente usesLeft
    ///     → Si usesLeft > 0 : repasse en queue
    ///     → Si usesLeft == 0 : supprimée définitivement
    ///
    /// Retourne null si la file est vide (le CombatManager fera une attaque de base).
    /// </summary>
    public SkillData GetAndAdvanceAction()
    {
        if (queue.Count == 0) return null;

        RuntimeAction current = queue[0];
        queue.RemoveAt(0); // retire de la tête

        SkillData skill = current.skill;

        if (current.maxUses == 0)
        {
            // Illimité : retour en queue sans rien décrémenter
            queue.Add(current);
            Debug.Log($"[EnemyAI] Action '{skill.skillName}' exécutée — retour en queue (illimitée)");
        }
        else
        {
            current.usesLeft--;

            if (current.usesLeft > 0)
            {
                queue.Add(current); // encore des utilisations
                Debug.Log($"[EnemyAI] Action '{skill.skillName}' exécutée — {current.usesLeft} utilisation(s) restante(s)");
            }
            else
            {
                // Épuisée : elle disparaît définitivement de la file
                Debug.Log($"[EnemyAI] Action '{skill.skillName}' épuisée — retirée de la file");
            }
        }

        return skill;
    }

    // -----------------------------------------------
    // CLASSE INTERNE — ÉTAT RUNTIME D'UNE ACTION
    // -----------------------------------------------

    /// <summary>
    /// Enveloppe une EnemyAction avec son état runtime (utilisations restantes).
    /// Séparé de EnemyAction pour ne pas modifier le ScriptableObject pendant le jeu.
    /// </summary>
    private class RuntimeAction
    {
        public SkillData skill;
        public int       maxUses;  // 0 = illimité
        public int       usesLeft; // décrémenté à chaque utilisation

        public RuntimeAction(EnemyAction source)
        {
            skill    = source.skill;
            maxUses  = source.maxUses;
            usesLeft = source.maxUses; // au départ = max
        }
    }
}
