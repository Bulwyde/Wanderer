using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Utilitaire partagé entre les editors des Data classes.
/// Dessine une List<TagData> sous forme de popup filtré sur une catégorie donnée.
///
/// Seuls les tags dont la catégorie inclut <filterCategorie> sont proposés.
/// Un tag avec toutes les catégories cochées (= "Everything") apparaît dans tous les editors.
///
/// Usage :
///   TagListFilterUtil.DrawFilteredTagList(
///       serializedObject.FindProperty("tags"),
///       TagCategorie.Ennemi);
/// </summary>
public static class TagListFilterUtil
{
    // -----------------------------------------------
    // CACHE PAR CATÉGORIE
    // -----------------------------------------------

    // Le cache est statique — valide jusqu'à la prochaine recompilation.
    // Invalidé automatiquement car les classes Editor sont réinstanciées à chaque compile.
    private static readonly Dictionary<TagCategorie, List<TagData>> _tagCache
        = new Dictionary<TagCategorie, List<TagData>>();

    private static readonly Dictionary<TagCategorie, string[]> _optionsCache
        = new Dictionary<TagCategorie, string[]>();

    // -----------------------------------------------
    // MÉTHODE PRINCIPALE
    // -----------------------------------------------

    /// <summary>
    /// Dessine la liste de tags avec un popup filtré sur <filterCategorie>.
    /// Remplace le dessin par défaut de Unity (ObjectField non filtré).
    /// </summary>
    public static void DrawFilteredTagList(SerializedProperty listProp, TagCategorie filterCategorie)
    {
        EnsureCached(filterCategorie);
        List<TagData> tags    = _tagCache[filterCategorie];
        string[]      options = _optionsCache[filterCategorie];

        // Entête pliable — reproduit l'apparence standard d'une List dans Unity
        listProp.isExpanded = EditorGUILayout.Foldout(
            listProp.isExpanded,
            $"Tags ({listProp.arraySize})",
            true);

        if (!listProp.isExpanded) return;

        EditorGUI.indentLevel++;

        // Dessin de chaque élément de la liste
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty element = listProp.GetArrayElementAtIndex(i);
            TagData currentTag = element.objectReferenceValue as TagData;

            // Trouve l'index courant dans les options filtrées (0 = "(aucun)")
            int currentIndex = 0;
            if (currentTag != null)
            {
                int idx = tags.IndexOf(currentTag);
                currentIndex = idx >= 0 ? idx + 1 : 0;
            }

            EditorGUILayout.BeginHorizontal();

            int newIndex = EditorGUILayout.Popup(currentIndex, options);
            element.objectReferenceValue = newIndex == 0 ? null : tags[newIndex - 1];

            // Bouton de suppression de l'élément
            if (GUILayout.Button("-", GUILayout.Width(22)))
            {
                // Si l'élément pointe sur un objet, il faut appeler DeleteArrayElementAtIndex
                // deux fois : d'abord pour mettre à null, ensuite pour retirer l'entrée.
                if (element.objectReferenceValue != null)
                    listProp.DeleteArrayElementAtIndex(i);
                listProp.DeleteArrayElementAtIndex(i);
                break; // évite les accès hors limites après suppression
            }

            EditorGUILayout.EndHorizontal();
        }

        // Bouton d'ajout d'un tag
        if (GUILayout.Button("+ Ajouter un tag"))
        {
            listProp.InsertArrayElementAtIndex(listProp.arraySize);
            // Initialise le nouvel élément à null
            listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = null;
        }

        EditorGUI.indentLevel--;
    }

    // -----------------------------------------------
    // CHARGEMENT ET MISE EN CACHE
    // -----------------------------------------------

    private static void EnsureCached(TagCategorie filterCategorie)
    {
        if (_tagCache.ContainsKey(filterCategorie)) return;

        List<TagData> filtered = new List<TagData>();

        string[] guids = AssetDatabase.FindAssets("t:TagData");
        foreach (string guid in guids)
        {
            string  path = AssetDatabase.GUIDToAssetPath(guid);
            TagData tag  = AssetDatabase.LoadAssetAtPath<TagData>(path);
            if (tag == null) continue;

            // Inclut le tag si sa catégorie croise filterCategorie.
            // Un tag avec toutes les catégories cochées (Everything = tous les bits)
            // croise n'importe quelle catégorie — il apparaît dans tous les editors.
            if ((tag.categorie & filterCategorie) != 0)
                filtered.Add(tag);
        }

        filtered.Sort((a, b) =>
            string.Compare(a.tagName, b.tagName, System.StringComparison.OrdinalIgnoreCase));

        string[] options = new string[filtered.Count + 1];
        options[0] = "(aucun)";
        for (int i = 0; i < filtered.Count; i++)
            options[i + 1] = filtered[i].tagName;

        _tagCache[filterCategorie]   = filtered;
        _optionsCache[filterCategorie] = options;
    }
}
