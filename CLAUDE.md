# CLAUDE.md — Contexte du projet "Petit turn based"

> Ce fichier est lu automatiquement par Claude au démarrage de chaque conversation.
> Il contient tout le contexte nécessaire pour reprendre le développement sans perdre l'historique.
> **Mettre à jour ce fichier après chaque session de travail significative.**

---

## Vue d'ensemble du projet

Roguelike au tour par tour en Unity (C#), inspiré de Slay the Spire et Darkest Dungeon.
Le joueur explore une carte, tombe sur des salles (combat, événement, etc.), gagne de l'équipement et progresse jusqu'au boss.

**Moteur :** Unity (version LTS récente)
**Langage :** C#
**UI :** Unity UI (Canvas) + TextMeshPro
**Git :** dépôt initialisé à la racine du projet

---

## Architecture des scènes

| Scène        | Rôle                                                                 |
|-------------|----------------------------------------------------------------------|
| `MainMenu`  | Écran d'accueil / sélection personnage (non développé pour l'instant) |
| `Navigation`| Carte du donjon — déplacement du joueur case par case                |
| `Combat`    | Combat au tour par tour                                               |
| `Event`     | Événement narratif avec choix (style "carte Rencontre")              |

**Transitions :** toutes gérées par `SceneLoader` (singleton DontDestroyOnLoad).
```
SceneLoader.Instance.GoToNavigation()
SceneLoader.Instance.GoToCombat()
SceneLoader.Instance.GoToEvent()
SceneLoader.Instance.GoToMainMenu()
```

---

## Scripts principaux

### Singletons persistants (DontDestroyOnLoad)

**`RunManager.cs`**
- Singleton central — conserve tout l'état de la run entre les scènes
- Contient : HP courants/max, personnage sélectionné, position sur la carte, équipement porté, salles complétées, flags d'événements, état de navigation sauvegardé
- Méthodes clés : `StartNewRun()`, `EnterRoom(CellData)`, `SaveNavigationState()`, `ClearCurrentRoom()`, `EquipItem()`, `GetEquipped()`, `IsSlotFree()`
- Modules : `AddModule()`, `GetModules()`, `HasModule()`
- Consommables : `AddConsumable()`, `RemoveConsumable()`, `GetConsumables()`, `HasConsumableSlotFree()` — slots limités par `maxConsumableSlots` (1–6). Flag `startingConsumablesSeeded` : empêche de redonner les consommables de départ si le joueur vide ses slots en cours de run.

**`SceneLoader.cs`**
- Singleton simple — wraps `SceneManager.LoadScene()`

---

### Scène Navigation

**`NavigationManager.cs`**
- Gère les déplacements (flèches clavier), la visibilité (brouillard de guerre, `visionRange = 1`)
- Trois sets : `visibleCells`, `visitedCells`, `exploredCells`
- En quittant vers Combat/Event : appelle `RunManager.SaveNavigationState()` puis `RunManager.EnterRoom()`
- Au retour d'une autre scène : restaure l'état depuis RunManager si `hasNavigationState == true`

**`MapRenderer.cs`**
- Affiche la carte (tiles) selon les sets de visibilité de NavigationManager

**`MapCameraController.cs`**
- Caméra de la carte

**`MapData.cs`**
- ScriptableObject — grille de `CellData[]`

**`MapData / CellData`**
```csharp
public enum CellType { Start, Classic, Boss, Event, Empty }
// CellData contient : int x, y ; CellType cellType ; string specificEventID
```

---

### Scène Combat

**`CombatManager.cs`** — le plus gros script du projet
- Machine à états : `PlayerTurn → EnemyTurn → Victory/Defeat`
- **Énergie** : rechargée à `effectiveMaxEnergy` chaque début de tour joueur
- **Armure** (style StS) : absorbe les dégâts directs, se remet à 0 au début du tour de l'entité concernée. Ne bloque PAS le poison.
- **Stats effectives** : calculées une fois au démarrage via `ResolveEquipment()` — base CharacterData + bonus de chaque pièce équipée (HP, ATK, DEF, crit, regen, lifesteal)
- **Compétences** : viennent de l'équipement
- **IA ennemie** : file d'actions circulaire gérée par `EnemyAI`
- **Statuts** : `playerStatuses` / `enemyStatuses` (Dictionary<StatusData, int>). Tick via `ProcessPerTurnStatuses()` — joueur au début de son tour, ennemi au début du sien. Si l'ennemi meurt de ses statuts, victoire immédiate.
- **Dégâts** : clampés entre 0 et 9999. Log format : `"X dégâts (dont +Y Statut×N [consommés])"` — le total affiché inclut déjà tous les bonus.
- **Loot** : tirage aléatoire Fisher-Yates dans `EnemyData.lootPool`, affichage de `lootOfferCount` cartes via `LootCard`
- **Consommables en combat** : `SpawnConsumableButtons()` génère les boutons depuis `RunManager.GetConsumables()`. `UseConsumable()` applique l'effet via `ApplyConsumableEffect()` (valeurs brutes — pas de stats ATK/DEF) puis retire le consommable du RunManager et recrée les boutons. `TryGrantConsumableLoot()` accorde automatiquement un consommable depuis `EnemyData.consumableLootPool` si slot libre (appelé dans `ShowLootPanel()`). Boutons bloqués pendant le tour ennemi / fin de combat.
- Champs UI : `consumableButtonContainer` (Transform) + `consumableButtonPrefab` (GameObject) à assigner dans l'Inspector.
- **Fin de combat victoire** : → panel Loot (avec loot consommable auto) → `OnLootContinueClicked()` → sauvegarde HP dans RunManager → `GoToNavigation()`
- **Fin de combat défaite** : → panel End → `GoToMainMenu()`
- Bouton debug "Victoire instantanée" (`victoireButton`) à retirer en production

**`EnemyAI.cs`**
- File d'actions circulaire (`List<SkillData>` de `EnemyData.actions`)
- `GetAndAdvanceAction()` retourne le prochain skill et avance l'index

**`SkillButton.cs`**
- `Setup(SkillData skill, Action<SkillData> callback)`
- Gère le grisage visuel (énergie insuffisante ou cooldown actif)

**`LootCard.cs`**
- `Setup(EquipmentData equip, Action<EquipmentData> callback)`
- `SetSelected(bool)` pour le feedback visuel de sélection

---

### Scène Event

**`EventManager.cs`**
- Lit `RunManager.currentSpecificEventID` → cherche dans `EventDatabase` → affiche titre, description, boutons de choix
- `SpawnChoiceButtons()` : instancie dynamiquement les boutons depuis le prefab
- Après choix : applique les effets (`ModifyHP` implémenté, reste à compléter), remplace la description par `outcomeText`, affiche le bouton "Continuer"
- "Continuer" → `RunManager.ClearCurrentRoom()` → `GoToNavigation()`

**`EventData.cs`**
```csharp
// ScriptableObject
public string eventID, title, description;
public Sprite backgroundImage;
public List<EventChoice> choices;

// EventChoice :
public string choiceText, outcomeText;
public List<EventEffect> effects;

// EventEffect :
public EventEffectType type;  // ModifyHP, ...
public int value;
```

**`EventDatabase.cs`**
- ScriptableObject contenant une `List<EventData>`, avec `GetByID(string id)`

---

### Données (ScriptableObjects)

**`CharacterData.cs`**
- Stats de base : `maxHP`, `attack`, `defense`, `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal`, `maxEnergy`
- `startingModule` : module passif donné au joueur au début de chaque run (peut être null)
- `startingHead/Torso/Legs/Arm1/Arm2` : équipement initial
- `startingConsumables` : consommables donnés au joueur au premier combat du run (une seule fois, gardé par `startingConsumablesSeeded` dans RunManager)

**`EnemyData.cs`**
- `maxHP`, `attack`, `defense`
- `List<EnemyAction> actions` (file de l'IA — `EnemyAction` contient `SkillData skill` + `int maxUses`)
- `List<EquipmentData> lootPool` + `int lootOfferCount`
- `List<ConsumableData> consumableLootPool` — un seul consommable accordé aléatoirement si slot libre

**`SkillData.cs`**
- `skillName`, `energyCost`, `cooldown`, `EffectData effect`

**`EffectData.cs`**
```csharp
public enum EffectAction { DealDamage, Heal, AddArmor, ApplyStatus, ModifyStat, ... }
public EffectAction action;
public float value;           // dégâts / soins / stacks à appliquer
public float secondaryValue;  // bonus de valeur par stack (pour scalingStatus)
public StatusData statusToApply;  // statut à appliquer (si action == ApplyStatus)
public StatusData scalingStatus;  // statut dont les stacks amplifient l'effet (DealDamage)
public bool consumeStacks;        // si true, les stacks du scalingStatus sont retirés après l'effet
```
`DealDamage`, `Heal`, `AddArmor` et `ApplyStatus` sont implémentés. `ModifyStat` reste à faire.
Les dégâts sont clampés entre 0 et 9999 dans `ApplyEffect` et `ApplyEnemyEffect`.

**`EquipmentData.cs`**
- `equipmentName`, `EquipmentSlot slot`
- Bonus stats : `bonusHP`, `bonusAttack`, `bonusDefense`, `bonusCriticalChance`, `bonusCriticalMultiplier`, `bonusRegeneration`, `bonusLifeSteal`
- `List<SkillData> skills`

```csharp
public enum EquipmentSlot { Head, Torso, Legs, Arm1, Arm2 }
```

**`StatusData.cs`**
- `statusID`, `statusName`, `description`, `icon`
- `StatusBehavior behavior` : `StackOnly` (pas d'effet auto) ou `PerTurnStart` (tick au début du tour)
- `EffectAction perTurnAction` + `float effectPerStack` — définissent ce que fait le statut par stack par tour
- `int decayPerTurn` — stacks perdus automatiquement par tour (0 = permanent)
- `int maxStacks` — plafond de stacks (0 = illimité)

**`ModuleData.cs`**
- `moduleID`, `moduleName`, `icon`, `description`, `keywords`
- `EffectData effect` — effet du module (non encore déclenché, en attente de GameEvents)
- `bool isStartingModule` — flag informatif (le lien avec le personnage est sur CharacterData)
- Tracké dans `RunManager.activeModules` pendant la run. Seedé au premier combat via `CombatManager.ResolveEquipment()`

**`ConsumableData.cs`**
- `consumableID`, `consumableName`, `icon`, `description`
- `EffectData effect` — effet appliqué à l'utilisation (via `ApplyConsumableEffect()`, valeurs brutes sans stats de combat)
- `bool usableInCombat`, `bool usableOnMap`

**`KeywordData.cs`** — défini mais pas encore utilisé activement

---

## Prefabs

| Prefab               | Usage                                           |
|---------------------|-------------------------------------------------|
| `ChoiceButton`         | Boutons de choix dans la scène Event                        |
| `SkillButtonPrefab`    | Boutons de compétences dans la scène Combat                 |
| `LootCard`             | Cartes d'équipement affichées après victoire                |
| `ConsumableButton`     | Bouton icône seul (pas de texte — tooltip prévu au survol)  |

### ChoiceButton — configuration importante
- Taille : **360×65 px**
- TextMeshPro avec **auto-sizing activé** (min 10pt, max 22pt), marges internes 8/4 px
- **Ne pas placer d'instance du prefab directement dans la scène** — assigner le prefab asset depuis `Assets/Prefab/` dans le champ `choiceButtonPrefab` de EventManager

### ChoiceContainer (scène Event)
- Ancrage **bas-centre** du ContentPanel (`AnchorMin/Max x=0.5, y=0`)
- `AnchoredPosition.y = 30` (30px du bas)
- Largeur fixe **380px**, hauteur auto (ContentSizeFitter VerticalFit=PreferredSize, HorizontalFit=Unconstrained)
- VerticalLayoutGroup : padding 10px, spacing 12px, `ChildControlWidth=true`, `ChildForceExpandWidth=true`

---

## Conventions de code

- Tous les scripts sont en français (commentaires, logs, noms de méthodes UI)
- Chaque script commence par un bloc `<summary>` expliquant son rôle et la structure de scène recommandée
- Les sections sont séparées par des commentaires `// ---...---`
- Les `Debug.Log` ont des préfixes explicites : `[RunManager]`, `[Combat]`, `[Event]`, etc.
- Préférer `Mathf.Max(1, ...)` pour éviter les dégâts nuls, et `Mathf.Clamp(x, 0, 9999)` pour plafonner
- Les ScriptableObjects ne sont jamais modifiés au runtime — les données variables vont dans RunManager

---

## État du développement (à mettre à jour)

### Fonctionnel ✅
- Système de combat complet (tours, énergie, armure, cooldowns, logs)
- IA ennemie avec file d'actions circulaire
- Équipement : résolution des stats effectives + compétences depuis l'équipement
- Loot post-combat (sélection de carte, gestion des slots bras)
- Navigation carte (brouillard de guerre, déplacement clavier, sauvegarde état)
- Système d'événements narratifs (chargement, choix, effets ModifyHP)
- Transitions entre scènes via SceneLoader + RunManager
- Stats avancées en combat : `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal` (+ bonus d'équipement accumulés dans `ResolveEquipment()`)
- Système de statuts : `StatusData` (ScriptableObject), `ApplyStatus()`, `ProcessPerTurnStatuses()`, stacks avec lecture et/ou consommation par les effets de dégâts (`scalingStatus`, `consumeStacks` dans `EffectData`)
- Modules passifs : `ModuleData` seedé depuis `CharacterData.startingModule` au premier combat, tracké dans `RunManager.activeModules`
- Loot post-combat : bouton "Continuer" toujours actif (le joueur peut ignorer le loot)
- Consommables : `ConsumableData` (ScriptableObject), `ConsumableButton` (UI), système complet en combat (spawn, use, loot auto depuis `EnemyData.consumableLootPool`)

### En cours / À faire 🔧
- Scène MainMenu (sélection de personnage non branchée)
- Effets de combat non implémentés : `ModifyStat`
- Effets d'événements non implémentés : tout sauf `ModifyHP`
- `KeywordData` défini mais non utilisé activement
- `GameEvents.cs` défini mais jamais déclenché (aucun `TriggerXxx()` appelé)
- Consommables côté navigation (hors combat) : `usableOnMap` présent sur `ConsumableData` mais l'intégration dans `NavigationManager` reste à faire
- Boss, salles spéciales
- Sons, animations, retours visuels
- Inventaire (prévu dans RunManager, commentaire `// Futur`)

### Localisation 🌍
- **À implémenter après la première version jouable de bout en bout** (pas avant que le contenu textuel soit stable)
- Utiliser le package officiel **Unity Localization** (`com.unity.localization`)
- Le refactoring consistera à : remplacer les `public string` de texte visible dans les ScriptableObjects par des `LocalizedString`, et ajouter des composants `LocalizeStringEvent` sur les TextMeshPro dans les scènes
- **Règle à tenir dès maintenant** : ne jamais hardcoder du texte visible dans le code C# — tout texte affiché au joueur doit passer par les ScriptableObjects ou les champs Inspector

---

## Pièges connus

1. **ChoiceButton dans la scène** : ne jamais glisser une instance du prefab dans la scène pour la référencer — utiliser le prefab asset directement depuis le dossier `Assets/Prefab/`. Sinon l'instance reste visible au runtime.
2. **RunManager null** : toujours utiliser `RunManager.Instance?.` (null-conditional) car en test direct d'une scène isolée, RunManager peut ne pas exister.
3. **Destroy() asynchrone** : `Destroy(go)` ne prend effet qu'à la fin du frame. Ne pas compter sur une destruction immédiate.
4. **VerticalLayoutGroup + ContentSizeFitter** : si la largeur du container est gérée par le ContentSizeFitter, ne pas activer `ChildControlWidth` en même temps — choisir l'un ou l'autre. Ici on utilise une largeur fixe + ContentSizeFitter vertical uniquement.
5. **Layout Group + taille des enfants** : si les boutons enfants ignorent la taille définie dans le prefab, c'est que `Control Child Size` est coché sur le Layout Group du conteneur. Le décocher (Width et Height) pour respecter la taille du prefab. Le `Content Size Fitter` doit être sur le *conteneur*, jamais sur les enfants si on veut une taille fixe.
