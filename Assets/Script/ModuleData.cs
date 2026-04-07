using UnityEngine;
using System.Collections.Generic;

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

    [Header("Effets")]
    // Effets déclenchés quand le module s'active — appliqués dans l'ordre de la liste.
    // Chaque EffectData porte son propre trigger : un module peut avoir des effets
    // déclenchés à des moments différents (ex : OnFightStart + OnPlayerTurnStart).
    public List<EffectData> effects = new List<EffectData>();

    [Header("Tags")]
    // Tags sémantiques pour les interactions et la gestion du loot
    // Ex : Tag_Offensif, Tag_Magie, Tag_Navigation — créer les assets dans Assets/ScriptableObjects/Tags/
    public List<TagData> tags = new List<TagData>();
}
