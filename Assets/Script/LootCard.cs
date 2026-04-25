using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Composant attaché au préfab d'une carte de loot.
/// Affiche les informations d'une EquipmentData et notifie le CombatManager
/// quand le joueur la sélectionne.
///
/// Structure recommandée pour le préfab :
///   LootCard (Button + LootCard)
///     ├─ EquipmentIcon  (Image)           — icône de la pièce
///     ├─ NameText       (TextMeshProUGUI) — nom de la pièce
///     ├─ TypeText       (TextMeshProUGUI) — type (Tête, Torse, Bras...)
///     ├─ StatsText      (TextMeshProUGUI) — bonus de stats (+HP, +ATK...)
///     ├─ SkillIconContainer (Transform)   — contiendra les icônes de compétences (TODO)
///     └─ SelectedFrame  (GameObject)      — cadre mis en évidence quand la carte est choisie
///
/// TODO UI — Améliorations prévues :
///   1. SkillIconContainer : instancier une icône par compétence de l'équipement.
///      Survoler une icône de compétence → afficher un tooltip (préfab TooltipPanel)
///      avec le nom, le coût, le cooldown et la description du skill.
///   2. Survoler l'icône de l'équipement (ou la carte entière) → afficher le même
///      préfab TooltipPanel avec les stats bonus, la description et les tags de la pièce.
///   Ces deux tooltips partagent le même préfab : TooltipPanel avec un titre,
///   un corps de texte, et une position dynamique qui suit la souris.
/// </summary>
public class LootCard : MonoBehaviour
{
    [Header("Références UI")]
    public Button          button;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI slotText;
    public TextMeshProUGUI statsText;

    // Objet visuel mis en évidence quand la carte est sélectionnée (optionnel)
    public GameObject selectedFrame;

    private EquipmentData equipment;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure la carte avec une pièce d'équipement et un callback de sélection.
    /// Le callback reçoit l'EquipmentData choisie — LootCard n'a pas besoin de
    /// connaître CombatManager directement.
    /// </summary>
    public void Setup(EquipmentData equip, Action<EquipmentData> onChosen)
    {
        equipment = equip;

        if (nameText != null)
            nameText.text = equip.equipmentName;

        if (slotText != null)
            slotText.text = TypeLabel(equip.equipmentType);

        if (statsText != null)
            statsText.text = BuildStatsText(equip);

        if (selectedFrame != null)
            selectedFrame.SetActive(false);

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onChosen?.Invoke(equipment));
    }

    // -----------------------------------------------
    // ÉTAT VISUEL
    // -----------------------------------------------

    /// <summary>
    /// Met en évidence la carte (ou la déselectionne).
    /// Appelé par CombatManager pour montrer le choix courant.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void SetSelected(bool selected)
    {
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);

        // On ne touche pas à button.interactable : toutes les cartes restent cliquables.
        // Le joueur peut changer d'avis et sélectionner une autre carte à tout moment.
        // Le cadre visuel (SelectedFrame) suffit à indiquer le choix courant.
    }

    // -----------------------------------------------
    // UTILITAIRES
    // -----------------------------------------------

    private static string TypeLabel(EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => "Tête",
            EquipmentType.Torso => "Torse",
            EquipmentType.Legs  => "Jambes",
            EquipmentType.Arm   => "Bras",
            _                   => type.ToString()
        };
    }

    /// <summary>
    /// Construit une ligne de stats lisibles à partir des bonus de la pièce.
    /// N'affiche que les stats dont la valeur est non nulle.
    /// </summary>
    private static string BuildStatsText(EquipmentData equip)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (equip.bonusHP      != 0) parts.Add($"HP {Sign(equip.bonusHP)}{equip.bonusHP}");
        if (equip.bonusAttack  != 0) parts.Add($"ATK {Sign(equip.bonusAttack)}{equip.bonusAttack}");
        if (equip.bonusDefense != 0) parts.Add($"DEF {Sign(equip.bonusDefense)}{equip.bonusDefense}");

        var equippedSkillNames = new System.Collections.Generic.List<string>();
        foreach (SkillSlot slot in equip.skillSlots)
        {
            if (slot == null) continue;
            if (slot.state != SkillSlot.SlotState.Used &&
                slot.state != SkillSlot.SlotState.LockedInUse) continue;
            equippedSkillNames.Add(slot.equippedSkill != null ? slot.equippedSkill.skillName : "?");
        }
        if (equippedSkillNames.Count > 0)
            parts.Add($"Sorts : {string.Join(", ", equippedSkillNames)}");

        return parts.Count > 0 ? string.Join("\n", parts) : "Aucun bonus";
    }

    // Renvoie "+" pour les valeurs positives (les négatives ont déjà leur signe)
    private static string Sign(int value) => value > 0 ? "+" : "";

    public EquipmentData Equipment => equipment;
}
