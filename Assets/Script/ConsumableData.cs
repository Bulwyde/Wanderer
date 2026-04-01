using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Définit un consommable — objet à usage unique utilisable en combat ou sur la carte.
/// Équivalent des potions dans Slay the Spire.
///
/// Le joueur peut porter jusqu'à maxConsumableSlots consommables (défini dans RunManager).
/// La liste des consommables actifs est stockée dans RunManager, jamais ici.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumable", menuName = "RPG/Consumable Data")]
public class ConsumableData : ScriptableObject
{
    // -----------------------------------------------
    // IDENTITÉ
    // -----------------------------------------------

    [Header("Identité")]
    // Identifiant unique — utilisé en interne pour référencer ce consommable
    public string consumableID;

    // Nom affiché au joueur (ex : "Potion de soin", "Fiole de poison")
    public string consumableName;

    // Icône affichée sur le bouton
    public Sprite icon;

    // Description courte affichée en tooltip
    [TextArea(2, 4)]
    public string description;

    // -----------------------------------------------
    // EFFET
    // -----------------------------------------------

    [Header("Effets")]
    // Effets appliqués quand le consommable est utilisé — dans l'ordre de la liste.
    // Utilise le même système qu'une compétence (EffectData).
    // Attention : certains effets n'ont de sens qu'en combat (DealDamage)
    // — s'assurer que usableInCombat / usableOnMap sont correctement configurés.
    public List<EffectData> effects = new List<EffectData>();

    // -----------------------------------------------
    // CONTEXTE D'UTILISATION
    // -----------------------------------------------

    [Header("Utilisation")]
    // Peut être utilisé pendant un combat (depuis l'interface de combat)
    public bool usableInCombat = true;

    // Peut être utilisé sur la carte (hors combat, depuis l'interface de navigation)
    public bool usableOnMap = true;

    // -----------------------------------------------
    // EFFETS DE NAVIGATION
    // -----------------------------------------------

    [Header("Effets sur la carte")]
    // Effets déclenchés quand le consommable est utilisé depuis la carte (hors combat).
    // Ignorés si usableOnMap = false ou si la liste est vide.
    // Plusieurs effets peuvent être combinés (ex : téléporter ET révéler une zone).
    public List<NavEffect> mapEffects = new List<NavEffect>();
}
