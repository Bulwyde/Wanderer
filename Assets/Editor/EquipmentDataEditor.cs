using UnityEditor;

/// <summary>
/// Éditeur personnalisé pour EquipmentData.
/// Filtre la liste de tags pour ne proposer que les tags de catégorie Equipement
/// (ou "Everything" — tags avec toutes les catégories cochées).
/// </summary>
[CustomEditor(typeof(EquipmentData))]
public class EquipmentDataEditor : Editor
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

            if (iterator.propertyPath == "passiveEffects")
                EditorGUILayout.HelpBox("ZONE 1 - Passifs globaux : effets declenches par des evenements de combat (trigger). Independants du skill source.", MessageType.None);

            if (iterator.propertyPath == "skillModifiers")
                EditorGUILayout.HelpBox("ZONE 2 - Modificateurs de skill : s'appliquent aux skills equipes sur CET objet a l'execution. conditionTag pour filtrer par tag.", MessageType.None);

            EditorGUILayout.PropertyField(iterator, true);
        }

        TagListFilterUtil.DrawFilteredTagList(
            serializedObject.FindProperty("tags"),
            TagCategorie.Equipement);

        serializedObject.ApplyModifiedProperties();
    }
}
