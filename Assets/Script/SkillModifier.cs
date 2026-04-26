using UnityEngine;

/// <summary>
/// Modificateur de skill embarque dans un EquipmentData.
/// Modifie le comportement des skills equipes sur cet equipement au moment de leur execution.
/// </summary>
[System.Serializable]
public class SkillModifier
{
    public SkillModifierType type;

    // Interpretation selon le type :
    //   BaseDamageMultiplier  : delta (ex : -0.2 = -20% sur skill.value avant l'attaque)
    //   DamageMultiplier      : delta (ex :  0.2 = +20% sur les degats finaux)
    //   CritChanceBonus       : valeur flat ajoutee a la chance de critique [0..1]
    //   RepeatExecution       : nb d'executions supplementaires (1 = 2 executions totales)
    //   EnergyCostModifier    : delta entier ajoute au cout en energie
    //   BonusStatusStacks     : stacks supplementaires sur tous les statuts appliques
    //   ForceAoE              : ignoree (effet binaire)
    public float value;

    // Si non null : s'applique seulement aux skills de cet equipement qui ont
    // ce tag dans skill.tags (comparaison par tagName).
    // Null = s'applique a tous les skills de l'equipement.
    public TagData conditionTag;
}

public enum SkillModifierType
{
    // Override le targetType vers AllEnemies pour ce cast
    ForceAoE,

    // Multiplie skill.value AVANT l'ajout de l'attaque (degats de base uniquement)
    BaseDamageMultiplier,

    // Execute les effets du skill N fois supplementaires.
    // SingleEnemy : meme cible pour les repetitions.
    // RandomEnemy : nouvelle cible aleatoire a chaque repetition (null passe dans ApplyEffect).
    RepeatExecution,

    // Multiplie les degats finaux (apres attaque, defense, crit) par (1 + value)
    DamageMultiplier,

    // Ajoute value a la chance de critique pour ce cast (flat, sera clamp [0,1])
    CritChanceBonus,

    // Ajoute value (int) au cout en energie effectif du skill
    EnergyCostModifier,

    // Ajoute value (int) stacks a chaque statut applique par le skill
    BonusStatusStacks,
}
