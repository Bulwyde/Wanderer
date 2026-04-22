using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    [SerializeField] private RectTransform legsSlot;
    [SerializeField] private RectTransform armsContainer;

    // Panneau droit — inventaires
    [SerializeField] private RectTransform panelInventaire;
    [SerializeField] private RectTransform panelEquipmentInventory;
    [SerializeField] private RectTransform panelSkillInventory;

    // Poubelle
    [SerializeField] private RectTransform panelPoubelle;

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
        RefreshUI();
        Debug.Log("[InventoryUIManager] Ouvert.");
    }

    /// <summary>
    /// Ferme le panneau d'inventaire.
    /// </summary>
    public void Close()
    {
        if (inventoryCanvas == null) return;
        inventoryCanvas.gameObject.SetActive(false);
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
    // REFRESH (stub — Phase 5)
    // -----------------------------------------------

    /// <summary>
    /// Rafraîchit l'ensemble de l'UI d'inventaire à partir de l'état courant du RunManager.
    /// Stub en Phase 4 — sera implémenté en Phase 5 pour générer les icônes.
    /// </summary>
    public void RefreshUI()
    {
        // Phase 5 : générer icônes équipements portés, inventaire équipements, inventaire skills
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

    private void OnConfirmerSuppression()
    {
        if (_confirmationPending != null)
            _confirmationPending.ExecuterSuppression();
        _confirmationPending = null;
        if (panelConfirmation != null)
            panelConfirmation.SetActive(false);
    }

    private void OnAnnulerSuppression()
    {
        _confirmationPending = null;
        if (panelConfirmation != null)
            panelConfirmation.SetActive(false);
    }

    // -----------------------------------------------
    // CRÉATION DYNAMIQUE DU CANVAS
    // -----------------------------------------------

    /// <summary>
    /// Si aucun Canvas n'est assigné dans l'Inspector, en crée un complet avec la hiérarchie UI de base.
    /// Appelé dans Awake() — le Canvas créé est DDOL via son parent (ce GO).
    /// </summary>
    private void CreateCanvasIfNeeded()
    {
        if (inventoryCanvas != null) return;

        // Canvas racine — enfant de ce GO pour hériter du DDOL
        GameObject canvasGO = new GameObject("InventoryCanvas");
        canvasGO.transform.SetParent(this.transform);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(640f, 360f);
        scaler.matchWidthOrHeight  = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();
        inventoryCanvas = canvas;

        // Désactivé par défaut — Open() l'activera
        canvasGO.SetActive(false);

        ConstruireHierarchie(canvasGO.transform);
        ConstruireIconeFlottante(canvasGO.transform);
        ConstruirePanelConfirmation(canvasGO.transform);

        Debug.Log("[InventoryUIManager] Canvas créé dynamiquement.");
    }

    // -----------------------------------------------
    // CONSTRUCTION DE LA HIÉRARCHIE
    // -----------------------------------------------

    private void ConstruireHierarchie(Transform canvasTransform)
    {
        // ── PanelEquipement (gauche) ──────────────────────────────────

        GameObject goEquip = CreerPanel("PanelEquipement", canvasTransform,
                                        new Color(0.08f, 0.08f, 0.12f, 0.92f));
        panelEquipement = goEquip.GetComponent<RectTransform>();
        ConfigurerAncrePleinHauteur(panelEquipement, anchorMinX: 0f, anchorMaxX: 0.45f,
                                    offsetGauche: 8f, offsetDroite: -4f);

        VerticalLayoutGroup vlgEquip = goEquip.AddComponent<VerticalLayoutGroup>();
        vlgEquip.spacing            = 8f;
        vlgEquip.padding            = new RectOffset(8, 8, 8, 8);
        vlgEquip.childForceExpandWidth  = true;
        vlgEquip.childForceExpandHeight = false;

        CreerTexte("TextHeader", goEquip.transform, "Equipement equipe");

        // Legs slot — un seul emplacement (jambes)
        GameObject goLegs = CreerPanel("LegsSlot", goEquip.transform,
                                       new Color(0.12f, 0.12f, 0.18f, 1f));
        legsSlot = goLegs.GetComponent<RectTransform>();
        LayoutElement leLegs = goLegs.AddComponent<LayoutElement>();
        leLegs.preferredHeight = 80f;
        InventoryDropZone legsZone = goLegs.AddComponent<InventoryDropZone>();
        legsZone.zoneType             = InventoryDropZone.ZoneType.EquipmentSlot;
        legsZone.targetEquipmentSlot  = EquipmentSlot.Legs;

        // Arms container — 2-4 slots bras selon CharacterData.maxEquippedArms
        GameObject goArms = CreerPanel("ArmsContainer", goEquip.transform,
                                       new Color(0f, 0f, 0f, 0f));
        armsContainer = goArms.GetComponent<RectTransform>();
        GridLayoutGroup glgArms = goArms.AddComponent<GridLayoutGroup>();
        glgArms.cellSize    = new Vector2(80f, 80f);
        glgArms.spacing     = new Vector2(4f, 4f);
        glgArms.startCorner = GridLayoutGroup.Corner.UpperLeft;
        LayoutElement leArms = goArms.AddComponent<LayoutElement>();
        leArms.preferredHeight = 172f;  // 2 lignes × 80 + spacing + padding

        // ── PanelInventaire (droite) ──────────────────────────────────

        GameObject goInv = CreerPanel("PanelInventaire", canvasTransform,
                                      new Color(0.08f, 0.08f, 0.12f, 0.92f));
        panelInventaire = goInv.GetComponent<RectTransform>();
        ConfigurerAncrePleinHauteur(panelInventaire, anchorMinX: 0.45f, anchorMaxX: 1f,
                                    offsetGauche: 4f, offsetDroite: -8f);

        VerticalLayoutGroup vlgInv = goInv.AddComponent<VerticalLayoutGroup>();
        vlgInv.spacing            = 8f;
        vlgInv.padding            = new RectOffset(8, 8, 8, 8);
        vlgInv.childForceExpandWidth  = true;
        vlgInv.childForceExpandHeight = false;

        // Sous-panel équipements en inventaire
        GameObject goEqInv = CreerPanel("PanelEquipmentInventory", goInv.transform,
                                        new Color(0.12f, 0.12f, 0.18f, 1f));
        panelEquipmentInventory = goEqInv.GetComponent<RectTransform>();
        GridLayoutGroup glgEqInv = goEqInv.AddComponent<GridLayoutGroup>();
        glgEqInv.cellSize    = new Vector2(64f, 64f);
        glgEqInv.spacing     = new Vector2(4f, 4f);
        glgEqInv.startCorner = GridLayoutGroup.Corner.UpperLeft;
        LayoutElement leEqInv = goEqInv.AddComponent<LayoutElement>();
        leEqInv.preferredHeight = 140f;
        InventoryDropZone eqInvZone = goEqInv.AddComponent<InventoryDropZone>();
        eqInvZone.zoneType = InventoryDropZone.ZoneType.InventaireEquipements;

        // Sous-panel skills en inventaire
        GameObject goSkInv = CreerPanel("PanelSkillInventory", goInv.transform,
                                        new Color(0.12f, 0.12f, 0.18f, 1f));
        panelSkillInventory = goSkInv.GetComponent<RectTransform>();
        GridLayoutGroup glgSkInv = goSkInv.AddComponent<GridLayoutGroup>();
        glgSkInv.cellSize    = new Vector2(48f, 48f);
        glgSkInv.spacing     = new Vector2(4f, 4f);
        glgSkInv.startCorner = GridLayoutGroup.Corner.UpperLeft;
        LayoutElement leSkInv = goSkInv.AddComponent<LayoutElement>();
        leSkInv.preferredHeight = 200f;
        InventoryDropZone skInvZone = goSkInv.AddComponent<InventoryDropZone>();
        skInvZone.zoneType = InventoryDropZone.ZoneType.InventaireSkills;

        // ── PanelPoubelle (coin bas-droit) ────────────────────────────

        GameObject goPoub = CreerPanel("PanelPoubelle", canvasTransform,
                                       new Color(0.6f, 0.1f, 0.1f, 0.85f));
        panelPoubelle = goPoub.GetComponent<RectTransform>();
        ConfigurerAncrePoint(panelPoubelle,
                             anchorX: 1f, anchorY: 0f,
                             pivotX: 1f, pivotY: 0f,
                             sizeDelta: new Vector2(60f, 60f),
                             position: new Vector2(-12f, 12f));
        CreerTexte("TextPoubelle", goPoub.transform, "X");
        InventoryDropZone poubelleZone = goPoub.AddComponent<InventoryDropZone>();
        poubelleZone.zoneType = InventoryDropZone.ZoneType.Poubelle;
    }

    // -----------------------------------------------
    // HELPERS DE CRÉATION UI
    // -----------------------------------------------

    private GameObject CreerPanel(string nom, Transform parent, Color couleur)
    {
        GameObject go = new GameObject(nom);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = couleur;
        return go;
    }

    private void CreerTexte(string nom, Transform parent, string contenu)
    {
        GameObject go = new GameObject(nom);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = contenu;
        tmp.fontSize  = 14f;
        tmp.alignment = TextAlignmentOptions.Center;
    }

    // Étire le panel sur toute la hauteur du canvas entre deux bornes X.
    private void ConfigurerAncrePleinHauteur(RectTransform rt,
                                             float anchorMinX, float anchorMaxX,
                                             float offsetGauche, float offsetDroite)
    {
        rt.anchorMin = new Vector2(anchorMinX, 0f);
        rt.anchorMax = new Vector2(anchorMaxX, 1f);
        rt.offsetMin = new Vector2(offsetGauche,  8f);
        rt.offsetMax = new Vector2(offsetDroite, -8f);
    }

    // Place un panneau de taille fixe ancré à un coin.
    private void ConfigurerAncrePoint(RectTransform rt,
                                      float anchorX, float anchorY,
                                      float pivotX,  float pivotY,
                                      Vector2 sizeDelta, Vector2 position)
    {
        rt.anchorMin        = new Vector2(anchorX, anchorY);
        rt.anchorMax        = new Vector2(anchorX, anchorY);
        rt.pivot            = new Vector2(pivotX, pivotY);
        rt.sizeDelta        = sizeDelta;
        rt.anchoredPosition = position;
    }

    // -----------------------------------------------
    // HELPERS DRAG'N'DROP
    // -----------------------------------------------

    private void ConstruireIconeFlottante(Transform canvasTransform)
    {
        GameObject go = new GameObject("DragFloatingIcon");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(canvasTransform, false);
        rt.sizeDelta = new Vector2(48f, 48f);

        dragFloatingIcon = go.AddComponent<Image>();
        dragFloatingIcon.raycastTarget = false;  // ne bloque pas les raycasts de dépôt
        go.SetActive(false);

        InventoryDragDropController.InitialiserPartages(dragFloatingIcon, inventoryCanvas);
    }

    private void ConstruirePanelConfirmation(Transform canvasTransform)
    {
        // Overlay plein écran semi-transparent — bloque les interactions pendant la confirmation
        GameObject goOverlay = new GameObject("PanelConfirmation");
        RectTransform rtOverlay = goOverlay.AddComponent<RectTransform>();
        rtOverlay.SetParent(canvasTransform, false);
        rtOverlay.anchorMin = Vector2.zero;
        rtOverlay.anchorMax = Vector2.one;
        rtOverlay.offsetMin = Vector2.zero;
        rtOverlay.offsetMax = Vector2.zero;
        Image imgOverlay = goOverlay.AddComponent<Image>();
        imgOverlay.color = new Color(0f, 0f, 0f, 0.6f);
        panelConfirmation = goOverlay;

        // Boîte de dialogue centrée
        GameObject goDialog = new GameObject("DialogBox");
        RectTransform rtDialog = goDialog.AddComponent<RectTransform>();
        rtDialog.SetParent(goOverlay.transform, false);
        rtDialog.sizeDelta        = new Vector2(200f, 100f);
        rtDialog.anchoredPosition = Vector2.zero;
        Image imgDialog = goDialog.AddComponent<Image>();
        imgDialog.color = new Color(0.15f, 0.15f, 0.2f, 1f);

        // Texte de la question (centré en haut de la dialog)
        GameObject goTxt = new GameObject("TextQuestion");
        RectTransform rtTxt = goTxt.AddComponent<RectTransform>();
        rtTxt.SetParent(goDialog.transform, false);
        rtTxt.anchorMin        = new Vector2(0f, 0.5f);
        rtTxt.anchorMax        = new Vector2(1f, 1f);
        rtTxt.offsetMin        = new Vector2(8f, 0f);
        rtTxt.offsetMax        = new Vector2(-8f, -4f);
        TextMeshProUGUI tmp = goTxt.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Supprimer cet objet ?";
        tmp.fontSize  = 12f;
        tmp.alignment = TextAlignmentOptions.Center;

        // Conteneur des boutons (bas de la dialog)
        GameObject goBtns = new GameObject("BoutonsContainer");
        RectTransform rtBtns = goBtns.AddComponent<RectTransform>();
        rtBtns.SetParent(goDialog.transform, false);
        rtBtns.anchorMin        = new Vector2(0f, 0f);
        rtBtns.anchorMax        = new Vector2(1f, 0.5f);
        rtBtns.offsetMin        = new Vector2(8f, 8f);
        rtBtns.offsetMax        = new Vector2(-8f, -4f);
        HorizontalLayoutGroup hlg = goBtns.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 8f;
        hlg.childForceExpandWidth  = true;
        hlg.childForceExpandHeight = true;

        Button btnOui = CreerBouton("BoutonOui", goBtns.transform, "Oui",
                                    new Color(0.7f, 0.2f, 0.2f, 1f));
        btnOui.onClick.AddListener(OnConfirmerSuppression);

        Button btnNon = CreerBouton("BoutonNon", goBtns.transform, "Non",
                                    new Color(0.2f, 0.4f, 0.2f, 1f));
        btnNon.onClick.AddListener(OnAnnulerSuppression);

        // Désactivé par défaut — DemanderConfirmationSuppression() l'activera
        goOverlay.SetActive(false);
    }

    private Button CreerBouton(string nom, Transform parent, string texte, Color couleur)
    {
        GameObject go = new GameObject(nom);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = couleur;
        Button btn = go.AddComponent<Button>();
        CreerTexte("Label", go.transform, texte);
        return btn;
    }
}
