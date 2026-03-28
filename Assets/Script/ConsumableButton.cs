using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Bouton représentant un consommable dans l'UI (combat ou navigation).
/// Affiche uniquement une icône — le nom et la description seront affichés via un tooltip au survol.
///
/// Setup() est appelé une fois pour configurer le bouton.
/// Quand le joueur clique, le callback onUse est invoqué avec le consommable associé.
/// </summary>
public class ConsumableButton : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES UI
    // -----------------------------------------------

    [Header("UI")]
    // Icône du consommable — seul élément visuel affiché (tooltip prévu pour le nom/description)
    public Image iconImage;

    // -----------------------------------------------
    // ÉTAT INTERNE
    // -----------------------------------------------

    private ConsumableData consumable;
    private Action<ConsumableData> onUse;
    private Button button;

    // Accesseur public — permet au manager de retrouver le consommable associé
    public ConsumableData Consumable => consumable;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Configure le bouton avec un consommable et un callback d'utilisation.
    /// À appeler une fois après l'instanciation du prefab.
    /// </summary>
    public void Setup(ConsumableData data, Action<ConsumableData> callback)
    {
        consumable = data;
        onUse      = callback;
        button     = GetComponent<Button>();

        if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;

        if (button != null)
            button.onClick.AddListener(() => onUse?.Invoke(consumable));
    }

    /// <summary>
    /// Active ou désactive l'interaction avec ce bouton.
    /// Appelé pour bloquer l'utilisation pendant le tour ennemi, par exemple.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;
    }
}
