using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Gère la scène Event : charge l'événement depuis RunManager,
/// affiche le texte et les choix, applique les effets du choix,
/// puis laisse le joueur continuer vers la navigation.
///
/// Structure de la scène recommandée :
///   Canvas
///     BackgroundImage      (Image — plein écran)
///     ContentPanel
///       TitleText          (TextMeshProUGUI)
///       DescriptionText    (TextMeshProUGUI)
///       ChoiceContainer    (Transform + Vertical Layout Group)
///     ContinueButton       (Button — caché jusqu'au choix ET après offre résolue)
///     EquipmentOfferArea   (GameObject — EquipmentOfferController, caché par défaut)
///       LootCardContainer  (Transform — Horizontal Layout Group)
///       SkipButton         (Button — "Passer")
///       ArmSelectionPanel  (GameObject — caché par défaut)
///         Arm1Button       (Button — "Bras gauche")
///         Arm2Button       (Button — "Bras droit")
/// </summary>
public class EventManager : MonoBehaviour
{
    // -----------------------------------------------
    // DONNÉES
    // -----------------------------------------------

    [Header("Données")]
    public EventDatabase eventDatabase;

    // -----------------------------------------------
    // UI — FOND
    // -----------------------------------------------

    [Header("UI — Fond")]
    // Image plein écran — son sprite est remplacé par celui de l'EventData
    public Image backgroundImage;

    // -----------------------------------------------
    // UI — TEXTE
    // -----------------------------------------------

    [Header("UI — Texte")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    // -----------------------------------------------
    // UI — CHOIX
    // -----------------------------------------------

    [Header("UI — Choix")]
    // Conteneur des boutons de choix (Vertical Layout Group recommandé)
    public Transform choiceContainer;

    // Préfab d'un bouton de choix — doit avoir un Button et un TextMeshProUGUI enfant
    public GameObject choiceButtonPrefab;

    // -----------------------------------------------
    // UI — CONTINUER
    // -----------------------------------------------

    [Header("UI — Continuer")]
    // Affiché après le choix, et après que l'offre d'équipement est résolue
    public Button continueButton;

    // -----------------------------------------------
    // UI — OFFRE D'ÉQUIPEMENT
    // -----------------------------------------------

    [Header("UI — Offre d'équipement")]
    // Composant partagé avec la scène Combat — gère l'affichage et la résolution des offres
    public EquipmentOfferController equipmentOfferController;

    // -----------------------------------------------
    // UI — CONSOMMABLES
    // -----------------------------------------------

    [Header("UI — Consommables")]
    // Conteneur où les icônes de consommables seront affichées (lecture seule en event)
    public Transform consumableContainer;

    // Préfab d'un bouton de consommable — même prefab que combat/navigation
    public GameObject consumableButtonPrefab;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private EventData currentEvent;

    // Liste des pièces à proposer au joueur (slot occupé → pas d'auto-équipement)
    private List<EquipmentData> pendingEquipmentOffers = new List<EquipmentData>();

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
            continueButton.gameObject.SetActive(false);
        }

        LoadAndDisplayEvent();
        SpawnConsumableButtons();
    }

    /// <summary>
    /// Lit l'eventID depuis RunManager, trouve l'EventData correspondant
    /// dans la database, et affiche son contenu.
    /// </summary>
    private void LoadAndDisplayEvent()
    {
        string eventID = RunManager.Instance?.currentSpecificEventID;

        if (string.IsNullOrEmpty(eventID))
        {
            Debug.LogError("[EventManager] Aucun eventID dans RunManager. " +
                           "Vérifie que NavigationManager appelle EnterRoom() avant GoToEvent().");
            return;
        }

        if (eventDatabase == null)
        {
            Debug.LogError("[EventManager] EventDatabase non assignée dans l'Inspector.");
            return;
        }

        currentEvent = eventDatabase.GetByID(eventID);

        if (currentEvent == null)
        {
            Debug.LogError($"[EventManager] Événement '{eventID}' introuvable dans la database.");
            return;
        }

        DisplayEvent();
    }

    // -----------------------------------------------
    // AFFICHAGE
    // -----------------------------------------------

    private void DisplayEvent()
    {
        // Fond d'écran
        if (backgroundImage != null && currentEvent.backgroundImage != null)
            backgroundImage.sprite = currentEvent.backgroundImage;

        // Texte
        if (titleText       != null) titleText.text       = currentEvent.title;
        if (descriptionText != null) descriptionText.text = currentEvent.description;

        SpawnChoiceButtons();
    }

    /// <summary>
    /// Affiche tous les consommables du joueur en lecture seule.
    /// Tous les boutons sont non interactables en scène Event :
    /// le joueur peut voir son inventaire mais pas l'utiliser pendant un événement.
    /// </summary>
    private void SpawnConsumableButtons()
    {
        if (consumableContainer == null || consumableButtonPrefab == null) return;
        if (RunManager.Instance == null) return;

        foreach (Transform child in consumableContainer)
            Destroy(child.gameObject);

        foreach (ConsumableData conso in RunManager.Instance.GetConsumables())
        {
            if (conso == null) continue;

            GameObject go = Instantiate(consumableButtonPrefab, consumableContainer);
            ConsumableButton cb = go.GetComponent<ConsumableButton>();
            if (cb == null) continue;

            cb.Setup(conso, null);
            cb.SetInteractable(false); // Jamais utilisable pendant un événement
        }
    }

    /// <summary>
    /// Instancie un bouton par choix disponible dans le choiceContainer.
    /// </summary>
    private void SpawnChoiceButtons()
    {
        if (choiceContainer == null || choiceButtonPrefab == null) return;

        // Nettoie les anciens boutons au cas où
        foreach (Transform child in choiceContainer)
            Destroy(child.gameObject);

        foreach (EventChoice choice in currentEvent.choices)
        {
            GameObject go = Instantiate(choiceButtonPrefab, choiceContainer);

            TextMeshProUGUI label = go.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.choiceText;

            Button btn = go.GetComponent<Button>();
            EventChoice captured = choice; // capture nécessaire pour le lambda
            if (btn != null)
                btn.onClick.AddListener(() => OnChoiceMade(captured));
        }
    }

    // -----------------------------------------------
    // CHOIX DU JOUEUR
    // -----------------------------------------------

    /// <summary>
    /// Appelé quand le joueur clique sur un bouton de choix.
    /// Applique les effets, remplace le texte par l'outcome, supprime les boutons.
    /// Si des équipements sont en attente, délègue à EquipmentOfferController avant "Continuer".
    /// </summary>
    private void OnChoiceMade(EventChoice choice)
    {
        pendingEquipmentOffers.Clear();

        ApplyEffects(choice.effects);

        // Remplace la description par le texte de résultat
        if (descriptionText != null)
            descriptionText.text = choice.outcomeText;

        // Supprime tous les boutons de choix
        foreach (Transform child in choiceContainer)
            Destroy(child.gameObject);

        // Si des pièces sont à proposer, déléguer au contrôleur partagé
        if (pendingEquipmentOffers.Count > 0 && equipmentOfferController != null)
        {
            equipmentOfferController.StartOffresSequentielles(pendingEquipmentOffers, OnEquipementResolu);
            pendingEquipmentOffers.Clear();
        }
        else
            MontrerContinueButton();
    }

    // -----------------------------------------------
    // EFFETS
    // -----------------------------------------------

    /// <summary>
    /// Applique les effets d'un choix sur l'état du run.
    /// Les pièces d'équipement qui ne peuvent pas être auto-équipées
    /// sont ajoutées à pendingEquipmentOffers pour une résolution interactive.
    /// </summary>
    private void ApplyEffects(List<EventEffect> effects)
    {
        if (effects == null || RunManager.Instance == null) return;

        foreach (EventEffect effect in effects)
        {
            switch (effect.type)
            {
                // -----------------------------------------------------------
                // MODIFICATEURS DE HP
                // -----------------------------------------------------------

                case EventEffectType.ModifyHP:
                    // Un événement ne tue jamais le joueur (plancher à 1)
                    int newHP = Mathf.Clamp(
                        RunManager.Instance.currentHP + effect.value,
                        1,
                        RunManager.Instance.maxHP
                    );
                    RunManager.Instance.currentHP = newHP;
                    Debug.Log($"[Event] HP : {(effect.value >= 0 ? "+" : "")}{effect.value} → {newHP}/{RunManager.Instance.maxHP}");
                    break;

                case EventEffectType.ModifyMaxHP:
                    // Le max ne peut pas descendre sous 1
                    int newMax = Mathf.Max(1, RunManager.Instance.maxHP + effect.value);
                    RunManager.Instance.maxHP = newMax;
                    // Les HP courants ne peuvent pas dépasser le nouveau max
                    RunManager.Instance.currentHP = Mathf.Min(RunManager.Instance.currentHP, newMax);
                    Debug.Log($"[Event] HP max : {(effect.value >= 0 ? "+" : "")}{effect.value} → max = {newMax}, courant = {RunManager.Instance.currentHP}");
                    break;

                case EventEffectType.HealToFull:
                    RunManager.Instance.currentHP = RunManager.Instance.maxHP;
                    Debug.Log($"[Event] Soin complet → {RunManager.Instance.currentHP}/{RunManager.Instance.maxHP}");
                    break;

                // -----------------------------------------------------------
                // OBJETS
                // -----------------------------------------------------------

                case EventEffectType.GainConsumable:
                    switch (effect.gainConsumableMode)
                    {
                        case GainConsumableMode.FromList:
                            if (effect.consumablesToGive == null || effect.consumablesToGive.Count == 0)
                            {
                                Debug.LogWarning("[Event] GainConsumable (FromList) : la liste consumablesToGive est vide.");
                                break;
                            }
                            foreach (ConsumableData consommable in effect.consumablesToGive)
                            {
                                if (consommable == null) continue;
                                if (!RunManager.Instance.AddConsumable(consommable))
                                {
                                    Debug.Log($"[Event] {consommable.consumableName} non obtenu — inventaire de consommables plein.");
                                    break; // Inutile de continuer si l'inventaire est déjà plein
                                }
                            }
                            break;

                        case GainConsumableMode.FromLootTable:
                            if (effect.consumableLootTable == null)
                            {
                                Debug.LogWarning("[Event] GainConsumable (FromLootTable) : aucune ConsumableLootTable assignée dans l'Inspector.");
                                break;
                            }
                            ConsumableData consommableTiré = effect.consumableLootTable.GetRandom();
                            if (consommableTiré != null)
                            {
                                if (!RunManager.Instance.AddConsumable(consommableTiré))
                                    Debug.Log($"[Event] {consommableTiré.consumableName} non obtenu — inventaire de consommables plein.");
                            }
                            break;
                    }
                    break;

                case EventEffectType.GainModule:
                    switch (effect.gainModuleMode)
                    {
                        case GainModuleMode.FromList:
                            if (effect.modulesToGive == null || effect.modulesToGive.Count == 0)
                            {
                                Debug.LogWarning("[Event] GainModule (FromList) : la liste modulesToGive est vide.");
                                break;
                            }
                            foreach (ModuleData module in effect.modulesToGive)
                            {
                                if (module == null) continue;
                                if (RunManager.Instance.HasModule(module))
                                {
                                    Debug.Log($"[Event] GainModule : '{module.moduleName}' déjà possédé — ignoré.");
                                    continue;
                                }
                                RunManager.Instance.AddModule(module);
                            }
                            break;

                        case GainModuleMode.FromLootTable:
                            if (effect.moduleLootTable == null)
                            {
                                Debug.LogWarning("[Event] GainModule (FromLootTable) : aucune ModuleLootTable assignée dans l'Inspector.");
                                break;
                            }
                            ModuleData tiré = effect.moduleLootTable.GetRandom();
                            if (tiré != null)
                                RunManager.Instance.AddModule(tiré);
                            break;
                    }
                    break;

                // -----------------------------------------------------------
                // ÉQUIPEMENT
                // -----------------------------------------------------------

                case EventEffectType.GainEquipment:
                    switch (effect.gainEquipmentMode)
                    {
                        case GainEquipmentMode.FromList:
                            if (effect.equipmentsToGive == null || effect.equipmentsToGive.Count == 0)
                            {
                                Debug.LogWarning("[Event] GainEquipment (FromList) : la liste equipmentsToGive est vide.");
                                break;
                            }
                            foreach (EquipmentData pièce in effect.equipmentsToGive)
                            {
                                if (pièce != null)
                                    TenterEquipementOuMettrEnFile(pièce);
                            }
                            break;

                        case GainEquipmentMode.FromLootTable:
                            if (effect.equipmentLootTable == null)
                            {
                                Debug.LogWarning("[Event] GainEquipment (FromLootTable) : aucune EquipmentLootTable assignée dans l'Inspector.");
                                break;
                            }
                            EquipmentData équipementTiré = effect.equipmentLootTable.GetRandom();
                            if (équipementTiré != null)
                                TenterEquipementOuMettrEnFile(équipementTiré);
                            break;
                    }
                    break;

                // -----------------------------------------------------------
                // FLAGS
                // -----------------------------------------------------------

                case EventEffectType.SetEventFlag:
                    if (string.IsNullOrEmpty(effect.flagKey))
                    {
                        Debug.LogWarning("[Event] SetEventFlag : flagKey est vide — aucun flag posé.");
                        break;
                    }
                    RunManager.Instance.SetEventFlag(effect.flagKey, effect.flagValue);
                    Debug.Log($"[Event] Flag '{effect.flagKey}' → {effect.flagValue}");
                    break;

                // -----------------------------------------------------------

                default:
                    Debug.LogWarning($"[Event] Effet '{effect.type}' non reconnu.");
                    break;
            }
        }
    }

    // -----------------------------------------------
    // ÉQUIPEMENT — AUTO-ÉQUIPEMENT ET FILE D'ATTENTE
    // -----------------------------------------------

    /// <summary>
    /// Tente d'équiper automatiquement une pièce si son slot est libre.
    /// Si le slot est occupé (ou les deux bras occupés pour un Arm),
    /// ajoute la pièce à pendingEquipmentOffers pour une résolution interactive
    /// via EquipmentOfferController.
    /// </summary>
    private void TenterEquipementOuMettrEnFile(EquipmentData pièce)
    {
        if (pièce == null || RunManager.Instance == null) return;

        if (pièce.equipmentType == EquipmentType.Arm)
        {
            // Pour les bras : cherche un slot libre en priorité
            if (RunManager.Instance.IsSlotFree(EquipmentSlot.Arm1))
            {
                RunManager.Instance.EquipItem(EquipmentSlot.Arm1, pièce);
                Debug.Log($"[Event] '{pièce.equipmentName}' équipé automatiquement → Arm1");
            }
            else if (RunManager.Instance.IsSlotFree(EquipmentSlot.Arm2))
            {
                RunManager.Instance.EquipItem(EquipmentSlot.Arm2, pièce);
                Debug.Log($"[Event] '{pièce.equipmentName}' équipé automatiquement → Arm2");
            }
            else
            {
                // Les deux bras sont occupés → proposer le remplacement au joueur
                Debug.Log($"[Event] '{pièce.equipmentName}' mis en attente — les deux bras sont occupés.");
                pendingEquipmentOffers.Add(pièce);
            }
        }
        else
        {
            EquipmentSlot slot = EquipmentTypeToSlot(pièce.equipmentType);
            if (RunManager.Instance.IsSlotFree(slot))
            {
                RunManager.Instance.EquipItem(slot, pièce);
                Debug.Log($"[Event] '{pièce.equipmentName}' équipé automatiquement → {slot}");
            }
            else
            {
                // Slot occupé → proposer le remplacement au joueur
                Debug.Log($"[Event] '{pièce.equipmentName}' mis en attente — slot {slot} occupé.");
                pendingEquipmentOffers.Add(pièce);
            }
        }
    }

    // -----------------------------------------------
    // ÉQUIPEMENT — CALLBACK DE FIN D'OFFRE
    // -----------------------------------------------

    /// <summary>
    /// Appelé par EquipmentOfferController quand toutes les offres sont résolues
    /// (équipées ou passées). Affiche le bouton "Continuer".
    /// </summary>
    private void OnEquipementResolu()
    {
        MontrerContinueButton();
    }

    /// <summary>
    /// Active le bouton "Continuer" et s'assure que son parent immédiat est lui aussi actif.
    /// Piège Unity : SetActive(true) sur un enfant n'a aucun effet visible si le parent est inactif.
    /// </summary>
    private void MontrerContinueButton()
    {
        if (continueButton == null) return;

        // Active tous les parents jusqu'au Canvas (exclu) pour s'assurer que le bouton
        // est visible quelle que soit sa profondeur dans la hiérarchie.
        // Piège Unity : SetActive(true) sur un enfant n'a aucun effet si un ancêtre est inactif.
        Transform t = continueButton.transform.parent;
        while (t != null && t.GetComponent<Canvas>() == null)
        {
            t.gameObject.SetActive(true);
            t = t.parent;
        }

        continueButton.gameObject.SetActive(true);
    }

    // -----------------------------------------------
    // UTILITAIRES ÉQUIPEMENT
    // -----------------------------------------------

    /// <summary>
    /// Convertit un EquipmentType (non-bras) en EquipmentSlot.
    /// Ne pas appeler avec EquipmentType.Arm — géré séparément.
    /// </summary>
    private static EquipmentSlot EquipmentTypeToSlot(EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => EquipmentSlot.Head,
            EquipmentType.Torso => EquipmentSlot.Torso,
            EquipmentType.Legs  => EquipmentSlot.Legs,
            _                   => EquipmentSlot.Head // fallback (ne devrait pas arriver)
        };
    }

    // -----------------------------------------------
    // CONTINUER
    // -----------------------------------------------

    private void OnContinueClicked()
    {
        if (RunManager.Instance != null)
        {
            // Marque l'event comme joué pour ne pas le retirer dans le pool d'une future visite
            RunManager.Instance.MarkEventPlayed(currentEvent.eventID);
            // Marque la salle comme complétée (comportement identique aux salles de combat)
            RunManager.Instance.ClearCurrentRoom();
        }
        SceneLoader.Instance.GoToNavigation();
    }
}
