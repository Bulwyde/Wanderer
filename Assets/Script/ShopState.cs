using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// État persistant d'un marchand sur la carte.
/// Généré une seule fois à la première visite de la case, puis conservé pour toute la run.
/// Stocké dans RunManager.shopStates, indexé par "x,y".
///
/// Chaque article garde sa référence ScriptableObject, son prix tiré aléatoirement
/// dans la fourchette définie sur la CellData, et un flag indiquant s'il a été acheté.
/// Les articles achetés restent visibles dans l'UI mais sont grisés et non cliquables.
/// </summary>

// -----------------------------------------------
// ARTICLES PAR TYPE
// -----------------------------------------------

[System.Serializable]
public class ShopItemEquipment
{
    public EquipmentData data;
    public int           prix;
    public bool          achete;
}

[System.Serializable]
public class ShopItemModule
{
    public ModuleData data;
    public int        prix;
    public bool       achete;
}

[System.Serializable]
public class ShopItemConsomable
{
    public ConsumableData data;
    public int            prix;
    public bool           achete;
}

[System.Serializable]
public class ShopItemSkill
{
    public SkillData data;
    public int       prix;
    public bool      achete;
}

// -----------------------------------------------
// ÉTAT COMPLET DU SHOP
// -----------------------------------------------

[System.Serializable]
public class ShopState
{
    // true une fois que l'inventaire a été généré (évite une double génération)
    public bool genere = false;

    public List<ShopItemEquipment>  equipements  = new List<ShopItemEquipment>();
    public List<ShopItemModule>     modules      = new List<ShopItemModule>();
    public List<ShopItemConsomable> consommables = new List<ShopItemConsomable>();
    public List<ShopItemSkill>      skills       = new List<ShopItemSkill>();
}
