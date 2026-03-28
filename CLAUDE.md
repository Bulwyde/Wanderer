# CLAUDE.md — Contexte du projet "Petit turn based"

> Ce fichier est lu automatiquement par Claude au démarrage de chaque conversation.
> Il contient tout le contexte nécessaire pour reprendre le développement sans perdre l'historique.
> **Mettre à jour ce fichier après chaque session de travail significative.**

---

## Consignes pour Claude

### Niveau de détail attendu dans les explications
- Quand une modification de code est faite, expliquer **pourquoi** (cause du problème, logique du fix) et pas seulement quoi.
- Pour le setup Unity (Inspector, hiérarchie), donner les **noms exacts des champs, composants et valeurs** à saisir — pas de descriptions vagues.
- Quand quelque chose ne fonctionne pas, diagnostiquer la **cause racine** avant de proposer un fix.
- Signaler les **pièges Unity spécifiques** au contexte (DontDestroyOnLoad, événements statiques vs instance, ordre d'initialisation, etc.).
- Ne pas hardcoder de caractères Unicode spéciaux (emoji, symboles) dans les textes TMP — utiliser des mots ou une `Image` séparée.
- Utiliser `FindFirstObjectByType<T>()` (pas l'ancienne `FindObjectOfType<T>()` dépréciée).

### Conventions de code
- Tous les scripts sont en **français** (commentaires, logs, noms de méthodes UI)
- Chaque script commence par un bloc `<summary>` expliquant son rôle et la structure de scène recommandée
- Les sections sont séparées par des commentaires `// ---...---`
- Les `Debug.Log` ont des préfixes explicites : `[RunManager]`, `[Combat]`, `[Module]`, `[Event]`, etc.
- Préférer `Mathf.Max(1, ...)` pour éviter les dégâts nuls, et `Mathf.Clamp(x, 0, 9999)` pour plafonner
- Les ScriptableObjects ne sont **jamais modifiés au runtime** — les données variables vont dans RunManager

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
- Modules : `AddModule()`, `GetModules()`, `HasModule()` — `AddModule()` appelle automatiquement `ModuleManager.NotifyModulesChanged()` pour rafraîchir le HUD
- Consommables : `AddConsumable()`, `RemoveConsumable()`, `GetConsumables()`, `HasConsumableSlotFree()` — slots limités par `maxConsumableSlots` (1–6). Flag `startingConsumablesSeeded` : empêche de redonner les consommables de départ si le joueur vide ses slots en cours de run.

**`SceneLoader.cs`**
- Singleton simple — wraps `SceneManager.LoadScene()`

**`ModuleManager.cs`**
- Singleton DontDestroyOnLoad — **doit être placé dans la scène Navigation** (scène de démarrage)
- S'abonne aux `GameEvents` et déclenche les effets des modules actifs selon leur `EffectData.trigger`
- `ApplyModulesWithTrigger(EffectTrigger trigger)` — méthode publique appelée aussi par `CombatManager`
- Événement statique `OnModulesChanged` — écouté par les `ModuleHUDManager` dans chaque scène
- `NotifyModulesChanged()` — méthode statique (fonctionne sans instance) pour rafraîchir le HUD
- ⚠️ **Piège** : `OnModulesChanged` étant statique, le HUD s'affiche même sans instance de `ModuleManager` dans la scène. Mais sans instance, les `GameEvents` ne sont pas écoutés et les effets ne se déclenchent pas.

---

### Scène Navigation

**`NavigationManager.cs`**
- Gère les déplacements (flèches clavier), la visibilité (brouillard de guerre, `visionRange = 1`)
- Trois sets : `visibleCells`, `visitedCells`, `exploredCells`
- En quittant vers Combat/Event : appelle `RunManager.SaveNavigationState()` puis `RunManager.EnterRoom()`
- Au retour d'une autre scène : restaure l'état depuis RunManager si `hasNavigationState == true`

**`MapRenderer.cs`**
- Affiche la carte (tiles) selon les sets de visibilité de NavigationManager
- La carte est dans un `mapContainer` (RectTransform) déplacé/zoomé par `MapCameraController`

**`MapCameraController.cs`**
- Déplace/zoome le `mapContainer` (RectTransform) — ce n'est **pas** une vraie caméra Unity
- ⚠️ Tout élément d'interface fixe à l'écran doit être **enfant direct du Canvas**, pas du `mapContainer`

**`NavigationHUD.cs`**
- Affiche les HP du joueur (`HP : X / Y`) dans la scène Navigation
- Mis à jour chaque frame via `Update()` → `RefreshHP()`
- Doit être placé sous le Canvas (pas sous `mapContainer`)

**`ModuleHUDManager.cs`** (présent dans Navigation et Combat)
- Instancie les icônes de modules depuis le prefab `ModuleIcon`
- S'abonne à `ModuleManager.OnModulesChanged` pour rafraîchir automatiquement
- Champs Inspector : `moduleIconContainer` (Transform) + `moduleIconPrefab` (GameObject)

**`MapData.cs`**
- ScriptableObject — grille de `CellData[]`

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
- **`ApplyStatus(StatusData, int stacks, bool toEnemy)`** : utilisé par `ApplyEffect`, `ApplyEnemyEffect`, `ApplyConsumableEffect`, `ApplyModuleEffect`. Le paramètre `toEnemy` est maintenant déterminé par `effect.target` dans toutes les méthodes (plus hardcodé).
  - `ApplyEffect` (skill joueur) : `toEnemy = effect.target != EffectTarget.Self`
  - `ApplyEnemyEffect` (skill ennemi) : `toEnemy = effect.target == EffectTarget.Self` (logique inversée — "Self" pour l'ennemi = l'ennemi lui-même)
  - `ApplyConsumableEffect` : même logique que `ApplyEffect`
- **Loot** : tirage aléatoire Fisher-Yates dans `EnemyData.lootPool`, affichage de `lootOfferCount` cartes via `LootCard`
- **Consommables en combat** : `SpawnConsumableButtons()` génère les boutons depuis `RunManager.GetConsumables()`. Boutons bloqués pendant le tour ennemi / fin de combat.
- **Modules en combat** :
  - `isFirstTurn` (bool) : vrai jusqu'au premier `StartPlayerTurn()`. Permet d'appliquer les modules `OnFightStart` **après** le reset d'armure initial.
  - `ApplyModuleEffect(EffectData, string moduleName)` : méthode publique appelée par `ModuleManager`. Respecte `effect.target` (Self → joueur, sinon ennemi). Dégâts bruts (pas d'ATK ajoutée).
- **GameEvents déclenchés** :
  - `TriggerPlayerTurnStarted()` → fin de `StartPlayerTurn()`
  - `TriggerPlayerTurnEnded()` → début de `OnEndTurn()`
  - `TriggerPlayerDealtDamage(hpDamage)` → dans `ApplyEffect` DealDamage
  - `TriggerPlayerDamaged(hpDamage)` → dans `ApplyEnemyEffect` DealDamage
  - `TriggerEnemyDied()` → dans `EndCombat(victory: true)`
- **Fin de combat victoire** : `TriggerEnemyDied()` → panel Loot → `OnLootContinueClicked()` → sauvegarde HP dans RunManager → `GoToNavigation()`
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
- ⚠️ Système d'effets propre (`EventEffect` / `EventEffectType`) — indépendant de `EffectData`

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
- `startingModule` : module donné au joueur au début de chaque run (seedé dans `ResolveEquipment()`)
- `startingHead/Torso/Legs/Arm1/Arm2` : équipement initial
- `startingConsumables` : consommables donnés au joueur au premier combat du run (une seule fois)

**`EnemyData.cs`**
- `maxHP`, `attack`, `defense`
- `List<EnemyAction> actions` (file de l'IA — `EnemyAction` contient `SkillData skill` + `int maxUses`)
- `List<EquipmentData> lootPool` + `int lootOfferCount`
- `List<ConsumableData> consumableLootPool` — un seul consommable accordé aléatoirement si slot libre

**`SkillData.cs`**
- `skillName`, `energyCost`, `cooldown`, `EffectData effect`

**`EffectData.cs`**
- Utilisé par : compétences, consommables, modules, passiveEffects d'équipement
- Le **trigger** et la **cible** sont définis ici — source unique de vérité pour tout ce qui concerne l'effet
```csharp
public EffectTrigger trigger;  // Quand l'effet se déclenche
public EffectAction  action;   // Ce que fait l'effet
public EffectTarget  target;   // Sur qui (Self = joueur, SingleEnemy = ennemi, etc.)
public float value;
public float secondaryValue;       // Bonus par stack du scalingStatus
public StatusData statusToApply;   // Pour ApplyStatus
public StatusData scalingStatus;   // Statut dont les stacks amplifient l'effet
public bool consumeStacks;
```

**Règles de saisie du `trigger` dans l'Inspector :**
- Compétences / Consommables → laisser sur `OnSkillUsed` (jamais lu par le code, géré implicitement)
- Modules → choisir le trigger voulu (voir tableau ci-dessous)
- `passiveEffects` d'équipement → `OnFightStart` pour un bonus de début de combat

| Trigger | Quand | Contexte |
|---|---|---|
| `OnFightStart` | Premier tour du combat, après le reset d'armure | Armure de départ, statut initial, soin d'entrée |
| `OnPlayerTurnStart` | Début de chaque tour joueur | Dégâts/soins récurrents |
| `OnPlayerTurnEnd` | Fin du tour joueur | Effets de fin de tour |
| `OnPlayerDamaged` | Quand le joueur reçoit des dégâts | Contre-attaque, bouclier réactif |
| `OnPlayerDealtDamage` | Quand le joueur inflige des dégâts | Bonus sur attaque |
| `OnEnemyDied` | Mort de l'ennemi (victoire) | Soin post-combat, bonus de run |
| `OnSkillUsed` | À l'utilisation d'une compétence | Usage compétences/consommables |

**`EffectAction` implémentés :** `DealDamage`, `Heal`, `AddArmor`, `ApplyStatus`. `ModifyStat` reste à faire.

**`EquipmentData.cs`**
- `equipmentName`, `EquipmentSlot slot`
- Bonus stats : `bonusHP`, `bonusAttack`, `bonusDefense`, `bonusCriticalChance`, `bonusCriticalMultiplier`, `bonusRegeneration`, `bonusLifeSteal`
- `List<SkillData> skills`
- `List<EffectData> passiveEffects` — **défini mais pas encore branché dans CombatManager** (prévu : effets passifs déclenchés par trigger, comme les modules mais portés par l'équipement)

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
- `moduleID`, `moduleName`, `icon`, `description`, `keywords`, `tags`
- `EffectData effect` — le trigger, l'action et la cible sont **tous sur l'EffectData** (pas de champ trigger sur ModuleData)
- Tracké dans `RunManager.activeModules`. Seedé depuis `CharacterData.startingModule` au premier combat via `CombatManager.ResolveEquipment()`
- Pour assigner un module de départ : le glisser dans `CharacterData.startingModule` (pas de flag `isStartingModule` sur ModuleData — retiré car redondant)

**`ConsumableData.cs`**
- `consumableID`, `consumableName`, `icon`, `description`
- `EffectData effect` — valeurs brutes à l'utilisation (sans stats ATK/DEF)
- `bool usableInCombat`, `bool usableOnMap`

**`KeywordData.cs`** — défini mais pas encore utilisé activement

---

## Prefabs

| Prefab               | Usage                                           |
|---------------------|-------------------------------------------------|
| `ChoiceButton`         | Boutons de choix dans la scène Event            |
| `SkillButtonPrefab`    | Boutons de compétences dans la scène Combat     |
| `LootCard`             | Cartes d'équipement affichées après victoire    |
| `ConsumableButton`     | Bouton icône (tooltip prévu au survol)          |
| `ModuleIcon`           | Icône de module dans le HUD (48×48 px, Image + script `ModuleIcon`) |

### ChoiceButton — configuration importante
- Taille : **360×65 px**
- TextMeshPro avec **auto-sizing activé** (min 10pt, max 22pt), marges internes 8/4 px
- **Ne pas placer d'instance du prefab directement dans la scène** — assigner le prefab asset depuis `Assets/Prefab/`

### ChoiceContainer (scène Event)
- Ancrage **bas-centre** du ContentPanel (`AnchorMin/Max x=0.5, y=0`)
- `AnchoredPosition.y = 30` (30px du bas)
- Largeur fixe **380px**, hauteur auto (ContentSizeFitter VerticalFit=PreferredSize, HorizontalFit=Unconstrained)
- VerticalLayoutGroup : padding 10px, spacing 12px, `ChildControlWidth=true`, `ChildForceExpandWidth=true`

### ModuleHUD — configuration (à répéter dans Navigation et Combat)
```
Canvas
├── mapContainer / CombatActivePanel   ← contenu de la scène (bouge / s'active)
└── ModuleHUD                          ← enfant DIRECT du Canvas, jamais du mapContainer
    └── ModuleIconContainer            ← HorizontalLayoutGroup, spacing 6, Child Control Size W+H : true, Force Expand : false
                                          + ContentSizeFitter (Horizontal + Vertical = Preferred Size)
```
- `ModuleHUDManager` sur le parent `ModuleHUD`, champs : `moduleIconContainer` + `moduleIconPrefab`

### NavigationHUD — configuration
```
Canvas
└── NavigationHUD                      ← enfant direct du Canvas, ancré haut-droite
    └── HPText                         ← TextMeshPro, affiche "HP : X / Y"
```
- `NavigationHUD` script sur le parent, champ : `hpText`

---

## Bus d'événements — GameEvents.cs

Événements déclenchés à ce jour :

| Méthode Trigger | Appelée dans | Écoutée par |
|---|---|---|
| `TriggerPlayerTurnStarted()` | `CombatManager.StartPlayerTurn()` | `ModuleManager` → `OnPlayerTurnStart` |
| `TriggerPlayerTurnEnded()` | `CombatManager.OnEndTurn()` | `ModuleManager` → `OnPlayerTurnEnd` |
| `TriggerPlayerDealtDamage(dmg)` | `CombatManager.ApplyEffect()` | `ModuleManager` → `OnPlayerDealtDamage` |
| `TriggerPlayerDamaged(dmg)` | `CombatManager.ApplyEnemyEffect()` | `ModuleManager` → `OnPlayerDamaged` |
| `TriggerEnemyDied()` | `CombatManager.EndCombat(victory)` | `ModuleManager` → `OnEnemyDied` |

`OnRoomEntered`, `OnChestOpened`, `OnShopEntered` sont définis dans `GameEvents` mais pas encore écoutés par les modules.

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
- Stats avancées en combat : `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal`
- Système de statuts complet avec scaling et consommation de stacks
- **Modules** : système complet — triggers via GameEvents, effets (DealDamage, Heal, AddArmor, ApplyStatus), HUD icônes dans Combat et Navigation, `OnFightStart` après reset d'armure
- Loot post-combat : bouton "Continuer" toujours actif
- Consommables : système complet en combat
- `ApplyStatus` respecte `EffectTarget` dans toutes les méthodes (joueur peut s'appliquer un statut à lui-même)
- HUD Navigation : affichage HP joueur

### En cours / À faire 🔧
- `passiveEffects` sur `EquipmentData` : champ défini mais non branché dans `CombatManager` — à implémenter comme les modules (triggers + `ApplyModuleEffect`)
- Scène MainMenu (sélection de personnage non branchée)
- Effets de combat non implémentés : `ModifyStat`
- Effets d'événements non implémentés : tout sauf `ModifyHP`
- `KeywordData` défini mais non utilisé activement
- Consommables côté navigation (hors combat) : `usableOnMap` présent mais non intégré dans `NavigationManager`
- Boss, salles spéciales
- Sons, animations, retours visuels
- Inventaire (prévu dans RunManager, commentaire `// Futur`)

### Localisation 🌍
- **À implémenter après la première version jouable de bout en bout**
- Utiliser le package officiel **Unity Localization** (`com.unity.localization`)
- Règle immédiate : ne jamais hardcoder du texte visible dans le code C# — tout passe par les ScriptableObjects ou les champs Inspector

---

## Pièges connus

1. **Prefab dans la scène** : ne jamais glisser une instance de prefab dans la scène pour la référencer — utiliser le prefab asset depuis `Assets/Prefab/`. Sinon l'instance reste visible au runtime.
2. **RunManager / ModuleManager null** : toujours utiliser `Instance?.` (null-conditional) — en test direct d'une scène isolée, les singletons peuvent ne pas exister.
3. **Destroy() asynchrone** : `Destroy(go)` ne prend effet qu'à la fin du frame.
4. **VerticalLayoutGroup + ContentSizeFitter** : largeur fixe + ContentSizeFitter vertical uniquement. Ne pas activer `ChildControlWidth` si la largeur est fixe — choisir l'un ou l'autre.
5. **Layout Group + taille des enfants** : si les enfants ignorent la taille du prefab, `Control Child Size` est coché sur le conteneur. Le décocher pour respecter la taille du prefab.
6. **Éléments HUD en Navigation** : le `MapCameraController` déplace `mapContainer`. Tout élément fixe à l'écran doit être **enfant direct du Canvas**, jamais de `mapContainer`.
7. **ModuleManager — événement statique vs instance** : `OnModulesChanged` est statique → le HUD fonctionne sans instance. Mais les abonnements aux `GameEvents` nécessitent une instance → sans `ModuleManager` dans la scène, les effets ne se déclenchent pas même si les icônes s'affichent.
8. **OnFightStart et reset d'armure** : les modules `OnFightStart` sont appliqués dans `StartPlayerTurn()` après `currentPlayerArmor = 0`, grâce au flag `isFirstTurn`. Ne pas déplacer cet appel avant le reset.
