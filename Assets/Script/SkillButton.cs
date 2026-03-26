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

    // La compétence associée à ce bouton
    private SkillData skill;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le bouton avec une compétence et un callback.
    /// Le callback est une Action<SkillData> : quand le joueur clique,
    /// on appelle CombatManager.UseSkill(skill) sans que SkillButton
    /// ait besoin de connaître CombatManager directement.
    /// </summary>
    public void Setup(SkillData skillData, Action<SkillData> onClickCallback)
    {
        skill = skillData;

        if (skillNameText != null)
            skillNameText.text = skill.skillName;

        if (energyCostText != null)
            energyCostText.text = $"{skill.energyCost}";

        // Supprime les anciens listeners avant d'en ajouter un nouveau
        // (important si le bouton est réutilisé)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClickCallback?.Invoke(skill));

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
    /// Active ou désactive le bouton (manque d'énergie, tour ennemi, cooldown...).
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    // Accesseur pour que CombatManager puisse retrouver la compétence liée
    public SkillData Skill => skill;
}
