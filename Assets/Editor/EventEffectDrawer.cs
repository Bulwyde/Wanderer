using UnityEditor;
using UnityEngine;

/// <summary>
/// Drawer personnalisé pour EventEffect.
/// Affiche uniquement les champs pertinents selon :
///   - le type d'effet sélectionné (ModifyHP, GainConsumable, etc.)
///   - le mode choisi pour les récompenses (FromList ou FromLootTable)
///
/// Logique d'affichage par type :
///   ModifyHP / ModifyMaxHP  → value
///   HealToFull              → (rien de plus)
///   GainConsumable          → gainConsumableMode + consumablesToGive OU consumableLootTable
///   GainModule              → gainModuleMode     + modulesToGive     OU moduleLootTable
///   GainEquipment           → gainEquipmentMode  + equipmentsToGive  OU equipmentLootTable
///   SetEventFlag            → flagKey + flagValue
/// </summary>
[CustomPropertyDrawer(typeof(EventEffect))]
public class EventEffectDrawer : PropertyDrawer
{
    // -----------------------------------------------
    // HAUTEUR TOTALE DE LA PROPRIÉTÉ
    // -----------------------------------------------

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH  = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float step   = lineH + spacing;

        // Type — toujours affiché
        float height = step;

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        EventEffectType type = (EventEffectType)typeProp.enumValueIndex;

        switch (type)
        {
            // -----------------------------------------------------------
            case EventEffectType.ModifyHP:
            case EventEffectType.ModifyMaxHP:
                height += step; // value
                break;

            // -----------------------------------------------------------
            case EventEffectType.HealToFull:
                // rien de plus
                break;

            // -----------------------------------------------------------
            case EventEffectType.GainConsumable:
            {
                height += step; // gainConsumableMode
                SerializedProperty modeProp = property.FindPropertyRelative("gainConsumableMode");
                if (modeProp.enumValueIndex == 0) // FromList
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("consumablesToGive"), true);
                else // FromLootTable
                    height += step; // consumableLootTable
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.GainModule:
            {
                height += step; // gainModuleMode
                SerializedProperty modeProp = property.FindPropertyRelative("gainModuleMode");
                if (modeProp.enumValueIndex == 0) // FromList
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("modulesToGive"), true);
                else // FromLootTable
                    height += step; // moduleLootTable
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.GainEquipment:
            {
                height += step; // gainEquipmentMode
                SerializedProperty modeProp = property.FindPropertyRelative("gainEquipmentMode");
                if (modeProp.enumValueIndex == 0) // FromList
                    height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("equipmentsToGive"), true);
                else // FromLootTable
                    height += step; // equipmentLootTable
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.SetEventFlag:
                height += step; // flagKey
                height += step; // flagValue
                break;

            // -----------------------------------------------------------
            case EventEffectType.TriggerNavEffect:
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("navEffect"), true);
                break;
        }

        return height;
    }

    // -----------------------------------------------
    // DESSIN DES CHAMPS
    // -----------------------------------------------

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float lineH   = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float step    = lineH + spacing;

        EditorGUI.BeginProperty(position, label, property);

        // Rect de travail — on avance .y au fur et à mesure
        Rect rect = new Rect(position.x, position.y, position.width, lineH);

        // Type — toujours affiché en premier
        SerializedProperty typeProp = property.FindPropertyRelative("type");
        EditorGUI.PropertyField(rect, typeProp);
        rect.y += step;

        EventEffectType type = (EventEffectType)typeProp.enumValueIndex;

        switch (type)
        {
            // -----------------------------------------------------------
            case EventEffectType.ModifyHP:
            case EventEffectType.ModifyMaxHP:
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("value"));
                break;

            // -----------------------------------------------------------
            case EventEffectType.HealToFull:
                // rien à afficher de plus
                break;

            // -----------------------------------------------------------
            case EventEffectType.GainConsumable:
            {
                SerializedProperty modeProp = property.FindPropertyRelative("gainConsumableMode");
                EditorGUI.PropertyField(rect, modeProp);
                rect.y += step;

                if (modeProp.enumValueIndex == 0) // FromList
                {
                    SerializedProperty listProp = property.FindPropertyRelative("consumablesToGive");
                    rect.height = EditorGUI.GetPropertyHeight(listProp, true);
                    EditorGUI.PropertyField(rect, listProp, true);
                }
                else // FromLootTable
                {
                    rect.height = lineH;
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("consumableLootTable"));
                }
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.GainModule:
            {
                SerializedProperty modeProp = property.FindPropertyRelative("gainModuleMode");
                EditorGUI.PropertyField(rect, modeProp);
                rect.y += step;

                if (modeProp.enumValueIndex == 0) // FromList
                {
                    SerializedProperty listProp = property.FindPropertyRelative("modulesToGive");
                    rect.height = EditorGUI.GetPropertyHeight(listProp, true);
                    EditorGUI.PropertyField(rect, listProp, true);
                }
                else // FromLootTable
                {
                    rect.height = lineH;
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("moduleLootTable"));
                }
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.GainEquipment:
            {
                SerializedProperty modeProp = property.FindPropertyRelative("gainEquipmentMode");
                EditorGUI.PropertyField(rect, modeProp);
                rect.y += step;

                if (modeProp.enumValueIndex == 0) // FromList
                {
                    SerializedProperty listProp = property.FindPropertyRelative("equipmentsToGive");
                    rect.height = EditorGUI.GetPropertyHeight(listProp, true);
                    EditorGUI.PropertyField(rect, listProp, true);
                }
                else // FromLootTable
                {
                    rect.height = lineH;
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("equipmentLootTable"));
                }
                break;
            }

            // -----------------------------------------------------------
            case EventEffectType.SetEventFlag:
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("flagKey"));
                rect.y += step;
                rect.height = lineH;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("flagValue"));
                break;

            // -----------------------------------------------------------
            case EventEffectType.TriggerNavEffect:
            {
                SerializedProperty navEffectProp = property.FindPropertyRelative("navEffect");
                rect.height = EditorGUI.GetPropertyHeight(navEffectProp, true);
                EditorGUI.PropertyField(rect, navEffectProp, new GUIContent("Effet de navigation"), true);
                break;
            }
        }

        EditorGUI.EndProperty();
    }
}
