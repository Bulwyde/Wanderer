using UnityEditor;

/// <summary>
/// Éditeur personnalisé pour CharacterData.
/// Filtre la liste de tags pour ne proposer que les tags de catégorie Hero
/// (ou "Everything" — tags avec toutes les catégories cochées).
/// </summary>
[CustomEditor(typeof(CharacterData))]
public class CharacterDataEditor : Editor
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
            TagCategorie.Hero);

        serializedObject.ApplyModifiedProperties();
    }
}
