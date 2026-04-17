using UnityEditor;
using UnityEngine;

/// <summary>
/// Éditeur personnalisé pour EffectData.
/// Remplace les labels génériques "Value" / "Secondary Value" par des labels
/// explicites selon l'action sélectionnée, et masque les champs non pertinents.
///
/// Correspondances action → champs affichés :
///
///   DealDamage    → "Dégâts de base" (value)
///                   "Statut de scaling" (scalingStatus) — optionnel
///                   Si scalingStatus renseigné :
///                     "Bonus par stack" (secondaryValue)
///                     "Consommer les stacks" (consumeStacks)
///
///   Heal          → "Soin de base" (value)
///                   Même logique de scaling que DealDamage
///
///   AddArmor      → "Armure de base" (value)
///                   Même logique de scaling que DealDamage
///
///   ApplyStatus   → "Statut à appliquer" (statusToApply)
///                   "Stacks à appliquer" (value)
///                   (secondaryValue et scalingStatus masqués)
///
///   ModifyStat    → "Valeur de modification" (value)
///                   (secondaryValue affiché sans scaling pour l'instant)
///
///   AddGold       → "Quantité" (value)
///                   (secondaryValue masqué)
///
///   RevealRoom    → (value et secondaryValue masqués — pas de paramètre numérique)
///   DisableEnemyPart → idem
///
///   DonnerConsommable → loot table consommable + filtreTag / filtreParTagHero
///   DonnerEquipement  → loot table équipement  + filtreTag / filtreParTagHero
///   DonnerModule      → loot table module       + filtreTag / filtreParTagHero
/// </summary>
[CustomEditor(typeof(EffectData))]
public class EffectDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // -----------------------------------------------
        // IDENTITÉ
        // -----------------------------------------------

        EditorGUILayout.LabelField("Identité", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("effectID"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"),
            new GUIContent("Nom affiché", "Nom court sur les boutons passifs. Si vide, effectID est utilisé."));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("description"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("keywords"));

        EditorGUILayout.Space();

        // -----------------------------------------------
        // DÉCLENCHEUR / ACTION / CIBLE
        // -----------------------------------------------

        EditorGUILayout.LabelField("Comportement", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("trigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("action"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("target"));

        EditorGUILayout.Space();

        // -----------------------------------------------
        // VALEURS — labels contextuels selon l'action
        // -----------------------------------------------

        SerializedProperty actionProp = serializedObject.FindProperty("action");
        EffectAction action = (EffectAction)actionProp.enumValueIndex;

        EditorGUILayout.LabelField("Valeurs", EditorStyles.boldLabel);

        switch (action)
        {
            // -----------------------------------------------------------
            case EffectAction.DealDamage:
            case EffectAction.Heal:
            case EffectAction.AddArmor:
            {
                string baseLabel = action == EffectAction.DealDamage ? "Degats de base"
                                 : action == EffectAction.Heal       ? "Soin de base"
                                 :                                      "Armure de base";

                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("value"),
                    new GUIContent(baseLabel));

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Scaling par statut (optionnel)", EditorStyles.miniBoldLabel);

                SerializedProperty scalingProp = serializedObject.FindProperty("scalingStatus");
                EditorGUILayout.PropertyField(scalingProp, new GUIContent("Statut de scaling"));

                // Affiche secondaryValue et consumeStacks seulement si un statut est assigné
                if (scalingProp.objectReferenceValue != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("secondaryValue"),
                        new GUIContent("Bonus par stack"));
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("consumeStacks"),
                        new GUIContent("Consommer les stacks"));
                    EditorGUI.indentLevel--;
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Aucun statut de scaling — secondaryValue ignoré.",
                        MessageType.None);
                }
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.ApplyStatus:
            {
                SerializedProperty statusProp = serializedObject.FindProperty("statusToApply");
                EditorGUILayout.PropertyField(statusProp, new GUIContent("Statut a appliquer"));

                if (statusProp.objectReferenceValue != null)
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("value"),
                        new GUIContent("Stacks a appliquer"));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Assigner un StatusData pour configurer le nombre de stacks.",
                        MessageType.Warning);
                }
                // secondaryValue et scalingStatus masqués pour ApplyStatus
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.ModifyStat:
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("statToModify"),
                    new GUIContent("Stat ciblee"));
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("value"),
                    new GUIContent("Valeur (plate)"));
                EditorGUILayout.HelpBox(
                    "Modules / passifs : bonus permanent sur le run.\n" +
                    "Skills / consommables : bonus temporaire ce combat.",
                    MessageType.None);
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.GainEnergy:
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("value"),
                    new GUIContent("Energie restauree"));
                EditorGUILayout.HelpBox(
                    "Restaure de l'energie courante du joueur, plafonnee a son max du tour.\n" +
                    "Uniquement utile en combat (ignoree hors combat).",
                    MessageType.None);
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.AddCredits:
            {
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("value"),
                    new GUIContent("Quantite de credits"));
                EditorGUILayout.HelpBox(
                    "Positif = gain de credits. Negatif = depense (ne peut pas passer sous 0).",
                    MessageType.None);
                // secondaryValue masqué
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.RevealRoom:
            case EffectAction.DisableEnemyPart:
            {
                EditorGUILayout.HelpBox(
                    "Cette action ne requiert pas de valeur numérique.",
                    MessageType.None);
                break;
            }

            // -----------------------------------------------------------
            case EffectAction.DonnerConsommable:
            case EffectAction.DonnerEquipement:
            case EffectAction.DonnerModule:
            {
                // Loot table correspondante au type d'action
                if (action == EffectAction.DonnerConsommable)
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("consommableLootTable"),
                        new GUIContent("Loot table consommable"));
                else if (action == EffectAction.DonnerEquipement)
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("equipementLootTable"),
                        new GUIContent("Loot table équipement"));
                else
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("moduleLootTable"),
                        new GUIContent("Loot table module"));

                EditorGUILayout.Space(4);

                // Filtre — filtreParTagHero et filtreTag sont mutuellement exclusifs
                SerializedProperty filtreHéroProp = serializedObject.FindProperty("filtreParTagHero");
                EditorGUILayout.PropertyField(filtreHéroProp,
                    new GUIContent("Utiliser le tag du héros comme filtre"));

                if (!filtreHéroProp.boolValue)
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("filtreTag"),
                        new GUIContent("Filtre par tag (optionnel)"));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "filtreTag ignoré — le premier tag du héros est utilisé comme filtre.",
                        MessageType.None);
                }
                break;
            }

            // -----------------------------------------------------------
            default:
            {
                // Fallback — affiche les champs bruts si une nouvelle action est ajoutée
                EditorGUILayout.PropertyField(serializedObject.FindProperty("value"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("secondaryValue"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("statusToApply"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("scalingStatus"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("consumeStacks"));
                break;
            }
        }

        EditorGUILayout.Space();

        // -----------------------------------------------
        // CONDITION DE TAG
        // -----------------------------------------------

        EditorGUILayout.LabelField("Condition de tag", EditorStyles.boldLabel);

        SerializedProperty conditionCibleProp = serializedObject.FindProperty("conditionCible");
        EditorGUILayout.PropertyField(conditionCibleProp, new GUIContent("Cible de la condition"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("conditionTag"), new GUIContent("Tag requis"));

        ConditionCible conditionCible = (ConditionCible)conditionCibleProp.enumValueIndex;

        // Bonus conditionnel — uniquement pour DealDamage quand un tag est assigné
        SerializedProperty conditionTagProp = serializedObject.FindProperty("conditionTag");
        if (conditionTagProp.objectReferenceValue != null && action == EffectAction.DealDamage)
        {
            EditorGUI.indentLevel++;

            SerializedProperty typeBonusProp = serializedObject.FindProperty("typeBonusConditionnel");
            EditorGUILayout.PropertyField(typeBonusProp, new GUIContent("Type de bonus conditionnel"));

            TypeBonusConditionnel typBonus = (TypeBonusConditionnel)typeBonusProp.enumValueIndex;
            string labelBonus = typBonus == TypeBonusConditionnel.Pourcentage
                ? "Bonus % (ex : 0.5 = +50%, -0.3 = -30%)"
                : "Bonus dégâts flat";

            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("bonusConditionnel"),
                new GUIContent(labelBonus));

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
