using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

/// <summary>
/// Déclenche le tooltip au SURVOL d'un élément UI.
/// Attaché à chaque bouton qui doit afficher un tooltip.
/// L'entrée du curseur ouvre le tooltip ; la sortie délègue à
/// TooltipController.OnHoverElementExit() — qui décide de masquer ou non
/// selon que la souris se trouve encore sur le tooltip.
/// </summary>
public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Données affichées par le tooltip — toujours écrasées au runtime par
    // SetTooltipData(). Non exposées dans l'Inspector (inutile).
    private string itemName;
    private string itemDescription;
    private List<TagData> itemTags = new List<TagData>();

    public void SetTooltipData(string name, string description, List<TagData> tags)
    {
        itemName = name;
        itemDescription = description;
        itemTags = tags ?? new List<TagData>();
    }

    // -----------------------------------------------
    // GESTION DU SURVOL
    // -----------------------------------------------

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (TooltipController.Instance != null)
            TooltipController.Instance.ShowTooltip(
                GetComponent<RectTransform>(), itemName, itemDescription, itemTags);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (TooltipController.Instance != null)
            TooltipController.Instance.OnHoverElementExit();
    }
}
