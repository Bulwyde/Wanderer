using UnityEditor;
using UnityEngine;

/// <summary>
/// Éditeur personnalisé pour StatusData.
/// Affiche les champs dans un ordre logique et masque les sections non pertinentes
/// selon le behavior et le decayTiming sélectionnés.
/// </summary>
[CustomEditor(typeof(StatusData))]
public class StatusDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // -----------------------------------------------
        // IDENTITÉ
        // -----------------------------------------------

        EditorGUILayout.LabelField("Identité", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("statusID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("statusName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));

        EditorGUILayout.Space();

        // -----------------------------------------------
        // COMPORTEMENT
        // -----------------------------------------------

        EditorGUILayout.LabelField("Comportement", EditorStyles.boldLabel);

        SerializedProperty behaviorProp = serializedObject.FindProperty("behavior");
        EditorGUILayout.PropertyField(behaviorProp);

        StatusBehavior behavior = (StatusBehavior)behaviorProp.enumValueIndex;

        if (behavior == StatusBehavior.PerTurnStart)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("perTurnAction"),
                new GUIContent("Action par tour"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("effectPerStack"),
                new GUIContent("Valeur par stack"));
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space();

        // -----------------------------------------------
        // DURÉE & DÉCROISSANCE
        // -----------------------------------------------

        EditorGUILayout.LabelField("Durée & Décroissance", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("decayPerTurn"));

        SerializedProperty decayTimingProp = serializedObject.FindProperty("decayTiming");
        EditorGUILayout.PropertyField(decayTimingProp);

        StatusDecayTiming decayTiming = (StatusDecayTiming)decayTimingProp.enumValueIndex;

        if (decayTiming == StatusDecayTiming.OnSkillUse)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("decayConditionTag"),
                new GUIContent("Tag requis sur le skill"));
            EditorGUILayout.HelpBox("Null = décroît sur tout skill utilisé.", MessageType.None);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStacks"));

        EditorGUILayout.Space();

        // -----------------------------------------------
        // MODIFICATION DE STAT (ModifyStat uniquement)
        // -----------------------------------------------

        EditorGUILayout.LabelField("Modification de stat", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("behavior = ModifyStat uniquement", MessageType.None);

        if (behavior == StatusBehavior.ModifyStat)
        {
            SerializedProperty statProp = serializedObject.FindProperty("statToModify");
            EditorGUILayout.PropertyField(statProp);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("statModifierType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("valueScalesWithStacks"));
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("effectPerStack"),
                new GUIContent("Valeur par stack"));

            // Affiche conditionTagForModifyStat seulement pour les stats combat-temporaires
            StatType stat = (StatType)statProp.enumValueIndex;
            bool isCombatTemporaryStat =
                stat == StatType.ArmorGainMultiplier ||
                stat == StatType.HealGainMultiplier ||
                stat == StatType.DamageGainMultiplier ||
                stat == StatType.EnergyCostReduction;

            if (isCombatTemporaryStat)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("conditionTagForModifyStat"),
                    new GUIContent("Filtre tag (optionnel)"));
                EditorGUILayout.HelpBox(
                    "Si renseigné, le bonus de stat ne s'applique que si le skill utilisé porte ce tag.\n" +
                    "Null = le bonus s'applique à tous les skills.",
                    MessageType.None);
            }
        }

        EditorGUILayout.Space();

        // -----------------------------------------------
        // EFFETS PASSIFS
        // -----------------------------------------------

        EditorGUILayout.LabelField("Effets passifs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Effets déclenchés automatiquement quand ce statut est actif (stacks > 0).\n" +
            "Le champ trigger de chaque EffectData détermine le moment de déclenchement.\n" +
            "Pour conditionner à un tag de skill (OnSkillUsed) : scalingSource = SkillUtilise + comptageTag.",
            MessageType.None);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("passiveEffects"), true);

        serializedObject.ApplyModifiedProperties();
    }
}
