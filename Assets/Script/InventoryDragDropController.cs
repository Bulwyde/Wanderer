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
    private EquipmentData _originEquipment;
    private int           _originSlotIndex = -1;

    private bool _isDragging = false;

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
    public void SetupSkill(SkillData skill, EquipmentData originEquipment = null, int slotIndex = -1)
    {
        _dragType          = DragItemType.Skill;
        _dragSkillData     = skill;
        _dragEquipmentData = null;
        _originEquipment   = originEquipment;
        _originSlotIndex   = slotIndex;

        if (dragIcon != null && skill != null)
            dragIcon.sprite = skill.icon;
    }

    /// <summary>
    /// Configure ce contrôleur pour un équipement (toujours depuis l'inventaire).
    /// </summary>
    public void SetupEquipment(EquipmentData equipment)
    {
        _dragType          = DragItemType.Equipment;
        _dragEquipmentData = equipment;
        _dragSkillData     = null;
        _originEquipment   = null;
        _originSlotIndex   = -1;

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
                // Déséquiper si le skill venait d'un slot → retour inventaire
                if (_dragType == DragItemType.Skill && _originEquipment != null && _originSlotIndex >= 0)
                    run.UnequipSkill(_originEquipment, _originSlotIndex);
                break;

            case InventoryDropZone.ZoneType.InventaireEquipements:
                // Les équipements de l'inventaire sont déjà en inventaire — pas d'action supplémentaire
                break;
        }

        if (InventoryUIManager.Instance != null)
            InventoryUIManager.Instance.RefreshUI();
    }

    private void TraiterDropSkillSlot(RunManager run, InventoryDropZone cible)
    {
        if (_dragType != DragItemType.Skill || _dragSkillData == null) return;
        if (cible.targetEquipment == null) return;

        int idx = cible.targetSlotIndex;
        List<SkillSlot> slots = cible.targetEquipment.skillSlots;
        if (idx < 0 || idx >= slots.Count) return;

        SkillSlot slot = slots[idx];

        if (slot.state == SkillSlot.SlotState.Available)
        {
            run.EquipSkill(cible.targetEquipment, idx, _dragSkillData);
        }
        else if (slot.state == SkillSlot.SlotState.Used)
        {
            // Drop sur le même slot → annuler
            if (_originEquipment == cible.targetEquipment && _originSlotIndex == idx) return;
            run.SwapSkill(cible.targetEquipment, idx, _dragSkillData);
        }
        else
        {
            Debug.LogWarning($"[InventoryDragDrop] Slot {idx} inaccessible (etat : {slot.state}).");
        }
    }

    private void TraiterDropEquipmentSlot(RunManager run, InventoryDropZone cible)
    {
        if (_dragType != DragItemType.Equipment || _dragEquipmentData == null) return;

        if (!run.TryEquipEquipment(cible.targetEquipmentSlot, _dragEquipmentData))
            Debug.LogWarning($"[InventoryDragDrop] Impossible d'equiper '{_dragEquipmentData.equipmentName}'" +
                             $" dans {cible.targetEquipmentSlot} (inventaire plein ?).");
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
            if (_originEquipment != null && _originSlotIndex >= 0)
                run.UnequipSkill(_originEquipment, _originSlotIndex);
            else
                run.RemoveSkillFromInventory(_dragSkillData);

            Debug.Log($"[InventoryDragDrop] Skill '{_dragSkillData.skillName}' supprime.");
        }
        else if (_dragType == DragItemType.Equipment && _dragEquipmentData != null)
        {
            run.RemoveEquipmentFromInventory(_dragEquipmentData);
            Debug.Log($"[InventoryDragDrop] Equipement '{_dragEquipmentData.equipmentName}' supprime.");
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

// -----------------------------------------------
// ZONE DE DÉPÔT
// -----------------------------------------------

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
