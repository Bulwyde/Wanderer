using UnityEngine;
using UnityEditor;

/// <summary>
/// Éditeur custom pour CellAleaPool.
/// Affiche la liste des entrées avec type, poids et maxOccurrences en colonnes lisibles.
/// </summary>
[CustomEditor(typeof(CellAleaPool))]
public class CellAleaPoolEditor : Editor
{
    private SerializedProperty entreesProperty;

    private void OnEnable()
    {
        entreesProperty = serializedObject.FindProperty("entrees");
    }

    /// <summary>
    /// Dessine l'Inspector custom avec en-tête de colonnes et boutons d'ajout/suppression.
    /// </summary>
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Entrées de la pool", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // En-tête des colonnes
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Type",            GUILayout.Width(130));
        EditorGUILayout.LabelField("Poids",           GUILayout.Width(50));
        EditorGUILayout.LabelField("Max occurrences", GUILayout.Width(110));
        EditorGUILayout.LabelField("",                GUILayout.Width(24));
        EditorGUILayout.EndHorizontal();

        EditorGUI.DrawRect(
            EditorGUILayout.GetControlRect(false, 1),
            new Color(0.5f, 0.5f, 0.5f, 0.5f));

        EditorGUILayout.Space(2);

        // Lignes
        for (int i = 0; i < entreesProperty.arraySize; i++)
        {
            SerializedProperty entree         = entreesProperty.GetArrayElementAtIndex(i);
            SerializedProperty typeProp       = entree.FindPropertyRelative("type");
            SerializedProperty poidsProp      = entree.FindPropertyRelative("poids");
            SerializedProperty maxOccProp     = entree.FindPropertyRelative("maxOccurrences");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(typeProp,   GUIContent.none, GUILayout.Width(130));
            EditorGUILayout.PropertyField(poidsProp,  GUIContent.none, GUILayout.Width(50));
            EditorGUILayout.PropertyField(maxOccProp, GUIContent.none, GUILayout.Width(110));

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                entreesProperty.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ Ajouter une entrée"))
        {
            entreesProperty.arraySize++;
            SerializedProperty nouvelle = entreesProperty.GetArrayElementAtIndex(entreesProperty.arraySize - 1);
            nouvelle.FindPropertyRelative("poids").intValue = 1;
            nouvelle.FindPropertyRelative("maxOccurrences").intValue = 0;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
