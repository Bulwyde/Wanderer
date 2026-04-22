using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Définit une compétence active utilisable en combat.
/// Portée par les équipements (bras : 1 à 4 compétences,
/// autres emplacements : jusqu'à 3 compétences).
/// </summary>
[CreateAssetMenu(fileName = "NewSkill", menuName = "RPG/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Identité")]
    public string skillID;
    public string skillName;
    public Sprite icon;

    [Header("Description")]
    // Description avec balises de mots-clés
    // Ex : "Inflige 10 dégâts et applique {$weakness} pendant 2 tours."
    [TextArea(2, 5)]
    public string description;

    // Mots-clés référencés dans la description
    public KeywordData[] keywords;

    [Header("Coût")]
    // Coût en énergie pour utiliser cette compétence (0 = gratuit)
    public int energyCost;

    // Nombre de tours avant de pouvoir réutiliser cette compétence (0 = pas de cooldown)
    public int cooldown;

    [Header("Effets")]
    // Effets déclenchés quand la compétence est utilisée — appliqués dans l'ordre de la liste.
    // Permet de combiner plusieurs effets sur un même skill (ex : DealDamage + ApplyStatus).
    public List<EffectData> effects = new List<EffectData>();

    [Header("Ciblage")]
    // Comment la compétence sélectionne sa cible
    public SkillTargetType targetType;

    [Header("Tags")]
    // Tags sémantiques pour les interactions et les conditions d'effets
    // Ex : Tag_Physique, Tag_Magie, Tag_Soin — créer les assets dans Assets/ScriptableObjects/Tags/
    public List<TagData> tags = new List<TagData>();

    // Runtime uniquement — rempli quand le skill est équipé dans un slot, vidé au déséquipement.
    // Contient les tags hérités de l'équipement porteur (sans doublons avec "tags").
    [HideInInspector]
    public List<TagData> inheritedTags = new List<TagData>();

    // -----------------------------------------------
    // COMPÉTENCE DE NAVIGATION (jambes)
    // -----------------------------------------------

    [Header("Compétence de navigation")]
    // Si vrai, cette compétence s'utilise sur la carte de navigation (pas en combat).
    // Typiquement réservé aux équipements de jambes.
    // Les champs energyCost et cooldown sont ignorés hors combat.
    public bool isNavigationSkill = false;

    // Effets déclenchés quand la compétence est utilisée depuis la carte.
    // Ignorés si isNavigationSkill = false ou si la liste est vide.
    public List<NavEffect> navEffects = new List<NavEffect>();

    [Header("Cooldown de navigation")]
    // Condition de rechargement du skill (None = toujours disponible, pas de cooldown).
    // Ignoré si isNavigationSkill = false.
    public NavCooldownType navCooldownType = NavCooldownType.None;

    // Pour CombatsTermines / EventsTermines : nombre d'occurrences requises avant rechargement.
    // Ignoré pour les autres types de cooldown.
    public int navCooldownCount = 1;

    // Pour EnnemisAvecTag : le tag que doit porter l'ennemi tué pour déclencher le rechargement.
    // Ignoré pour les autres types de cooldown.
    public TagData navCooldownTag;
}

/// <summary>
/// Définit comment la compétence sélectionne sa cible.
/// </summary>
public enum SkillTargetType
{
    SingleEnemy,        // Vise un ennemi au choix
    AllEnemies,         // Affecte tous les ennemis automatiquement
    RandomEnemy,        // Cible un ennemi aléatoire
    Self,               // Se cible soi-même (soin, buff...)
    AllCharacters,      // Cible tous les personnages
}

/// <summary>
/// Définit la condition de rechargement d'une compétence de navigation hors combat.
/// </summary>
public enum NavCooldownType
{
    None,                 // Toujours disponible — pas de cooldown
    ShopDecouvert,        // Se recharge après X nouvelles visites de marchands (X = navCooldownCount)
    CombatsTermines,      // Se recharge après X combats gagnés (X = navCooldownCount)
    EventsTermines,       // Se recharge après X événements complétés (X = navCooldownCount)
    MondeTermine,         // Se recharge après la victoire contre le boss
    CombatEnnemisAvecTag, // Se recharge après X combats gagnés contre un ennemi portant navCooldownTag (X = navCooldownCount)
}