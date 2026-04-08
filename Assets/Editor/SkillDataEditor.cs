using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Éditeur personnalisé pour SkillData.
/// Affiche navCooldownCount avec un label contextuel pour tous les types sauf None et MondeTermine.
/// Affiche navCooldownTag uniquement pour CombatEnnemisAvecTag,
/// filtré sur les tags de catégorie Ennemi.
/// </summary>
[CustomEditor(typeof(SkillData))]
public class SkillDataEditor : Editor
{
    // Cache des tags de catégorie Ennemi — chargé une fois, invalidé à la recompilation.
    private List<TagData> _tagsEnnemi;
    private string[]      _tagsEnnemyOptions; // options du popup (index 0 = "(aucun)")

    // -----------------------------------------------
    // DESSIN DE L'INSPECTOR
    // -----------------------------------------------

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Dessine toutes les propriétés dans leur ordre naturel,
        // sauf m_Script et les deux champs conditionnels de cooldown.
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.propertyPath == "m_Script")          continue;
            if (iterator.propertyPath == "tags")              continue;
            if (iterator.propertyPath == "navCooldownCount")  continue;
            if (iterator.propertyPath == "navCooldownTag")    continue;

            EditorGUILayout.PropertyField(iterator, true);
        }

        // Liste de tags filtrée sur la catégorie Skill
        TagListFilterUtil.DrawFilteredTagList(
            serializedObject.FindProperty("tags"),
            TagCategorie.Skill);

        // -----------------------------------------------
        // CHAMPS CONDITIONNELS (apparaissent juste après navCooldownType)
        // -----------------------------------------------

        SerializedProperty cooldownType  = serializedObject.FindProperty("navCooldownType");
        SerializedProperty cooldownCount = serializedObject.FindProperty("navCooldownCount");
        SerializedProperty cooldownTag   = serializedObject.FindProperty("navCooldownTag");

        NavCooldownType type = (NavCooldownType)cooldownType.enumValueIndex;

        // navCooldownCount — affiché pour tous les types sauf None et MondeTermine,
        // avec un label contextuel selon le type.
        switch (type)
        {
            case NavCooldownType.ShopDecouvert:
                EditorGUILayout.PropertyField(cooldownCount,
                    new GUIContent("Nombre de marchands",
                        "Nombre de nouveaux marchands à découvrir avant rechargement."));
                break;

            case NavCooldownType.CombatsTermines:
                EditorGUILayout.PropertyField(cooldownCount,
                    new GUIContent("Nombre de combats",
                        "Nombre de combats à remporter avant rechargement."));
                break;

            case NavCooldownType.EventsTermines:
                EditorGUILayout.PropertyField(cooldownCount,
                    new GUIContent("Nombre d'événements",
                        "Nombre d'événements à compléter avant rechargement."));
                break;

            case NavCooldownType.CombatEnnemisAvecTag:
                EditorGUILayout.PropertyField(cooldownCount,
                    new GUIContent("Nombre de combats",
                        "Nombre de combats contre un ennemi avec ce tag à remporter avant rechargement."));
                break;
        }

        // navCooldownTag — affiché uniquement pour CombatEnnemisAvecTag,
        // filtré sur les tags de catégorie Ennemi.
        if (type == NavCooldownType.CombatEnnemisAvecTag)
            DrawCooldownTagFiltre(cooldownTag);

        serializedObject.ApplyModifiedProperties();
    }

    // -----------------------------------------------
    // PICKER DE TAG FILTRÉ (catégorie Ennemi)
    // -----------------------------------------------

    /// <summary>
    /// Dessine un popup listant uniquement les TagData dont la catégorie inclut Ennemi.
    /// Remplace le champ ObjectField générique pour éviter d'assigner un tag non pertinent.
    /// </summary>
    private void DrawCooldownTagFiltre(SerializedProperty cooldownTag)
    {
        EnsureTagsCached();

        TagData tagActuel = cooldownTag.objectReferenceValue as TagData;

        if (_tagsEnnemi.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "Aucun tag de catégorie Ennemi trouvé.\n" +
                "Créez un TagData avec la catégorie Ennemi cochée.",
                MessageType.Warning);
            return;
        }

        // Index actuel dans le popup (0 = "(aucun)")
        int indexActuel = 0;
        if (tagActuel != null)
        {
            int idx = _tagsEnnemi.IndexOf(tagActuel);
            if (idx >= 0) indexActuel = idx + 1;
        }

        int nouvelIndex = EditorGUILayout.Popup(
            new GUIContent("Tag ennemi requis",
                "Le tag que doit porter l'ennemi vaincu pour déclencher le rechargement.\n" +
                "Seuls les tags de catégorie Ennemi sont listés."),
            indexActuel,
            _tagsEnnemyOptions);

        cooldownTag.objectReferenceValue = nouvelIndex == 0 ? null : _tagsEnnemi[nouvelIndex - 1];
    }

    /// <summary>
    /// Charge et met en cache tous les TagData dont la catégorie inclut Ennemi.
    /// Le cache est valide jusqu'à la prochaine recompilation (nouvelle instance d'Editor).
    /// </summary>
    private void EnsureTagsCached()
    {
        if (_tagsEnnemi != null) return;

        _tagsEnnemi = new List<TagData>();

        string[] guids = AssetDatabase.FindAssets("t:TagData");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TagData tag  = AssetDatabase.LoadAssetAtPath<TagData>(path);
            if (tag != null && (tag.categorie & TagCategorie.Ennemi) != 0)
                _tagsEnnemi.Add(tag);
        }

        _tagsEnnemi.Sort((a, b) =>
            string.Compare(a.tagName, b.tagName, System.StringComparison.OrdinalIgnoreCase));

        _tagsEnnemyOptions = new string[_tagsEnnemi.Count + 1];
        _tagsEnnemyOptions[0] = "(aucun)";
        for (int i = 0; i < _tagsEnnemi.Count; i++)
            _tagsEnnemyOptions[i + 1] = _tagsEnnemi[i].tagName;
    }
}
