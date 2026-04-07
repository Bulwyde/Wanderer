using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Synchronise tagName ↔ nom de l'asset TagData dans les deux sens :
/// - TagDataEditor    : tagName modifié dans l'Inspector → renomme l'asset
/// - TagDataWatcher   : asset renommé dans le Project     → met à jour tagName
/// </summary>

// -----------------------------------------------
// WATCHER — Project window → tagName
// -----------------------------------------------

/// <summary>
/// Détecte les renommages d'assets TagData dans le Project window
/// et répercute le nouveau nom dans le champ tagName.
/// </summary>
public class TagDataWatcher : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        foreach (string chemin in movedAssets)
        {
            TagData tag = AssetDatabase.LoadAssetAtPath<TagData>(chemin);
            if (tag == null) continue;

            string nouveauNom = Path.GetFileNameWithoutExtension(chemin);
            if (tag.tagName == nouveauNom) continue;

            tag.tagName = nouveauNom;
            EditorUtility.SetDirty(tag);
        }

        // Sauvegarde groupée après tous les renommages traités
        AssetDatabase.SaveAssets();
    }
}

// -----------------------------------------------
// EDITOR CUSTOM — tagName → nom de l'asset
// -----------------------------------------------

/// <summary>
/// Éditeur custom pour TagData.
/// Renomme automatiquement l'asset quand le champ "Tag Name" est validé
/// (touche Entrée ou clic hors du champ).
/// </summary>
[CustomEditor(typeof(TagData))]
[CanEditMultipleObjects]
public class TagDataEditor : Editor
{
    // -----------------------------------------------
    // PROPRIÉTÉS SÉRIALISÉES
    // -----------------------------------------------

    SerializedProperty tagNameProp;
    SerializedProperty categorieProp;
    SerializedProperty couleurProp;

    void OnEnable()
    {
        tagNameProp  = serializedObject.FindProperty("tagName");
        categorieProp = serializedObject.FindProperty("categorie");
        couleurProp  = serializedObject.FindProperty("couleur");
    }

    // -----------------------------------------------
    // INSPECTOR
    // -----------------------------------------------

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // DelayedTextField : ne valide que sur Entrée ou perte de focus
        // → évite de renommer l'asset à chaque caractère saisi
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.DelayedTextField(tagNameProp, new GUIContent("Tag Name"));
        bool nomModifie = EditorGUI.EndChangeCheck();

        EditorGUILayout.PropertyField(categorieProp, new GUIContent("Catégorie"));
        EditorGUILayout.PropertyField(couleurProp,   new GUIContent("Couleur"));

        serializedObject.ApplyModifiedProperties();

        // Renommage de l'asset si le nom a changé et n'est pas vide
        if (nomModifie)
        {
            TagData tag = (TagData)target;
            string nouveauNom = tag.tagName;

            if (!string.IsNullOrWhiteSpace(nouveauNom))
            {
                string cheminAsset = AssetDatabase.GetAssetPath(target);
                AssetDatabase.RenameAsset(cheminAsset, nouveauNom);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
