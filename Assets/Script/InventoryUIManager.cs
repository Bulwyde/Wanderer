using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Singleton DDOL qui gère le Canvas d'inventaire overlay.
/// Le Canvas persiste entre les scènes et s'active/désactive selon l'état de l'inventaire.
/// Si aucun Canvas n'est assigné via l'Inspector, CreateCanvasIfNeeded() en crée un basique
/// au démarrage — utile pour tester sans setup Unity préalable.
///
/// Raccourcis clavier (test) : I = toggle, Escape = fermer.
/// API : Open() / Close() / Toggle() / IsOpen.
/// </summary>
public class InventoryUIManager : MonoBehaviour
{
    // -----------------------------------------------
    // SINGLETON
    // -----------------------------------------------

    public static InventoryUIManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CreateCanvasIfNeeded();
    }

    void Start()
    {
        // Si canvas assigné via Inspector (pas créé dynamiquement),
        // initialiser les partagés du drag'n'drop maintenant.
        if (dragFloatingIcon != null && inventoryCanvas != null)
            InventoryDragDropController.InitialiserPartages(dragFloatingIcon, inventoryCanvas);
    }

    // -----------------------------------------------
    // RÉFÉRENCES UI
    // -----------------------------------------------

    [SerializeField] private Canvas inventoryCanvas;

    // Panneau gauche — équipements portés
    [SerializeField] private RectTransform panelEquipement;
    [SerializeField] private Sprite defaultEquipmentSlotIcon;
    [SerializeField] private Sprite defaultSkillSlotIcon;

    [Header("Slots équipés — containers assignés depuis l'Inspector")]
    [SerializeField] private RectTransform legsContainer;
    [SerializeField] private RectTransform arm1Container;
    [SerializeField] private RectTransform arm2Container;
    [SerializeField] private RectTransform arm3Container;
    [SerializeField] private RectTransform arm4Container;

    [Header("Grilles de skills — pré-créées dans l'Editor, une par slot")]
    [SerializeField] private RectTransform legsSkillGrid;
    [SerializeField] private RectTransform arm1SkillGrid;
    [SerializeField] private RectTransform arm2SkillGrid;
    [SerializeField] private RectTransform arm3SkillGrid;
    [SerializeField] private RectTransform arm4SkillGrid;

    // Panneau droit — inventaires
    [SerializeField] private RectTransform panelInventaire;
    [SerializeField] private RectTransform panelEquipmentInventory;
    [SerializeField] private RectTransform panelSkillInventory;

    // Poubelle
    [SerializeField] private RectTransform panelPoubelle;

    // Modal blocker
    [SerializeField] private Image         modalBlocker;     // Panneau semi-transparent qui bloque les raycasts

    // Drag'n'drop
    [SerializeField] private Image         dragFloatingIcon;
    [SerializeField] private GameObject    panelConfirmation;

    // Contrôleur en attente de confirmation de suppression (null si aucun)
    private InventoryDragDropController _confirmationPending;

    // -----------------------------------------------
    // ÉTAT
    // -----------------------------------------------

    public bool IsOpen => inventoryCanvas != null && inventoryCanvas.gameObject.activeSelf;

    // -----------------------------------------------
    // CONTRÔLE
    // -----------------------------------------------

    /// <summary>
    /// Ouvre le panneau d'inventaire et rafraîchit l'affichage.
    /// </summary>
    public void Open()
    {
        if (inventoryCanvas == null) return;
        inventoryCanvas.gameObject.SetActive(true);

        // Montrer le blocker pour empêcher les interactions en arrière-plan
        if (modalBlocker != null)
            modalBlocker.gameObject.SetActive(true);

        RefreshUI();
        Debug.Log("[InventoryUIManager] Ouvert.");
    }

    /// <summary>
    /// Ferme le panneau d'inventaire.
    /// </summary>
    public void Close()
    {
        if (inventoryCanvas == null) return;

        // Annuler toute confirmation de suppression en attente avant de fermer
        // (évite une référence pendante vers un contrôleur détruit au prochain RefreshUI)
        if (_confirmationPending != null)
            OnAnnulerSuppression();

        inventoryCanvas.gameObject.SetActive(false);

        // Cacher le blocker
        if (modalBlocker != null)
            modalBlocker.gameObject.SetActive(false);

        Debug.Log("[InventoryUIManager] Fermé.");
    }

    /// <summary>
    /// Bascule entre ouvert et fermé.
    /// </summary>
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    // -----------------------------------------------
    // ENTRÉES (test)
    // -----------------------------------------------

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.I))
            Toggle();
        else if (Input.GetKeyDown(KeyCode.Escape) && IsOpen)
            Close();
    }

    // -----------------------------------------------
    // REFRESH
    // -----------------------------------------------

    public void RefreshUI()
    {
        if (RunManager.Instance == null) return;
        RafraichirEquipementsPortes();
        RafraichirInventaireEquipements();
        RafraichirInventaireSkills();
    }

    private void RafraichirEquipementsPortes()
    {
        if (RunManager.Instance == null) return;

        CharacterData chara = RunManager.Instance.selectedCharacter;
        int maxArms = (chara != null) ? chara.maxEquippedArms : 2;

        RafraichirContenuSlot(legsContainer, legsSkillGrid, EquipmentSlot.Legs);
        RafraichirContenuSlot(arm1Container, arm1SkillGrid, EquipmentSlot.Arm1);
        RafraichirContenuSlot(arm2Container, arm2SkillGrid, EquipmentSlot.Arm2);

        // Arm3 et Arm4 affichés seulement si maxEquippedArms >= 3 et >= 4
        if (maxArms >= 3)
            RafraichirContenuSlot(arm3Container, arm3SkillGrid, EquipmentSlot.Arm3);
        else if (arm3Container != null)
            arm3Container.gameObject.SetActive(false);

        if (maxArms >= 4)
            RafraichirContenuSlot(arm4Container, arm4SkillGrid, EquipmentSlot.Arm4);
        else if (arm4Container != null)
            arm4Container.gameObject.SetActive(false);
    }

    private void RafraichirContenuSlot(RectTransform container, RectTransform skillGridContainer, EquipmentSlot slot)
    {
        if (container == null) return;
        // Réactiver le container au cas où il avait été masqué lors d'un refresh précédent
        // (ex : maxEquippedArms était < 3/4 et a augmenté entre deux ouvertures)
        container.gameObject.SetActive(true);
        ViderPanel(container);

        // S'assurer que la DropZone est présente sur le container (une seule fois)
        InventoryDropZone dropEquip = container.GetComponent<InventoryDropZone>();
        if (dropEquip == null)
        {
            dropEquip                     = container.gameObject.AddComponent<InventoryDropZone>();
            dropEquip.zoneType            = InventoryDropZone.ZoneType.EquipmentSlot;
            dropEquip.targetEquipmentSlot = slot;
        }

        // Un container sans Image est invisible aux raycasts UI → la DropZone ne serait jamais détectée
        if (container.GetComponent<Image>() == null)
        {
            Image bg         = container.gameObject.AddComponent<Image>();
            bg.color         = new Color(0f, 0f, 0f, 0f);  // transparent
            bg.raycastTarget = true;
        }

        EquipmentData equip = RunManager.Instance.GetEquipped(slot);

        // ── Icône équipement (64×64, taille fixe, pas d'étirement) ──
        GameObject goIcon = new GameObject("EquipIcon", typeof(RectTransform));
        goIcon.transform.SetParent(container, false);
        Image imgEquip             = goIcon.AddComponent<Image>();
        RectTransform iconRT       = goIcon.GetComponent<RectTransform>();
        iconRT.sizeDelta           = new Vector2(64f, 64f);
        LayoutElement leIcon       = goIcon.AddComponent<LayoutElement>();
        leIcon.minWidth            = 64f;
        leIcon.preferredWidth      = 64f;
        leIcon.minHeight           = 64f;
        leIcon.preferredHeight     = 64f;
        leIcon.flexibleWidth       = 0f;
        leIcon.flexibleHeight      = 0f;

        if (equip != null)
        {
            imgEquip.sprite = equip.icon != null ? equip.icon : defaultEquipmentSlotIcon;
            imgEquip.color  = Color.white;
            InventoryDragDropController ctrl = goIcon.AddComponent<InventoryDragDropController>();
            ctrl.SetupEquipment(equip, slot);
        }
        else
        {
            imgEquip.sprite = defaultEquipmentSlotIcon;
            imgEquip.color  = new Color(1f, 1f, 1f, 0.35f);
        }

        // ── Grille de skill slots ──
        if (equip == null) return;

        bool aDesSlots = false;
        foreach (SkillSlot ss in equip.skillSlots)
            if (ss != null && ss.state != SkillSlot.SlotState.Unavailable) { aDesSlots = true; break; }
        if (!aDesSlots) return;

        // Utiliser le container externe assigné dans l'Inspector si disponible,
        // sinon créer une grille dynamique comme enfant du container d'équipement.
        RectTransform gridParent;
        if (skillGridContainer != null)
        {
            ViderPanel(skillGridContainer);
            gridParent = skillGridContainer;
        }
        else
        {
            GameObject goGrid = new GameObject("SkillGrid");
            goGrid.transform.SetParent(container, false);

            GridLayoutGroup glg = goGrid.AddComponent<GridLayoutGroup>();
            glg.cellSize        = new Vector2(32f, 32f);
            glg.spacing         = new Vector2(2f, 2f);
            glg.startCorner     = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis       = GridLayoutGroup.Axis.Horizontal;
            glg.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            ContentSizeFitter csfGrid = goGrid.AddComponent<ContentSizeFitter>();
            csfGrid.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csfGrid.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            gridParent = goGrid.GetComponent<RectTransform>();
        }

        for (int idx = 0; idx < equip.skillSlots.Count; idx++)
        {
            SkillSlot ss = equip.skillSlots[idx];
            if (ss == null || ss.state == SkillSlot.SlotState.Unavailable) continue;

            int           capturedIdx   = idx;
            EquipmentData capturedEquip = equip;

            GameObject goSlot = new GameObject($"SkillSlot_{idx}");
            goSlot.transform.SetParent(gridParent, false);
            Image imgSlot = goSlot.AddComponent<Image>();

            InventoryDropZone dropSkill = goSlot.AddComponent<InventoryDropZone>();
            dropSkill.zoneType        = InventoryDropZone.ZoneType.SkillSlot;
            dropSkill.targetEquipment = capturedEquip;
            dropSkill.targetSlotIndex = capturedIdx;

            if ((ss.state == SkillSlot.SlotState.Used ||
                 ss.state == SkillSlot.SlotState.LockedInUse)
                && ss.equippedSkill != null)
            {
                imgSlot.sprite = ss.equippedSkill.icon != null ? ss.equippedSkill.icon : defaultSkillSlotIcon;
                imgSlot.color  = Color.white;
                InventoryDragDropController ctrlSkill = goSlot.AddComponent<InventoryDragDropController>();
                ctrlSkill.SetupSkill(ss.equippedSkill, capturedEquip, capturedIdx);
            }
            else
            {
                imgSlot.sprite = defaultSkillSlotIcon;
                imgSlot.color  = new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    private void RafraichirInventaireEquipements()
    {
        if (panelEquipmentInventory == null || RunManager.Instance == null) return;
        ViderPanel(panelEquipmentInventory);

        List<EquipmentData> items = RunManager.Instance.GetInventoryEquipments();
        int total = RunManager.Instance.maxInventoryEquipments;

        for (int i = 0; i < total; i++)
        {
            GameObject go = new GameObject($"SlotEquip_{i}");
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelEquipmentInventory, false);

            Image img = go.AddComponent<Image>();

            // Ajouter InventoryDropZone avec l'index du slot
            InventoryDropZone dropZone = go.AddComponent<InventoryDropZone>();
            dropZone.zoneType        = InventoryDropZone.ZoneType.InventaireEquipements;
            dropZone.targetSlotIndex = i;

            if (i < items.Count && items[i] != null)
            {
                img.sprite = items[i].icon != null ? items[i].icon : defaultEquipmentSlotIcon;
                img.color  = Color.white;
                InventoryDragDropController ctrl = go.AddComponent<InventoryDragDropController>();
                ctrl.SetupEquipment(items[i], null, i);
            }
            else
            {
                img.sprite = defaultEquipmentSlotIcon;
                img.color  = new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    private void RafraichirInventaireSkills()
    {
        if (panelSkillInventory == null || RunManager.Instance == null) return;
        ViderPanel(panelSkillInventory);

        List<SkillData> items = RunManager.Instance.GetInventorySkills();
        int total = RunManager.Instance.maxInventorySkills;

        for (int i = 0; i < total; i++)
        {
            GameObject go = new GameObject($"SlotSkill_{i}");
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.SetParent(panelSkillInventory, false);

            Image img = go.AddComponent<Image>();

            // Ajouter InventoryDropZone avec l'index du slot
            InventoryDropZone dropZone = go.AddComponent<InventoryDropZone>();
            dropZone.zoneType        = InventoryDropZone.ZoneType.InventaireSkills;
            dropZone.targetSlotIndex = i;

            if (i < items.Count && items[i] != null)
            {
                img.sprite = items[i].icon != null ? items[i].icon : defaultSkillSlotIcon;
                img.color  = Color.white;
                InventoryDragDropController ctrl = go.AddComponent<InventoryDragDropController>();
                ctrl.SetupSkill(items[i], null, -1, i);
            }
            else
            {
                img.sprite = defaultSkillSlotIcon;
                img.color  = new Color(1f, 1f, 1f, 0.35f);
            }
        }
    }

    private GameObject CreerIcone(Sprite sprite, RectTransform parent)
    {
        GameObject go = new GameObject("Icone");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        if (sprite != null)
            img.sprite = sprite;
        return go;
    }

    private void ViderPanel(RectTransform panel)
    {
        for (int i = panel.childCount - 1; i >= 0; i--)
            DestroyImmediate(panel.GetChild(i).gameObject);
    }

    // -----------------------------------------------
    // DRAG'N'DROP
    // -----------------------------------------------

    /// <summary>
    /// Active ou désactive le drag'n'drop de l'inventaire.
    /// Appeler avec false au début d'un combat, true à l'apparition du loot.
    /// </summary>
    public void SetDragDropEnabled(bool enabled)
    {
        InventoryDragDropController.EnableDragDrop(enabled);
    }

    /// <summary>
    /// Affiche le panel de confirmation de suppression pour l'item dragué vers la poubelle.
    /// Appelé par InventoryDragDropController quand l'item est déposé sur la PanelPoubelle.
    /// </summary>
    public void DemanderConfirmationSuppression(InventoryDragDropController contrôleur)
    {
        _confirmationPending = contrôleur;
        if (panelConfirmation != null)
            panelConfirmation.SetActive(true);
    }

    public void OnConfirmerSuppression()
    {
        if (_confirmationPending != null)
            _confirmationPending.ExecuterSuppression();
        _confirmationPending = null;
        if (panelConfirmation != null)
            panelConfirmation.SetActive(false);
    }

    public void OnAnnulerSuppression()
    {
        _confirmationPending = null;
        if (panelConfirmation != null)
            panelConfirmation.SetActive(false);
    }

    // -----------------------------------------------
    // CANVAS
    // -----------------------------------------------

    private void CreateCanvasIfNeeded()
    {
        if (inventoryCanvas == null)
        {
            Debug.LogWarning("[InventoryUIManager] Aucun Canvas assigné dans l'Inspector. " +
                             "Créer le Canvas manuellement comme enfant de ce GameObject " +
                             "et assigner toutes les références dans l'Inspector.");
            return;
        }

        // Canvas présent — s'assurer qu'il démarre fermé
        inventoryCanvas.gameObject.SetActive(false);

        // Initialiser le drag'n'drop si les ressources sont déjà assignées
        if (dragFloatingIcon != null)
            InventoryDragDropController.InitialiserPartages(dragFloatingIcon, inventoryCanvas);
    }

    // -----------------------------------------------
    // HELPERS DE CRÉATION UI
    // -----------------------------------------------

}
