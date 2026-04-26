using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Singleton persistant entre les scènes (DontDestroyOnLoad).
/// Stocke tout l'état de la run en cours : personnage, HP, position sur la carte,
/// salle actuelle, difficulté et flags d'événements.
/// </summary>
public class RunManager : MonoBehaviour
{
    // -----------------------------------------------
    // SINGLETON
    // -----------------------------------------------

    public static RunManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // -----------------------------------------------
    // ÉTAT DE LA RUN EN COURS
    // -----------------------------------------------

    [Header("Personnage")]
    // Référence directe au ScriptableObject du personnage choisi.
    // Assignée par MainMenuManager (ou la future scène de sélection) via StartNewRun().
    public CharacterData selectedCharacter;

    [Header("État du joueur")]
    public int currentHP;
    public int maxHP;

    // Crédits — ressource run persistante (marchands, événements à coût, etc.).
    // Valeur positive uniquement : plancher à 0 dans AddCredits().
    public int credits;

    [Header("Navigation")]
    // Identifiant de la mission (map) en cours
    public string currentMissionID;

    // Position de la salle actuelle sur la carte
    // Stockée avant de changer de scène pour pouvoir y revenir
    public int currentRoomX;
    public int currentRoomY;

    // Type de la salle actuelle (Classic, Boss, Event...)
    // Permet aux autres scènes de savoir dans quel contexte elles s'exécutent
    public CellType currentCellType;

    // Ennemi à affronter dans la salle courante (combat solo).
    // Assigné par EnterRoom() depuis la CellData. Null si groupe ou fallback Inspector.
    public EnemyData currentEnemyData;

    // Groupe d'ennemis à affronter dans la salle courante (combat multi).
    // Assigné par EnterRoom() si la case ou la pool renvoie un groupe.
    // Si non-null, CombatManager l'utilise en priorité sur currentEnemyData.
    public EnemyGroup currentEnemyGroup;

    // ID d'événement spécifique si la salle est de type Event
    public string currentSpecificEventID;

    // MapData de la carte en cours — assigné par NavigationManager avant chaque transition de scène.
    // Permet aux scènes Shop (et futures scènes) d'accéder à la CellData courante.
    public MapData currentMapData;

    [Header("Modificateurs de difficulté")]
    public float difficultyModifier = 1.0f;

    [Header("Flags d'événements")]
    public SerializableDictionary<string, bool> eventFlags = new SerializableDictionary<string, bool>();

    // -----------------------------------------------
    // ÉQUIPEMENT PORTÉ
    // -----------------------------------------------

    // Pièces actuellement équipées, indexées par slot.
    // Un slot absent du dictionnaire (ou valeur null) signifie slot vide.
    //
    // Note architecture : si un inventaire est ajouté un jour, ce dictionnaire
    // reste tel quel — on ajoutera simplement une List<EquipmentData> inventory
    // à côté, et EquipItem() y déplacera l'ancienne pièce au lieu de la jeter.
    private Dictionary<EquipmentSlot, EquipmentData> equippedItems
        = new Dictionary<EquipmentSlot, EquipmentData>();

    /// <summary>
    /// Équipe une pièce dans le slot donné.
    /// Remplace silencieusement ce qui était déjà là.
    /// (Futur : déplacer l'ancienne pièce dans l'inventaire au lieu de la supprimer.)
    /// </summary>
    public void EquipItem(EquipmentSlot slot, EquipmentData item)
    {
        equippedItems[slot] = item;
        Debug.Log($"[RunManager] Équipement — {slot} : {item?.equipmentName ?? "aucun"}");

        // Recalcule le maxHP si les stats ont déjà été initialisées (post-SeedDonneesDepart).
        // Pendant le seeding initial, maxHP vaut 0 → on laisse InitialiserStats() faire son travail.
        if (selectedCharacter != null && maxHP > 0)
            RecalculerMaxHP();
    }

    /// <summary>
    /// Recalcule maxHP à partir de characterData.maxHP + bonus de chaque pièce équipée.
    /// Ajuste currentHP du même delta pour que le joueur "ressente" le gain (style StS).
    /// Appelé automatiquement par EquipItem() pendant la run (hors seeding initial).
    /// </summary>
    public void RecalculerMaxHP()
    {
        if (selectedCharacter == null) return;

        int nouveauMax = selectedCharacter.maxHP;
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = GetEquipped(slot);
            if (equip != null) nouveauMax += equip.bonusHP;
        }
        // Inclut le bonus de run accumulé (events, modules, etc.)
        nouveauMax += Mathf.RoundToInt(GetStatBonus(StatType.MaxHP));
        nouveauMax = Mathf.Max(1, nouveauMax);

        int delta = nouveauMax - maxHP;
        maxHP = nouveauMax;

        if (delta != 0)
        {
            currentHP = Mathf.Clamp(currentHP + delta, 1, maxHP);
            Debug.Log($"[RunManager] MaxHP recalculé — delta : {delta:+#;-#;0} → HP : {currentHP}/{maxHP}");
        }
    }

    /// <summary>
    /// Retourne la pièce équipée dans le slot, ou null si le slot est vide.
    /// </summary>
    public EquipmentData GetEquipped(EquipmentSlot slot)
    {
        return equippedItems.TryGetValue(slot, out EquipmentData item) ? item : null;
    }

    /// <summary>
    /// Retourne true si le slot est vide (aucune pièce équipée).
    /// </summary>
    public bool IsSlotFree(EquipmentSlot slot)
    {
        return GetEquipped(slot) == null;
    }

    // -----------------------------------------------
    // INVENTAIRES
    // -----------------------------------------------

    [Header("Inventaires")]
    public int maxInventoryEquipments = 8;
    public int maxInventorySkills = 16;

    private List<EquipmentData> inventoryEquipments;
    private List<SkillData> inventorySkills;

    /// <summary>
    /// Crée un clone runtime indépendant d'un équipement looté.
    /// Appeler systématiquement pour tout équipement obtenu en jeu (loot, event, shop) :
    /// chaque instance doit pouvoir être modifiée sans impacter l'asset SO d'origine.
    /// </summary>
    public EquipmentData CloneEquipmentForLoot(EquipmentData original)
    {
        if (original == null) return null;

        EquipmentData clone = Instantiate(original);

        // Deep copy des skillSlots — chaque clone a sa propre liste indépendante
        clone.skillSlots = new List<SkillSlot>();
        foreach (SkillSlot slot in original.skillSlots)
        {
            SkillSlot newSlot = new SkillSlot
            {
                state         = slot.state,
                equippedSkill = slot.equippedSkill  // L'asset SkillData lui-même n'est pas cloné (immutable)
            };
            clone.skillSlots.Add(newSlot);
        }

        return clone;
    }

    /// <summary>
    /// Ajoute un équipement à l'inventaire si la capacité le permet.
    /// Retourne true si l'ajout a réussi, false si l'inventaire est plein.
    /// </summary>
    public bool AddEquipmentToInventory(EquipmentData equipment)
    {
        if (equipment == null) return false;

        // Ajouter au premier slot vide
        for (int i = 0; i < inventoryEquipments.Count; i++)
        {
            if (inventoryEquipments[i] == null)
            {
                inventoryEquipments[i] = equipment;
                Debug.Log($"[RunManager] Inventaire — équipement ajouté : '{equipment.equipmentName}' (slot {i}).");
                return true;
            }
        }

        Debug.LogWarning($"[RunManager] Inventaire équipements plein — '{equipment.equipmentName}' non ajouté.");
        return false;  // Aucun slot vide
    }

    /// <summary>
    /// Place un équipement à un index spécifique de l'inventaire.
    /// </summary>
    public bool SetEquipmentToInventorySlot(int slotIndex, EquipmentData equipment)
    {
        if (slotIndex < 0 || slotIndex >= inventoryEquipments.Count) return false;

        // Refuser de placer sur un slot occupé (à moins qu'on place null pour vider)
        if (equipment != null && inventoryEquipments[slotIndex] != null)
            return false;

        inventoryEquipments[slotIndex] = equipment;
        return true;
    }

    /// <summary>
    /// Retire un équipement de l'inventaire (set à null).
    /// Retourne true si l'équipement était présent et a été retiré, false sinon.
    /// </summary>
    public bool RemoveEquipmentFromInventory(EquipmentData equipment)
    {
        for (int i = 0; i < inventoryEquipments.Count; i++)
        {
            if (inventoryEquipments[i] == equipment)
            {
                inventoryEquipments[i] = null;
                Debug.Log($"[RunManager] Inventaire — équipement retiré : '{equipment?.equipmentName}'.");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Ajoute un skill à l'inventaire si la capacité le permet.
    /// Retourne true si l'ajout a réussi, false si l'inventaire est plein.
    /// </summary>
    public bool AddSkillToInventory(SkillData skill)
    {
        if (skill == null) return false;

        // Ajouter au premier slot vide
        for (int i = 0; i < inventorySkills.Count; i++)
        {
            if (inventorySkills[i] == null)
            {
                inventorySkills[i] = skill;
                Debug.Log($"[RunManager] Inventaire — skill ajouté : '{skill.skillName}' (slot {i}).");
                return true;
            }
        }

        Debug.LogWarning($"[RunManager] Inventaire skills plein — '{skill.skillName}' non ajouté.");
        return false;  // Aucun slot vide
    }

    /// <summary>
    /// Place un skill à un index spécifique de l'inventaire.
    /// </summary>
    public bool SetSkillToInventorySlot(int slotIndex, SkillData skill)
    {
        if (slotIndex < 0 || slotIndex >= inventorySkills.Count) return false;

        // Refuser de placer sur un slot occupé (à moins qu'on place null pour vider)
        if (skill != null && inventorySkills[slotIndex] != null)
            return false;

        inventorySkills[slotIndex] = skill;
        return true;
    }

    /// <summary>
    /// Retire un skill de l'inventaire (set à null).
    /// Retourne true si le skill était présent et a été retiré, false sinon.
    /// </summary>
    public bool RemoveSkillFromInventory(SkillData skill)
    {
        for (int i = 0; i < inventorySkills.Count; i++)
        {
            if (inventorySkills[i] == skill)
            {
                inventorySkills[i] = null;
                Debug.Log($"[RunManager] Inventaire — skill retiré : '{skill?.skillName}'.");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Équipe une pièce dans un slot du joueur en gérant correctement l'ancienne pièce.
    /// Si le slot est occupé, déplace l'ancienne pièce dans l'inventaire avant d'équiper.
    /// Si l'inventaire est plein et le slot occupé, l'opération est annulée (retourne false).
    /// Si la nouvelle pièce vient de l'inventaire, elle en est retirée automatiquement.
    /// </summary>
    /// <summary>
    /// Retourne true si le type d'équipement est compatible avec le slot cible.
    /// Head/Torso/Legs → un seul slot correspondant.
    /// Arm → Arm1 ou Arm2.
    /// </summary>
    public static bool IsSlotCompatible(EquipmentSlot slot, EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => slot == EquipmentSlot.Head,
            EquipmentType.Torso => slot == EquipmentSlot.Torso,
            EquipmentType.Legs  => slot == EquipmentSlot.Legs,
            EquipmentType.Arm   => slot == EquipmentSlot.Arm1 || slot == EquipmentSlot.Arm2 ||
                                   slot == EquipmentSlot.Arm3 || slot == EquipmentSlot.Arm4,
            _                   => false
        };
    }

    public bool TryEquipEquipment(EquipmentSlot slot, EquipmentData equipment)
    {
        if (equipment == null) return false;

        if (!IsSlotCompatible(slot, equipment.equipmentType))
        {
            Debug.LogWarning($"[RunManager] TryEquipEquipment — incompatible : " +
                             $"'{equipment.equipmentName}' ({equipment.equipmentType}) ne peut pas" +
                             $" aller dans le slot {slot}.");
            return false;
        }

        // Vérifier que le slot Arm est autorisé par le personnage actuel
        if (selectedCharacter != null)
        {
            if (slot == EquipmentSlot.Arm3 && selectedCharacter.maxEquippedArms < 3)
            {
                Debug.LogWarning($"[RunManager] Slot Arm3 non disponible — maxEquippedArms = {selectedCharacter.maxEquippedArms}");
                return false;
            }
            if (slot == EquipmentSlot.Arm4 && selectedCharacter.maxEquippedArms < 4)
            {
                Debug.LogWarning($"[RunManager] Slot Arm4 non disponible — maxEquippedArms = {selectedCharacter.maxEquippedArms}");
                return false;
            }
        }

        // Si le slot est occupé, tenter de déplacer l'ancienne pièce en inventaire
        EquipmentData ancienne = GetEquipped(slot);
        if (ancienne != null)
        {
            if (!AddEquipmentToInventory(ancienne))
            {
                Debug.LogWarning($"[RunManager] TryEquipEquipment — inventaire plein," +
                                 $" impossible de déplacer '{ancienne.equipmentName}'.");
                return false;
            }
        }

        // Retirer la nouvelle pièce de l'inventaire si elle y était
        RemoveEquipmentFromInventory(equipment);

        equippedItems[slot] = equipment;
        RecalculerMaxHP();

        Debug.Log($"[RunManager] TryEquipEquipment — {slot} : '{equipment.equipmentName}' équipé" +
                  (ancienne != null ? $" ('{ancienne.equipmentName}' → inventaire)" : "") + ".");
        return true;
    }

    /// <summary>
    /// Vide un slot d'équipement sans interaction avec l'inventaire.
    /// Utilisé lors d'un déplacement slot → slot pour libérer l'origine
    /// avant d'équiper dans la destination.
    /// Les skills équipés sur la pièce ne sont PAS récupérés — utiliser
    /// UnequipEquipmentAndMoveSkillsToInventory() quand on veut les conserver.
    /// </summary>
    public void ClearEquipmentSlot(EquipmentSlot slot)
    {
        if (!equippedItems.ContainsKey(slot)) return;
        string nom = equippedItems[slot]?.equipmentName ?? "aucun";
        equippedItems.Remove(slot);
        RecalculerMaxHP();
        Debug.Log($"[RunManager] ClearEquipmentSlot — {slot} vidé ('{nom}').");
    }

    /// <summary>
    /// Déséquipe un équipement et rapatrie ses skills vers l'inventaire avant de libérer le slot.
    /// À utiliser à la place de ClearEquipmentSlot() quand l'équipement est retiré définitivement
    /// (suppression via poubelle, déséquipement manuel) et que les skills ne doivent pas être perdus.
    /// </summary>
    public void UnequipEquipmentAndMoveSkillsToInventory(EquipmentSlot slot)
    {
        if (!equippedItems.ContainsKey(slot)) return;

        EquipmentData equip = equippedItems[slot];

        // Rapatrier les skills équipés en inventaire avant de vider le slot
        if (equip != null && equip.skillSlots != null)
        {
            foreach (SkillSlot skillSlot in equip.skillSlots)
            {
                if ((skillSlot.state == SkillSlot.SlotState.Used ||
                     skillSlot.state == SkillSlot.SlotState.LockedInUse) &&
                    skillSlot.equippedSkill != null)
                {
                    if (AddSkillToInventory(skillSlot.equippedSkill))
                    {
                        // Nettoyer le slot seulement si le rapatriation a réussi
                        skillSlot.equippedSkill = null;
                        skillSlot.state         = SkillSlot.SlotState.Available;
                    }
                    else
                        Debug.LogWarning($"[RunManager] Inventaire skills plein — '{skillSlot.equippedSkill.skillName}' conservé sur son slot (non perdu).");
                }
            }
        }

        equippedItems.Remove(slot);
        RecalculerMaxHP();
        Debug.Log($"[RunManager] UnequipEquipmentAndMoveSkillsToInventory — {slot} vidé ('{equip?.equipmentName ?? "aucun"}').");
    }

    /// <summary>
    /// Retourne une copie de la liste des équipements en inventaire.
    /// </summary>
    public List<EquipmentData> GetInventoryEquipments() => new List<EquipmentData>(inventoryEquipments);

    /// <summary>
    /// Retourne une copie de la liste des skills en inventaire.
    /// </summary>
    public List<SkillData> GetInventorySkills() => new List<SkillData>(inventorySkills);

    // -----------------------------------------------
    // ÉQUIPEMENT / DÉSÉQUIPEMENT DE SKILLS
    // -----------------------------------------------

    /// <summary>
    /// Retourne les tags de l'équipement qui ne sont pas déjà présents dans les tags propres du skill.
    /// Ces tags sont ceux à ajouter dans inheritedTags lors de l'équipement (et à retirer lors du déséquipement).
    /// Évite les doublons si le skill et l'équipement partagent un même tag.
    /// </summary>
    private List<TagData> GetEquipmentTagsNotInSkill(EquipmentData equipment, SkillData skill)
    {
        List<TagData> result = new List<TagData>();
        if (equipment == null || equipment.tags == null || skill == null || skill.tags == null)
            return result;

        foreach (TagData tag in equipment.tags)
        {
            if (tag == null) continue;
            bool déjàPrésent = false;
            foreach (TagData skillTag in skill.tags)
            {
                if (skillTag != null && skillTag.tagName == tag.tagName)
                {
                    déjàPrésent = true;
                    break;
                }
            }
            if (!déjàPrésent) result.Add(tag);
        }

        return result;
    }

    /// <summary>
    /// Équipe un skill dans un slot d'équipement.
    /// Le skill doit être dans l'inventaire. Le slot doit être dans l'état Available.
    /// Les tags de l'équipement non-présents dans le skill sont ajoutés à inheritedTags.
    /// </summary>
    public bool EquipSkill(EquipmentData equipment, int slotIndex, SkillData skill)
    {
        if (equipment == null || skill == null)
        {
            Debug.LogWarning("[RunManager] EquipSkill — equipment ou skill null.");
            return false;
        }
        if (slotIndex < 0 || slotIndex >= equipment.skillSlots.Count)
        {
            Debug.LogWarning($"[RunManager] EquipSkill — index {slotIndex} hors limites" +
                             $" (taille : {equipment.skillSlots.Count}).");
            return false;
        }

        SkillSlot slot = equipment.skillSlots[slotIndex];

        switch (slot.state)
        {
            case SkillSlot.SlotState.Unavailable:
                Debug.LogWarning($"[RunManager] EquipSkill — slot {slotIndex} de '{equipment.equipmentName}' non disponible.");
                return false;
            case SkillSlot.SlotState.LockedInUse:
                Debug.LogWarning($"[RunManager] EquipSkill — slot {slotIndex} de '{equipment.equipmentName}' verrouillé.");
                return false;
            case SkillSlot.SlotState.Used:
                Debug.LogWarning($"[RunManager] EquipSkill — slot {slotIndex} de '{equipment.equipmentName}' déjà occupé." +
                                 " Utiliser SwapSkill() pour remplacer.");
                return false;
        }

        // Appliquer les tags hérités (tags de l'équipement absents des tags propres du skill)
        List<TagData> tagsÀHériter = GetEquipmentTagsNotInSkill(equipment, skill);
        foreach (TagData tag in tagsÀHériter)
            skill.inheritedTags.Add(tag);

        slot.state         = SkillSlot.SlotState.Used;
        slot.equippedSkill = skill;
        RemoveSkillFromInventory(skill);

        Debug.Log($"[RunManager] EquipSkill — '{skill.skillName}' → slot {slotIndex} de '{equipment.equipmentName}'" +
                  $" ({tagsÀHériter.Count} tag(s) hérité(s)).");
        return true;
    }

    /// <summary>
    /// Déséquipe le skill d'un slot d'équipement et le remet dans l'inventaire.
    /// Les tags hérités de l'équipement sont retirés du skill.
    /// Échoue si le slot est vide, verrouillé ou si l'inventaire de skills est plein.
    /// </summary>
    public bool UnequipSkill(EquipmentData equipment, int slotIndex)
    {
        if (equipment == null)
        {
            Debug.LogWarning("[RunManager] UnequipSkill — equipment null.");
            return false;
        }
        if (slotIndex < 0 || slotIndex >= equipment.skillSlots.Count)
        {
            Debug.LogWarning($"[RunManager] UnequipSkill — index {slotIndex} hors limites" +
                             $" (taille : {equipment.skillSlots.Count}).");
            return false;
        }

        SkillSlot slot = equipment.skillSlots[slotIndex];

        if (slot.state != SkillSlot.SlotState.Used && slot.state != SkillSlot.SlotState.LockedInUse)
        {
            Debug.LogWarning($"[RunManager] UnequipSkill — slot {slotIndex} de '{equipment.equipmentName}'" +
                             " n'est pas occupé.");
            return false;
        }
        if (slot.state == SkillSlot.SlotState.LockedInUse)
        {
            Debug.LogWarning($"[RunManager] UnequipSkill — slot {slotIndex} de '{equipment.equipmentName}'" +
                             " verrouillé, impossible de déséquiper.");
            return false;
        }

        SkillData skill = slot.equippedSkill;

        // Retirer les tags hérités de l'équipement
        List<TagData> tagsÀRetirer = GetEquipmentTagsNotInSkill(equipment, skill);
        foreach (TagData tag in tagsÀRetirer)
            skill.inheritedTags.Remove(tag);

        // Tenter de remettre le skill en inventaire avant de libérer le slot
        if (!AddSkillToInventory(skill))
        {
            // Revert : remettre les tags hérités
            foreach (TagData tag in tagsÀRetirer)
                skill.inheritedTags.Add(tag);

            Debug.LogWarning($"[RunManager] UnequipSkill — inventaire skills plein," +
                             $" impossible de déséquiper '{skill.skillName}'.");
            return false;
        }

        slot.state         = SkillSlot.SlotState.Available;
        slot.equippedSkill = null;

        Debug.Log($"[RunManager] UnequipSkill — '{skill.skillName}' retiré du slot {slotIndex}" +
                  $" de '{equipment.equipmentName}' ({tagsÀRetirer.Count} tag(s) hérité(s) retirés).");
        return true;
    }

    /// <summary>
    /// Remplace le skill d'un slot par un nouveau skill en une seule transaction.
    /// Déséquipe l'ancien (→ inventaire), puis équipe le nouveau (← inventaire).
    /// Échoue si le slot n'est pas occupé, si le skill est verrouillé, ou si l'inventaire est plein.
    /// </summary>
    public bool SwapSkill(EquipmentData equipment, int slotIndex, SkillData newSkill)
    {
        if (!UnequipSkill(equipment, slotIndex))
            return false;

        if (!EquipSkill(equipment, slotIndex, newSkill))
        {
            Debug.LogError($"[RunManager] SwapSkill — déséquipement réussi mais EquipSkill a échoué" +
                           $" pour '{newSkill?.skillName}' au slot {slotIndex}. État incohérent.");
            return false;
        }

        return true;
    }

    // -----------------------------------------------
    // MODULES ACTIFS
    // -----------------------------------------------

    // Modules passifs acquis pendant la run (équivalent des reliques dans StS).
    // Le module de départ du personnage est seedé au premier combat via CombatManager.
    private List<ModuleData> activeModules = new List<ModuleData>();

    /// <summary>
    /// Ajoute un module à la liste des modules actifs.
    /// Ignore les doublons — un même module ne peut être ajouté qu'une fois.
    /// </summary>
    public void AddModule(ModuleData module)
    {
        if (module == null || activeModules.Contains(module)) return;
        activeModules.Add(module);
        Debug.Log($"[RunManager] Module acquis : {module.moduleName}");

        // Notifie les HUDs pour qu'ils rafraîchissent l'affichage des icônes
        ModuleManager.NotifyModulesChanged();
    }

    /// <summary>
    /// Retourne une copie de la liste des modules actifs.
    /// </summary>
    public List<ModuleData> GetModules() => new List<ModuleData>(activeModules);

    /// <summary>
    /// Retourne true si le module est actuellement actif.
    /// </summary>
    public bool HasModule(ModuleData module) => module != null && activeModules.Contains(module);

    // -----------------------------------------------
    // CONSOMMABLES
    // -----------------------------------------------

    // Nombre de slots disponibles — 3 par défaut, modifiable par modules (max 6)
    public int maxConsumableSlots = 3;

    // Indique si les consommables de départ ont déjà été donnés au joueur ce run.
    // Évite de les redonner si le joueur a tout utilisé et entre dans un nouveau combat.
    public bool startingConsumablesSeeded = false;

    // Consommables actuellement en possession du joueur
    private List<ConsumableData> consumables = new List<ConsumableData>();

    /// <summary>
    /// Ajoute un consommable si un slot est disponible.
    /// Retourne true si l'ajout réussit, false si l'inventaire est plein.
    /// </summary>
    public bool AddConsumable(ConsumableData consumable)
    {
        if (consumable == null) return false;

        int effectiveMax = Mathf.Clamp(maxConsumableSlots, 1, 6);
        if (consumables.Count >= effectiveMax)
        {
            Debug.Log($"[RunManager] Impossible d'ajouter {consumable.consumableName} " +
                      $"— inventaire plein ({consumables.Count}/{effectiveMax})");
            return false;
        }

        consumables.Add(consumable);
        Debug.Log($"[RunManager] Consommable obtenu : {consumable.consumableName} " +
                  $"({consumables.Count}/{effectiveMax})");
        return true;
    }

    /// <summary>
    /// Retire un consommable de l'inventaire (après utilisation).
    /// </summary>
    public void RemoveConsumable(ConsumableData consumable)
    {
        if (consumables.Remove(consumable))
            Debug.Log($"[RunManager] Consommable utilisé : {consumable.consumableName}");
    }

    /// <summary>
    /// Retourne une copie de la liste des consommables actifs.
    /// </summary>
    public List<ConsumableData> GetConsumables() => new List<ConsumableData>(consumables);

    /// <summary>
    /// Retourne true si le joueur a au moins un slot de consommable libre.
    /// </summary>
    public bool HasConsumableSlotFree() => consumables.Count < Mathf.Clamp(maxConsumableSlots, 1, 6);

    // Salles complétées pendant la run — stockées par clé "x,y".
    // HashSet est idéal ici : vérification instantanée, pas de doublons.
    // Ce n'est pas sérialisé car c'est un état purement runtime (réinitialisé à chaque run).
    private HashSet<string> clearedRooms = new HashSet<string>();

    // -----------------------------------------------
    // ÉTATS DES MARCHANDS
    // -----------------------------------------------

    // Un ShopState par case Marchand, indexé par clé "x,y".
    // Généré à la première visite, conservé pour toute la run.
    // Les items achetés sont marqués dans ShopState : ils restent visibles mais grisés.
    private Dictionary<string, ShopState> shopStates = new Dictionary<string, ShopState>();

    // -----------------------------------------------
    // CASES ALÉATOIRES ET POST-VISITE
    // -----------------------------------------------

    // Types résolus des cases Aléatoires (clé : "x,y").
    // Peuplé par NavigationManager au premier affichage de la carte.
    private Dictionary<string, CellType> resolvedAleatoireCells = new Dictionary<string, CellType>();

    // Types affichés après visite (Ferrailleur→FerailleurUtilise, Teleporteur→TeleporteurUtilise, etc.)
    // Clé : "x,y". Consulté par MapRenderer pour choisir la couleur/icône post-visite.
    private Dictionary<string, CellType> postVisitCellTypes = new Dictionary<string, CellType>();

    // Cases remplacées par le système de maximum (clé : "x,y").
    // Distinct de resolvedAleatoireCells — ces cases avaient un type fixe mais dépassaient le quota.
    private Dictionary<string, CellType> overridesMaximum = new Dictionary<string, CellType>();

    // Indique si la carte courante a déjà été initialisée ce run (cases Aléatoires résolues, etc.).
    public bool carteInitialisee = false;

    // NavEffects déclenchés hors scène Navigation (ex. depuis une scène Event)
    // et en attente d'application au prochain chargement de NavigationManager.
    public List<NavEffect> navEffectsEnAttente = new List<NavEffect>();

    // -----------------------------------------------
    // COMPTEURS DE NAVIGATION
    // -----------------------------------------------

    // Compteurs nommés — usage libre pour les mécaniques de run.
    // Ex : "cles" pour ouvrir des passages, "ferveur" pour un événement conditionnel.
    // Accès via GetCounter / IncrementCounter / SetCounter.
    private Dictionary<string, int> navigationCounters = new Dictionary<string, int>();

    // Bonus de portée de vision accumulés pendant le run.
    // S'ajoute au baseVisionRange défini dans NavigationManager.
    public int visionRangeBonus = 0;

    // Nombre total de combats gagnés pendant ce run — utilisé par les cooldowns de type CombatsTermines.
    public int combatsTermines = 0;

    // Nombre total d'événements complétés pendant ce run — utilisé par les cooldowns de type EventsTermines.
    public int eventsTermines = 0;

    // -----------------------------------------------
    // COOLDOWNS DE COMPÉTENCES DE NAVIGATION
    // -----------------------------------------------

    // État de cooldown des compétences de navigation.
    // Clé = skillID, valeur = type + nombre de déclenchements encore nécessaires.
    // Clé absente (ou remaining ≤ 0) = compétence disponible.
    private Dictionary<string, NavSkillCooldownState> navSkillCooldowns
        = new Dictionary<string, NavSkillCooldownState>();

    /// <summary>
    /// Met une compétence de navigation en cooldown.
    /// Sera rechargée après <count> occurrences de l'événement correspondant à <type>.
    /// Ignoré si type vaut None ou si skillID est vide.
    /// </summary>
    public void SetNavSkillCooldown(string skillID, NavCooldownType type, int count, TagData tagRequis = null)
    {
        if (string.IsNullOrEmpty(skillID) || type == NavCooldownType.None) return;
        navSkillCooldowns[skillID] = new NavSkillCooldownState
        {
            type      = type,
            remaining = Mathf.Max(1, count),
            tagRequis = tagRequis
        };
        Debug.Log($"[RunManager] Cooldown — '{skillID}' en cooldown ({type} x{count})");
    }

    /// <summary>
    /// Retourne true si la compétence de navigation est prête à être utilisée.
    /// Un skillID vide ou absent du dictionnaire = toujours prêt.
    /// </summary>
    public bool IsNavSkillReady(string skillID)
    {
        if (string.IsNullOrEmpty(skillID)) return true;
        return !navSkillCooldowns.TryGetValue(skillID, out NavSkillCooldownState state)
               || state.remaining <= 0;
    }

    /// <summary>
    /// Décrémente d'un cran tous les cooldowns du type donné.
    /// Les compétences qui atteignent 0 sont retirées du dictionnaire (= de nouveau disponibles).
    /// </summary>
    public void TickCooldownsDe(NavCooldownType type)
    {
        if (type == NavCooldownType.None) return;

        List<string> aRetirer = new List<string>();
        List<string> cles = new List<string>(navSkillCooldowns.Keys);

        foreach (string skillID in cles)
        {
            NavSkillCooldownState state = navSkillCooldowns[skillID];
            if (state.type != type) continue;

            state.remaining--;
            if (state.remaining <= 0)
            {
                aRetirer.Add(skillID);
                Debug.Log($"[RunManager] Cooldown — '{skillID}' rechargé ({type})");
            }
            else
            {
                Debug.Log($"[RunManager] Cooldown — '{skillID}' : {state.remaining} restant(s) ({type})");
            }
        }

        foreach (string skillID in aRetirer)
            navSkillCooldowns.Remove(skillID);
    }

    /// <summary>
    /// Décrémente d'un cran les cooldowns de type EnnemisAvecTag
    /// dont le tag requis figure dans la liste de tags de l'ennemi vaincu.
    /// </summary>
    public void TickCooldownsAvecTag(List<TagData> tagsEnnemi)
    {
        if (tagsEnnemi == null || tagsEnnemi.Count == 0) return;

        List<string> aRetirer = new List<string>();
        List<string> cles = new List<string>(navSkillCooldowns.Keys);

        foreach (string skillID in cles)
        {
            NavSkillCooldownState state = navSkillCooldowns[skillID];
            if (state.type != NavCooldownType.CombatEnnemisAvecTag) continue;
            if (state.tagRequis == null || !tagsEnnemi.Contains(state.tagRequis)) continue;

            state.remaining--;
            if (state.remaining <= 0)
            {
                aRetirer.Add(skillID);
                Debug.Log($"[RunManager] Cooldown — '{skillID}' rechargé (tag {state.tagRequis.tagName})");
            }
            else
            {
                Debug.Log($"[RunManager] Cooldown — '{skillID}' : {state.remaining} restant(s) (tag {state.tagRequis.tagName})");
            }
        }

        foreach (string skillID in aRetirer)
            navSkillCooldowns.Remove(skillID);
    }

    // -----------------------------------------------
    // BONUS DE STATS DE RUN
    // -----------------------------------------------

    // Modificateurs de stats permanents accumulés pendant le run (events, modules, etc.).
    // Lus par CombatManager.ResolveEquipment() pour les intégrer aux stats effectives.
    // MaxHP est traité séparément : il met aussi à jour maxHP et currentHP directement.
    private Dictionary<StatType, float> runStatBonuses = new Dictionary<StatType, float>();

    /// <summary>
    /// Ajoute un bonus permanent à une stat pour le reste du run.
    /// Pour MaxHP : met aussi à jour maxHP et currentHP immédiatement (style StS).
    /// </summary>
    public void AddStatBonus(StatType stat, float value)
    {
        if (!runStatBonuses.ContainsKey(stat))
            runStatBonuses[stat] = 0f;
        runStatBonuses[stat] += value;

        // MaxHP : synchronise les champs maxHP et currentHP en plus du dictionnaire
        if (stat == StatType.MaxHP)
        {
            int delta    = Mathf.RoundToInt(value);
            int newMax   = Mathf.Max(1, maxHP + delta);
            int actualDelta = newMax - maxHP;
            maxHP = newMax;
            // Gain de maxHP → même gain sur les HP courants (style Slay the Spire)
            if (actualDelta > 0)
                currentHP = Mathf.Min(currentHP + actualDelta, maxHP);
            else
                currentHP = Mathf.Min(currentHP, maxHP);
            Debug.Log($"[RunManager] Bonus run — MaxHP {(value >= 0 ? "+" : "")}{value} " +
                      $"→ maxHP : {maxHP}, HP : {currentHP}");
        }
        else
        {
            Debug.Log($"[RunManager] Bonus run — {stat} {(value >= 0 ? "+" : "")}{value} " +
                      $"→ total : {runStatBonuses[stat]}");
        }
    }

    /// <summary>
    /// Retourne le bonus de run accumulé pour une stat donnée (0 si aucun).
    /// </summary>
    public float GetStatBonus(StatType stat)
    {
        return runStatBonuses.TryGetValue(stat, out float bonus) ? bonus : 0f;
    }

    // -----------------------------------------------
    // CRÉDITS
    // -----------------------------------------------

    /// <summary>
    /// Modifie les crédits du joueur. Valeur positive = gain, négative = dépense.
    /// Le total ne peut jamais descendre sous 0.
    /// </summary>
    public void AddCredits(int amount)
    {
        int avant = credits;
        credits = Mathf.Max(0, credits + amount);
        string signe = amount >= 0 ? "+" : "";
        Debug.Log($"[RunManager] Crédits : {signe}{amount} → {credits} (était {avant})");
    }

    /// <summary>
    /// Retourne true si le joueur possède au moins <amount> crédits.
    /// </summary>
    public bool HasEnoughCredits(int amount)
    {
        return credits >= amount;
    }

    /// <summary>
    /// Incrémente (ou décrémente) un compteur nommé de `delta`.
    /// Crée le compteur s'il n'existe pas encore (valeur de départ : 0).
    /// </summary>
    public void IncrementCounter(string key, int delta)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (!navigationCounters.ContainsKey(key))
            navigationCounters[key] = 0;
        navigationCounters[key] += delta;
        Debug.Log($"[RunManager] Compteur '{key}' : {navigationCounters[key]} ({(delta >= 0 ? "+" : "")}{delta})");
    }

    /// <summary>
    /// Retourne la valeur actuelle d'un compteur nommé (0 si inexistant).
    /// </summary>
    public int GetCounter(string key)
    {
        return string.IsNullOrEmpty(key) ? 0 :
               navigationCounters.TryGetValue(key, out int val) ? val : 0;
    }

    /// <summary>
    /// Pose un compteur à une valeur absolue.
    /// </summary>
    public void SetCounter(string key, int value)
    {
        if (string.IsNullOrEmpty(key)) return;
        navigationCounters[key] = value;
        Debug.Log($"[RunManager] Compteur '{key}' fixé à {value}");
    }

    // -----------------------------------------------
    // ÉVÉNEMENTS JOUÉS
    // -----------------------------------------------

    // IDs des événements déjà joués pendant la run.
    // Permet d'exclure un event du pool d'une salle s'il a déjà été déclenché.
    private HashSet<string> playedEventIDs = new HashSet<string>();

    /// <summary>
    /// Marque un événement comme joué.
    /// Appelé par EventManager quand le joueur clique sur "Continuer".
    /// </summary>
    public void MarkEventPlayed(string eventID)
    {
        if (string.IsNullOrEmpty(eventID)) return;
        playedEventIDs.Add(eventID);
        Debug.Log($"[RunManager] Événement '{eventID}' marqué comme joué.");
    }

    /// <summary>
    /// Retourne true si cet événement a déjà été joué pendant la run.
    /// </summary>
    public bool IsEventPlayed(string eventID)
    {
        return !string.IsNullOrEmpty(eventID) && playedEventIDs.Contains(eventID);
    }

    // -----------------------------------------------
    // ÉTAT DE NAVIGATION SAUVEGARDÉ
    // -----------------------------------------------

    // Indique si on a un état de navigation à restaurer.
    // Vaut false au démarrage d'un run (le joueur n'est pas encore parti en combat).
    [Header("État de la run")]
    public bool hasActiveRun = false;

    [Header("Navigation sauvegardée")]
    public bool hasNavigationState = false;

    // Dernière position connue du joueur sur la carte
    public int savedPlayerX;
    public int savedPlayerY;

    // Listes des cases visitées et explorées.
    // On utilise List<Vector2Int> plutôt que HashSet car Unity
    // peut sérialiser les List mais pas les HashSet.
    // NavigationManager les reconvertira en HashSet au chargement.
    public List<Vector2Int> savedVisitedCells  = new List<Vector2Int>();
    public List<Vector2Int> savedExploredCells = new List<Vector2Int>();

    // -----------------------------------------------
    // MÉTHODES
    // -----------------------------------------------

    /// <summary>
    /// Initialise une nouvelle run depuis zéro.
    /// </summary>
    public void StartNewRun(CharacterData character, string missionID)
    {
        selectedCharacter = character;
        currentMissionID  = missionID;
        currentRoomX        = 0;
        currentRoomY        = 0;
        difficultyModifier  = 1.0f;
        eventFlags.Clear();
        clearedRooms.Clear();
        playedEventIDs.Clear();
        equippedItems.Clear();
        activeModules.Clear();
        consumables.Clear();
        maxConsumableSlots = 3;
        startingConsumablesSeeded = false;
        navigationCounters.Clear();
        visionRangeBonus = 0;
        combatsTermines = 0;
        eventsTermines = 0;
        navSkillCooldowns.Clear();
        runStatBonuses.Clear();
        credits = 0;
        shopStates.Clear();
        resolvedAleatoireCells.Clear();
        postVisitCellTypes.Clear();
        overridesMaximum.Clear();
        carteInitialisee = false;
        navEffectsEnAttente.Clear();
        currentMapData   = null;
        currentEnemyData  = null;
        currentEnemyGroup = null;

        // Réinitialise l'état de navigation : le joueur repart de la case de départ
        hasNavigationState = false;
        savedVisitedCells.Clear();
        savedExploredCells.Clear();

        hasActiveRun = true;

        // Initialiser les inventaires à la taille max avec nulls (slots vides)
        inventoryEquipments = new List<EquipmentData>(maxInventoryEquipments);
        for (int i = 0; i < maxInventoryEquipments; i++)
            inventoryEquipments.Add(null);

        inventorySkills = new List<SkillData>(maxInventorySkills);
        for (int i = 0; i < maxInventorySkills; i++)
            inventorySkills.Add(null);

        // Seed l'équipement, le module et les consommables de départ
        // dès le lancement du run, pour que la Navigation les affiche immédiatement.
        SeedDonneesDepart(character);

        Debug.Log($"[RunManager] Nouveau run — Personnage : {character?.characterName ?? "inconnu"} | Mission : {missionID}");
    }

    /// <summary>
    /// Enregistre la salle dans laquelle le joueur entre.
    /// Appelé par NavigationManager juste avant de changer de scène,
    /// pour que la scène de destination sache quelle salle traiter.
    /// Utilise GetEffectiveCellType pour que les cases Aléatoires résolues
    /// et les overrides de maximum se comportent correctement dans les scènes cibles.
    /// </summary>
    public void EnterRoom(CellData cell)
    {
        currentRoomX    = cell.x;
        currentRoomY    = cell.y;
        currentCellType = GetEffectiveCellType(cell);

        // Réinitialise les deux champs avant résolution
        currentEnemyData  = null;
        currentEnemyGroup = null;

        // Priorité de résolution :
        //   1. specificGroup sur la case (groupe fixe)
        //   2. specificEnemy sur la case (ennemi fixe solo)
        //   3. Pool de la MapData → peut renvoyer un groupe ou un solo
        //   4. Null → fallback Inspector dans CombatManager
        if (cell.specificGroup != null)
        {
            currentEnemyGroup = cell.specificGroup;
            Debug.Log($"[RunManager] Entrée en salle ({cell.x},{cell.y}) — Type : {cell.cellType}" +
                      $" | Groupe fixe : {currentEnemyGroup.groupName}");
        }
        else if (cell.specificEnemy != null)
        {
            currentEnemyData = cell.specificEnemy;
            Debug.Log($"[RunManager] Entrée en salle ({cell.x},{cell.y}) — Type : {cell.cellType}" +
                      $" | Ennemi fixe : {currentEnemyData.enemyName}");
        }
        else
        {
            EnemyPool pool = ResolveEnemyPool(currentCellType);
            if (pool != null)
            {
                EnemyPoolEntry entry = pool.PickRandom();
                if (entry != null)
                {
                    if (entry.IsGroup)
                        currentEnemyGroup = entry.enemyGroup;
                    else
                        currentEnemyData = entry.enemyData;
                }
            }

            string nomRencontre = currentEnemyGroup != null ? $"Groupe : {currentEnemyGroup.groupName}"
                                : currentEnemyData  != null ? $"Ennemi : {currentEnemyData.enemyName}"
                                : "fallback Inspector";
            Debug.Log($"[RunManager] Entrée en salle ({cell.x},{cell.y}) — Type : {cell.cellType} | {nomRencontre}");
        }

        // currentSpecificEventID est assigné par NavigationManager après tirage dans ChoisirEventAleatoire()
    }

    /// <summary>
    /// Retourne la EnemyPool de la MapData courante correspondant au type de case.
    /// Retourne null si currentMapData est null ou si aucune pool n'est assignée pour ce type.
    /// </summary>
    private EnemyPool ResolveEnemyPool(CellType cellType)
    {
        if (currentMapData == null) return null;

        switch (cellType)
        {
            case CellType.CombatSimple: return currentMapData.normalEnemyPool;
            case CellType.Elite:        return currentMapData.eliteEnemyPool;
            case CellType.Boss:         return currentMapData.bossEnemyPool;
            default:                    return null;
        }
    }

    /// <summary>
    /// Sauvegarde l'état complet de navigation avant de quitter la scène Navigation.
    /// Appelé par NavigationManager juste avant GoToCombat().
    /// On passe les HashSets qu'on convertit en Lists pour la sérialisation.
    /// </summary>
    public void SaveNavigationState(int playerX, int playerY,
        HashSet<Vector2Int> visitedCells, HashSet<Vector2Int> exploredCells)
    {
        savedPlayerX = playerX;
        savedPlayerY = playerY;

        // Conversion HashSet → List
        savedVisitedCells  = new List<Vector2Int>(visitedCells);
        savedExploredCells = new List<Vector2Int>(exploredCells);

        hasNavigationState = true;
        Debug.Log($"Navigation sauvegardée — Position : ({playerX}, {playerY}), " +
                  $"Visitées : {visitedCells.Count}, Explorées : {exploredCells.Count}");
    }

    /// <summary>
    /// Marque la salle courante (currentRoomX, currentRoomY) comme complétée.
    /// Calcule et stocke le type post-visite via DeterminerPostVisitType.
    /// Appelé par CombatManager quand le joueur remporte le combat.
    /// </summary>
    public void ClearCurrentRoom()
    {
        string key = $"{currentRoomX},{currentRoomY}";
        clearedRooms.Add(key);

        // currentCellType est déjà le type effectif (résolu par GetEffectiveCellType dans EnterRoom)
        CellType postVisit = DeterminerPostVisitType(currentCellType);
        SetPostVisitType(currentRoomX, currentRoomY, postVisit);

        Debug.Log($"[RunManager] Salle ({currentRoomX},{currentRoomY}) complétée — affichage : {postVisit}");
    }

    /// <summary>
    /// Retourne le type affiché sur la carte après visite d'une salle.
    /// Ferrailleur et Teleporteur passent à leur variante "Utilisé".
    /// Shop reste Shop (marchand toujours accessible).
    /// Tous les autres types (combat, event, etc.) deviennent Empty.
    /// </summary>
    private CellType DeterminerPostVisitType(CellType typeEffectif)
    {
        switch (typeEffectif)
        {
            case CellType.Ferrailleur:  return CellType.FerailleurUtilise;
            case CellType.Teleporteur:  return CellType.TeleporteurUtilise;
            case CellType.Shop:         return CellType.Shop;
            case CellType.PointInteret: return CellType.Empty;
            case CellType.Radar:        return CellType.Empty;
            case CellType.Coffre:       return CellType.Empty;
            default:                    return CellType.Empty;
        }
    }

    /// <summary>
    /// Retourne true si la salle à la position (x, y) a déjà été complétée.
    /// Utilisé par MapRenderer pour afficher les salles vidées différemment.
    /// </summary>
    public bool IsRoomCleared(int x, int y)
    {
        return clearedRooms.Contains($"{x},{y}");
    }

    // -----------------------------------------------
    // CASES ALÉATOIRES
    // -----------------------------------------------

    /// <summary>
    /// Enregistre le type résolu d'une case Aléatoire.
    /// Appelé par NavigationManager lors de l'initialisation de la carte.
    /// </summary>
    public void SetResolvedAleatoire(int x, int y, CellType type)
    {
        resolvedAleatoireCells[$"{x},{y}"] = type;
    }

    /// <summary>
    /// Retourne le type résolu d'une case Aléatoire.
    /// Retourne CellType.Aleatoire si la case n'a pas encore été résolue.
    /// </summary>
    public CellType GetResolvedAleatoire(int x, int y)
    {
        return resolvedAleatoireCells.TryGetValue($"{x},{y}", out CellType type)
            ? type
            : CellType.Aleatoire;
    }

    /// <summary>
    /// Retourne true si le type de la case Aléatoire à (x, y) a déjà été tiré.
    /// </summary>
    public bool IsAleatoireResolu(int x, int y)
    {
        return resolvedAleatoireCells.ContainsKey($"{x},{y}");
    }

    // -----------------------------------------------
    // OVERRIDES DE MAXIMUM
    // -----------------------------------------------

    /// <summary>
    /// Enregistre un override de type pour une case dépassant son quota de maximum.
    /// Appelé par NavigationManager.InitialiserCarte() à la première arrivée sur la carte.
    /// </summary>
    public void SetOverrideMaximum(int x, int y, CellType type)
    {
        overridesMaximum[$"{x},{y}"] = type;
    }

    /// <summary>
    /// Retourne true si une case a été remplacée par le système de maximum.
    /// </summary>
    public bool HasOverrideMaximum(int x, int y)
    {
        return overridesMaximum.ContainsKey($"{x},{y}");
    }

    /// <summary>
    /// Retourne le type de remplacement d'une case overridée, ou CellType.Empty si non défini.
    /// </summary>
    public CellType GetOverrideMaximum(int x, int y)
    {
        return overridesMaximum.TryGetValue($"{x},{y}", out CellType type)
            ? type
            : CellType.Empty;
    }

    /// <summary>
    /// Retourne le type effectif d'une case en appliquant la chaîne de priorité :
    /// postVisitCellTypes → overridesMaximum → resolvedAleatoireCells (si Aléatoire) → cellType original.
    /// Source unique de vérité pour tout code qui a besoin du type réel d'une case au runtime.
    /// </summary>
    public CellType GetEffectiveCellType(CellData cell)
    {
        string cle = $"{cell.x},{cell.y}";

        if (postVisitCellTypes.TryGetValue(cle, out CellType postVisit))
            return postVisit;

        if (overridesMaximum.TryGetValue(cle, out CellType overrideType))
            return overrideType;

        if (cell.cellType == CellType.Aleatoire &&
            resolvedAleatoireCells.TryGetValue(cle, out CellType resolu))
            return resolu;

        return cell.cellType;
    }

    // -----------------------------------------------
    // TYPES POST-VISITE
    // -----------------------------------------------

    /// <summary>
    /// Enregistre le type affiché après visite d'une case
    /// (ex : Ferrailleur → FerailleurUtilise, Teleporteur → TeleporteurUtilise).
    /// Appelé par NavigationManager quand le joueur quitte la salle.
    /// </summary>
    public void SetPostVisitType(int x, int y, CellType type)
    {
        postVisitCellTypes[$"{x},{y}"] = type;
    }

    /// <summary>
    /// Retourne true si une case a un type post-visite enregistré.
    /// </summary>
    public bool HasPostVisitType(int x, int y)
    {
        return postVisitCellTypes.ContainsKey($"{x},{y}");
    }

    /// <summary>
    /// Retourne le type post-visite d'une case, ou CellType.Empty si non défini.
    /// </summary>
    public CellType GetPostVisitType(int x, int y)
    {
        return postVisitCellTypes.TryGetValue($"{x},{y}", out CellType type)
            ? type
            : CellType.Empty;
    }

    public void SetEventFlag(string key, bool value)
    {
        eventFlags[key] = value;
    }

    public bool GetEventFlag(string key)
    {
        return eventFlags.ContainsKey(key) && eventFlags[key];
    }

    /// <summary>
    /// Termine la run en cours (défaite ou abandon).
    /// Remet hasActiveRun à false — le MainMenu désactivera "Continuer" en conséquence.
    /// </summary>
    /// <summary>
    /// Seed l'équipement, le module et les consommables de départ du personnage.
    /// Appelé depuis StartNewRun() — les guards (IsSlotFree, HasModule, startingConsumablesSeeded)
    /// garantissent que CombatManager ne re-seedera rien lors du premier combat.
    /// </summary>
    private void SeedDonneesDepart(CharacterData character)
    {
        if (character == null) return;

        // Équipement de départ — un slot par slot, seulement si vide
        SeedSlotSiVide(EquipmentSlot.Head,  character.startingHead);
        SeedSlotSiVide(EquipmentSlot.Torso, character.startingTorso);
        SeedSlotSiVide(EquipmentSlot.Legs,  character.startingLegs);
        SeedSlotSiVide(EquipmentSlot.Arm1,  character.startingArm1);
        SeedSlotSiVide(EquipmentSlot.Arm2,  character.startingArm2);
        if (character.maxEquippedArms >= 3)
            SeedSlotSiVide(EquipmentSlot.Arm3, character.startingArm3);
        if (character.maxEquippedArms >= 4)
            SeedSlotSiVide(EquipmentSlot.Arm4, character.startingArm4);

        // Module de départ
        if (character.startingModule != null && !HasModule(character.startingModule))
            AddModule(character.startingModule);

        // Consommables de départ (une seule fois par run)
        if (character.startingConsumables != null && !startingConsumablesSeeded)
        {
            foreach (ConsumableData consumable in character.startingConsumables)
            {
                if (consumable != null)
                    AddConsumable(consumable);
            }
            startingConsumablesSeeded = true;
        }

        // Stats de départ — calculées après le seeding de l'équipement
        // pour inclure les bonus des pièces de départ dans le total.
        InitialiserStats(character);

        Debug.Log($"[RunManager] Données de départ seedées pour {character.characterName}.");
    }

    /// <summary>
    /// Calcule les stats de départ (HP, et à terme toutes les ressources)
    /// à partir du CharacterData et des bonus d'équipement déjà seedés.
    /// Appelé depuis SeedDonneesDepart(), après le seeding de l'équipement.
    /// </summary>
    private void InitialiserStats(CharacterData character)
    {
        // HP max = base + bonus de chaque slot équipé
        int hpMax = character.maxHP;
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            EquipmentData equip = GetEquipped(slot);
            if (equip != null)
                hpMax += equip.bonusHP;
        }

        maxHP     = Mathf.Max(1, hpMax);
        currentHP = maxHP;

        // Crédits de départ — définis par personnage dans CharacterData
        credits = Mathf.Max(0, character.startingCredits);

        Debug.Log($"[RunManager] Stats initialisées — HP : {currentHP}/{maxHP}");
    }

    private void SeedSlotSiVide(EquipmentSlot slot, EquipmentData equipment)
    {
        if (equipment == null || equippedItems.ContainsKey(slot)) return;

        // Cloner l'équipement de départ pour éviter de modifier l'asset ScriptableObject.
        // Crucial si plusieurs slots utilisent le même équipement de base.
        EquipmentData clone = CloneEquipmentForLoot(equipment);

        if (TryEquipEquipment(slot, clone))
            Debug.Log($"[RunManager] Équipé au démarrage : {clone.equipmentName}");
    }

    // -----------------------------------------------
    // MARCHANDS — GÉNÉRATION ET ACCÈS
    // -----------------------------------------------

    /// <summary>
    /// Retourne l'état du marchand pour la case donnée.
    /// S'il n'existe pas encore, génère l'inventaire à partir du ShopData fourni.
    ///
    /// Le ShopData est résolu en amont (priorité : cell.shopData → mapData.defaultShopData).
    /// Si shopData est null, le marchand sera vide et un warning est loggué.
    ///
    /// Anti-duplicata : on retire chaque article tiré de la liste des disponibles
    /// avant de tirer le suivant, garantissant l'unicité au sein de chaque catégorie.
    /// Modules : en plus, les modules déjà possédés par le joueur sont filtrés dès le départ.
    /// </summary>
    public ShopState GetOrCreateShopState(CellData cell, ShopData shopData)
    {
        string key = $"{cell.x},{cell.y}";
        if (shopStates.TryGetValue(key, out ShopState existing))
            return existing;

        ShopState state = new ShopState { genere = true };

        if (shopData == null)
        {
            Debug.LogWarning($"[RunManager] Shop ({cell.x},{cell.y}) — aucun ShopData fourni, marchand vide.");
            shopStates[key] = state;
            return state;
        }

        // ── Équipements ──────────────────────────────────────────────────────
        if (shopData.equipmentLootTable != null && shopData.equipmentCount > 0)
        {
            List<EquipmentData> pool = new List<EquipmentData>(shopData.equipmentLootTable.equipments);
            pool.RemoveAll(e => e == null);

            int nbATirer = Mathf.Min(shopData.equipmentCount, pool.Count);
            for (int i = 0; i < nbATirer; i++)
            {
                int idx = Random.Range(0, pool.Count);
                EquipmentData item = pool[idx];
                pool.RemoveAt(idx);

                state.equipements.Add(new ShopItemEquipment
                {
                    data = item,
                    prix = Random.Range(shopData.equipmentPriceRange.x,
                                        shopData.equipmentPriceRange.y + 1)
                });
            }
        }

        // ── Modules ──────────────────────────────────────────────────────────
        if (shopData.moduleLootTable != null && shopData.moduleCount > 0)
        {
            List<ModuleData> pool = new List<ModuleData>(shopData.moduleLootTable.modules);
            pool.RemoveAll(m => m == null || HasModule(m));

            int nbATirer = Mathf.Min(shopData.moduleCount, pool.Count);
            for (int i = 0; i < nbATirer; i++)
            {
                int idx = Random.Range(0, pool.Count);
                ModuleData item = pool[idx];
                pool.RemoveAt(idx);

                state.modules.Add(new ShopItemModule
                {
                    data = item,
                    prix = Random.Range(shopData.modulePriceRange.x,
                                        shopData.modulePriceRange.y + 1)
                });
            }
        }

        // ── Consommables ─────────────────────────────────────────────────────
        if (shopData.consumableLootTable != null && shopData.consumableCount > 0)
        {
            List<ConsumableData> pool = new List<ConsumableData>(shopData.consumableLootTable.consumables);
            pool.RemoveAll(c => c == null);

            int nbATirer = Mathf.Min(shopData.consumableCount, pool.Count);
            for (int i = 0; i < nbATirer; i++)
            {
                int idx = Random.Range(0, pool.Count);
                ConsumableData item = pool[idx];
                pool.RemoveAt(idx);

                state.consommables.Add(new ShopItemConsomable
                {
                    data = item,
                    prix = Random.Range(shopData.consumablePriceRange.x,
                                        shopData.consumablePriceRange.y + 1)
                });
            }
        }

        // ── Skills ───────────────────────────────────────────────────────────
        if (shopData.skillLootTable != null && shopData.skillCount > 0)
        {
            List<SkillData> pool = new List<SkillData>(shopData.skillLootTable.skills);
            pool.RemoveAll(s => s == null);

            int nbATirer = Mathf.Min(shopData.skillCount, pool.Count);
            for (int i = 0; i < nbATirer; i++)
            {
                int idx = Random.Range(0, pool.Count);
                SkillData item = pool[idx];
                pool.RemoveAt(idx);

                state.skills.Add(new ShopItemSkill
                {
                    data = item,
                    prix = Random.Range(shopData.skillPriceRange.x,
                                        shopData.skillPriceRange.y + 1)
                });
            }
        }

        shopStates[key] = state;
        Debug.Log($"[RunManager] Shop généré en ({cell.x},{cell.y}) avec '{shopData.name}' — " +
                  $"{state.equipements.Count} équipements, {state.modules.Count} modules, " +
                  $"{state.consommables.Count} consommables, {state.skills.Count} skills.");
        return state;
    }

    /// <summary>
    /// Retourne l'état du marchand pour la position (x, y), ou null s'il n'a pas encore été généré.
    /// À utiliser uniquement en lecture (ex : vérifier si le shop existe déjà).
    /// Pour obtenir ou créer, utiliser GetOrCreateShopState().
    /// </summary>
    public ShopState GetShopState(int x, int y)
    {
        shopStates.TryGetValue($"{x},{y}", out ShopState state);
        return state;
    }

    public void EndRun()
    {
        hasActiveRun = false;
        Debug.Log("[RunManager] Run terminée.");
    }

    public void AddDifficultyModifier(float delta)
    {
        difficultyModifier = Mathf.Clamp(difficultyModifier + delta, 0.5f, 3.0f);
    }
}

// -----------------------------------------------
// CLASSES HELPER
// -----------------------------------------------

/// <summary>
/// Stocke l'état de cooldown d'une compétence de navigation.
/// </summary>
public class NavSkillCooldownState
{
    // Type de condition qui déclenche le rechargement
    public NavCooldownType type;
    // Nombre d'occurrences restantes avant que le skill soit de nouveau disponible
    public int remaining;
    // Pour le type EnnemisAvecTag : le tag que doit porter l'ennemi tué
    public TagData tagRequis;
}
