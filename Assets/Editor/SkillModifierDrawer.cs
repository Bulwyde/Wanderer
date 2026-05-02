using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(SkillModifier))]
public class SkillModifierDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProp = property.FindPropertyRelative("type");
        SkillModifierType type = (SkillModifierType)typeProp.enumValueIndex;

        float height = EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing * 2;

        // Ajoute une ligne pour value sauf si c'est ForceAoE
        if (type != SkillModifierType.ForceAoE)
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        // Ajoute une ligne pour effectToTrigger si AfterNSkillsUsed
        if (type == SkillModifierType.AfterNSkillsUsed)
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty typeProp         = property.FindPropertyRelative("type");
        SerializedProperty valueProp        = property.FindPropertyRelative("value");
        SerializedProperty conditionTagProp = property.FindPropertyRelative("conditionTag");

        SkillModifierType type = (SkillModifierType)typeProp.enumValueIndex;

        float y = position.y;
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        // Type
        Rect typeRect = new Rect(position.x, y, position.width, lineHeight);
        EditorGUI.PropertyField(typeRect, typeProp);
        y += lineHeight + spacing;

        // Value (masque si ForceAoE)
        if (type != SkillModifierType.ForceAoE)
        {
            Rect valueRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(valueRect, valueProp);
            y += lineHeight + spacing;
        }

        // Condition Tag
        Rect conditionTagRect = new Rect(position.x, y, position.width, lineHeight);
        EditorGUI.PropertyField(conditionTagRect, conditionTagProp);

        // Effect To Trigger (AfterNSkillsUsed uniquement)
        if (type == SkillModifierType.AfterNSkillsUsed)
        {
            y += lineHeight + spacing;
            SerializedProperty effectProp = property.FindPropertyRelative("effectToTrigger");
            Rect effectRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(effectRect, effectProp);
        }

        EditorGUI.EndProperty();
    }
}
