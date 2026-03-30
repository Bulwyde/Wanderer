using UnityEditor;
using UnityEngine;

/// <summary>
/// Drawer personnalisé pour NavEffect.
/// Affiche uniquement les champs pertinents selon le type d'effet sélectionné.
///
/// Logique d'affichage par type :
///   TeleportRandom      → allowedCellTypes
///   RevealZoneRandom    → value (libellé "Rayon")
///   RevealZoneChoice    → value (libellé "Rayon")
///   IncreaseVisionRange → value (libellé "Delta vision")
///   IncrementCounter    → counterKey + value (libellé "Delta")
/// </summary>
[CustomPropertyDrawer(typeof(NavEffect))]
public class NavEffectDrawer : PropertyDrawer
{
    // -----------------------------------------------
    // HAUTEUR TOTALE
    // -----------------------------------------------

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH  = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float step   = lineH + spacing;

        // Type — toujours affiché
        float height = step;

        SerializedProperty typeProp = property.FindPropertyRelative("type");
        NavEffectType type = (NavEffectType)typeProp.enumValueIndex;

        switch (type)
        {
            case NavEffectType.TeleportRandom:
                height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("allowedCellTypes"), true);
                break;

            case NavEffectType.RevealZoneRandom:
            case NavEffectType.RevealZoneChoice:
            case NavEffectType.IncreaseVisionRange:
                height += step; // value
                break;

            case NavEffectType.IncrementCounter:
                height += step; // counterKey
                height += step; // value (delta)
                break;
        }

        return height;
    }

    // -----------------------------------------------
    // DESSIN DES CHAMPS
    // -----------------------------------------------

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        float lineH  = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;
        float step   = lineH + spacing;

        EditorGUI.BeginProperty(position, label, property);

        Rect rect = new Rect(position.x, position.y, position.width, lineH);

        // Type — toujours affiché en premier
        SerializedProperty typeProp = property.FindPropertyRelative("type");
        EditorGUI.PropertyField(rect, typeProp);
        rect.y += step;

        NavEffectType type = (NavEffectType)typeProp.enumValueIndex;

        switch (type)
        {
            // -----------------------------------------------------------
            case NavEffectType.TeleportRandom:
            {
                SerializedProperty listProp = property.FindPropertyRelative("allowedCellTypes");
                rect.height = EditorGUI.GetPropertyHeight(listProp, true);
                EditorGUI.PropertyField(rect, listProp, true);
                break;
            }

            // -----------------------------------------------------------
            case NavEffectType.RevealZoneRandom:
            case NavEffectType.RevealZoneChoice:
            {
                rect.height = lineH;
                SerializedProperty valueProp = property.FindPropertyRelative("value");
                EditorGUI.PropertyField(rect, valueProp, new GUIContent("Rayon"));
                break;
            }

            // -----------------------------------------------------------
            case NavEffectType.IncreaseVisionRange:
            {
                rect.height = lineH;
                SerializedProperty valueProp = property.FindPropertyRelative("value");
                EditorGUI.PropertyField(rect, valueProp, new GUIContent("Delta vision"));
                break;
            }

            // -----------------------------------------------------------
            case NavEffectType.IncrementCounter:
            {
                rect.height = lineH;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("counterKey"),
                    new GUIContent("Cle du compteur"));
                rect.y += step;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("value"),
                    new GUIContent("Delta"));
                break;
            }
        }

        EditorGUI.EndProperty();
    }
}
