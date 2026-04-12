using UnityEditor;

/// <summary>
/// Éditeur personnalisé pour EnemyData.
/// - Filtre la liste de tags pour ne proposer que les tags de catégorie Ennemi.
/// - Ordre d'affichage forcé : Identité → Stats → Tags → Actions → Effets spéciaux → Loot.
/// - Toutes les propriétés sont dessinées par FindProperty explicite pour garantir l'ordre,
///   indépendamment de leur déclaration dans le script.
/// </summary>
[CustomEditor(typeof(EnemyData))]
public class EnemyDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // --- Identité ---
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyID"),   true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("enemyName"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portrait"),  true);

        // --- Stats ---
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHP"),   true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attack"),  true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("defense"), true);

        // --- Tags (filtré par catégorie Ennemi) ---
        TagListFilterUtil.DrawFilteredTagList(
            serializedObject.FindProperty("tags"),
            TagCategorie.Ennemi);

        // --- Actions ---
        EditorGUILayout.PropertyField(serializedObject.FindProperty("actions"), true);

        // --- Effets spéciaux ---
        // PropertyField délègue automatiquement le rendu à EffectDataEditor (CustomPropertyDrawer).
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spawnEffects"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("deathEffects"), true);

        // --- Loot ---
        EditorGUILayout.PropertyField(serializedObject.FindProperty("creditsLoot"),        true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lootPool"),           true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lootOfferCount"),     true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("consumableLootPool"), true);

        serializedObject.ApplyModifiedProperties();
    }
}
