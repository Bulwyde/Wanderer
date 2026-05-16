using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Contrôleur UI pour afficher les armes équipées + leurs skills en icones.
/// Remplace l'ancienne hiérarchie de boutons texte.
/// Affichage uniquement si contenu (arme équipée + au moins 1 skill).
/// </summary>
public class CombatUIArmsController : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private Transform armsContainer;
    [SerializeField] private GameObject armSlotUIPrefab;

    // List des slots UI créés (pour cleanup)
    private List<GameObject> _spawnedArmSlots = new List<GameObject>();

    // Callback vers CombatManager pour les clics skills
    private System.Action<SkillData, EquipmentData> _onSkillClicked;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le contrôleur avec les références et callback.
    /// Appelé depuis CombatManager.InitializeArmsUI().
    /// </summary>
    public void Initialize(Transform container, GameObject slotUIPrefab,
                          System.Action<SkillData, EquipmentData> onSkillClickedCallback)
    {
        armsContainer = container;
        armSlotUIPrefab = slotUIPrefab;
        _onSkillClicked = onSkillClickedCallback;
    }

    // -----------------------------------------------
    // CONSTRUCTION DE L'AFFICHAGE
    // -----------------------------------------------

    /// <summary>
    /// Construit la liste des armes équipées + leurs skills.
    /// Retourne une liste de CombatUIArmSetup (une par arme équipée).
    /// </summary>
    public List<CombatUIArmSetup> BuildArmSetups()
    {
        var setups = new List<CombatUIArmSetup>();

        // Itère tous les slots Arm (Arm1..Arm4) via l'enum
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            // Filtre les slots bras seulement
            if (slot != EquipmentSlot.Arm1 && slot != EquipmentSlot.Arm2 &&
                slot != EquipmentSlot.Arm3 && slot != EquipmentSlot.Arm4)
                continue;

            EquipmentData equipment = RunManager.Instance?.GetEquipped(slot);
            if (equipment == null) continue; // Slot vide = skip

            // Collecte les skills équipés (état Used ou LockedInUse)
            var equippedSkills = new List<SkillData>();
            if (equipment.skillSlots != null)
            {
                foreach (SkillSlot skillSlot in equipment.skillSlots)
                {
                    if (skillSlot == null) continue;
                    if (skillSlot.state != SkillSlot.SlotState.Used &&
                        skillSlot.state != SkillSlot.SlotState.LockedInUse)
                        continue;
                    if (skillSlot.equippedSkill != null)
                        equippedSkills.Add(skillSlot.equippedSkill);
                }
            }

            // Affichage seulement si contenu (skills équipés)
            if (equippedSkills.Count > 0)
            {
                setups.Add(new CombatUIArmSetup
                {
                    arm = equipment,
                    equippedSkills = equippedSkills
                });
            }
        }

        return setups;
    }

    /// <summary>
    /// Crée la hiérarchie UI d'affichage des armes + skills.
    /// Appelle BuildArmSetups() puis génère les ArmSlotUI.
    /// </summary>
    public void SpawnArmsUI()
    {
        // Cleanup ancien affichage
        foreach (GameObject go in _spawnedArmSlots)
            Destroy(go);
        _spawnedArmSlots.Clear();

        if (armsContainer == null || armSlotUIPrefab == null)
        {
            Debug.LogWarning("[CombatUIArmsController] armsContainer ou armSlotUIPrefab non assigné!");
            return;
        }

        // Construit la liste des armes + skills
        List<CombatUIArmSetup> setups = BuildArmSetups();

        // Génère un ArmSlotUI par arme
        foreach (CombatUIArmSetup setup in setups)
        {
            GameObject slotGO = Instantiate(armSlotUIPrefab, armsContainer);
            _spawnedArmSlots.Add(slotGO);

            ArmSlotUI armSlotUI = slotGO.GetComponent<ArmSlotUI>();
            if (armSlotUI != null)
            {
                armSlotUI.Setup(setup.arm, setup.equippedSkills, _onSkillClicked);
            }
            else
            {
                Debug.LogWarning("[CombatUIArmsController] ArmSlotUI composant non trouvé sur le prefab!");
            }
        }
    }

    // -----------------------------------------------
    // CLEANUP ET ACCESSEURS
    // -----------------------------------------------

    /// <summary>
    /// Nettoie tous les slots UI générés.
    /// </summary>
    public void Clear()
    {
        foreach (GameObject go in _spawnedArmSlots)
            if (go != null) Destroy(go);
        _spawnedArmSlots.Clear();
    }

    /// <summary>
    /// Retourne tous les SkillIconButton spawned dans tous les ArmSlotUI.
    /// Utilisé par CombatManager pour mettre à jour les états (cooldown, énergie).
    /// </summary>
    public List<SkillIconButton> GetAllSpawnedSkillButtons()
    {
        var allButtons = new List<SkillIconButton>();

        foreach (GameObject slotGO in _spawnedArmSlots)
        {
            if (slotGO == null) continue;

            ArmSlotUI armSlot = slotGO.GetComponent<ArmSlotUI>();
            if (armSlot != null)
            {
                var armSlotButtons = armSlot.GetSpawnedSkillButtons();
                if (armSlotButtons != null)
                    allButtons.AddRange(armSlotButtons);
            }
        }

        return allButtons;
    }
}
