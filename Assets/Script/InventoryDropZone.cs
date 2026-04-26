using UnityEngine;

/// <summary>
/// Marqueur léger attaché aux zones de dépôt de l'inventaire.
/// Identifie le type de zone et les données associées (slot, équipement cible...).
/// Ajouté par InventoryUIManager lors de la construction de la hiérarchie UI.
/// </summary>
public class InventoryDropZone : MonoBehaviour
{
    public enum ZoneType
    {
        SkillSlot,              // Slot d'un équipement — reçoit un skill
        EquipmentSlot,          // Slot du joueur — reçoit un équipement
        Poubelle,               // Zone de suppression (confirmation requise)
        InventaireSkills,       // Zone inventaire skills — déséquipe → retour inventaire
        InventaireEquipements,  // Zone inventaire équipements
    }

    public ZoneType zoneType;

    // Rempli si zoneType == SkillSlot
    public EquipmentData targetEquipment;
    public int           targetSlotIndex = -1;

    // Rempli si zoneType == EquipmentSlot
    public EquipmentSlot targetEquipmentSlot;
}
