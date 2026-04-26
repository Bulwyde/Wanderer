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
    // Containers pour les 4 catégories d'articles vendus par le marchand
    public Transform equipementContainer;
    public Transform moduleContainer;
    public Transform consommableShopContainer;
    public Transform skillShopContainer;

    [Header("UI — Disposition des articles")]
    // Nombre d'articles par ligne avant de passer à la ligne suivante
    public int equipmentParLigne    = 3;
    public int moduleParLigne       = 3;
    public int consommableParLigne  = 3;
    public int skillParLigne        = 3;

    // Espacement entre les articles dans une même rangée
    public float equipmentSpacingColumns   = 4f;
    public float moduleSpacingColumns      = 4f;
    public float consommableSpacingColumns = 4f;
    public float skillSpacingColumns       = 4f;

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
    private readonly List<(ShopItemButton btn, ShopItemSkill item)>      _boutonsSkills       = new();

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
        GenererArticlesSkills();
    }

    /// <summary>
    /// Configure (ou ajoute) le VerticalLayoutGroup + ContentSizeFitter du container
    /// pour que les rangées s'ancrent en haut et s'empilent vers le bas.
    /// Sans ça, le childAlignment par défaut (MiddleCenter) recentre tout le contenu
    /// à chaque ajout de rangée, décalant la première ligne vers le haut.
    /// Le spacing entre rangées est intentionnellement laissé à la valeur de l'Inspector.
    /// </summary>
    private void ConfigurerContainerVertical(Transform container)
    {
        if (container == null) return;

        // Pivot à (0.5, 1) : le container grandit vers le bas uniquement.
        // Avec le pivot par défaut (0.5, 0.5), ContentSizeFitter agrandit dans les deux sens
        // et remonte la première rangée quand le spacing ou le nombre de lignes augmente.
        RectTransform rt = container.GetComponent<RectTransform>();
        if (rt != null) rt.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup vlg = container.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = container.gameObject.AddComponent<VerticalLayoutGroup>();

        vlg.childAlignment         = TextAnchor.UpperCenter;
        vlg.childControlWidth      = false;
        vlg.childControlHeight     = false;
        vlg.childForceExpandWidth  = false;
        vlg.childForceExpandHeight = false;
        // spacing : géré dans l'Inspector, on ne l'écrase pas

        ContentSizeFitter csf = container.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = container.gameObject.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    /// <summary>
    /// Crée une nouvelle rangée horizontale enfant du container donné.
    /// </summary>
    private Transform CreerRangee(Transform container, float spacing)
    {
        GameObject rangeeGO = new GameObject("Rangee");
        rangeeGO.transform.SetParent(container, false);

        RectTransform rt = rangeeGO.AddComponent<RectTransform>();
        rt.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup hlg = rangeeGO.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlWidth      = false;
        hlg.childControlHeight     = false;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;
        hlg.spacing = spacing;

        ContentSizeFitter csf = rangeeGO.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        return rangeeGO.transform;
    }

    private void GenererArticlesEquipement()
    {
        if (equipementContainer == null || shopItemPrefab == null) return;
        ViderContainer(equipementContainer);
        ConfigurerContainerVertical(equipementContainer);
        _boutonsEquipement.Clear();

        int maxParLigne      = Mathf.Max(1, equipmentParLigne);
        Transform rangeeActuelle = null;
        int indexDansRangee  = 0;

        foreach (ShopItemEquipment item in shopState.equipements)
        {
            if (item?.data == null) continue;

            if (rangeeActuelle == null || indexDansRangee >= maxParLigne)
            {
                rangeeActuelle  = CreerRangee(equipementContainer, equipmentSpacingColumns);
                indexDansRangee = 0;
            }

            ShopItemEquipment itemRef = item;
            bool achetable = !item.achete && RunManager.Instance.HasEnoughCredits(item.prix);
            string label   = item.achete ? "Acheté" : null;

            GameObject go = Instantiate(shopItemPrefab, rangeeActuelle);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.equipmentName, item.prix, achetable,
                () => AcheterEquipement(itemRef), label);

            _boutonsEquipement.Add((btn, item));
            indexDansRangee++;
        }
    }

    private void GenererArticlesModules()
    {
        if (moduleContainer == null || shopItemPrefab == null) return;
        ViderContainer(moduleContainer);
        ConfigurerContainerVertical(moduleContainer);
        _boutonsModules.Clear();

        int maxParLigne      = Mathf.Max(1, moduleParLigne);
        Transform rangeeActuelle = null;
        int indexDansRangee  = 0;

        foreach (ShopItemModule item in shopState.modules)
        {
            if (item?.data == null) continue;

            if (rangeeActuelle == null || indexDansRangee >= maxParLigne)
            {
                rangeeActuelle  = CreerRangee(moduleContainer, moduleSpacingColumns);
                indexDansRangee = 0;
            }

            ShopItemModule itemRef = item;
            bool dejaPosse         = RunManager.Instance.HasModule(item.data);
            bool achetable         = !item.achete && !dejaPosse &&
                                     RunManager.Instance.HasEnoughCredits(item.prix);
            string label           = item.achete ? "Acheté"
                                   : dejaPosse   ? "Déjà possédé"
                                   : null;

            GameObject go = Instantiate(shopItemPrefab, rangeeActuelle);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.moduleName, item.prix, achetable,
                () => AcheterModule(itemRef), label);

            _boutonsModules.Add((btn, item));
            indexDansRangee++;
        }
    }

    private void GenererArticlesConsommables()
    {
        if (consommableShopContainer == null || shopItemPrefab == null) return;
        ViderContainer(consommableShopContainer);
        ConfigurerContainerVertical(consommableShopContainer);
        _boutonsConsommables.Clear();

        int maxParLigne      = Mathf.Max(1, consommableParLigne);
        Transform rangeeActuelle = null;
        int indexDansRangee  = 0;

        foreach (ShopItemConsomable item in shopState.consommables)
        {
            if (item?.data == null) continue;

            if (rangeeActuelle == null || indexDansRangee >= maxParLigne)
            {
                rangeeActuelle  = CreerRangee(consommableShopContainer, consommableSpacingColumns);
                indexDansRangee = 0;
            }

            ShopItemConsomable itemRef = item;
            bool achetable = !item.achete &&
                             RunManager.Instance.HasEnoughCredits(item.prix);
            string label   = item.achete ? "Acheté" : null;

            GameObject go = Instantiate(shopItemPrefab, rangeeActuelle);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.consumableName, item.prix, achetable,
                () => AcheterConsommable(itemRef), label);

            _boutonsConsommables.Add((btn, item));
            indexDansRangee++;
        }
    }

    private void GenererArticlesSkills()
    {
        if (skillShopContainer == null || shopItemPrefab == null) return;
        ViderContainer(skillShopContainer);
        ConfigurerContainerVertical(skillShopContainer);
        _boutonsSkills.Clear();

        int maxParLigne      = Mathf.Max(1, skillParLigne);
        Transform rangeeActuelle = null;
        int indexDansRangee  = 0;

        foreach (ShopItemSkill item in shopState.skills)
        {
            if (item?.data == null) continue;

            if (rangeeActuelle == null || indexDansRangee >= maxParLigne)
            {
                rangeeActuelle  = CreerRangee(skillShopContainer, skillSpacingColumns);
                indexDansRangee = 0;
            }

            ShopItemSkill itemRef = item;
            bool achetable = !item.achete &&
                             RunManager.Instance.HasEnoughCredits(item.prix);
            string label   = item.achete ? "Acheté" : null;

            GameObject go = Instantiate(shopItemPrefab, rangeeActuelle);
            ShopItemButton btn = go.GetComponent<ShopItemButton>();
            if (btn == null) continue;

            btn.Setup(item.data.skillName, item.prix, achetable,
                () => AcheterSkill(itemRef), label);

            _boutonsSkills.Add((btn, item));
            indexDansRangee++;
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
        EquipmentData clone = RunManager.Instance.CloneEquipmentForLoot(equip);
        RunManager.Instance.TryEquipEquipment(slot, clone);
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

    private void AcheterSkill(ShopItemSkill item)
    {
        if (item == null || item.achete) return;
        if (!RunManager.Instance.HasEnoughCredits(item.prix)) return;

        bool ajouté = RunManager.Instance.AddSkillToInventory(item.data);
        if (!ajouté)
        {
            Debug.Log($"[ShopManager] Impossible d'acheter '{item.data.skillName}' — inventaire skills plein.");
            return;
        }

        RunManager.Instance.AddCredits(-item.prix);
        item.achete = true;

        Debug.Log($"[ShopManager] Skill acheté : {item.data.skillName} | -{item.prix} credits");

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
        foreach (var (btn, item) in _boutonsSkills)
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
