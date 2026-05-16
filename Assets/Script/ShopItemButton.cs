using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Composant d'un bouton d'article dans la scène Marchand.
/// Affiche le nom de l'article, son prix, et son état (achetable / acheté / inaccessible).
///
/// Setup prefab recommandé :
///   ShopItemButton   (Button + ShopItemButton)
///     ├── ItemNameText  (TextMeshProUGUI — nom de l'article)
///     └── PriceText     (TextMeshProUGUI — prix ou statut)
/// </summary>
public class ShopItemButton : MonoBehaviour
{
    [Header("Références")]
    public TextMeshProUGUI itemNameText;
    public TextMeshProUGUI priceText;
    public Image itemIconImage;  // Affiche l'icone de l'article

    private Button     button;
    private Action     onAcheter;
    private List<TagData> _itemTags = new List<TagData>();

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    /// <summary>
    /// Initialise le bouton avec le nom, le prix, l'icone et le callback d'achat.
    /// Si achetable = false (déjà acheté ou fonds insuffisants), le bouton est grisé.
    /// labelPrix permet de surcharger l'affichage (ex : "Acheté" ou "Déjà possédé").
    /// icon affiche l'icone de l'article (optionnel).
    /// tags affiche les tags dans le tooltip (optionnel).
    /// description affiche la description dans le tooltip (optionnel).
    /// </summary>
    public void Setup(string nom, int prix, bool achetable, Action callback,
                      string labelPrix = null, Sprite icon = null, List<TagData> tags = null,
                      string description = "")
    {
        onAcheter = callback;
        _itemTags = tags ?? new List<TagData>();

        if (itemNameText != null)
            itemNameText.text = nom;

        if (priceText != null)
            priceText.text = labelPrix ?? $"{prix} credits";

        // Affiche l'icone si fourni
        if (itemIconImage != null && icon != null)
            itemIconImage.sprite = icon;

        SetInteractable(achetable);

        // Configure le tooltip — ajout dynamique du TooltipTrigger si absent du prefab (piège 34)
        TooltipTrigger trigger = GetComponent<TooltipTrigger>();
        if (trigger == null)
            trigger = gameObject.AddComponent<TooltipTrigger>();
        trigger.SetTooltipData(nom, description, _itemTags);
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    private void OnClicked()
    {
        onAcheter?.Invoke();
    }
}
