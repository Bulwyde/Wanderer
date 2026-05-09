using System.Collections.Generic;

/// <summary>
/// Structure simple : une arme équipée + ses skills actifs.
/// Utilisée par CombatUIArmsController pour construire l'affichage des armes en combat.
/// </summary>
public class CombatUIArmSetup
{
    public EquipmentData arm;
    public List<SkillData> equippedSkills = new List<SkillData>();
}
