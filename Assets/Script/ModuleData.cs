using UnityEngine;

/// <summary>
/// Définit un module — effet flottant cumulable que le joueur
/// peut acquérir et perdre en cours de run.
/// Equivalent des reliques dans Slay the Spire.
/// </summary>
[CreateAssetMenu(fileName = "NewModule", menuName = "RPG/Module Data")]
public class ModuleData : ScriptableObject
{
    [Header("Identité")]
    // Identifiant unique utilisé par le code pour référencer ce module
    public string moduleID;

    // Nom affiché au joueur
    public string moduleName;

    // Icône affichée dans l'UI
    public Sprite icon;

    [Header("Description")]
    // Description avec balises de mots-clés
    // Ex : "Au début du tour, inflige 5 de {$weakness} à l'ennemi."
    [TextArea(2, 5)]
    public string description;

    // Mots-clés référencés dans la description
    public KeywordData[] keywords;

    [Header("Effet")]
    // L'effet unique de ce module
    public EffectData effect;

    [Header("Tags")]
    // Tags pour la gestion du loot et les interactions entre modules
    // Ex : "Offensif", "Magie", "Navigation", "SetAncienGuerrier"
    public string[] tags;

    [Header("Acquisition")]
    // Si true, ce module fait partie de l'équipement de base d'un personnage
    public bool isStartingModule;
}