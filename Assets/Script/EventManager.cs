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
                case EventEffectType.ModifyHP:
                    int newHP = Mathf.Clamp(
                        RunManager.Instance.currentHP + effect.value,
                        1,                          // un événement ne tue pas le joueur
                        RunManager.Instance.maxHP
                    );
                    RunManager.Instance.currentHP = newHP;
                    Debug.Log($"[Event] HP : {(effect.value >= 0 ? "+" : "")}{effect.value} → {newHP}/{RunManager.Instance.maxHP}");
                    break;

                default:
                    Debug.Log($"[Event] Effet '{effect.type}' non encore implémenté.");
                    break;
            }
        }
    }

    // -----------------------------------------------
    // CONTINUER
    // -----------------------------------------------

    private void OnContinueClicked()
    {
        // Marque la salle comme complétée pour ne pas y revenir
        RunManager.Instance?.ClearCurrentRoom();
        SceneLoader.Instance.GoToNavigation();
    }
}
