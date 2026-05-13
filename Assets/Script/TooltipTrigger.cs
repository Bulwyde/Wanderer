using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Déclenche le tooltip au CLIC DROIT sur un élément UI.
/// Attaché à chaque bouton qui doit afficher un tooltip.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private string itemName;
    [SerializeField] private string itemDescription;
    [SerializeField] private List<TagData> itemTags = new List<TagData>();

    public void SetTooltipData(string name, string description, List<TagData> tags)
    {
        itemName = name;
        itemDescription = description;
        itemTags = tags ?? new List<TagData>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Vérifie que c'est un clic droit (bouton 1 = clic droit)
        if (eventData.button != PointerEventData.InputButton.Right)
            return;

        if (TooltipController.Instance != null)
            TooltipController.Instance.ShowTooltip(GetComponent<RectTransform>(), itemName, itemDescription, itemTags);
    }
}
