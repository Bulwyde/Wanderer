using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// Bouton représentant un consommable dans l'UI (combat, navigation ou event).
/// Affiche uniquement une icône — le nom et la description seront affichés via un tooltip au survol.
///
/// SetInteractable(false) grise le bouton ET le réduit d'un tiers visuellement,
/// pour indiquer que le consommable n'est pas utilisable dans le contexte actuel.
///
/// À terme : remplacer le clic direct par un popup "Utiliser / Jeter",
/// où seule l'option "Utiliser" sera désactivée selon le contexte.
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
    private RectTransform rectTransform;

    // Taille d'origine lue depuis le prefab — sert de référence pour le redimensionnement
    private Vector2 tailleNormale;

    // Accesseur public — permet au manager de retrouver le consommable associé
    public ConsumableData Consumable => consumable;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    void Awake()
    {
        button        = GetComponent<Button>();
        rectTransform = GetComponent<RectTransform>();

        // Mémorise la taille définie dans le prefab
        tailleNormale = rectTransform.sizeDelta;

        // Fallback si la taille n'est pas définie (cas layout group ChildControlSize)
        if (tailleNormale == Vector2.zero)
            tailleNormale = new Vector2(50f, 50f);
    }

    /// <summary>
    /// Configure le bouton avec un consommable et un callback d'utilisation.
    /// À appeler une fois après l'instanciation du prefab.
    /// </summary>
    public void Setup(ConsumableData data, Action<ConsumableData> callback)
    {
        consumable = data;
        onUse      = callback;

        if (iconImage != null && data.icon != null) iconImage.sprite = data.icon;

        if (button != null)
            button.onClick.AddListener(() => onUse?.Invoke(consumable));
    }

    /// <summary>
    /// Active ou désactive l'interaction avec ce bouton.
    /// Non interactable = bouton grisé (Unity Button) + taille réduite d'un tiers.
    ///
    /// À terme : quand le popup "Utiliser / Jeter" sera implémenté,
    /// ce sera l'option "Utiliser" qui sera grisée, pas le bouton entier.
    /// </summary>
    public void SetInteractable(bool interactable)
    {
        if (button != null) button.interactable = interactable;

        // Taille : normale si utilisable, réduite d'un tiers si non utilisable
        Vector2 taille = interactable ? tailleNormale : tailleNormale * (2f / 3f);

        // sizeDelta pour les cas hors layout group
        rectTransform.sizeDelta = taille;

        // LayoutElement pour les conteneurs avec Layout Group (preferredSize prime sur sizeDelta)
        LayoutElement le = GetComponent<LayoutElement>();
        if (le == null && !interactable) le = gameObject.AddComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth  = interactable ? -1 : taille.x;
            le.preferredHeight = interactable ? -1 : taille.y;
        }
    }
}
