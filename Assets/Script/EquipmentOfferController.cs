using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Gère l'affichage et la résolution des offres d'équipement — logique partagée
/// entre la scène Combat et la scène Event.
///
/// Deux modes d'utilisation :
///
///   StartOffresSimultanées (combat) :
///     Toutes les cartes sont affichées en même temps.
///     Le joueur en choisit UNE — les autres sont ignorées.
///     Le callback est appelé dès qu'une pièce est choisie.
///
///   StartOffresSequentielles (événement) :
///     Les offres sont présentées une par une.
///     Pour chaque pièce : le joueur peut équiper ou passer.
///     Le callback est appelé quand toutes les offres sont résolues.
///
/// Structure du prefab recommandée :
///   EquipmentOfferArea     ← ce composant
///     ├─ LootCardContainer (Transform — Horizontal Layout Group)
///     ├─ SkipButton        (Button — "Passer", optionnel — séquentiel uniquement)
///     └─ ArmSelectionPanel (GameObject — désactivé par défaut)
///          ├─ Arm1Button   (Button — "Bras gauche")
///          └─ Arm2Button   (Button — "Bras droit")
///
/// En mode simultané (combat) : laisser skipButton non assigné.
/// En mode séquentiel (événement) : assigner skipButton.
/// </summary>
public class EquipmentOfferController : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES UI
    // -----------------------------------------------

    [Header("Cartes")]
    public Transform  lootCardContainer;
    public GameObject lootCardPrefab;

    [Header("Navigation")]
    // Optionnel — uniquement en mode séquentiel (événements)
    // En mode simultané (combat), laisser vide
    public Button     skipButton;

    [Header("Sélection slot bras")]
    public GameObject armSelectionPanel;
    public Button     armSelectArm1Button;
    public Button     armSelectArm2Button;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    // true = mode séquentiel (une offre à la fois), false = simultané (toutes d'un coup)
    private bool isSequential;

    // File des offres en attente — utilisée uniquement en mode séquentiel
    private Queue<EquipmentData> pendingSequential = new Queue<EquipmentData>();

    // Cartes instanciées pour l'offre en cours
    private List<LootCard> spawnedCards = new List<LootCard>();

    // Pièce de bras en attente de confirmation du slot (Arm1 ou Arm2)
    private EquipmentData pendingArmChoice;

    // Appelé quand toutes les offres sont résolues (équipées ou passées)
    private Action onAllResolved;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Awake()
    {
        if (skipButton          != null) skipButton.onClick.AddListener(OnSkipClicked);
        if (armSelectArm1Button != null) armSelectArm1Button.onClick.AddListener(() => ConfirmArmPlacement(EquipmentSlot.Arm1));
        if (armSelectArm2Button != null) armSelectArm2Button.onClick.AddListener(() => ConfirmArmPlacement(EquipmentSlot.Arm2));

        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);

        // Le panel se désactive lui-même — les managers n'ont pas à le faire
        gameObject.SetActive(false);
    }

    // -----------------------------------------------
    // API PUBLIQUE
    // -----------------------------------------------

    /// <summary>
    /// Mode simultané — style combat.
    /// Toutes les offres sont affichées en même temps.
    /// Le joueur choisit une pièce ou utilise le bouton "Continuer" externe pour passer.
    /// onTerminé peut être null si aucune action n'est requise après sélection.
    /// </summary>
    public void StartOffresSimultanées(List<EquipmentData> offres, Action onTerminé)
    {
        isSequential  = false;
        onAllResolved = onTerminé;

        if (skipButton != null) skipButton.gameObject.SetActive(false);

        NettoierCartes();
        AfficherCartes(offres);
    }

    /// <summary>
    /// Mode séquentiel — style événement.
    /// Les offres sont présentées une par une.
    /// Chaque pièce peut être équipée ou passée avant de passer à la suivante.
    /// onTerminé est appelé quand toutes les offres sont résolues.
    /// </summary>
    public void StartOffresSequentielles(List<EquipmentData> offres, Action onTerminé)
    {
        isSequential  = true;
        onAllResolved = onTerminé;

        if (skipButton != null) skipButton.gameObject.SetActive(true);

        pendingSequential.Clear();
        foreach (EquipmentData o in offres)
            if (o != null) pendingSequential.Enqueue(o);

        AfficherSuivante();
    }

    // -----------------------------------------------
    // AFFICHAGE DES CARTES
    // -----------------------------------------------

    /// <summary>
    /// Instancie une LootCard par pièce dans la liste et active le panel.
    /// Si la liste est vide, ferme le panel et appelle le callback immédiatement.
    /// </summary>
    private void AfficherCartes(List<EquipmentData> offres)
    {
        if (offres == null || offres.Count == 0)
        {
            gameObject.SetActive(false);
            onAllResolved?.Invoke();
            return;
        }

        gameObject.SetActive(true);

        foreach (EquipmentData pièce in offres)
        {
            if (pièce == null || lootCardPrefab == null || lootCardContainer == null) continue;

            GameObject go   = Instantiate(lootCardPrefab, lootCardContainer);
            LootCard   card = go.GetComponent<LootCard>();
            if (card == null) continue;

            card.Setup(pièce, OnCardChosen);
            spawnedCards.Add(card);
        }
    }

    /// <summary>
    /// (Mode séquentiel) Dépile la prochaine offre et l'affiche.
    /// Si la file est vide, ferme le panel et appelle le callback.
    /// </summary>
    private void AfficherSuivante()
    {
        NettoierCartes();
        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);

        if (pendingSequential.Count == 0)
        {
            gameObject.SetActive(false);
            onAllResolved?.Invoke();
            return;
        }

        AfficherCartes(new List<EquipmentData> { pendingSequential.Dequeue() });
    }

    private void NettoierCartes()
    {
        foreach (LootCard c in spawnedCards)
            if (c != null) Destroy(c.gameObject);
        spawnedCards.Clear();
        pendingArmChoice = null;
    }

    // -----------------------------------------------
    // SÉLECTION D'UNE CARTE
    // -----------------------------------------------

    private void OnCardChosen(EquipmentData chosen)
    {
        // Feedback visuel — sélectionne la carte cliquée
        foreach (LootCard c in spawnedCards)
            c.SetSelected(c.Equipment == chosen);

        if (chosen.equipmentType == EquipmentType.Arm)
        {
            // Cherche d'abord un slot bras libre — équipement automatique si possible
            if (RunManager.Instance != null && RunManager.Instance.IsSlotFree(EquipmentSlot.Arm1))
            {
                FinalizeEquip(EquipmentSlot.Arm1, chosen);
            }
            else if (RunManager.Instance != null && RunManager.Instance.IsSlotFree(EquipmentSlot.Arm2))
            {
                FinalizeEquip(EquipmentSlot.Arm2, chosen);
            }
            else
            {
                // Les deux bras sont occupés → le joueur choisit lequel remplacer
                pendingArmChoice = chosen;
                ShowArmSelectionPanel();
            }
        }
        else
        {
            FinalizeEquip(EquipmentTypeToSlot(chosen.equipmentType), chosen);
        }
    }

    // -----------------------------------------------
    // SÉLECTION DU SLOT BRAS
    // -----------------------------------------------

    private void ShowArmSelectionPanel()
    {
        if (armSelectionPanel == null) return;
        armSelectionPanel.SetActive(true);

        if (armSelectArm1Button != null)
        {
            TextMeshProUGUI label = armSelectArm1Button.GetComponentInChildren<TextMeshProUGUI>();
            EquipmentData actuel  = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm1);
            if (label != null)
                label.text = $"Bras gauche\n({actuel?.equipmentName ?? "vide"})";
        }

        if (armSelectArm2Button != null)
        {
            TextMeshProUGUI label = armSelectArm2Button.GetComponentInChildren<TextMeshProUGUI>();
            EquipmentData actuel  = RunManager.Instance?.GetEquipped(EquipmentSlot.Arm2);
            if (label != null)
                label.text = $"Bras droit\n({actuel?.equipmentName ?? "vide"})";
        }
    }

    private void ConfirmArmPlacement(EquipmentSlot slot)
    {
        if (pendingArmChoice == null) return;
        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);
        FinalizeEquip(slot, pendingArmChoice);
        pendingArmChoice = null;
    }

    // -----------------------------------------------
    // FINALISATION
    // -----------------------------------------------

    private void FinalizeEquip(EquipmentSlot slot, EquipmentData item)
    {
        RunManager.Instance?.EquipItem(slot, item);
        Debug.Log($"[EquipmentOfferController] '{item.equipmentName}' → slot {slot}");

        foreach (LootCard c in spawnedCards)
            c.SetInteractable(false);

        if (isSequential)
            AfficherSuivante(); // passe à l'offre suivante (ou ferme si file vide)
        else
        {
            // Mode simultané : cartes verrouillées, le panel parent gère sa propre fermeture.
            // On n'appelle PAS SetActive(false) ici — le bouton "Continuer" doit rester accessible
            // que celui-ci soit enfant de ce GO ou d'un GO frère.
            onAllResolved?.Invoke();
        }
    }

    // -----------------------------------------------
    // PASSER
    // -----------------------------------------------

    private void OnSkipClicked()
    {
        if (armSelectionPanel != null) armSelectionPanel.SetActive(false);
        pendingArmChoice = null;
        Debug.Log("[EquipmentOfferController] Offre ignorée par le joueur.");

        if (isSequential)
            AfficherSuivante();
        else
        {
            // Mode simultané : même logique que FinalizeEquip — pas de SetActive(false)
            onAllResolved?.Invoke();
        }
    }

    // -----------------------------------------------
    // UTILITAIRES
    // -----------------------------------------------

    /// <summary>
    /// Convertit un EquipmentType (non-bras) en EquipmentSlot.
    /// Ne pas appeler avec EquipmentType.Arm — géré séparément via ShowArmSelectionPanel.
    /// </summary>
    private static EquipmentSlot EquipmentTypeToSlot(EquipmentType type)
    {
        return type switch
        {
            EquipmentType.Head  => EquipmentSlot.Head,
            EquipmentType.Torso => EquipmentSlot.Torso,
            EquipmentType.Legs  => EquipmentSlot.Legs,
            _                   => EquipmentSlot.Head // fallback — Arm géré en amont
        };
    }
}
