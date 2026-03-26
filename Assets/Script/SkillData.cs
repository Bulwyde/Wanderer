using UnityEngine;

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

    [Header("Effet")]
    // L'effet déclenché quand la compétence est utilisée
    public EffectData effect;

    [Header("Ciblage")]
    // Comment la compétence sélectionne sa cible
    public SkillTargetType targetType;

    [Header("Tags")]
    // Tags internes pour la gestion et les interactions
    // Ex : "Physique", "Magie", "Soin", "Zone"
    public string[] tags;
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