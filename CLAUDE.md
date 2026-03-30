# CLAUDE.md — Contexte du projet "Petit turn based"

> Ce fichier est lu automatiquement par Claude au démarrage de chaque conversation.
> Il contient tout le contexte nécessaire pour reprendre le développement sans perdre l'historique.
> **Mettre à jour ce fichier après chaque session de travail significative.**

---

## Consignes pour Claude

### Validation avant les gros chantiers
- Avant tout chantier impliquant **3 fichiers ou plus**, ou une refonte d'architecture, **demander validation** à Elisyo avec un résumé de ce qui va être modifié et pourquoi — ne jamais partir directement dans l'implémentation.
- Les petits fixes (1–2 fichiers, changement localisé) peuvent être faits directement.

### Niveau de détail attendu dans les explications
- Quand une modification de code est faite, expliquer **pourquoi** (cause du problème, logique du fix) et pas seulement quoi.
- Pour le setup Unity (Inspector, hiérarchie), donner les **noms exacts des champs, composants et valeurs** à saisir — pas de descriptions vagues.
- Quand un setup Unity est demandé ou nécessaire après une modification de code, **toujours détailler les étapes complètes** : nom exact du GameObject à créer, composants à ajouter, valeurs à saisir dans chaque champ, position dans la hiérarchie (parent, enfant de quoi).
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
- Événements joués : `MarkEventPlayed(string)`, `IsEventPlayed(string)` — `HashSet<string> playedEventIDs`, remis à zéro dans `StartNewRun()`
- ⚠️ `EnterRoom(CellData)` ne set plus `currentSpecificEventID` — c'est `NavigationManager` qui l'assigne après le tirage aléatoire

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
- Gère les déplacements (flèches clavier), la visibilité (brouillard de guerre, `baseVisionRange = 1`)
- Portée effective : `baseVisionRange + RunManager.visionRangeBonus` (propriété `PorteeVisionEffective`)
- Trois sets : `visibleCells`, `visitedCells`, `exploredCells`
- En quittant vers Combat/Event : appelle `RunManager.SaveNavigationState()` puis `RunManager.EnterRoom()`
- Au retour d'une autre scène : restaure l'état depuis RunManager si `hasNavigationState == true`
- `ChoisirEventAleatoire(CellData)` : dispatch selon `eventCellMode` (ManualList → `ChoisirDepuisListe()`, FromPool → `EventPool.GetRandom()`). Retourne `null` si tout est joué → salle ignorée sans transition
- ⚠️ Après `EnterRoom()`, le code écrase `RunManager.Instance.currentSpecificEventID` avec l'ID de l'event tiré
- **Effets de navigation** : `AppliquerEffetNav(NavEffect)` / `AppliquerEffetsNav(List<NavEffect>)` — exécute tous les `NavEffectType`
- `TeleporterJoueur(x, y)` — téléportation sans déclencher `OnRoomEntered`
- `RevelerZone(cx, cy, rayon)` — ajoute des cases à `exploredCells` et rafraîchit la carte
- `DemarrerSelectionZone(rayon)` — active le mode clic ; `GererSelectionZone()` convertit le clic écran → coordonnées de case via `RectTransformUtility.ScreenPointToLocalPointInRectangle`
- **UI Navigation** : `RafraichirUINavigation()` — recrée les boutons consommables (`consommableContainer`) et skills jambes (`skillContainer`). Tous les champs UI sont optionnels.
- ⚠️ `baseVisionRange` remplace l'ancien `visionRange` (champ renommé) — penser à re-saisir la valeur dans l'Inspector après la mise à jour du script

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
public enum CellType { Empty, Start, Boss, Classic, Event, NonNavigable }

public enum EventCellMode { ManualList, FromPool }

// CellData contient :
//   int x, y
//   CellType cellType
//   EventCellMode eventCellMode
//   List<EventData> eventList   ← mode ManualList : liste saisie à la main
//   EventPool eventPool         ← mode FromPool : ScriptableObject réutilisable
```

- Les cases de type `Event` utilisent `eventCellMode` pour choisir entre liste manuelle et pool prédéfini
- `NavigationManager.ChoisirEventAleatoire()` dispatch selon le mode, filtre les events déjà joués, retourne `null` si tout est joué (salle ignorée sans transition)
- ⚠️ Éditeur de carte : `MapEditorWindow` (Assets/Editor) — le panneau "Événements" apparaît uniquement pour les cases de type `Event`

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
- **Loot** : tirage aléatoire Fisher-Yates dans `EnemyData.lootPool`, délégué à `EquipmentOfferController.StartOffresSimultanées()`. `lootContinueButton` affiché via `SetActive(true)` dans `ShowLootPanel()` et masqué dans `Start()`.
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
- `SetInteractable(bool)` pour verrouiller la carte après sélection
- `Equipment` (propriété publique) — retourne l'`EquipmentData` associée

**`EquipmentOfferController.cs`** — MonoBehaviour partagé entre Combat et Event
- Gère l'affichage et la résolution des offres d'équipement (LootCards + sélection slot bras)
- Deux modes : `StartOffresSimultanées(List<EquipmentData>, Action)` (combat) et `StartOffresSequentielles(List<EquipmentData>, Action)` (event)
- `Awake()` : branche les listeners skip/bras, désactive `armSelectionPanel`, appelle `gameObject.SetActive(false)`
- **Mode simultané** : toutes les cartes affichées, le joueur en choisit une. Après choix, cartes verrouillées, callback appelé — le GO ne se désactive **pas** (le panel parent gère sa propre fermeture).
- **Mode séquentiel** : une carte à la fois avec bouton "Passer". Quand la file est vide : `gameObject.SetActive(false)` puis callback.
- `OnCardChosen` vérifie `IsSlotFree(Arm1)` puis `IsSlotFree(Arm2)` avant d'afficher `ArmSelectionPanel` — auto-équipement si un slot est libre.
- ⚠️ Laisser `skipButton` non assigné en mode simultané (combat).

Structure du prefab recommandée :
```
EquipmentOfferArea        ← ce composant (EquipmentOfferController)
  ├─ LootCardContainer    (Transform — Horizontal Layout Group)
  ├─ SkipButton           (Button — optionnel, séquentiel uniquement)
  └─ ArmSelectionPanel    (GameObject — désactivé par défaut)
       ├─ Arm1Button      (Button)
       └─ Arm2Button      (Button)
```

⚠️ **Piège** : `EquipmentOfferController.Awake()` désactive son GO. Si `lootContinueButton` (Combat) ou `continueButton` (Event) est **enfant** de ce GO, il sera invisible au démarrage. Placer ces boutons comme **frères** de `EquipmentOfferArea`, pas à l'intérieur.

⚠️ **Piège** : `lootContinueButton` doit être explicitement caché dans `CombatManager.Start()` (`SetActive(false)`) puis réactivé dans `ShowLootPanel()` — sans ça, il est cliquable avant la fin du combat.

---

### Scène Event

**`EventManager.cs`**
- Lit `RunManager.currentSpecificEventID` → cherche dans `EventDatabase` → affiche titre, description, boutons de choix
- `SpawnChoiceButtons()` : instancie dynamiquement les boutons depuis le prefab
- Après choix : applique les effets via `ApplyEffects()`, remplace la description par `outcomeText`, supprime les boutons
- Si des équipements ne peuvent pas être auto-équipés → `pendingEquipmentOffers` (List) → `equipmentOfferController.StartOffresSequentielles(...)` → callback `OnEquipementResolu()`
- Si aucun équipement en attente → `MontrerContinueButton()` directement
- `MontrerContinueButton()` : remonte toute la hiérarchie jusqu'au Canvas et active chaque parent avant d'activer le bouton (protège contre les panels désactivés)
- "Continuer" → `RunManager.MarkEventPlayed(eventID)` → `RunManager.ClearCurrentRoom()` → `GoToNavigation()`
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

// EventEffect — champs affichés conditionnellement via EventEffectDrawer (CustomPropertyDrawer) :
public EventEffectType type;
public int value;                              // ModifyHP, ModifyMaxHP
public GainConsumableMode gainConsumableMode;  // GainConsumable : FromList ou FromLootTable
public List<ConsumableData> consumablesToGive; // GainConsumable → FromList
public ConsumableLootTable consumableLootTable;// GainConsumable → FromLootTable
public GainModuleMode gainModuleMode;          // GainModule : FromList ou FromLootTable
public List<ModuleData> modulesToGive;         // GainModule → FromList (anti-doublon)
public ModuleLootTable moduleLootTable;        // GainModule → FromLootTable
public GainEquipmentMode gainEquipmentMode;    // GainEquipment : FromList ou FromLootTable
public List<EquipmentData> equipmentsToGive;   // GainEquipment → FromList
public EquipmentLootTable equipmentLootTable;  // GainEquipment → FromLootTable
public string flagKey;                         // SetEventFlag
public bool flagValue;                         // SetEventFlag
```

⚠️ **Ne jamais mettre `[Header]` sur des champs d'une classe gérée par un `CustomPropertyDrawer`** — Unity rend les headers dans `OnGUI` mais ne les inclut pas dans `GetPropertyHeight`, causant des décalages/chevauchements dans l'Inspector.

**`EventEffectType` implémentés :**
| Type | Effet | Champ(s) |
|---|---|---|
| `ModifyHP` | Soigne/blesse le joueur (plancher à 1) | `value` |
| `ModifyMaxHP` | Modifie les HP max (plancher à 1, clamp HP courants) | `value` |
| `HealToFull` | Soin complet | — |
| `GainConsumable` | Donne un ou des consommables si slot libre | `gainConsumableMode` + `consumablesToGive` ou `consumableLootTable` |
| `GainModule` | Donne un ou des modules (anti-doublon) | `gainModuleMode` + `modulesToGive` ou `moduleLootTable` |
| `GainEquipment` | Donne un équipement (auto-équipe si slot libre, sinon propose remplacement) | `gainEquipmentMode` + `equipmentsToGive` ou `equipmentLootTable` |
| `SetEventFlag` | Pose un flag booléen dans RunManager | `flagKey`, `flagValue` |

⚠️ **`GainConsumable` plein** : si l'inventaire est plein, l'objet n'est pas donné (log console). À terme, afficher un message et permettre de jeter un consommable manuellement.

⚠️ **`GainEquipment` slot occupé** : passe par `EquipmentOfferController.StartOffresSequentielles` — le joueur voit la carte et peut choisir de remplacer ou passer.

**`EventDatabase.cs`**
- ScriptableObject contenant une `List<EventData>`, avec `GetByID(string id)`

**`EventPool.cs`**
- ScriptableObject réutilisable (`RPG → Event Pool`) — `List<EventData> events`
- `GetRandom()` : exclut les events déjà joués (`RunManager.IsEventPlayed`) et tire au sort
- Retourne `null` si tout a été joué

**`ModuleLootTable.cs`**
- ScriptableObject réutilisable (`RPG → Module Loot Table`) — `List<ModuleData> modules`
- `GetRandom()` : exclut les modules déjà possédés (`RunManager.HasModule`) et tire au sort
- Retourne `null` si tout est possédé

**`ConsumableLootTable.cs`**
- ScriptableObject (`RPG → Consumable Loot Table`) — `List<ConsumableData> consumables`
- `GetRandom()` : tire au sort (pas de filtre "déjà possédé" — les consommables peuvent se cumuler)
- Retourne `null` si la liste est vide

**`EquipmentLootTable.cs`**
- ScriptableObject (`RPG → Equipment Loot Table`) — `List<EquipmentData> equipments`
- `GetRandom()` : tire au sort (pas de filtre — les slots peuvent être remplacés)
- Retourne `null` si la liste est vide

---

### Données (ScriptableObjects)

**`CharacterData.cs`**
- Stats de base : `maxHP`, `attack`, `defense`, `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal`, `maxEnergy`
- `baseVisionRange` : portée de vision de base sur la carte (portée effective = `baseVisionRange + RunManager.visionRangeBonus`)
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

**`EffectDataEditor`** (Assets/Editor) : `CustomEditor` sur `EffectData` — labels contextuels pour `value` et `secondaryValue` selon `action`. `secondaryValue` et `scalingStatus` masqués quand non pertinents (ex : `ApplyStatus`, `AddGold`). `consumeStacks` n'apparaît que si `scalingStatus` est assigné.

**Règles de saisie du `trigger` dans l'Inspector :**
- Compétences / Consommables → laisser sur `None` (valeur par défaut, jamais lu par le code, géré implicitement)
- Modules → choisir le trigger voulu (voir tableau ci-dessous)
- `passiveEffects` d'équipement → `OnFightStart` pour un bonus de début de combat
- ⚠️ `None = 0` est la **nouvelle valeur par défaut** — les assets existants dont le trigger était `OnPlayerTurnStart` (anciennement index 0) afficheront désormais `None` et devront être reconfigurés manuellement

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

**`NavEffect.cs`**
- Classe sérialisable + enum `NavEffectType` — utilisée par consommables, skills de jambes, effets d'événements
- Champs : `type`, `value`, `counterKey`, `allowedCellTypes`
- `NavEffectDrawer` (Assets/Editor) : affiche uniquement les champs pertinents selon le type

| Type | Champ(s) utilisé(s) |
|---|---|
| `TeleportRandom` | `allowedCellTypes` (si vide : toutes les cases navigables) |
| `RevealZoneRandom` | `value` (rayon) |
| `RevealZoneChoice` | `value` (rayon) — attend un clic du joueur |
| `IncreaseVisionRange` | `value` (delta permanent) |
| `IncrementCounter` | `counterKey` + `value` (delta, peut être négatif) |

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
- Transitions entre scènes via SceneLoader + RunManager
- Stats avancées en combat : `criticalChance`, `criticalMultiplier`, `regeneration`, `lifeSteal`
- Système de statuts complet avec scaling et consommation de stacks
- **Modules** : système complet — triggers via GameEvents, effets (DealDamage, Heal, AddArmor, ApplyStatus), HUD icônes dans Combat et Navigation, `OnFightStart` après reset d'armure
- Loot post-combat : bouton "Continuer" toujours actif
- Consommables : système complet en combat
- `ApplyStatus` respecte `EffectTarget` dans toutes les méthodes (joueur peut s'appliquer un statut à lui-même)
- HUD Navigation : affichage HP joueur
- **Événements narratifs** : système complet — effets `ModifyHP`, `ModifyMaxHP`, `HealToFull`, `GainConsumable`, `GainModule` (liste ou loot table, anti-doublon), `SetEventFlag`
- **Pool d'events par salle** : cases Event utilisent `EventCellMode` (ManualList ou FromPool via `EventPool` ScriptableObject). Events déjà joués exclus du tirage. Salle ignorée si tout est joué.
- **`ModuleLootTable`** : ScriptableObject pour tirer un module aléatoire non encore possédé
- **`EventPool`** : ScriptableObject pour tirer un event aléatoire non encore joué
- Éditeur de carte (`MapEditorWindow`) : gestion des deux modes de pool d'events par case
- **`ConsumableLootTable` / `EquipmentLootTable`** : ScriptableObjects pour loot tables de consommables et d'équipements
- **`GainEquipment`** dans `EventEffectType` : donne un équipement depuis une liste ou une loot table, avec auto-équipement si slot libre et proposition de remplacement sinon
- **`EquipmentOfferController`** : logique d'offre d'équipement factorisée, partagée entre Combat (mode simultané) et Event (mode séquentiel)
- **`EventEffectDrawer`** (`Assets/Editor`) : `CustomPropertyDrawer` sur `EventEffect` — masque les champs inutiles selon le type et le mode sélectionné dans l'Inspector
- **Effets de navigation** : système complet — `NavEffectType` (TeleportRandom, RevealZoneRandom, RevealZoneChoice, IncreaseVisionRange, IncrementCounter), `NavEffect` (classe sérialisable), `NavEffectDrawer` (Inspector conditionnel). Branchés sur : consommables (`mapEffects`), skills de jambes (`isNavigationSkill` + `navEffects`), événements (`TriggerNavEffect`). `NavigationManager` exécute tout via `AppliquerEffetsNav()`. `RevealZoneChoice` bloque la navigation clavier et attend un clic sur la carte. Compteurs nommés dans `RunManager` (`IncrementCounter`, `GetCounter`, `SetCounter`). Bonus de vision via `RunManager.visionRangeBonus` (permanent pour le run).
- **Skills de navigation exclus du combat** : `CombatManager.SpawnSkillButtons()` filtre les skills avec `isNavigationSkill = true` — ils n'apparaissent jamais en combat.
- **`EffectTrigger.None`** : valeur par défaut (= 0) dans l'enum — les compétences et consommables laissent le trigger sur `None`, jamais lu par le code (géré implicitement).
- **`EffectDataEditor`** (`Assets/Editor`) : `CustomEditor` sur `EffectData` — labels contextuels pour `value`/`secondaryValue` selon `action`, champs non pertinents masqués.
- **`baseVisionRange` dans `CharacterData`** : portée de vision de base par personnage. `NavigationManager` lit `characterData.baseVisionRange` + `RunManager.visionRangeBonus` via la propriété `PorteeVisionEffective`. Champ `CharacterData characterData` à assigner dans l'Inspector de la scène Navigation.
- Cases Event affichées en orange sur la carte (couleur configurable via `colorEvent` dans `MapRenderer`).

### En cours / À faire 🔧
- **Scène MainMenu** (priorité avant toute autre fonctionnalité) : scène de démarrage obligatoire qui initialise correctement la run avant d'entrer en Navigation. Doit appeler `RunManager.StartNewRun(characterID, missionID)` et `SceneLoader.Instance.GoToNavigation()`. Sans elle, tester depuis la scène Navigation ne charge pas l'équipement de départ (les skills de jambes, modules, consommables de départ ne sont seedés que dans `CombatManager.ResolveEquipment()` — à terme, une partie du seeding devra être faite dès le MainMenu pour que la Navigation soit correcte dès le premier lancement).
- `passiveEffects` sur `EquipmentData` : champ défini mais non branché dans `CombatManager` — à implémenter comme les modules (triggers + `ApplyModuleEffect`)
- Effets de combat non implémentés : `ModifyStat`
- `KeywordData` défini mais non utilisé activement
- **Cooldown des skills de navigation** : non géré hors combat pour l'instant (pas d'état de tour). À implémenter si besoin.
- Boss, salles spéciales
- **Icônes de cases (graphique)** : permettre d'assigner une image/sprite par type de case (`CellType`) directement dans l'Inspector du `MapRenderer`, au lieu de simples couleurs. Chaque case afficherait son sprite quand découverte. À faire après le boss, avant sons/animations.
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
9. **GUILayout Begin/End dans les boucles Editor** : tout `EditorGUILayout.BeginHorizontal()` doit avoir son `EndHorizontal()` appelé dans **tous** les chemins d'exécution, y compris avant un `break`. Sinon : `GUI Error: Invalid GUILayout state`.
10. **`[Header]` dans un `CustomPropertyDrawer`** : Unity rend les attributs `[Header]` dans `OnGUI` mais ne les inclut pas dans `GetPropertyHeight`. Résultat : les champs se chevauchent. Ne jamais mettre `[Header]` sur des champs d'une classe gérée par un `CustomPropertyDrawer`.
11. **`SetActive(true)` sur un enfant d'un GO inactif** : l'enfant ne devient pas visible — il faut activer tous les parents jusqu'au Canvas d'abord. `MontrerContinueButton()` dans `EventManager` illustre ce pattern (boucle sur `transform.parent` jusqu'au Canvas).
12. **`interactable = true` ≠ visible** : sur un `Button`, `interactable = true` ne rend pas le GO actif. Toujours faire `gameObject.SetActive(true)` en plus si le GO peut être inactif.
13. **`EquipmentOfferController` + bouton "Continuer"** : le controller se désactive lui-même via `Awake()`. Tout bouton "Continuer" doit être **frère** du GO `EquipmentOfferArea`, jamais enfant — sinon il disparaît avec le controller. En Combat, le `lootContinueButton` doit aussi être explicitement masqué dans `Start()` (il serait cliquable pendant le combat sinon).
14. **`RevealZoneChoice` et clics simultanés** : en mode sélection de zone (`modeSelectionZone = true`), `Update()` bloque les déplacements clavier mais **pas** les clics sur les boutons de l'UI (consommables, skills). Un clic sur un bouton UI passe **aussi** par `Input.GetMouseButtonDown(0)`. Pour éviter de consommer involontairement le clic du bouton comme sélection de zone, utiliser `EventSystem.current.IsPointerOverGameObject()` avant de traiter le clic — à ajouter si des conflits sont observés.
15. **`visionRange` renommé en `baseVisionRange`** : l'ancien champ public `visionRange` est maintenant `[SerializeField] private int baseVisionRange`. La valeur Inspector est réinitialisée à sa valeur par défaut (`1`) après la mise à jour du script — penser à la re-saisir manuellement dans la scène Navigation.
