using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Affiche la rangée d'icônes des modules actifs dans le HUD.
/// À placer dans chaque scène où l'on veut voir les modules (Combat, Navigation).
///
/// Hiérarchie recommandée dans le Canvas :
///   Canvas
///   └── ModuleHUD (RectTransform ancré en haut, ex : haut-gauche)
///       └── ModuleIconContainer  ← assigner dans l'Inspector
///               Composants : HorizontalLayoutGroup
///                            spacing 6px, padding 4px
///                            ChildControlSize Width+Height : true
///                            ChildForceExpand Width+Height : false
///
/// Assigner dans l'Inspector :
///   - moduleIconContainer : le Transform du container ci-dessus
///   - moduleIconPrefab    : le prefab ModuleIcon (48×48 px, Image + composant ModuleIcon)
/// </summary>
public class ModuleHUDManager : MonoBehaviour
{
    // -----------------------------------------------
    // RÉFÉRENCES
    // -----------------------------------------------

    [Header("UI")]
    // Transform parent où les icônes sont instanciées
    public Transform  moduleIconContainer;

    // Préfab d'une icône — doit avoir le composant ModuleIcon
    public GameObject moduleIconPrefab;

    // Icônes actuellement affichées dans le HUD
    private List<ModuleIcon> spawnedIcons = new List<ModuleIcon>();

    // -----------------------------------------------
    // CYCLE DE VIE
    // -----------------------------------------------

    void OnEnable()
    {
        // S'abonne aux changements de modules pour garder l'affichage à jour
        ModuleManager.OnModulesChanged += RefreshModuleIcons;
    }

    void OnDisable()
    {
        ModuleManager.OnModulesChanged -= RefreshModuleIcons;
    }

    void Start()
    {
        // Affiche les modules déjà présents au chargement de la scène
        RefreshModuleIcons();
    }

    // -----------------------------------------------
    // AFFICHAGE
    // -----------------------------------------------

    /// <summary>
    /// Recrée toutes les icônes depuis la liste des modules actifs du RunManager.
    /// Appelée automatiquement à l'initialisation et à chaque changement de modules.
    /// </summary>
    public void RefreshModuleIcons()
    {
        if (moduleIconPrefab == null || moduleIconContainer == null) return;

        // Détruit les anciennes icônes proprement
        foreach (ModuleIcon icon in spawnedIcons)
        {
            if (icon != null)
                Destroy(icon.gameObject);
        }
        spawnedIcons.Clear();

        if (RunManager.Instance == null) return;

        // Recrée une icône par module actif
        foreach (ModuleData module in RunManager.Instance.GetModules())
        {
            if (module == null) continue;

            GameObject go   = Instantiate(moduleIconPrefab, moduleIconContainer);
            ModuleIcon icon = go.GetComponent<ModuleIcon>();

            if (icon != null)
            {
                icon.Setup(module);
                spawnedIcons.Add(icon);
            }
        }

        Debug.Log($"[ModuleHUD] {spawnedIcons.Count} icône(s) de module affichée(s).");
    }
}
