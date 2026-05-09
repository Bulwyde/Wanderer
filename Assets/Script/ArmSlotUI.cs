using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Composant attaché au prefab ArmSlotUI.
/// Affiche une arme (icone) + ses skills en grille (icones + boutons).
///
/// Hiérarchie attendue du prefab :
///   ArmSlotUI (root, HorizontalLayoutGroup)
///   ├─ ArmIconContainer (Image du container)
///   └─ SkillsGridContainer (GridLayoutGroup, cellSize=48×48)
///       ├─ SkillIcon_0 (SkillIconButton prefab instance)
///       ├─ SkillIcon_1 (SkillIconButton prefab instance)
///       └─ ...
/// </summary>
public class ArmSlotUI : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Image armIconImage;
    [SerializeField] private Transform skillsGridContainer;  // Changed: GridLayoutGroup → Transform
    [SerializeField] private GameObject skillIconPrefab;

    private EquipmentData _currentArm;
    private List<SkillIconButton> _spawnedSkillButtons = new List<SkillIconButton>();

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le slot avec une arme et ses skills.
    /// </summary>
    public void Setup(EquipmentData arm, List<SkillData> equippedSkills,
                      System.Action<SkillData, EquipmentData> onSkillClicked)
    {
        _currentArm = arm;

        // Affiche l'icone de l'arme
        if (armIconImage != null && arm.icon != null)
            armIconImage.sprite = arm.icon;

        // Génère les boutons skills
        SpawnSkillIcons(equippedSkills, arm, onSkillClicked);
    }

    // -----------------------------------------------
    // GÉNÉRATION DES SKILLS
    // -----------------------------------------------

    /// <summary>
    /// Crée un SkillIconButton par skill équipé.
    /// </summary>
    private void SpawnSkillIcons(List<SkillData> equippedSkills, EquipmentData sourceEquipment,
                                  System.Action<SkillData, EquipmentData> onSkillClicked)
    {
        // Cleanup ancien affichage
        foreach (SkillIconButton btn in _spawnedSkillButtons)
            if (btn != null) Destroy(btn.gameObject);
        _spawnedSkillButtons.Clear();

        if (skillsGridContainer == null || skillIconPrefab == null) return;

        // Crée un bouton par skill
        foreach (SkillData skill in equippedSkills)
        {
            if (skill == null) continue;

            GameObject skillGO = Instantiate(skillIconPrefab, skillsGridContainer);
            SkillIconButton skillBtn = skillGO.GetComponent<SkillIconButton>();
            if (skillBtn != null)
            {
                // Calcul du coût effectif (coût base + modifiers de l'équipement)
                int effectiveCost = skill.energyCost;

                // Si équipement a des modifiers, les appliquer
                if (sourceEquipment != null && sourceEquipment.skillModifiers != null)
                {
                    foreach (SkillModifier modifier in sourceEquipment.skillModifiers)
                    {
                        if (modifier != null && modifier.type == SkillModifierType.EnergyCostModifier)
                        {
                            // Vérifier si le modifier s'applique à ce skill (par tag conditionnel)
                            if (modifier.conditionTag == null ||
                                skill.tags.Any(t => t != null && t.tagName == modifier.conditionTag.tagName))
                            {
                                effectiveCost += (int)modifier.value;
                            }
                        }
                    }
                }

                effectiveCost = Mathf.Max(0, effectiveCost);

                skillBtn.Setup(skill, sourceEquipment, effectiveCost, onSkillClicked);
                _spawnedSkillButtons.Add(skillBtn);
            }
        }
    }

    // -----------------------------------------------
    // CLEANUP ET ACCESSEURS
    // -----------------------------------------------

    /// <summary>
    /// Nettoie les boutons skills.
    /// </summary>
    public void Clear()
    {
        foreach (SkillIconButton btn in _spawnedSkillButtons)
            if (btn != null) Destroy(btn.gameObject);
        _spawnedSkillButtons.Clear();
    }

    /// <summary>
    /// Retourne la liste des SkillIconButton spawned dans ce slot.
    /// Utilisé par CombatUIArmsController pour accéder à tous les buttons.
    /// </summary>
    public List<SkillIconButton> GetSpawnedSkillButtons()
    {
        return _spawnedSkillButtons;
    }
}
