using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gère la scène MainMenu : état des boutons, popup d'abandon de run, transitions.
///
/// Structure de scène recommandée :
/// Canvas
/// ├── KeyArt              (Image, ancré plein écran, premier enfant = derrière tout)
/// ├── Logo                (Image ou Panel, ancré haut-centre)
/// │   └── LogoText        (TextMeshPro)
/// ├── ButtonPanel         (VerticalLayoutGroup, ancré gauche-centre)
/// │   ├── ContinuerButton
/// │   ├── NouvellePartieButton
/// │   ├── ParamètresButton
/// │   └── QuitterButton
/// └── ConfirmationPopup   (GameObject, désactivé par défaut)
///     ├── DarkOverlay     (Image plein écran, noir semi-transparent)
///     └── PopupPanel      (Image centrée)
///         ├── MessageText (TextMeshPro)
///         ├── OuiButton   (Button)
///         └── NonButton   (Button)
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES UI
    // -----------------------------------------------

    [Header("Personnage par défaut")]
    // CharacterData utilisé jusqu'à l'implémentation de la sélection de personnage.
    // TODO : remplacer par le résultat de la sélection quand la scène existera.
    [SerializeField] private CharacterData defaultCharacter;

    [Header("Boutons principaux")]
    [SerializeField] private Button continuerButton;
    [SerializeField] private Button nouvellePartieButton;
    [SerializeField] private Button parametresButton;
    [SerializeField] private Button quitterButton;

    [Header("Popup confirmation abandon")]
    [SerializeField] private GameObject confirmationPopup;
    [SerializeField] private Button ouiButton;
    [SerializeField] private Button nonButton;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Start()
    {
        // Masquer le popup au démarrage
        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        // Brancher les listeners
        continuerButton?.onClick.AddListener(OnContinuerClique);
        nouvellePartieButton?.onClick.AddListener(OnNouvellePartieClique);
        parametresButton?.onClick.AddListener(OnParametresClique);
        quitterButton?.onClick.AddListener(OnQuitterClique);
        ouiButton?.onClick.AddListener(OnOuiClique);
        nonButton?.onClick.AddListener(OnNonClique);

        // Mettre à jour l'état des boutons selon la run en cours
        RafraichirBoutons();
    }

    // -----------------------------------------------
    // ÉTAT DES BOUTONS
    // -----------------------------------------------

    private void RafraichirBoutons()
    {
        bool runEnCours = RunManager.Instance != null && RunManager.Instance.hasActiveRun;

        // "Continuer" : actif seulement si une run est en cours
        if (continuerButton != null)
            continuerButton.interactable = runEnCours;

        Debug.Log($"[MainMenu] Rafraîchissement boutons — run en cours : {runEnCours}");
    }

    // -----------------------------------------------
    // CALLBACKS BOUTONS PRINCIPAUX
    // -----------------------------------------------

    private void OnContinuerClique()
    {
        Debug.Log("[MainMenu] Continuer la run.");
        SceneLoader.Instance.GoToNavigation();
    }

    private void OnNouvellePartieClique()
    {
        bool runEnCours = RunManager.Instance != null && RunManager.Instance.hasActiveRun;

        if (runEnCours)
        {
            // Une run est en cours : demander confirmation avant d'abandonner
            Debug.Log("[MainMenu] Run en cours — affichage popup de confirmation.");
            if (confirmationPopup != null)
                confirmationPopup.SetActive(true);
        }
        else
        {
            // Pas de run en cours : démarrer directement
            DemarrerNouvellePartie();
        }
    }

    private void OnParametresClique()
    {
        // TODO : ouvrir le panel de paramètres
        Debug.Log("[MainMenu] Paramètres — à implémenter.");
    }

    private void OnQuitterClique()
    {
        Debug.Log("[MainMenu] Quitter l'application.");
        Application.Quit();

        // En éditeur Unity, arrêter le Play Mode
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // -----------------------------------------------
    // CALLBACKS POPUP CONFIRMATION
    // -----------------------------------------------

    private void OnOuiClique()
    {
        Debug.Log("[MainMenu] Abandon de la run confirmé.");

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);

        // Terminer la run en cours puis en démarrer une nouvelle
        RunManager.Instance?.EndRun();
        DemarrerNouvellePartie();
    }

    private void OnNonClique()
    {
        Debug.Log("[MainMenu] Abandon annulé.");

        if (confirmationPopup != null)
            confirmationPopup.SetActive(false);
    }

    // -----------------------------------------------
    // HELPERS
    // -----------------------------------------------

    /// <summary>
    /// Lance une nouvelle partie.
    /// TODO : remplacer par GoToCharacterSelection() quand la scène de sélection sera prête.
    /// Pour l'instant, démarre avec un ID par défaut et va en Navigation.
    /// </summary>
    private void DemarrerNouvellePartie()
    {
        Debug.Log($"[MainMenu] Démarrage nouvelle partie — personnage : {defaultCharacter?.characterName ?? "non assigné"}");

        if (defaultCharacter == null)
            Debug.LogWarning("[MainMenu] defaultCharacter non assigné dans l'Inspector — les stats et l'équipement de départ ne seront pas chargés.");

        // TODO : remplacer defaultCharacter par le personnage choisi quand la sélection existera
        RunManager.Instance?.StartNewRun(defaultCharacter, "map_01");
        SceneLoader.Instance.GoToNavigation();
    }
}
