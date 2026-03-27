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
- **Stats effectives** : calculées une fois au démarrage via `ResolveEquipment()` — base CharacterData + bonus de chaque pièce équipée
- **Compétences** : viennent de l'équipement (fallback : `CharacterData.startingSkills`)
- **IA ennemie** : file d'actions circulaire gérée par `EnemyAI`
- **Loot** : tirage aléatoire Fisher-Yates dans `EnemyData.lootPool`, affichage de `lootOfferCount` cartes via `LootCard`
- **Fin de combat victoire** : → panel Loot → `OnLootContinueClicked()` → sauvegarde HP dans RunManager → `GoToNavigation()`
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
- `startingSkills` : fallback si pas d'équipement
- `startingHead/Torso/Legs/Arm1/Arm2` : équipement initial

**`EnemyData.cs`**
- `maxHP`, `attack`, `defense`
- `List<SkillData> actions` (file de l'IA)
- `List<EquipmentData> lootPool` + `int lootOfferCount`

**`SkillData.cs`**
- `skillName`, `energyCost`, `cooldown`, `EffectData effect`

**`EffectData.cs`**
```csharp
public enum EffectAction { DealDamage, Heal, AddArmor, ApplyStatus, ModifyStat }
public EffectAction action;
public float value;
```
Seuls `DealDamage`, `Heal` et `AddArmor` sont implémentés dans CombatManager.

**`EquipmentData.cs`**
- `equipmentName`, `EquipmentSlot slot`
- Bonus : `bonusHP`, `bonusAttack`, `bonusDefense`
- `List<SkillData> skills`

```csharp
public enum EquipmentSlot { Head, Torso, Legs, Arm1, Arm2 }
```

**`KeywordData.cs`**, **`ModuleData.cs`** — définis mais pas encore utilisés activement

---

## Prefabs

| Prefab               | Usage                                           |
|---------------------|-------------------------------------------------|
| `ChoiceButton`      | Boutons de choix dans la scène Event            |
| `SkillButtonPrefab` | Boutons de compétences dans la scène Combat      |
| `LootCard`          | Cartes d'équipement affichées après victoire     |

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
- Préférer `Mathf.Max(1, ...)` pour éviter les dégâts nuls
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

### En cours / À faire 🔧
- Scène MainMenu (sélection de personnage non branchée)
- Effets de combat non implémentés : `ApplyStatus`, `ModifyStat`
- Effets d'événements non implémentés : tout sauf `ModifyHP`
- `KeywordData` et `ModuleData` définis mais non utilisés
- Stats avancées non exploitées en combat : `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal`
- Boss, salles spéciales
- Sons, animations, retours visuels

---

## Pièges connus

1. **ChoiceButton dans la scène** : ne jamais glisser une instance du prefab dans la scène pour la référencer — utiliser le prefab asset directement depuis le dossier `Assets/Prefab/`. Sinon l'instance reste visible au runtime.
2. **RunManager null** : toujours utiliser `RunManager.Instance?.` (null-conditional) car en test direct d'une scène isolée, RunManager peut ne pas exister.
3. **Destroy() asynchrone** : `Destroy(go)` ne prend effet qu'à la fin du frame. Ne pas compter sur une destruction immédiate.
4. **VerticalLayoutGroup + ContentSizeFitter** : si la largeur du container est gérée par le ContentSizeFitter, ne pas activer `ChildControlWidth` en même temps — choisir l'un ou l'autre. Ici on utilise une largeur fixe + ContentSizeFitter vertical uniquement.
