using UnityEditor;

/// <summary>
/// Éditeur personnalisé pour MapData.
/// Filtre la liste de tags pour ne proposer que les tags de catégorie Carte
/// (ou "Everything" — tags avec toutes les catégories cochées).
///
/// Note : MapEditorWindow est une EditorWindow séparée — aucun conflit.
/// </summary>
[CustomEditor(typeof(MapData))]
public class MapDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.propertyPath == "m_Script") continue;
            if (iterator.propertyPath == "tags")     continue;
            EditorGUILayout.PropertyField(iterator, true);
        }

        TagListFilterUtil.DrawFilteredTagList(
            serializedObject.FindProperty("tags"),
            TagCategorie.Carte);

        serializedObject.ApplyModifiedProperties();
    }
}
