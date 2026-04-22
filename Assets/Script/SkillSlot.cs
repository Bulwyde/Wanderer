using UnityEngine;

/// <summary>
/// Emplacement de compétence sur un équipement.
/// Chaque EquipmentData porte une liste de SkillSlot configurables dans l'inspector.
/// </summary>
[System.Serializable]
public class SkillSlot
{
    // -----------------------------------------------
    // ÉTAT DU SLOT
    // -----------------------------------------------

    public enum SlotState
    {
        Available,      // Libre — peut accueillir un skill
        Used,           // Occupé par un skill équipé
        Unavailable,    // Non-disponible — caché dans l'UI, réserve pour déblocage futur
        LockedInUse,    // Occupé et verrouillé — ne peut pas être retiré par drag'n'drop
    }

    public SlotState state = SlotState.Available;

    // Skill équipé dans ce slot. Null si state = Available ou Unavailable.
    public SkillData equippedSkill = null;
}
