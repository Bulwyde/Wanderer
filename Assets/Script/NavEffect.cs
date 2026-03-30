using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Types d'effets déclenchables depuis la carte de navigation.
/// Utilisés par les consommables (usableOnMap), les skills de jambes,
/// les effets d'événements (TriggerNavEffect) et les modules.
///
/// Correspondances type → champs de NavEffect utilisés :
///   TeleportRandom      → allowedCellTypes (si vide : toutes les cases navigables)
///   RevealZoneRandom    → value (rayon, ex : 1 = zone 3×3)
///   RevealZoneChoice    → value (rayon) — attend un clic du joueur sur la carte
///   IncreaseVisionRange → value (delta de portée, permanent pour le run)
///   IncrementCounter    → counterKey + value (delta du compteur)
/// </summary>
public enum NavEffectType
{
    TeleportRandom,      // Téléporte le joueur sur une case aléatoire (filtrée par allowedCellTypes)
    RevealZoneRandom,    // Révèle une zone (2×value+1)×(2×value+1) autour d'une case aléatoire
    RevealZoneChoice,    // Révèle une zone (2×value+1)×(2×value+1) autour d'une case choisie par clic
    IncreaseVisionRange, // Augmente la portée de vision du joueur de `value` cases (permanent pour le run)
    IncrementCounter,    // Incrémente le compteur nommé `counterKey` de `value`
}

/// <summary>
/// Effet de navigation — déclenché depuis la carte (hors combat).
/// Partagé entre consommables (mapEffects), skills de jambes (navEffects)
/// et effets d'événements (TriggerNavEffect).
/// </summary>
[System.Serializable]
public class NavEffect
{
    [Tooltip("Type d'effet de navigation déclenché.")]
    public NavEffectType type;

    [Tooltip("Rayon de révélation (RevealZone*) : 1 = zone 3×3, 2 = zone 5×5...\n" +
             "Delta de portée de vision (IncreaseVisionRange).\n" +
             "Delta du compteur (IncrementCounter, peut être négatif).")]
    public int value = 1;

    [Tooltip("Clé du compteur nommé à incrémenter.\nEx : \"cles\", \"ferveur\", \"offrandes\".\nUtilisé par : IncrementCounter.")]
    public string counterKey;

    [Tooltip("Types de cases autorisés pour la téléportation.\nSi la liste est vide : toutes les cases navigables sont acceptées.\nUtilisé par : TeleportRandom.")]
    public List<CellType> allowedCellTypes = new List<CellType>();
}
