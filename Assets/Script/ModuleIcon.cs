using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Composant d'une icône de module dans le HUD.
/// Affiche l'icône du module et conserve ses données pour un futur tooltip.
///
/// Structure recommandée pour le prefab ModuleIcon :
///   ModuleIcon  (48×48 px)
///   ├── Image          ← composant sur la racine — assigner iconImage
///   └── (TooltipText)  ← TextMeshPro optionnel, masqué par défaut, pour tooltip au survol
/// </summary>
public class ModuleIcon : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES
    // -----------------------------------------------

    [Header("Références")]
    // Image qui affiche l'icône du module — assigner dans l'Inspector ou sur la racine du prefab
    public Image iconImage;

    // -----------------------------------------------
    // DONNÉES
    // -----------------------------------------------

    // Données du module affiché — conservé pour le tooltip (futur)
    private ModuleData moduleData;

    // -----------------------------------------------
    // INITIALISATION
    // -----------------------------------------------

    /// <summary>
    /// Initialise l'icône avec les données du module.
    /// Appelé par ModuleHUDManager lors de l'instanciation du prefab.
    /// </summary>
    public void Setup(ModuleData module)
    {
        moduleData = module;

        if (iconImage == null)
        {
            // Tentative de récupération automatique si non assigné dans l'Inspector
            iconImage = GetComponent<Image>();
        }

        if (iconImage != null)
        {
            if (module.icon != null)
                iconImage.sprite = module.icon;
            else
                iconImage.color = new Color(0.55f, 0.55f, 0.55f); // Gris si pas d'icône définie
        }

        gameObject.name = $"ModuleIcon_{module.moduleID}";
    }

    // -----------------------------------------------
    // ACCESSEUR (pour futur tooltip)
    // -----------------------------------------------

    /// <summary>Retourne les données du module associé à cette icône.</summary>
    public ModuleData GetModuleData() => moduleData;
}
