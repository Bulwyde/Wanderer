using UnityEditor;

/// <summary>
/// Éditeur personnalisé pour EnemyData.
/// Filtre la liste de tags pour ne proposer que les tags de catégorie Ennemi
/// (ou "Everything" — tags avec toutes les catégories cochées).
/// </summary>
[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
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
            TagCategorie.Ennemi);

        serializedObject.ApplyModifiedProperties();
    }
}
