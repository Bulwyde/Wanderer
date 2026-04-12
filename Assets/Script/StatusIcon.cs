using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Composant d'une icône de statut dans le HUD de combat.
/// Affiche le sprite du statut et le nombre de stacks actifs.
///
/// Structure recommandée pour le prefab StatusIconPrefab :
///   StatusIconPrefab  (ex : 32×32 px)
///   ├── IconImage   (Image)            ← assigner iconImage  — remplit tout le prefab
///   └── StackText   (TextMeshProUGUI)  ← assigner stackText  — ancré en bas à droite
///
/// Le container parent doit avoir un GridLayoutGroup :
///   Constraint      = Fixed Column Count
///   Constraint Count = défini au runtime par CombatManager.statusIconsPerRow
///   Cell Size       = taille du prefab (ex : 32×32)
///   Spacing         = espacement souhaité
/// </summary>
public class StatusIcon : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES
    // -----------------------------------------------

    [Header("Références")]
    // Image du statut — couvre l'intégralité du prefab
    public Image           iconImage;

    // Texte du nombre de stacks — affiché en superposition (ex : bas-droite)
    public TextMeshProUGUI stackText;

    // -----------------------------------------------
    // DONNÉES
    // -----------------------------------------------

    // Statut affiché — conservé pour un futur tooltip
    private StatusData statusData;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Initialise l'icône avec le statut et le nombre de stacks initial.
    /// Appelé par CombatManager lors de la création de l'icône.
    /// </summary>
    public void Setup(StatusData status, int stacks)
    {
        statusData = status;

        if (iconImage != null)
        {
            if (status.icon != null)
            {
                iconImage.sprite = status.icon;
                iconImage.color  = Color.white;
            }
            else
            {
                // Pas d'icône définie : carré gris en attendant
                iconImage.sprite = null;
                iconImage.color  = new Color(0.55f, 0.55f, 0.55f);
            }
        }

        UpdateStacks(stacks);
        gameObject.name = $"StatusIcon_{status.statusID}";
    }

    // -----------------------------------------------
    // MISE À JOUR
    // -----------------------------------------------

    /// <summary>
    /// Met à jour l'affichage du nombre de stacks sans recréer l'icône.
    /// </summary>
    public void UpdateStacks(int stacks)
    {
        if (stackText != null)
            stackText.text = stacks.ToString();
    }

    // -----------------------------------------------
    // ACCESSEUR (pour futur tooltip)
    // -----------------------------------------------

    /// <summary>Retourne le statut associé à cette icône.</summary>
    public StatusData GetStatusData() => statusData;
}
