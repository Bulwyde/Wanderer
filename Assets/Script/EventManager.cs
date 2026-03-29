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
///     BackgroundImage   (Image — plein écran)
///     ContentPanel
///       TitleText       (TextMeshProUGUI)
///       DescriptionText (TextMeshProUGUI)
///       ChoiceContainer (Transform + Vertical Layout Group)
///     ContinueButton    (Button — caché jusqu'au choix)
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
    // Affiché uniquement après que le joueur a fait un choix
    public Button continueButton;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private EventData currentEvent;

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
    /// Applique les effets, remplace le texte par l'outcome,
    /// supprime les boutons de choix et affiche "Continuer".
    /// </summary>
    private void OnChoiceMade(EventChoice choice)
    {
        ApplyEffects(choice.effects);

        // Remplace la description par le texte de résultat
        if (descriptionText != null)
            descriptionText.text = choice.outcomeText;

        // Supprime tous les boutons de choix
        foreach (Transform child in choiceContainer)
            Destroy(child.gameObject);

        // Affiche le bouton "Continuer"
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    // -----------------------------------------------
    // EFFETS
    // -----------------------------------------------

    /// <summary>
    /// Applique les effets d'un choix sur l'état du run.
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
                    if (effect.consumableToGive == null)
                    {
                        Debug.LogWarning("[Event] GainConsumable : aucun ConsumableData assigné dans l'Inspector.");
                        break;
                    }
                    bool ajouté = RunManager.Instance.AddConsumable(effect.consumableToGive);
                    if (!ajouté)
                        Debug.Log($"[Event] {effect.consumableToGive.consumableName} non obtenu — inventaire de consommables plein.");
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
                            // Si tiré == null : GetRandom() a déjà loggé la raison (table vide ou tout possédé)
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
