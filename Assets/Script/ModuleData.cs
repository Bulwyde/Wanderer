using UnityEngine;

/// <summary>
/// Définit un module — effet passif ou déclenché que le joueur
/// peut acquérir et conserver en cours de run.
/// Équivalent des reliques dans Slay the Spire.
///
/// Le QUAND et le SUR QUI sont définis directement sur l'EffectData (trigger + target),
/// pas ici — un seul endroit pour tout ce qui concerne l'effet.
/// </summary>
[CreateAssetMenu(fileName = "NewModule", menuName = "RPG/Module Data")]
public class ModuleData : ScriptableObject
{
    [Header("Identité")]
    // Identifiant unique utilisé par le code pour référencer ce module
    public string moduleID;

    // Nom affiché au joueur
    public string moduleName;

    // Icône affichée dans l'UI (HUD modules)
    public Sprite icon;

    [Header("Description")]
    // Description lisible par le joueur
    // Ex : "Au début du tour, inflige 3 dégâts à l'ennemi."
    [TextArea(2, 5)]
    public string description;

    // Mots-clés référencés dans la description
    public KeywordData[] keywords;

    [Header("Effet")]
    // Trigger, action, valeur et cible sont tous définis dans l'EffectData
    public EffectData effect;

    [Header("Tags")]
    // Tags pour la gestion du loot et les interactions entre modules
    // Ex : "Offensif", "Magie", "Navigation", "SetAncienGuerrier"
    public string[] tags;
}
