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
}
