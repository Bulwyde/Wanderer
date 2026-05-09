using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Composant attaché au prefab SkillIconButton.
/// Affiche l'icone d'un skill + coût + cooldown.
///
/// Hiérarchie attendue du prefab :
///   SkillIconButton (root, Image + Button)
///   ├─ CostText (TextMeshProUGUI, coin bas-droit)
///   └─ CooldownText (TextMeshProUGUI, centré sur icone)
/// </summary>
public class SkillIconButton : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private TextMeshProUGUI cooldownText;

    private SkillData _skill;
    private EquipmentData _sourceEquipment;
    private int _effectiveCost;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le bouton avec un skill, son équipement source et son coût effectif.
    /// </summary>
    public void Setup(SkillData skill, EquipmentData sourceEquipment,
                      int effectiveCost, System.Action<SkillData, EquipmentData> onClickCallback)
    {
        _skill = skill;
        _sourceEquipment = sourceEquipment;
        _effectiveCost = Mathf.Max(0, effectiveCost);

        // Affiche l'icone du skill
        if (iconImage != null && skill.icon != null)
            iconImage.sprite = skill.icon;

        // Affiche le coût en énergie
        SetDisplayedCost(_effectiveCost);

        // Configure le callback du bouton
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClickCallback?.Invoke(_skill, _sourceEquipment));
        }

        // Pas de cooldown au départ
        SetCooldown(0);
    }

    // -----------------------------------------------
    // ÉTAT DU BOUTON
    // -----------------------------------------------

    /// <summary>
    /// Met à jour le coût affiché (après réduction globale d'énergie).
    /// </summary>
    public void SetDisplayedCost(int cost)
    {
        if (costText != null)
            costText.text = $"{cost}";
    }

    /// <summary>
    /// Met à jour l'affichage du cooldown.
    /// Si remainingTurns > 0, affiche le chiffre centré en gros, grise l'icone.
    /// </summary>
    public void SetCooldown(int remainingTurns)
    {
        bool onCooldown = remainingTurns > 0;

        // Affiche/masque le texte cooldown
        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(onCooldown);
            cooldownText.text = onCooldown ? $"{remainingTurns}" : "";
        }

        // Grise l'icone si en cooldown
        SetInteractable(!onCooldown);
    }

    /// <summary>
    /// Active ou désactive le bouton (manque d'énergie, cooldown...).
    /// Grise l'icone en modifiant son opacité.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;

        // Réduire opacité si grisé
        if (iconImage != null)
        {
            Color color = iconImage.color;
            color.a = interactable ? 1f : 0.5f;
            iconImage.color = color;
        }
    }

    // -----------------------------------------------
    // ACCESSEURS
    // -----------------------------------------------

    public SkillData Skill => _skill;
    public EquipmentData SourceEquipment => _sourceEquipment;
    public int EffectiveCost => _effectiveCost;
}
