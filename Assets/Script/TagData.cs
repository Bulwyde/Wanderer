using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Définit un tag — étiquette sémantique attachée à un objet du jeu.
/// Utilisé pour filtrer et conditionner des effets (ex : "si l'arme a le tag Épée").
///
/// Créer un asset par tag via : clic droit → Create → RPG/Tag Data
/// Ranger dans Assets/ScriptableObjects/Tags/ en sous-dossiers par catégorie.
/// </summary>
[CreateAssetMenu(fileName = "NewTag", menuName = "RPG/Tag Data")]
public class TagData : ScriptableObject
{
    // -----------------------------------------------
    // IDENTITÉ
    // -----------------------------------------------

    [Header("Identité")]
    // Nom affiché dans l'Inspector et dans l'UI (ex : "Épée", "Feu", "Humain")
    public string tagName;

    // -----------------------------------------------
    // CATÉGORIE
    // -----------------------------------------------

    [Header("Catégorie")]
    // À quels types d'objets ce tag peut-il être assigné ?
    // Flags : cocher plusieurs cases si le tag est partagé entre catégories.
    // Ex : "Feu" peut s'appliquer à la fois aux Ennemis et aux Équipements.
    public TagCategorie categorie;

    // -----------------------------------------------
    // INITIALISATION AUTOMATIQUE
    // -----------------------------------------------

#if UNITY_EDITOR
    /// <summary>
    /// À la création de l'asset, si tagName est vide,
    /// il est automatiquement rempli avec le nom du fichier.
    /// </summary>
    void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            string chemin = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(chemin))
            {
                tagName = System.IO.Path.GetFileNameWithoutExtension(chemin);
                EditorUtility.SetDirty(this);
            }
        }
    }
#endif

    // -----------------------------------------------
    // AFFICHAGE (optionnel)
    // -----------------------------------------------

    [Header("Affichage")]
    // Couleur d'accentuation — pour un éventuel affichage dans l'UI ou l'Inspector
    public Color couleur = Color.white;
}

/// <summary>
/// Catégories d'objets auxquelles un tag peut être rattaché.
/// [Flags] permet de cocher plusieurs catégories sur un même tag.
/// </summary>
[Flags]
public enum TagCategorie
{
    Equipement  = 1 << 0,   // EquipmentData
    Ennemi      = 1 << 1,   // EnemyData
    Evenement   = 1 << 2,   // EventData
    Consommable = 1 << 3,   // ConsumableData
    Module      = 1 << 4,   // ModuleData
    Skill       = 1 << 5,   // SkillData
    Hero        = 1 << 6,   // CharacterData
    Carte       = 1 << 7,   // MapData
}
