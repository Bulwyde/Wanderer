using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Composant attaché au préfab de bouton de compétence.
/// Affiche les infos d'une SkillData (nom, coût en énergie, cooldown restant)
/// et notifie le CombatManager quand le joueur clique dessus.
/// </summary>
public class SkillButton : MonoBehaviour
{
    [Header("Références UI")]
    public Button      button;
    public TextMeshProUGUI skillNameText;
    public TextMeshProUGUI energyCostText;

    // Affiché uniquement quand la compétence est en cooldown
    public TextMeshProUGUI cooldownText;

    // La compétence associée à ce bouton (null pour les boutons passifs)
    private SkillData     skill;
    private EquipmentData _sourceEquipment;
    private int           _effectiveCost;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le bouton avec une compétence, son equipement source, le cout effectif et un callback.
    /// sourceEquipment : equipement portant ce skill (null si inconnu).
    /// effectiveCost   : cout apres application des EnergyCostModifier de l'equipement.
    /// Le callback est une Action<SkillData, EquipmentData> : transmet la source pour
    /// que CombatManager evite de recalculer l'equipement par recherche reference.
    /// </summary>
    public void Setup(SkillData skillData, EquipmentData sourceEquipment,
                      int effectiveCost, Action<SkillData, EquipmentData> onClickCallback)
    {
        skill            = skillData;
        _sourceEquipment = sourceEquipment;
        _effectiveCost   = Mathf.Max(0, effectiveCost);

        if (skillNameText  != null) skillNameText.text  = skill.skillName;
        if (energyCostText != null) energyCostText.text = $"{_effectiveCost}";

        // Supprime les anciens listeners avant d'en ajouter un nouveau
        // (important si le bouton est réutilisé)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClickCallback?.Invoke(skill, _sourceEquipment));

        // Cooldown à 0 au départ — aucun texte de cooldown visible
        SetCooldown(0);
    }

    // -----------------------------------------------
    // ÉTAT DU BOUTON
    // -----------------------------------------------

    /// <summary>
    /// Met à jour l'affichage du cooldown.
    /// Si remainingTurns > 0, on affiche "X tours" et on désactive le bouton.
    /// </summary>
    public void SetCooldown(int remainingTurns)
    {
        bool onCooldown = remainingTurns > 0;

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            cooldownText.text = onCooldown ? $"CD : {remainingTurns}" : "";
        }
    }

    /// <summary>
    /// Met à jour le coût affiché (après réduction globale d'énergie).
    /// </summary>
    public void SetDisplayedCost(int cost)
    {
        if (energyCostText != null)
            energyCostText.text = $"{cost}";
    }

    /// <summary>
    /// Active ou désactive le bouton (manque d'énergie, tour ennemi, cooldown...).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    // Accesseurs pour que CombatManager puisse retrouver la compétence et sa source
    public SkillData     Skill           => skill;
    public EquipmentData SourceEquipment => _sourceEquipment;
    public int           EffectiveCost   => _effectiveCost;

    // -----------------------------------------------
    // BOUTON PASSIF (effet d'équipement)
    // -----------------------------------------------

    /// <summary>
    /// Configure le bouton en mode "effet passif" :
    /// affiche le nom de l'effet (displayName ou effectID en fallback),
    /// remplace le coût en énergie par "Passif",
    /// désactive le bouton (toujours grisé — non cliquable).
    /// </summary>
    public void SetupPassif(EffectData effet)
    {
        skill = null;

        string nom = (!string.IsNullOrEmpty(effet.displayName)) ? effet.displayName : effet.effectID;

        if (skillNameText != null)
            skillNameText.text = nom;

        if (energyCostText != null)
            energyCostText.text = "Passif";

        // Aucun cooldown à afficher sur un passif
        SetCooldown(0);

        // Supprime tout listener existant et désactive le bouton
        button.onClick.RemoveAllListeners();
        SetInteractable(false);
    }
}
