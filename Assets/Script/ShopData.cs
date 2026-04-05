using UnityEngine;

/// <summary>
/// ScriptableObject définissant la configuration d'un marchand :
/// loot tables source, quantités d'articles proposés et fourchettes de prix par catégorie.
///
/// Peut être assigné :
///   - sur une CellData individuelle (case Marchand spécifique) via l'éditeur de carte
///   - sur la MapData en tant que ShopData par défaut (fallback pour toutes les cases
///     Marchand qui n'ont pas de ShopData explicitement assigné)
///
/// Créer via : clic droit dans Project → Create → RPG → Shop Data
/// </summary>
[CreateAssetMenu(fileName = "NewShopData", menuName = "RPG/Shop Data")]
public class ShopData : ScriptableObject
{
    [Header("Équipements")]
    public EquipmentLootTable equipmentLootTable;
    public int                equipmentCount        = 6;
    public Vector2Int         equipmentPriceRange   = new Vector2Int(50, 150);

    [Header("Modules")]
    public ModuleLootTable moduleLootTable;
    public int             moduleCount        = 3;
    public Vector2Int      modulePriceRange   = new Vector2Int(80, 200);

    [Header("Consommables")]
    public ConsumableLootTable consumableLootTable;
    public int                 consumableCount      = 3;
    public Vector2Int          consumablePriceRange = new Vector2Int(20, 60);
}
