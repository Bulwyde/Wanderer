using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

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

    private Button     button;
    private Action     onAcheter;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    /// <summary>
    /// Initialise le bouton avec le nom, le prix et le callback d'achat.
    /// Si achetable = false (déjà acheté ou fonds insuffisants), le bouton est grisé.
    /// labelPrix permet de surcharger l'affichage (ex : "Acheté" ou "Déjà possédé").
    /// </summary>
    public void Setup(string nom, int prix, bool achetable, Action callback, string labelPrix = null)
    {
        onAcheter = callback;

        if (itemNameText != null)
            itemNameText.text = nom;

        if (priceText != null)
            priceText.text = labelPrix ?? $"{prix} credits";

        SetInteractable(achetable);
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
