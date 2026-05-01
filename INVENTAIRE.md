# INVENTAIRE.md — Système d'inventaire et skills équipés

**Dernière mise à jour** : 2026-04-30  
**État** : ✅ Implémenté et testé — tous les bugs corrigés

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

> ✅ **Les 6 phases sont implémentées.** Les phases ci-dessous sont conservées à titre de référence.

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

## Bugs corrigés et architecture finalisée

### CloneEquipmentForLoot — Deep copy des skillSlots
**Bug :** Équipements clones partageaient les mêmes skillSlots → modifier un skill sur un clone l'affectait sur tous les autres.
**Fix :** `CloneEquipmentForLoot()` effectue une deep copy : chaque clone a sa propre `List<SkillSlot>` indépendante.

### SeedSlotSiVide — Clonage des équipements de départ
**Bug :** Les équipements de départ (`startingArm1`, `startingArm2`, etc.) modifiaient l'asset ScriptableObject.
**Fix :** `SeedSlotSiVide()` appelle `CloneEquipmentForLoot()` avant d'équiper, garantissant l'indépendance des clones.

### Équipements indexés — Inventaires à slots fixes
**Architecture :** Les inventaires (`inventoryEquipments` et `inventorySkills`) sont maintenant des `List<T>` de taille fixe (`maxInventoryEquipments` / `maxInventorySkills`) remplies de nulls.
**Bénéfice :** Permet le placement d'items à des slots spécifiques au lieu du "premier disponible" → drag'n'drop plus intuitif.

### Placement avant modification
**Bug :** Quand on déséquipait un item en le draggant vers l'inventaire, s'il ne pouvait pas être placé, l'item était perdu.
**Fix :** Tous les `TraiterDrop*` tentent le placement AVANT de modifier l'état (unequip, clear origin slot).

### Validation skills — Navigation vs Arms
**Règle :** Les skills avec `isNavigationSkill=true` ne peuvent aller que sur Legs. Les autres ne peuvent aller que sur Arms (Arm1/2/3/4).
**Implémentation :** `TraiterDropSkillSlot()` valide le type avant d'équiper.

### MaxEquippedArms — Validation runtime
**Règle :** Arm3 et Arm4 ne sont accessibles que si `maxEquippedArms >= 3/4`.
**Implémentation :**
- `RafraichirEquipementsPortes()` masque `arm3Container`/`arm4Container` visuellement
- `TryEquipEquipment()` rejette les placements sur Arm3/Arm4 si `maxEquippedArms < 3/4`

### Modal blocker — Bloquer interactions en arrière-plan
**Implémentation :** Un Panel semi-transparent (`raycastTarget=true`, couleur noire alpha=0) masque les éléments en arrière-plan quand l'inventaire est ouvert. Assigné via `[SerializeField] Image modalBlocker`.

### Container-per-slot architecture
**Pattern :** Au lieu de générer toute la hiérarchie du panneau gauche dynamiquement, chaque slot d'équipement (Legs, Arm1-4) a ses propres `[SerializeField] RectTransform` :
- `legsContainer`, `arm1Container`, etc. (pour l'icône d'équipement)
- `legsSkillGrid`, `arm1SkillGrid`, etc. (pour la grille de skills)

Le code ne peuple que l'intérieur de ces containers, laissant la disposition aux mains du designer.

### Floating icon taille dynamique
**Implémentation :** `OnBeginDrag()` définit la taille du `DragFloatingIcon` selon le type d'item :
- 32×32 pour les skills
- 64×64 pour les équipements

### Unequip avec gestion des skills
**Implémentation :** `UnequipEquipmentAndMoveSkillsToInventory()` :
1. Itère les skillSlots équipés (Used/LockedInUse)
2. Ajoute chaque skill à l'inventaire
3. Reset le skillSlot à Available avec `equippedSkill=null`
4. Libère le slot d'équipement

### ViderPanel — DestroyImmediate au lieu de Destroy
**Bug :** `Destroy()` est différé (fin de frame) → les anciens et nouveaux enfants étaient présents simultanément → layout calculé incorrectement.
**Fix :** `ViderPanel()` utilise `DestroyImmediate()` pour nettoyer immédiatement.

### Icônes équipement 64×64 fixes
**Bug :** Icônes s'étiraient à cause du `VerticalLayoutGroup` du container.
**Fix :** `RafraichirContenuSlot()` :
- Set `RectTransform.sizeDelta = (64, 64)`
- `LayoutElement` : `minWidth`/`preferredWidth` = 64, `flexibleWidth = 0`

### Container DropZone — Image transparente requise
**Bug :** Un `RectTransform` sans `Image` est invisible aux raycasts UI → la `DropZone` n'était jamais détectée.
**Fix :** `RafraichirContenuSlot()` ajoute une `Image` transparente (`color alpha=0`, `raycastTarget=true`) sur chaque container si absente.

---

### Bugs corrigés — Session 2 (2026-04-30)

#### RepeatExecution — mort de la cible SingleEnemy
**Bug :** Si `RepeatExecution` était actif et que le premier cast tuait un ennemi `SingleEnemy`, les répétitions suivantes continuaient sur une cible morte → null reference ou effets appliqués à tort.  
**Fix :** `OnEnemyCibleClique` : la boucle de répétition vérifie `!cible.IsAlive` avant chaque itération (break immédiat pour `SingleEnemy`). Garde `!combatEnded` maintenue en parallèle.

#### Drag/drop slot Used — duplication d'équipement
**Bug :** Faire glisser un skill vers un slot `Used` d'un équipement différent appelait `SwapSkill` sans libérer le slot d'origine → le skill apparaissait dans les deux slots simultanément.  
**Fix :** `TraiterDropSkillSlot()` appelle `run.UnequipSkill(_originEquipment, _originSlotIndex)` avant `run.SwapSkill(...)` quand l'origine est un slot équipé.

#### EffectiveCost — affichage et remboursement incorrects
**Bug :** `UpdateSkillButtons` et `CancelTargetSelection` utilisaient `skill.energyCost` brut — les `EnergyCostModifier` de l'équipement étaient ignorés. L'UI affichait le mauvais coût et le remboursement sur annulation était erroné.  
**Fix :** `SpawnSkillButtons` calcule le coût effectif (base + `EnergyCostModifier`) et le stocke dans `SkillButton._effectiveCost`. `UpdateSkillButtons` et `CancelTargetSelection` lisent `sb.EffectiveCost`.

#### Source équipement incorrecte au clic (SkillModifier)
**Bug :** `UseSkill` cherchait l'équipement source par comparaison de référence `SkillData` au moment du clic. Si deux équipements partageaient le même asset skill, le mauvais équipement était identifié → `skillModifiers` du mauvais équipement appliqués.  
**Fix :** `SpawnSkillButtons` maintient `_availableSkillSources` (liste parallèle à `availableSkills`). Source trackée à la création des boutons, transmise via callback `Action<SkillData, EquipmentData>`. `UseSkill` reçoit directement l'équipement — aucune recherche au clic.

#### SeedSlotIfFree — duplication d'équipement au combat
**Bug :** `CombatManager` appelait `SeedSlotIfFree` dans `InitialiserEquipementEtSkills`, qui écrivait les SO assets directement en slot. Au second combat, les équipements de départ étaient dupliqués — le SO était muté entre sessions.  
**Fix :** `SeedSlotIfFree` entièrement supprimé de `CombatManager`. Le seeding se fait uniquement dans `RunManager.StartNewRun()` → `SeedDonneesDepart()` → `SeedSlotSiVide()` (utilise `CloneEquipmentForLoot`).

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

- [ ] Inventaire — équipements et skills s'affichent dans l'UI (touche `I`)
- [ ] Drag skill → slot Available d'un équipement = équipé, skill retiré de l'inventaire
- [ ] Drag skill → slot Used d'un équipement = swap
- [ ] Drag skill LockedInUse → pas draggable
- [ ] Drag équipement → slot joueur = équipé, ancien équipement va en inventaire
- [ ] Drag item → poubelle + confirmation = supprimé
- [ ] Loot combat → équipement cloné, skills ajoutés à l'inventaire
- [ ] Shop → skill achetable, ajouté à l'inventaire
- [ ] Tags hérités : équiper skill → inheritedTags remplis. Déséquiper → inheritedTags vidés.
- [ ] Combat : drag'n'drop désactivé au démarrage. Réactivé à l'apparition du loot.

---

## Conventions de code (voir CLAUDE.md)

- Scripts en français
- Bloc `<summary>` en tête
- Sections `// ---...---`
- Logs préfixés `[RunManager]` ou `[InventoryUIManager]`
- `FindFirstObjectByType<T>()` (pas `FindObjectOfType<T>()` déprécié)
- Vérifier null avec `if (x == null)` pas `??` avec objets Unity
- ScriptableObjects jamais modifiés directement
- Toujours cloner les équipements via `CloneEquipmentForLoot()` avant d'équiper ou d'ajouter à l'inventaire
- `SetupEquipment`/`SetupSkill` acceptent des paramètres optionnels pour tracker l'origine (`originSlot`, `originInventorySlotIndex`)
- Placement d'item = premier essai. Modification d'état = seulement si placement réussit (atomicité)

---

## Références

- **CLAUDE.md** : conventions, pièges, architecture générale du projet
- **EquipmentLootTable.cs** : pattern à copier pour SkillLootTable
- **RunManager.cs** : structure actuelle des équipements
- **SkillData.cs** et **EquipmentData.cs** : structures existantes à adapter

---

## Limites connues

- **Bras — clipping visuel** : `armsContainer` utilise un `GridLayoutGroup` avec `cellSize = 80×80`.
  Si un équipement possède de nombreux skill slots, le contenu peut dépasser la cellule et être clippé.
  Ajuster `cellSize` ou `leArms.preferredHeight` dans `ConstruireHierarchie()` selon les assets réels.
- **Tête et Torse non affichés** : le panneau gauche n'affiche que Legs + Arm1/Arm2. Head et Torso ne
  sont pas visibles dans l'inventaire UI actuel (pas de skill slots sur ces slots par design actuel).
- **Inventaire plein** : si inventaire équipements ou skills plein lors d'un loot, un log warning est
  émis mais aucun feedback visuel n'est présenté au joueur. À améliorer.
