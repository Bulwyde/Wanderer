using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Composant attaché à chaque icône draggable dans l'inventaire (skill ou équipement).
/// Implémente le cycle complet de drag'n'drop : début, déplacement, dépôt.
///
/// Architecture :
///   - Un contrôleur par icône, configuré via SetupSkill() ou SetupEquipment().
///   - L'état global (activé, icône fantôme, canvas) est partagé via champs statiques.
///   - La zone de dépôt est détectée par raycast EventSystem → InventoryDropZone.
///   - La suppression passe par InventoryUIManager.DemanderConfirmationSuppression().
///
/// Activation : EnableDragDrop(false) au début d'un combat, (true) au loot.
/// </summary>
public class InventoryDragDropController : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // -----------------------------------------------
    // TYPES
    // -----------------------------------------------

    public enum DragItemType { Skill, Equipment }

    // -----------------------------------------------
    // ÉTAT STATIQUE — partagé entre toutes les instances
    // -----------------------------------------------

    private static bool   _dragEnabled  = true;
    private static Image  _floatingIcon;   // icône fantôme qui suit le curseur
    private static Canvas _rootCanvas;     // canvas racine pour la conversion de coordonnées

    // -----------------------------------------------
    // CHAMPS D'INSTANCE
    // -----------------------------------------------

    // Icône de l'item sur ce GO — devient semi-transparente pendant le drag.
    [SerializeField] private Image dragIcon;

    private DragItemType  _dragType;
    private SkillData     _dragSkillData;
    private EquipmentData _dragEquipmentData;

    // Origine : _originEquipment null = skill venant de l'inventaire libre.
    private EquipmentData  _originEquipment;
    private int            _originSlotIndex     = -1;
    private EquipmentSlot? _originEquipmentSlot = null;

    private bool _isDragging = false;

    // Origine : si vient d'un slot d'inventaire au lieu d'un slot équipé
    private int _originInventoryEquipmentSlotIndex = -1;
    private int _originInventorySkillSlotIndex     = -1;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Awake()
    {
        if (dragIcon == null)
            dragIcon = GetComponent<Image>();
    }

    // -----------------------------------------------
    // SETUP
    // -----------------------------------------------

    /// <summary>
    /// Configure ce contrôleur pour un skill.
    /// originEquipment null et slotIndex -1 = skill depuis l'inventaire libre.
    /// </summary>
    public void SetupSkill(SkillData skill, EquipmentData originEquipment = null, int slotIndex = -1, int originInventorySlotIndex = -1)
    {
        _dragType                      = DragItemType.Skill;
        _dragSkillData                 = skill;
        _dragEquipmentData             = null;
        _originEquipment               = originEquipment;
        _originSlotIndex               = slotIndex;
        _originInventorySkillSlotIndex = originInventorySlotIndex;

        if (dragIcon != null && skill != null)
            dragIcon.sprite = skill.icon;
    }

    /// <summary>
    /// Configure ce contrôleur pour un équipement.
    /// originSlot non-null = équipement venant d'un slot équipé (panneau gauche).
    /// originSlot null     = équipement venant de l'inventaire libre (panneau droit).
    /// </summary>
    public void SetupEquipment(EquipmentData equipment, EquipmentSlot? originSlot = null, int originInventorySlotIndex = -1)
    {
        _dragType                          = DragItemType.Equipment;
        _dragEquipmentData                 = equipment;
        _dragSkillData                     = null;
        _originEquipment                   = null;
        _originSlotIndex                   = -1;
        _originEquipmentSlot               = originSlot;
        _originInventoryEquipmentSlotIndex = originInventorySlotIndex;

        if (dragIcon != null && equipment != null)
            dragIcon.sprite = equipment.icon;
    }

    // -----------------------------------------------
    // DRAG HANDLERS
    // -----------------------------------------------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!_dragEnabled) return;

        // Un skill LockedInUse n'est pas draggable
        if (_dragType == DragItemType.Skill && _originEquipment != null && _originSlotIndex >= 0)
        {
            List<SkillSlot> slots = _originEquipment.skillSlots;
            if (_originSlotIndex < slots.Count &&
                slots[_originSlotIndex].state == SkillSlot.SlotState.LockedInUse)
            {
                Debug.LogWarning("[InventoryDragDrop] Skill verrouille — drag annule.");
                return;
            }
        }

        _isDragging = true;

        // Rendre l'icône d'origine semi-transparente pour signaler le drag
        if (dragIcon != null)
            dragIcon.color = new Color(1f, 1f, 1f, 0.4f);

        // Afficher et configurer l'icône fantôme
        if (_floatingIcon != null && dragIcon != null)
        {
            _floatingIcon.sprite = dragIcon.sprite;
            _floatingIcon.color  = Color.white;

            // Ajuster la taille de l'icône fantôme selon le type d'objet dragué
            RectTransform floatingRT = _floatingIcon.GetComponent<RectTransform>();
            if (floatingRT != null)
            {
                if (_dragType == DragItemType.Skill)
                    floatingRT.sizeDelta = new Vector2(32f, 32f);
                else if (_dragType == DragItemType.Equipment)
                    floatingRT.sizeDelta = new Vector2(64f, 64f);
            }

            _floatingIcon.gameObject.SetActive(true);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _floatingIcon == null || _rootCanvas == null) return;

        // Convertir la position écran en position locale dans le canvas racine
        RectTransform canvasRT = _rootCanvas.GetComponent<RectTransform>();
        Camera cam = (_rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, eventData.position, cam, out Vector2 localPos))
            _floatingIcon.rectTransform.localPosition = localPos;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;

        // Restaurer l'alpha de l'icône d'origine
        if (dragIcon != null)
            dragIcon.color = Color.white;

        // Cacher l'icône fantôme
        if (_floatingIcon != null)
            _floatingIcon.gameObject.SetActive(false);

        // Détecter la zone de dépôt sous le curseur
        List<RaycastResult> résultats = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, résultats);

        InventoryDropZone cible = null;
        foreach (RaycastResult r in résultats)
        {
            cible = r.gameObject.GetComponent<InventoryDropZone>();
            if (cible != null) break;
        }

        if (cible == null) return;  // drop dans le vide → annuler

        TraiterDrop(cible);
    }

    // -----------------------------------------------
    // TRAITEMENT DU DROP
    // -----------------------------------------------

    private void TraiterDrop(InventoryDropZone cible)
    {
        RunManager run = RunManager.Instance;
        if (run == null) return;

        switch (cible.zoneType)
        {
            case InventoryDropZone.ZoneType.Poubelle:
                if (InventoryUIManager.Instance != null)
                    InventoryUIManager.Instance.DemanderConfirmationSuppression(this);
                return;  // RefreshUI sera appelé après confirmation, pas maintenant

            case InventoryDropZone.ZoneType.SkillSlot:
                TraiterDropSkillSlot(run, cible);
                break;

            case InventoryDropZone.ZoneType.EquipmentSlot:
                TraiterDropEquipmentSlot(run, cible);
                break;

            case InventoryDropZone.ZoneType.InventaireSkills:
                if (_dragType == DragItemType.Skill && _dragSkillData != null)
                {
                    // Essayer de placer AVANT toute modification (sans appeler UnequipSkill qui ajoute à l'inventaire)
                    if (run.SetSkillToInventorySlot(cible.targetSlotIndex, _dragSkillData))
                    {
                        // Placement réussi → maintenant on peut modifier l'état

                        // Si vient d'un slot équipé, nettoyer manuellement.
                        // Note : on ne peut pas appeler UnequipSkill ici car il remettrait le skill
                        // en inventaire, alors qu'il vient d'être placé via SetSkillToInventorySlot.
                        // On utilise GetEquipmentTagsNotInSkill pour la suppression sélective des
                        // inheritedTags — même logique que UnequipSkill, sans la gestion d'inventaire.
                        if (_originEquipment != null && _originSlotIndex >= 0)
                        {
                            SkillSlot originSlot = _originEquipment.skillSlots[_originSlotIndex];

                            // Retirer sélectivement les tags hérités (cohérent avec UnequipSkill)
                            RunManager run2 = RunManager.Instance;
                            if (run2 != null)
                            {
                                foreach (TagData tag in run2.GetEquipmentTagsNotInSkill(_originEquipment, _dragSkillData))
                                    _dragSkillData.inheritedTags.Remove(tag);
                            }
                            else if (_dragSkillData.inheritedTags != null)
                            {
                                _dragSkillData.inheritedTags.Clear();  // fallback test isolé
                            }

                            originSlot.equippedSkill = null;
                            originSlot.state         = SkillSlot.SlotState.Available;
                        }

                        // Si vient d'un slot d'inventaire, vider l'origine
                        if (_originInventorySkillSlotIndex >= 0)
                            run.SetSkillToInventorySlot(_originInventorySkillSlotIndex, null);
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryDragDrop] Impossible de placer '{_dragSkillData.skillName}' à l'index {cible.targetSlotIndex} (slot occupé ?).");
                    }
                }
                break;

            case InventoryDropZone.ZoneType.InventaireEquipements:
                if (_dragType == DragItemType.Equipment && _dragEquipmentData != null)
                {
                    // Essayer de placer AVANT toute modification
                    if (run.SetEquipmentToInventorySlot(cible.targetSlotIndex, _dragEquipmentData))
                    {
                        // Placement réussi → maintenant on peut modifier l'état

                        // Si vient d'un slot équipé, déplacer les skills à l'inventaire puis vider le slot
                        if (_originEquipmentSlot.HasValue)
                        {
                            EquipmentData equippedData = run.GetEquipped(_originEquipmentSlot.Value);
                            if (equippedData != null && equippedData.skillSlots != null)
                            {
                                foreach (SkillSlot skillSlot in equippedData.skillSlots)
                                {
                                    if ((skillSlot.state == SkillSlot.SlotState.Used ||
                                         skillSlot.state == SkillSlot.SlotState.LockedInUse) &&
                                        skillSlot.equippedSkill != null)
                                    {
                                        if (run.AddSkillToInventory(skillSlot.equippedSkill))
                                        {
                                            skillSlot.equippedSkill = null;
                                            skillSlot.state         = SkillSlot.SlotState.Available;
                                        }
                                        else
                                            Debug.LogWarning($"[InventoryDragDrop] Inventaire skills plein — '{skillSlot.equippedSkill.skillName}' conservé sur son slot (non perdu).");
                                    }
                                }
                            }

                            run.ClearEquipmentSlot(_originEquipmentSlot.Value);
                        }

                        // Si vient d'un slot d'inventaire, vider l'origine
                        if (_originInventoryEquipmentSlotIndex >= 0)
                            run.SetEquipmentToInventorySlot(_originInventoryEquipmentSlotIndex, null);
                    }
                    else
                    {
                        Debug.LogWarning($"[InventoryDragDrop] Impossible de placer '{_dragEquipmentData.equipmentName}' à l'index {cible.targetSlotIndex} (slot occupé ?).");
                    }
                }
                break;
        }

        if (InventoryUIManager.Instance != null)
            InventoryUIManager.Instance.RefreshUI();
    }

    private void TraiterDropSkillSlot(RunManager run, InventoryDropZone cible)
    {
        if (_dragType != DragItemType.Skill || _dragSkillData == null) return;
        if (cible.targetEquipment == null) return;

        // Validation : Navigation skills vont sur Legs, autres sur Arms
        if (_dragSkillData.isNavigationSkill)
        {
            if (cible.targetEquipment.equipmentType != EquipmentType.Legs)
            {
                Debug.LogWarning($"[InventoryDragDrop] '{_dragSkillData.skillName}' est un skill de navigation — il ne peut aller que sur des Legs.");
                return;
            }
        }
        else
        {
            if (cible.targetEquipment.equipmentType != EquipmentType.Arm)
            {
                Debug.LogWarning($"[InventoryDragDrop] '{_dragSkillData.skillName}' ne peut aller que sur des Arms.");
                return;
            }
        }

        int idx = cible.targetSlotIndex;
        List<SkillSlot> slots = cible.targetEquipment.skillSlots;
        if (idx < 0 || idx >= slots.Count) return;

        SkillSlot slot = slots[idx];

        if (slot.state == SkillSlot.SlotState.Available)
        {
            // Si le skill vient d'un slot équipé, le déséquiper d'abord
            // (même si c'est sur le même équipement, on passe par l'inventaire)
            if (_originEquipment != null && _originSlotIndex >= 0)
                run.UnequipSkill(_originEquipment, _originSlotIndex);

            // Puis l'équiper dans le nouveau slot
            run.EquipSkill(cible.targetEquipment, idx, _dragSkillData);
        }
        else if (slot.state == SkillSlot.SlotState.Used)
        {
            // Drop sur le même slot → annuler
            if (_originEquipment == cible.targetEquipment && _originSlotIndex == idx) return;
            // Liberer le slot d'origine si le skill vient d'un slot equipe
            if (_originEquipment != null && _originSlotIndex >= 0)
                run.UnequipSkill(_originEquipment, _originSlotIndex);
            run.SwapSkill(cible.targetEquipment, idx, _dragSkillData);
        }
        else
        {
            Debug.LogWarning($"[InventoryDragDrop] Slot {idx} inaccessible (état : {slot.state}).");
        }
    }

    private void TraiterDropEquipmentSlot(RunManager run, InventoryDropZone cible)
    {
        if (_dragType != DragItemType.Equipment || _dragEquipmentData == null) return;

        // Vérifier la compatibilité type/slot AVANT de modifier quoi que ce soit
        if (!RunManager.IsSlotCompatible(cible.targetEquipmentSlot, _dragEquipmentData.equipmentType))
        {
            Debug.LogWarning($"[InventoryDragDrop] Type incompatible : {_dragEquipmentData.equipmentType}" +
                             $" ne peut pas aller dans {cible.targetEquipmentSlot}.");
            return;  // Annuler complètement — le slot d'origine n'est pas touché
        }

        // Maintenant qu'on sait que c'est compatible, on peut toucher à l'état
        if (_originEquipmentSlot.HasValue)
        {
            // Drop sur le même slot → rien à faire
            if (_originEquipmentSlot.Value == cible.targetEquipmentSlot) return;

            // Libérer le slot d'origine sans envoyer en inventaire
            // (TryEquipEquipment va le placer dans le slot cible)
            run.ClearEquipmentSlot(_originEquipmentSlot.Value);
        }

        if (!run.TryEquipEquipment(cible.targetEquipmentSlot, _dragEquipmentData))
        {
            // TryEquipEquipment a échoué (cible occupée + inventaire plein ?) :
            // restaurer le slot d'origine si on l'avait vidé pour éviter toute perte.
            if (_originEquipmentSlot.HasValue)
                run.EquipItem(_originEquipmentSlot.Value, _dragEquipmentData);
            Debug.LogWarning($"[InventoryDragDrop] Impossible d'équiper '{_dragEquipmentData.equipmentName}'" +
                             $" dans {cible.targetEquipmentSlot} — slot d'origine restauré.");
        }
    }

    // -----------------------------------------------
    // SUPPRESSION (après confirmation)
    // -----------------------------------------------

    /// <summary>
    /// Supprime définitivement l'item représenté par ce contrôleur.
    /// Appelé par InventoryUIManager.OnConfirmerSuppression() après validation joueur.
    /// </summary>
    public void ExecuterSuppression()
    {
        RunManager run = RunManager.Instance;
        if (run == null) return;

        if (_dragType == DragItemType.Skill && _dragSkillData != null)
        {
            // Skill : si équipé, le déséquiper d'abord (retire les tags hérités, etc.)
            if (_originEquipment != null && _originSlotIndex >= 0)
                run.UnequipSkill(_originEquipment, _originSlotIndex);

            // Puis le supprimer complètement de l'inventaire
            run.RemoveSkillFromInventory(_dragSkillData);
            Debug.Log($"[InventoryDragDrop] Skill '{_dragSkillData.skillName}' supprimé.");
        }
        else if (_dragType == DragItemType.Equipment && _dragEquipmentData != null)
        {
            // Équipement : si équipé, libérer le slot et rapatrier les skills en inventaire
            if (_originEquipmentSlot.HasValue)
                run.UnequipEquipmentAndMoveSkillsToInventory(_originEquipmentSlot.Value);

            // Puis le supprimer de l'inventaire
            if (!run.RemoveEquipmentFromInventory(_dragEquipmentData))
                Debug.LogWarning($"[InventoryDragDrop] '{_dragEquipmentData.equipmentName}' pas en inventaire (peut-être équipé ?).");

            Debug.Log($"[InventoryDragDrop] Équipement '{_dragEquipmentData.equipmentName}' supprimé.");
        }

        if (InventoryUIManager.Instance != null)
            InventoryUIManager.Instance.RefreshUI();
    }

    // -----------------------------------------------
    // ACTIVATION GLOBALE
    // -----------------------------------------------

    /// <summary>
    /// Active ou désactive le drag'n'drop pour toutes les icônes.
    /// Désactivé au début du combat, réactivé à l'apparition du loot.
    /// </summary>
    public static void EnableDragDrop(bool enabled)
    {
        _dragEnabled = enabled;
        Debug.Log($"[InventoryDragDrop] Drag'n'drop {(enabled ? "active" : "desactive")}.");
    }

    // -----------------------------------------------
    // INITIALISATION DES RESSOURCES PARTAGÉES
    // -----------------------------------------------

    /// <summary>
    /// Enregistre l'icône fantôme et le canvas racine utilisés par toutes les instances.
    /// Appelé une seule fois par InventoryUIManager au démarrage (ou après création du canvas).
    /// </summary>
    public static void InitialiserPartages(Image floatingIcon, Canvas rootCanvas)
    {
        _floatingIcon = floatingIcon;
        _rootCanvas   = rootCanvas;

        if (_floatingIcon != null)
            _floatingIcon.gameObject.SetActive(false);
    }
}
