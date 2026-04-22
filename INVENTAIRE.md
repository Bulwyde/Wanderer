# INVENTAIRE.md — Système d'inventaire et skills équipés

**Dernière mise à jour** : 2026-04-23  
**État** : Conception finalisée, prêt pour implémentation (6 phases)

---

## Vue d'ensemble

Rework du système d'équipements pour supporter :
- **Inventaire d'équipements** : jusqu'à 5 équipements en stock (configurable)
- **Inventaire de skills** : jusqu'à 10 skills en stock (configurable)
- **Emplacements de skills par équipement** : 1-6 slots par équipement avec états (libre/utilisé/non-disponible/utilisé+bloqué)
- **UI overlay** : InventoryUIManager (DDOL) pour gérer l'interface
- **Drag'n'drop** : équiper/déséquiper skills et équipements par drag
- **Tags hérités** : un skill équipé hérite des tags de son équipement (appliqués/retirés immédiatement)
- **Clonage** : chaque équipement loot est un clone indépendant du SO asset

---

## Décisions architecturales

### Clonage des équipements
**Pourquoi** : Si tu lootes 2x la même épée, ce sont 2 clones indépendants. Un module qui buffie l'épée #1 ne buffie pas l'épée #2.

**Comment** : `RunManager.CloneEquipmentForLoot(EquipmentData original)` crée une instance runtime via `Instantiate()` + deep copy des `skillSlots`.

### Tags hérités
**Exemple** : Un skill "Slash" a le tag "Physique". Tu l'équipes dans une arme "Feu". Le skill devient "Physique + Feu" (hérité). Si tu le déséquipes, il redevient "Physique" seulement.

**Implémentation** : SkillData a un champ `List<TagData> inheritedTags` (runtime uniquement, caché dans l'inspector). Rempli au drag'n'drop, vidé au déséquipement.

**Application immédiate** : Quand on équipe/déséquipe, les tags changent tout de suite → le joueur voit l'impact dans l'UI d'inventaire.

### Canvas overlay DDOL
**Pas de nouvelle scène** : l'InventoryUIManager crée un Canvas qui persiste entre les scènes (DontDestroyOnLoad). Ouverture/fermeture = activation/désactivation du Canvas.

**Blocage des inputs** : Au début du combat, drag'n'drop est désactivé. À l'apparition des loots, il est réactivé.

### Skills bloqués
**Verrouillage complet** : Un skill "utilisé + bloqué" ne peut pas être drag'n'drop vers la poubelle. C'est intégré à l'équipement et persiste tant que la source du verrouillage existe (module, événement, etc.).

### Emplacements non-disponibles
**Masqués** : Un slot "non-disponible" n'apparaît pas dans l'UI. Aucun "trou" visuel. C'est une réserve pour des déblocages futurs (modules, upgrades, etc.).

---

## Architecture technique

### Structures de données

#### SkillSlot (nouveau)
```csharp
[System.Serializable]
public class SkillSlot
{
    public enum SlotState { Available, Used, Unavailable, LockedInUse }
    public SlotState state = SlotState.Available;
    public SkillData equippedSkill = null;  // null si Available/Unavailable
}
```

#### Modifications EquipmentData
- **AVANT** : `List<SkillData> skills` (1-4 skills, limite fixe)
- **APRÈS** : `List<SkillSlot> skillSlots` (emplacements configurables avec états)

#### Modifications SkillData
- **NOUVEAU** : `[HideInInspector] List<TagData> inheritedTags` (runtime, pas sérialisé)

#### SkillLootTable (nouveau)
Copie directe de EquipmentLootTable mais avec `List<SkillData> skills` au lieu de equipments.

#### CharacterData
- **NOUVEAU** : `int maxEquippedArms` (2, 3 ou 4 selon le personnage)

### RunManager

#### Inventaires
```csharp
private List<EquipmentData> inventoryEquipments;      // clones non équipés
private List<SkillData> inventorySkills;
public int maxInventoryEquipments = 5;
public int maxInventorySkills = 10;
```

#### Méthodes clés
- `EquipmentData CloneEquipmentForLoot(EquipmentData)` → crée un clone indépendant
- `bool EquipSkill(equipment, slotIndex, skill)` → ajoute skill au slot, applique tags, retire de l'inventaire
- `bool UnequipSkill(equipment, slotIndex)` → retire skill du slot, retire tags, ajoute au inventaire
- `bool SwapSkill(equipment, slotIndex, newSkill)` → swap en une transaction
- `bool TryEquipEquipment(slot, equipment)` → équipe dans un slot du joueur, ancien va en inventaire
- `bool AddEquipmentToInventory(equipment)` / `RemoveEquipmentFromInventory(equipment)` → gestion inventaire
- `bool AddSkillToInventory(skill)` / `RemoveSkillFromInventory(skill)` → gestion inventaire

### UI (InventoryUIManager)

#### Canvas overlay (DDOL)
- Singleton global, persiste entre les scènes
- Ouverture/fermeture via `Open()` / `Close()` / `Toggle()`
- Escape ferme, I ouvre (pour le test)

#### Layout
```
Canvas (overlay)
├─ PanelEquipement (gauche)
│  ├─ Legs slot (1 emplacement)
│  └─ Arms container (jusqu'à 4 slots Arm selon CharacterData.maxEquippedArms)
│
├─ PanelInventaire (droite, vertical)
│  ├─ PanelEquipmentInventory (haut)
│  │  └─ GridEquipmentInventory (icônes + emplacements vides)
│  │
│  └─ PanelSkillInventory (bas)
│     └─ GridSkillInventory (icônes + emplacements vides)
│
└─ PanelPoubelle (coin, icône trash)
```

#### Fonctionnalités UI
- Affichage des icônes (équipements et skills)
- Emplacements libres visibles (pour montrer au joueur l'espace disponible)
- Tooltips au survol (nom + description)
- Icône cadenas sur les skills bloqués (visuellement)
- Drag'n'drop activé/désactivé selon contexte

### Système de loot

#### Nouveaux types de loot
- `SkillLootTable` : pool de skills
- Support dans tous les loot (combat, events, shop)

#### Déroulement
1. **Équipement looté** :
   - Clone via `CloneEquipmentForLoot()`
   - Cherche un slot vide du type correspondant → équipe directement
   - Si aucun slot vide → ajoute à `inventoryEquipments`
   - Si inventaire plein → **erreur "Inventaire d'équipements plein"** (à améliorer plus tard)

2. **Skill loot** :
   - Ajoute directement à `inventorySkills`
   - Si inventaire plein → **erreur "Inventaire de skills plein"** (à améliorer plus tard)

---

## Fichiers à créer/modifier

| Fichier | Type | Action |
|---------|------|--------|
| **SkillSlot.cs** | Créer | Classe sérialisable |
| **SkillData.cs** | Modifier | Ajouter `inheritedTags` |
| **EquipmentData.cs** | Modifier | Remplacer `skills` par `skillSlots` |
| **CharacterData.cs** | Modifier | Ajouter `maxEquippedArms` |
| **SkillLootTable.cs** | Créer | Copie EquipmentLootTable |
| **RunManager.cs** | Modifier | Inventaires + clonage + équipement/déséquipement |
| **EquipmentManager.cs** | Créer (optionnel) | Si RunManager devient trop gros |
| **InventoryUIManager.cs** | Créer | DDOL, Canvas overlay, gestion UI |
| **InventoryDragDropController.cs** | Créer | Gestion drag'n'drop |
| **CombatManager.cs** | Modifier | Support loot skills + clones |
| **EventManager.cs** | Modifier | Support loot skills + clones |
| **ShopManager.cs** | Modifier | Support skills à vendre |
| **CLAUDE.md** | Modifier | Ajouter section inventaire |

---

## Phases d'implémentation

### Phase 1 : Structures de données (indépendant)
- SkillSlot.cs création
- EquipmentData : remplacer skills → skillSlots
- SkillData : ajouter inheritedTags
- SkillLootTable.cs création
- CharacterData : ajouter maxEquippedArms
- **Durée** : ~30 min
- **Livrable** : structures compilent, pas de logique encore

### Phase 2 : RunManager - Inventaires et clonage (dépend Phase 1)
- Ajouter inventaires (List<EquipmentData> + List<SkillData>)
- CloneEquipmentForLoot()
- Méthodes Add/Remove inventaire
- TryEquipEquipment()
- **Durée** : ~45 min
- **Livrable** : RunManager gère les inventaires et clonage

### Phase 3 : Équipement/déséquipement de skills (dépend Phase 2)
- EquipSkill() + UnequipSkill() + SwapSkill()
- Gestion tags hérités
- Vérifications d'état (bloqué, non-disponible, etc.)
- **Durée** : ~45 min
- **Livrable** : système d'équipement de skills fonctionnel

### Phase 4 : InventoryUIManager et Canvas overlay (dépend Phase 1-3)
- InventoryUIManager.cs création (DDOL)
- Canvas overlay création
- Hiérarchie UI de base
- Refresh basique (affichage équipements et skills)
- **Durée** : ~1h
- **Livrable** : UI visible et updatee

### Phase 5 : Système de drag'n'drop (dépend Phase 4)
- InventoryDragDropController.cs
- Drag skill → slot d'équipement / inventaire
- Drag équipement → slot joueur / inventaire
- Poubelle + confirmation
- Gestion des erreurs (plein, bloqué, etc.)
- **Durée** : ~1h30
- **Livrable** : drag'n'drop fonctionnel

### Phase 6 : Intégration système de loot (dépend Phase 1-5)
- CombatManager : support loot skills + clones équipements
- EventManager : support loot skills + clones équipements
- ShopManager : support vente skills
- Messages d'erreur (inventaire plein)
- **Durée** : ~1h
- **Livrable** : système de loot complètement intégré

**Temps total estimé** : ~5h30 pour les 6 phases

---

## Pièges et points d'attention

1. **Clonage vs. asset modifié** : Ne JAMAIS modifier un EquipmentData asset en runtime. Cloner toujours.
2. **Tags hérités — double** : Un skill a tag "Damage", l'équipement aussi → pas de doublon dans inheritedTags.
3. **Déséquipement — inventaire plein** : Si inventaire plein et on déséquipe, erreur (pour l'instant). À améliorer.
4. **Skills bloqués — UI** : Un skill bloqué ne doit pas être draggable. Visuellement, afficher un cadenas.
5. **Emplacements non-disponibles — masqués** : Ne pas afficher de "trou" dans l'UI, les masquer complètement.
6. **Canvas DDOL — z-index** : Sortingorder élevée pour être au-dessus. EventSystem gérera la priorité.
7. **Drag'n'drop désactivé au combat** : Au Start du combat, désactiver drag'n'drop. Réactiver à loot.
8. **Persistance** : Les clones et inventaires doivent être sauvegardés dans RunManager (sérialisation future).

---

## Tests de validation

- [ ] Phase 1 : Structures compilent, inspector affiche bien
- [ ] Phase 2 : Clone indépendant, inventaires Add/Remove OK
- [ ] Phase 3 : Équip skill → inventaire vide, tags hérités appliqués. Déséquip → retour inventaire, tags retirés
- [ ] Phase 4 : UI affichée, updatee avec équipements et skills. Escape ferme, I ouvre
- [ ] Phase 5 : Drag skill → slot vide = équipé. Drag vers poubelle + confirm = détruit. Erreur si bloqué
- [ ] Phase 6 : Loot équipement + skill en combat/event → clonés, ajoutés correctement. Erreur si inventaire plein

---

## Conventions de code (voir CLAUDE.md)

- Scripts en français
- Bloc `<summary>` en tête
- Sections `// ---...---`
- Logs préfixés `[RunManager]` ou `[InventoryUIManager]`
- `FindFirstObjectByType<T>()` (pas `FindObjectOfType<T>()` déprécié)
- Vérifier null avec `if (x == null)` pas `??` avec objets Unity
- SkriptableObjects jamais modifiés directement

---

## Références

- **CLAUDE.md** : conventions, pièges, architecture générale du projet
- **EquipmentLootTable.cs** : pattern à copier pour SkillLootTable
- **RunManager.cs** : structure actuelle des équipements
- **SkillData.cs** et **EquipmentData.cs** : structures existantes à adapter
