using UnityEngine;

/// <summary>
/// Définit un mot-clé du jeu (ex : "Faiblesse", "Poison", "Bouclier").
/// Utilisé pour afficher des tooltips dans l'UI et identifier
/// les effets mécaniques associés dans le code.
/// </summary>
[CreateAssetMenu(fileName = "NewKeyword", menuName = "RPG/Keyword Data")]
public class KeywordData : ScriptableObject
{
    [Header("Identité")]
    // Identifiant unique utilisé par le code pour reconnaître ce mot-clé
    // Ex : "weakness", "poison", "shield"
    public string keywordID;

    // Nom affiché dans l'UI et les tooltips
    // Ex : "Faiblesse", "Poison", "Bouclier"
    public string keywordName;

    [Header("Description")]
    // Texte affiché dans le tooltip quand le joueur survole ce mot-clé
    // Ex : "Réduit les dégâts infligés de 20% pendant X tours."
    [TextArea(2, 5)]
    public string description;

    [Header("Couleur")]
    // Couleur du mot-clé dans les descriptions pour le rendre visible
    // Ex : rouge pour Faiblesse, vert pour Régénération...
    public Color highlightColor = Color.yellow;

    [Header("Références")]
    // Autres mots-clés mentionnés dans la description de celui-ci
    // Ex : "Faiblesse" pourrait mentionner "Armure" dans sa description
    // Utilisé pour construire la chaîne de tooltips imbriqués
    public KeywordData[] relatedKeywords;
}