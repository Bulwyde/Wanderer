using UnityEngine;
using TMPro;

/// <summary>
/// Affiche les informations persistantes du joueur sur la carte de navigation :
/// HP courants / HP max, crédits, et tout autre indicateur pertinent hors combat.
///
/// Hiérarchie recommandée (enfant DIRECT du Canvas, pas de mapContainer) :
///   Canvas
///   ├── mapContainer        ← la carte (se déplace avec le drag)
///   └── NavigationHUD       ← ce GameObject, ancré en haut-droite par exemple
///       ├── HPText          ← TextMeshPro  ex : "HP : 85 / 100"
///       ├── CreditsText     ← TextMeshPro  ex : "Credits : 120"
///       └── ModuleHUD       ← le ModuleHUDManager peut être ici aussi
///
/// Assigner dans l'Inspector :
///   - hpText      : le TextMeshPro qui affiche les HP
///   - creditsText : le TextMeshPro qui affiche les crédits (optionnel)
/// </summary>
public class NavigationHUD : MonoBehaviour
{
    [Header("Références UI")]
    public TextMeshProUGUI hpText;

    // Optionnel — laissez vide si vous ne voulez pas afficher les crédits ici.
    public TextMeshProUGUI creditsText;

    void Update()
    {
        RefreshHP();
        RefreshCredits();
    }

    // -----------------------------------------------
    // AFFICHAGE
    // -----------------------------------------------

    private void RefreshHP()
    {
        if (hpText == null || RunManager.Instance == null) return;

        int current = RunManager.Instance.currentHP;
        int max     = RunManager.Instance.maxHP;

        hpText.text = $"HP : {current} / {max}";
    }

    private void RefreshCredits()
    {
        if (creditsText == null || RunManager.Instance == null) return;
        creditsText.text = $"Credits : {RunManager.Instance.credits}";
    }
}
