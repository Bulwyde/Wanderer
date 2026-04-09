using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Gère la scène Marchand.
/// Lit l'état du shop depuis RunManager (généré à la première visite, persistant ensuite),
/// affiche les articles disponibles par catégorie, gère les achats et le remplacement
/// d'équipement via EquipmentOfferController.
///
/// Les consommables du joueur sont affichés et utilisables (même logique que scène Event :
/// seuls ceux avec usableInEvents = true sont cliquables).
///
/// Structure de la scène :
///   Canvas
///   ├── HUDPanel (enfant direct du Canvas)
///   │   ├── HPText                      (TextMeshProUGUI)
///   │   ├── CreditsText                 (TextMeshProUGUI)
///   │   ├── ConsommableContainer        (HorizontalLayoutGroup — désactivé par défaut,
///   │   │                                activé automatiquement par GenererConsommablesJoueur)
///   │   │   → assigné au champ "Joueur Consommable Container" de ShopManager
///   │   └── ModuleHUD                   (prefab ModuleHUD — piloté par ModuleManager.OnModulesChanged)
///   │       └── ModuleIconContainer     (HorizontalLayoutGroup + ContentSizeFitter)
///   ├── ShopPanel
///   │   ├── EquipementSection
///   │   │   └── EquipementContainer     (VerticalLayoutGroup ou GridLayout)
///   │   ├── ModuleSection
///   │   │   └── ModuleContainer
///   │   └── ConsommableSection
///   │       └── ConsommableShopContainer
///   ├── EquipmentOfferArea              (prefab EquipmentOfferController — se désactive via Awake)
///   │   ├── LootCardContainer
///   │   ├── SkipButton                  (Button + Text TMP)
///   │   └── ArmSelectionPanel           (désactivé par défaut)
///   │       ├── PromptText              (TextMeshProUGUI)
///   │       ├── Arm1Button              (Button + Text TMP)
///   │       └── Arm2Button              (Button + Text TMP)
///   └── QuitterButton                   (Button — retour à la Navigation)
///
/// ⚠️ EquipmentOfferController se désactive lui-même dans Awake() — c'est normal.
/// ⚠️ QuitterButton doit être frère de EquipmentOfferArea, jamais enfant.
/// ⚠️ ConsommableContainer (joueur) désactivé par défaut — GenererConsommablesJoueur() le réactive.
/// ⚠️ ModuleHUD doit être enfant direct du Canvas (ou d'un GO non affecté par le scroll/layout shop).
/// </summary>
public class ShopManager : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES UI — ARTICLES DU SHOP
    // -----------------------------------------------

    [Header("UI — Articles du shop")]
    // Containers pour les 3 catégories d'articles vendus par le marchand
    public Transform equipementContainer;
    public Transform moduleContainer;
    public Transform consommableShopContainer;

    // Espacement vertical entre les items dans chaque colonne d'équipement
    public float equipementColonneSpacing = 4f;

    // -----------------------------------------------
    // RÉFÉRENCES UI — CONSOMMABLES DU JOUEUR
    // -----------------------------------------------

    [Header("UI — Consommables du joueur")]
    // Container des icônes de consommables du joueur (utilisables dans la scène)
    public Transform joueurConsommableContainer;

    // -----------------------------------------------
    // RÉFÉRENCES UI — HUD
    // -----------------------------------------------

    [Header("UI — HUD")]
    // Optionnels — mis à jour au Start() et après chaque achat
    public TextMeshProUGUI hpText;
    public TextMeshProUGUI creditsText;

    // -----------------------------------------------
    // RÉFÉRENCES UI — OFFRE D'ÉQUIPEMENT (remplacement)
    // -----------------------------------------------

    [Header("UI — Remplacement d'équipement")]
    // Même composant partagé que dans Event — gère le remplacement quand le slot est plein
    public EquipmentOfferController equipmentOfferController;
    // Panel parent (même structure que LootPanel en Combat) — caché par défaut
    public GameObject lootPanel;
    // Bouton "Continuer" affiché après résolution — ferme le LootPanel, reste dans le shop
    public Button     lootContinueButton;

    // -----------------------------------------------
    // RÉFÉRENCES UI — NAVIGATION
    // -----------------------------------------------

    [Header("UI — Navigation")]
    public Button quitterButton;

    // -----------------------------------------------
    // PREFABS
    // -----------------------------------------------

    [Header("Prefabs")]
    // Bouton d'article du shop — doit avoir un composant ShopItemButton
    public GameObject shopItemPrefab;

    // Bouton de consommable du joueur — même prefab que Combat/Navigation/Event
    public GameObject consommablePrefab;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private ShopState shopState;
    private CellData  cellCourante;

    // Données de l'achat en cours (remplacement d'équipement — slot plein)
    private EquipmentData    pendingEquipement;
    private int              pendingPrix;
    private ShopItemEquipment pendingShopItemEquipement;

    // Références aux boutons actifs — permettent une mise à jour in-place
    // de la disponibilité (interactable) sans détruire/recréer les GO.
    private readonly List<(ShopItemButton btn, ShopItemEquipment item)>  _boutonsEquipement   = new();
    private readonly List<(ShopItemButton btn, ShopItemModule item)>     _boutonsModules      = new();
    private readonly List<(ShopItemButton btn, ShopItemConsomable item)> _boutonsConsommables = new();

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        if (RunManager.Instance == null)
        {
            Debug.LogError("[ShopManager] RunManager introuvable !");
            return;
        }

        // Récupère la CellData du marchand via la MapData stockée dans RunManager
        cellCourante = TrouverCellCourante();
        if (cellCourante == null)
        {
            Debug.LogError("[ShopManager] CellData introuvable — MapData non assignée dans RunManager ?");
            return;
        }

        // Résout le ShopData à utiliser :
        // 1. ShopData spécifique à la case (priorité)
        // 2. ShopData par défaut de la MapData (fallback)
        // 3. null → marchand vide + warning dans RunManager
        ShopData shopData = cellCourante.shopData
                         ?? RunManager.Instance.currentMapData?.defaultShopData;

        if (shopData == null)
            Debug.LogWarning("[ShopManager] Aucun ShopData trouvé (ni sur la case, ni sur la MapData). " +
                             "Le marchand sera vide.");

        // Génère l'état du shop s'il n'existe pas encore, sinon le récupère tel quel
        shopState = RunManager.Instance.GetOrCreateShopState(cellCourante, shopData);

        if (quitterButton != null)
            quitterButton.onClick.AddListener(Quitter);

        if (lootPanel          != null) lootPanel.SetActive(false);
        if (lootContinueButton != null)
        {
            lootContinueButton.onClick.AddListener(OnLootContinuerCliqué);
            lootContinueButton.gameObject.SetActive(false);
        }

        RafraichirHUD();
        GenererArticlesShop();
        GenererConsommablesJoueur();
    }

    // -----------------------------------------------
    // RÉCUPÉRATION DE LA CELLDATA
    // -----------------------------------------------

    private CellData TrouverCellCourante()
    {
        MapData map = RunManager.Instance.currentMapData;
        if (map == null)
        {
            Debug.LogError("[ShopManager] RunManager.currentMapData est null. " +
                           "NavigationManager doit l'assigner avant GoToShop().");
            return null;
        }
        return map.GetCell(RunManager.Instance.currentRoomX, RunManager.Instance.currentRoomY);
    }

    // -----------------------------------------------
    // GÉNÉRATION DE L'UI DES ARTICLES
    // -----------------------------------------------

    /// <summary>
    /// Instancie un ShopItemButton par article dans chaque container de catégorie.
    /// Appelé une fois au Start(), puis à nouveau après chaque achat (RafraichirArticles).
    /// </summary>
    private void GenererArticlesShop()
    {
        GenererArticlesEquipement();
        GenererArticlesModules();
        GenererArticlesConsommables();
    }

    private void GenererArticlesEquipement()
    {
        if (equipementContainer == null || shopItemPrefab == null) return;
        ViderContainer(equipementContainer);
        _boutonsEquipement.Clear();

        // Les équipements s'affichent en colonnes de MAX_PAR_COLONNE items.
        // EquipementContainer doit avoir un HorizontalLayoutGroup.
        // Chaque colonne est un GO enfant avec un VerticalLayoutGroup.
        const int MAX_PAR_COLONNE = 3;
        Transform colonneActuelle = null;
        int indexDansColonne = 0;

        foreach (ShopItemEquipment item in shopState.equipements)
        {
            if (item?.data == null) continue;

            // Nouvelle colonne si première itération ou colonne pleine
            if (colonneActuelle == null || indexDansColonne >= MAX_PAR_COLONNE)
            {
                GameObject colGO = new GameObject("ColonneEquipement");
                colGO.transform.SetParent(equipementContainer, false);

                RectTransform rt = colGO.AddComponent<RectTransform>();
                rt.sizeDelta = Vector2.zero;

                VerticalLayoutGroup vlg = colGO.AddComponent<VerticalLayoutGroup>();
                vlg.childControlWidth      = false; // le prefab a une largeur fixe
                vlg.childControlHeight     = false;
                vlg.childForceExpandWidth  = false;
                vlg.childForceExpandHeight = false;
                vlg.spacing = equipementColonneSpacing;

                // Seul le fit vertical est nécessaire : la hauteur de la colonne
                // s'adapte au nombre d'items. La largeur est fixée par le prefab.
                ContentSizeFitter csf = colGO.AddComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

                colonneActuelle  = colGO.transform;
                indexDansColonne = 0;
            }

            ShopItemEquipment itemRef = item;
            bool achetable = !item.achete && RunManager.Instance.HasEnoughCredits(item.prix);
            string label   = item.achete ? "Acheté" : null;

            GameObject go = Instantiate(shopItemPrefab, colonneActuelle);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.equipmentName, item.prix, achetable,
                () => AcheterEquipement(itemRef), label);

            _boutonsEquipement.Add((btn, item));
            indexDansColonne++;
        }
    }

    private void GenererArticlesModules()
    {
        if (moduleContainer == null || shopItemPrefab == null) return;
        ViderContainer(moduleContainer);
        _boutonsModules.Clear();

        foreach (ShopItemModule item in shopState.modules)
        {
            if (item?.data == null) continue;
            ShopItemModule itemRef = item;

            // Un module déjà possédé ne devrait pas apparaître (filtré à la génération),
            // mais on double-vérifie ici par sécurité.
            bool dejaPosse = RunManager.Instance.HasModule(item.data);
            bool achetable = !item.achete && !dejaPosse &&
                             RunManager.Instance.HasEnoughCredits(item.prix);

            string label = item.achete    ? "Acheté"
                         : dejaPosse      ? "Déjà possédé"
                         : null;

            GameObject go = Instantiate(shopItemPrefab, moduleContainer);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.moduleName, item.prix, achetable,
                () => AcheterModule(itemRef), label);

            _boutonsModules.Add((btn, item));
        }
    }

    private void GenererArticlesConsommables()
    {
        if (consommableShopContainer == null || shopItemPrefab == null) return;
        ViderContainer(consommableShopContainer);
        _boutonsConsommables.Clear();

        foreach (ShopItemConsomable item in shopState.consommables)
        {
            if (item?.data == null) continue;
            ShopItemConsomable itemRef = item;

            bool achetable = !item.achete &&
                             RunManager.Instance.HasEnoughCredits(item.prix);

            string label = item.achete ? "Acheté" : null;

            GameObject go = Instantiate(shopItemPrefab, consommableShopContainer);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.consumableName, item.prix, achetable,
                () => AcheterConsommable(itemRef), label);

            _boutonsConsommables.Add((btn, item));
        }
    }

    // -----------------------------------------------
    // CONSOMMABLES DU JOUEUR
    // -----------------------------------------------

    /// <summary>
    /// Affiche les consommables du joueur (même logique que scène Event).
    /// Seuls ceux avec usableInEvents = true sont cliquables.
    /// Appelé au Start() et après utilisation d'un consommable.
    /// </summary>
    private void GenererConsommablesJoueur()
    {
        if (joueurConsommableContainer == null || consommablePrefab == null) return;

        joueurConsommableContainer.gameObject.SetActive(true);
        ViderContainer(joueurConsommableContainer);

        foreach (ConsumableData conso in RunManager.Instance.GetConsumables())
        {
            if (conso == null) continue;

            GameObject go = Instantiate(consommablePrefab, joueurConsommableContainer);
            ConsumableButton cb = go.GetComponent<ConsumableButton>();
            if (cb == null) continue;

            // Même logique que EventManager : callback uniquement si usableInEvents
            System.Action<ConsumableData> callback = null;
            if (conso.usableInEvents)
                callback = UtiliserConsommableJoueur;

            cb.Setup(conso, callback);
            cb.SetInteractable(conso.usableInEvents);
        }

        // Force le recalcul synchrone du layout pour éviter que la modification
        // de ce container ne laisse le reste de la scène en état "dirty" un frame.
        Canvas.ForceUpdateCanvases();
    }

    private void UtiliserConsommableJoueur(ConsumableData conso)
    {
        if (conso == null || RunManager.Instance == null) return;

        Debug.Log($"[ShopManager] Consommable utilisé : {conso.consumableName}");

        // Application des effets hors-combat — même pattern qu'EventManager
        if (conso.effects != null)
        {
            foreach (EffectData effet in conso.effects)
                AppliquerEffetHorsCombat(effet, conso.consumableName);
        }

        RunManager.Instance.RemoveConsumable(conso);

        RafraichirHUD();
        GenererConsommablesJoueur();
        // Mise à jour in-place : pas de destruction/recréation des boutons → pas de flash
        MettreAJourDisponibilite();
    }

    /// <summary>
    /// Applique un effet de consommable hors-combat.
    /// Même logique qu'EventManager.AppliquerEffetHorsCombat.
    /// </summary>
    private void AppliquerEffetHorsCombat(EffectData effet, string source)
    {
        if (effet == null || RunManager.Instance == null) return;

        switch (effet.action)
        {
            case EffectAction.Heal:
            {
                int soin = Mathf.Min(
                    Mathf.Max(0, Mathf.RoundToInt(effet.value)),
                    RunManager.Instance.maxHP - RunManager.Instance.currentHP);
                if (soin > 0)
                {
                    RunManager.Instance.currentHP += soin;
                    Debug.Log($"[ShopManager] {source} — Soin : +{soin} HP " +
                              $"→ {RunManager.Instance.currentHP}/{RunManager.Instance.maxHP}");
                }
                break;
            }

            case EffectAction.AddCredits:
            {
                int montant = Mathf.RoundToInt(effet.value);
                RunManager.Instance.AddCredits(montant);
                Debug.Log($"[ShopManager] {source} — {(montant >= 0 ? "+" : "")}{montant} credits");
                break;
            }

            case EffectAction.ModifyStat:
            {
                RunManager.Instance.AddStatBonus(effet.statToModify, effet.value);
                Debug.Log($"[ShopManager] {source} — ModifyStat : {effet.statToModify} " +
                          $"{(effet.value >= 0 ? "+" : "")}{effet.value}");
                break;
            }

            default:
                Debug.Log($"[ShopManager] {source} — Effet '{effet.action}' non applicable hors combat, ignoré.");
                break;
        }
    }

    // -----------------------------------------------
    // LOGIQUE D'ACHAT
    // -----------------------------------------------

    private void AcheterEquipement(ShopItemEquipment item)
    {
        if (item == null || item.achete) return;
        if (!RunManager.Instance.HasEnoughCredits(item.prix))
        {
            Debug.Log($"[ShopManager] Crédits insuffisants pour {item.data.equipmentName} " +
                      $"({item.prix} requis, {RunManager.Instance.credits} disponibles)");
            return;
        }

        EquipmentData equip = item.data;

        if (equip.equipmentType == EquipmentType.Arm)
        {
            // Bras : cherche un slot libre, sinon ouvre le panel de remplacement
            if (RunManager.Instance.IsSlotFree(EquipmentSlot.Arm1))
            {
                ConfirmerAchatEquipement(item, EquipmentSlot.Arm1, equip);
            }
            else if (RunManager.Instance.IsSlotFree(EquipmentSlot.Arm2))
            {
                ConfirmerAchatEquipement(item, EquipmentSlot.Arm2, equip);
            }
            else
            {
                // Les deux bras sont occupés — l'EquipmentOfferController gère le choix
                OuvrirRemplacement(item);
            }
        }
        else
        {
            // Autres slots (Head, Torso, Legs)
            EquipmentSlot slot = EquipmentTypeToSlot(equip.equipmentType);
            if (RunManager.Instance.IsSlotFree(slot))
            {
                ConfirmerAchatEquipement(item, slot, equip);
            }
            else
            {
                // Slot occupé — ouvre le panel de remplacement
                OuvrirRemplacement(item);
            }
        }
    }

    /// <summary>
    /// Équipe directement et finalise l'achat (slot libre ou slot choisi confirmé).
    /// </summary>
    private void ConfirmerAchatEquipement(ShopItemEquipment item, EquipmentSlot slot, EquipmentData equip)
    {
        RunManager.Instance.EquipItem(slot, equip);
        RunManager.Instance.AddCredits(-item.prix);
        item.achete = true;

        Debug.Log($"[ShopManager] Équipement acheté : {equip.equipmentName} → slot {slot} " +
                  $"| -{item.prix} credits");

        RafraichirHUD();
        RafraichirArticles();
    }

    /// <summary>
    /// Déclenche le flow de remplacement via EquipmentOfferController.
    /// Les crédits sont déduits et l'article marqué "acheté" UNIQUEMENT si l'item
    /// est effectivement équipé à l'issue du flow (le joueur peut "passer" sans équiper).
    /// </summary>
    private void OuvrirRemplacement(ShopItemEquipment item)
    {
        if (equipmentOfferController == null)
        {
            Debug.LogError("[ShopManager] equipmentOfferController non assigné !");
            return;
        }

        pendingEquipement        = item.data;
        pendingPrix              = item.prix;
        pendingShopItemEquipement = item;

        // Affiche le LootPanel (structure partagée avec Combat) si assigné
        if (lootPanel          != null) lootPanel.SetActive(true);
        if (lootContinueButton != null) lootContinueButton.gameObject.SetActive(false);

        equipmentOfferController.StartOffresSequentielles(
            new List<EquipmentData> { item.data },
            OnRemplacementResolu);
    }

    /// <summary>
    /// Callback appelé par EquipmentOfferController après résolution du remplacement.
    /// Vérifie si l'item a été équipé pour décider de facturer ou non.
    /// </summary>
    private void OnRemplacementResolu()
    {
        if (pendingEquipement != null && EstEquipe(pendingEquipement))
        {
            RunManager.Instance.AddCredits(-pendingPrix);
            if (pendingShopItemEquipement != null)
                pendingShopItemEquipement.achete = true;

            Debug.Log($"[ShopManager] Remplacement confirmé : {pendingEquipement.equipmentName} " +
                      $"| -{pendingPrix} credits");
        }
        else
        {
            Debug.Log($"[ShopManager] Remplacement annulé — aucun achat effectué.");
        }

        pendingEquipement         = null;
        pendingPrix               = 0;
        pendingShopItemEquipement = null;

        RafraichirHUD();
        RafraichirArticles();

        // Dans le shop, on ferme directement le LootPanel sans passer par un bouton "Continuer"
        // Le joueur reste dans le shop et peut continuer ses achats
        if (lootPanel != null) lootPanel.SetActive(false);
    }

    /// <summary>
    /// Ferme le LootPanel après résolution d'un remplacement d'équipement.
    /// Le joueur reste dans le shop.
    /// </summary>
    private void OnLootContinuerCliqué()
    {
        if (lootPanel != null) lootPanel.SetActive(false);
    }

    private void AcheterModule(ShopItemModule item)
    {
        if (item == null || item.achete) return;
        if (RunManager.Instance.HasModule(item.data))
        {
            Debug.Log($"[ShopManager] Module déjà possédé : {item.data.moduleName}");
            return;
        }
        if (!RunManager.Instance.HasEnoughCredits(item.prix)) return;

        RunManager.Instance.AddModule(item.data);
        RunManager.Instance.AddCredits(-item.prix);
        item.achete = true;

        Debug.Log($"[ShopManager] Module acheté : {item.data.moduleName} | -{item.prix} credits");

        RafraichirHUD();
        RafraichirArticles();
    }

    private void AcheterConsommable(ShopItemConsomable item)
    {
        if (item == null || item.achete) return;
        if (!RunManager.Instance.HasEnoughCredits(item.prix)) return;

        if (!RunManager.Instance.HasConsumableSlotFree())
        {
            Debug.Log($"[ShopManager] Impossible d'acheter {item.data.consumableName} — " +
                      $"inventaire de consommables plein.");
            return;
        }

        bool ajouté = RunManager.Instance.AddConsumable(item.data);
        if (!ajouté) return; // Vérification de sécurité

        RunManager.Instance.AddCredits(-item.prix);
        item.achete = true;

        Debug.Log($"[ShopManager] Consommable acheté : {item.data.consumableName} | -{item.prix} credits");

        RafraichirHUD();
        RafraichirArticles();
        GenererConsommablesJoueur(); // Actualise les icônes du joueur
    }

    // -----------------------------------------------
    // RAFRAÎCHISSEMENT DE L'UI
    // -----------------------------------------------

    private void RafraichirHUD()
    {
        if (hpText != null && RunManager.Instance != null)
            hpText.text = $"{RunManager.Instance.currentHP} / {RunManager.Instance.maxHP}";

        if (creditsText != null && RunManager.Instance != null)
            creditsText.text = $"{RunManager.Instance.credits} credits";
    }

    /// <summary>
    /// Met à jour l'état interactable des boutons existants selon les crédits actuels,
    /// SANS détruire ni recréer les GO. Aucun flash possible puisque les boutons
    /// ne passent jamais par l'état "disponible" par défaut du prefab.
    /// À appeler quand seuls les crédits changent (consommable, etc.).
    /// </summary>
    private void MettreAJourDisponibilite()
    {
        foreach (var (btn, item) in _boutonsEquipement)
        {
            if (btn == null) continue;
            bool achetable = !item.achete && RunManager.Instance.HasEnoughCredits(item.prix);
            btn.SetInteractable(achetable);
        }
        foreach (var (btn, item) in _boutonsModules)
        {
            if (btn == null) continue;
            bool dejaPosse = RunManager.Instance.HasModule(item.data);
            bool achetable = !item.achete && !dejaPosse &&
                             RunManager.Instance.HasEnoughCredits(item.prix);
            btn.SetInteractable(achetable);
        }
        foreach (var (btn, item) in _boutonsConsommables)
        {
            if (btn == null) continue;
            bool achetable = !item.achete && RunManager.Instance.HasEnoughCredits(item.prix);
            btn.SetInteractable(achetable);
        }
    }

    /// <summary>
    /// Recrée tous les boutons d'articles du shop (après un achat).
    /// Nécessaire quand le label d'un article change (ex : "Acheté").
    /// Pour une simple mise à jour de disponibilité, préférer MettreAJourDisponibilite().
    /// </summary>
    private void RafraichirArticles()
    {
        GenererArticlesShop();

        // Force le recalcul synchrone de TOUT le layout Canvas.
        // Sans ça, Unity diffère le recalcul à la fin du frame : pendant ce délai,
        // les LayoutGroups (HLG des colonnes, VLG des items) sont en état "dirty" et
        // les positions affichées sont incorrectes → décalage visuel d'un frame.
        Canvas.ForceUpdateCanvases();
    }

    // -----------------------------------------------
    // NAVIGATION
    // -----------------------------------------------

    private void Quitter()
    {
        if (SceneLoader.Instance == null)
        {
            Debug.LogError("[ShopManager] SceneLoader introuvable !");
            return;
        }
        Debug.Log("[ShopManager] Retour à la Navigation.");
        SceneLoader.Instance.GoToNavigation();
    }

    // -----------------------------------------------
    // UTILITAIRES
    // -----------------------------------------------

    private void ViderContainer(Transform container)
    {
        // SetActive(false) immédiatement : le LayoutGroup ignore les enfants inactifs
        // dès ce frame, avant que Destroy() soit réellement exécuté en fin de frame.
        // Sans ça, les anciens enfants (pending destroy) et les nouveaux coexistent
        // le temps d'un frame → décalage visuel dans le HorizontalLayoutGroup.
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            GameObject child = container.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    /// <summary>
    /// Vérifie si une pièce d'équipement est actuellement équipée dans l'un des slots.
    /// Utilisé après le flow de remplacement pour savoir si l'achat a été confirmé.
    /// </summary>
    private bool EstEquipe(EquipmentData equip)
    {
        foreach (EquipmentSlot slot in System.Enum.GetValues(typeof(EquipmentSlot)))
        {
            if (RunManager.Instance?.GetEquipped(slot) == equip)
                return true;
        }
        return false;
    }

    private static EquipmentSlot EquipmentTypeToSlot(EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => EquipmentSlot.Head,
            EquipmentType.Torso => EquipmentSlot.Torso,
            EquipmentType.Legs  => EquipmentSlot.Legs,
            _                   => EquipmentSlot.Head
        };
    }
}
