# CLAUDE.md — Contexte du projet "Petit turn based"

> Ce fichier est lu automatiquement par Claude au démarrage de chaque conversation.
> Il contient tout le contexte nécessaire pour reprendre le développement sans perdre l'historique.
> **Mettre à jour ce fichier après chaque session de travail significative.**

> **Stratégie de lecture :** Ce fichier ne duplique pas les détails d'implémentation déjà dans le code (signatures, champs, valeurs enum). Pour ces détails, Claude lit directement les scripts concernés. Ce fichier se concentre sur : conventions, architecture, décisions de design, pièges et état du dev.

---

## Consignes pour Claude

### Validation avant les gros chantiers
- Avant tout chantier impliquant **3 fichiers ou plus**, ou une refonte d'architecture, **demander validation** à Elisyo avec un résumé de ce qui va être modifié et pourquoi — ne jamais partir directement dans l'implémentation.
- Les petits fixes (1–2 fichiers, changement localisé) peuvent être faits directement.

### Niveau de détail attendu dans les explications
- Expliquer **pourquoi** (cause du problème, logique du fix) et pas seulement quoi.
- Pour le setup Unity (Inspector, hiérarchie), donner les **noms exacts des champs, composants et valeurs** — pas de descriptions vagues.
- Quand un setup Unity est nécessaire, **toujours détailler les étapes complètes** : nom exact du GameObject, composants à ajouter, valeurs, position dans la hiérarchie.
- Diagnostiquer la **cause racine** avant de proposer un fix.
- Signaler les **pièges Unity spécifiques** au contexte.
- Ne pas hardcoder de caractères Unicode spéciaux (emoji, symboles) dans les textes TMP.
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

**Moteur :** Unity (version LTS récente) | **Langage :** C# | **UI :** Unity UI (Canvas) + TextMeshPro | **Git :** dépôt à la racine

---

## Architecture des scènes

| Scène | Rôle |
|---|---|
| `MainMenu` | Écran d'accueil / sélection personnage (non développé) |
| `Navigation` | Carte du donjon — déplacement case par case |
| `Combat` | Combat au tour par tour |
| `Event` | Événement narratif avec choix |

**Transitions :** toutes via `SceneLoader` (singleton DontDestroyOnLoad) — `GoToNavigation()`, `GoToCombat()`, `GoToEvent()`, `GoToMainMenu()`.

---

## Scripts principaux

### Singletons persistants (DontDestroyOnLoad)

**`RunManager.cs`** — État central de la run (HP, personnage, position, équipement, modules, consommables, flags d'events, état nav sauvegardé). `AddModule()` appelle automatiquement `ModuleManager.NotifyModulesChanged()`.
- ⚠️ `EnterRoom(CellData)` ne set plus `currentSpecificEventID` — c'est `NavigationManager` qui l'assigne après le tirage aléatoire.

**`SceneLoader.cs`** — Singleton simple, wrappe `SceneManager.LoadScene()`.

**`ModuleManager.cs`** — Singleton DontDestroyOnLoad, **doit être dans la scène Navigation** (scène de démarrage). S'abonne aux `GameEvents`, déclenche les effets des modules. `OnModulesChanged` est statique — le HUD fonctionne sans instance, mais les effets ne se déclenchent pas sans instance.

---

### Scène Navigation

**`NavigationManager.cs`** — Déplacements clavier, brouillard de guerre, 3 sets (`visibleCells`, `visitedCells`, `exploredCells`). Portée effective = `baseVisionRange + RunManager.visionRangeBonus`. Gère les effets de navigation (`AppliquerEffetsNav`), la sélection de zone (`RevealZoneChoice`), et le tirage d'events aléatoires (`ChoisirEventAleatoire` avec fallback `RandomEvents`).
- ⚠️ `baseVisionRange` remplace l'ancien `visionRange` — re-saisir la valeur dans l'Inspector après mise à jour du script.

**`MapRenderer.cs`** — Affiche les tiles selon les sets de visibilité. Cases `NonNavigable` toujours `Color.clear`. Gère le preview hover `RevealZoneChoice` via `PreviewZone()` / `ClearPreview()` / `RefreshSingleCell()`.

**`MapCameraController.cs`** — Déplace/zoome le `mapContainer` (RectTransform). ⚠️ Tout élément HUD fixe doit être **enfant direct du Canvas**, jamais du `mapContainer`.

**`NavigationHUD.cs`** — Affiche HP joueur, mis à jour chaque frame. Enfant direct du Canvas.

**`MapData.cs`** — ScriptableObject grille. Cases `Event` utilisent `EventCellMode` (ManualList ou FromPool). Case par défaut : `NonNavigable`. Helper `AUnVoisinNavigable(x, y)`.

---

### Scène Combat

**`CombatManager.cs`** — Machine à états `PlayerTurn → EnemyTurn → Victory/Defeat`. Stats effectives calculées une fois au démarrage via `ResolveEquipment()`. Armure style StS (reset début de tour, ne bloque pas le poison). Modules `OnFightStart` appliqués après le reset d'armure initial (flag `isFirstTurn`). Loot délégué à `EquipmentOfferController.StartOffresSimultanées()`.
- `ApplyStatus` : `toEnemy` déterminé par `effect.target` dans toutes les méthodes (plus hardcodé).
- Consommables : `SetInteractable` respecte `usableInCombat` y compris à la réactivation en début de tour.
- `victoireButton` (debug) à retirer en production.

**`EnemyAI.cs`** — File d'actions circulaire.

**`EquipmentOfferController.cs`** — Partagé entre Combat (mode simultané) et Event (mode séquentiel). `Awake()` désactive son propre GO.
- ⚠️ Les boutons "Continuer" doivent être **frères** de ce GO, jamais enfants.
- ⚠️ `lootContinueButton` doit être masqué dans `CombatManager.Start()`.

---

### Scène Event

**`EventManager.cs`** — Lit `RunManager.currentSpecificEventID`, cherche dans `EventDatabase`, affiche titre/description/boutons. Après choix : applique effets, affiche `outcomeText`. Si équipement en attente → `EquipmentOfferController.StartOffresSequentielles()`. `MontrerContinueButton()` remonte toute la hiérarchie pour activer les parents avant d'activer le bouton.
- ⚠️ Système d'effets propre (`EventEffect` / `EventEffectType`) — indépendant de `EffectData`.
- ⚠️ Ne jamais mettre `[Header]` sur des champs gérés par un `CustomPropertyDrawer`.

---

### Données (ScriptableObjects) — pour les champs détaillés, lire les scripts

| Script | Rôle |
|---|---|
| `CharacterData` | Stats de base, équipement/module/consommables de départ, `baseVisionRange` |
| `EnemyData` | Stats ennemis, file d'actions IA, lootPool, consumableLootPool |
| `SkillData` | Compétence (nom, coût énergie, cooldown, `EffectData`) |
| `EffectData` | Effet universel (trigger, action, target, value). Utilisé par skills, consommables, modules, passiveEffects |
| `StatusData` | Statut (behavior, perTurnAction, decayPerTurn, maxStacks) |
| `EquipmentData` | Équipement (slot, bonus stats, skills, passiveEffects — **non branché**) |
| `ModuleData` | Module (moduleID, effect avec trigger) — tracké dans `RunManager.activeModules` |
| `ConsumableData` | Consommable (effect, usableInCombat, usableOnMap) |
| `EventData` | Event narratif (eventID, title, choices avec effects) |
| `NavEffect` | Effet de navigation (TeleportRandom, RevealZoneRandom/Choice, IncreaseVisionRange, IncrementCounter) |
| `EventPool` | Pool d'events aléatoires (filtre déjà joués) |
| `ModuleLootTable` | Pool de modules aléatoires (filtre déjà possédés) |
| `ConsumableLootTable` | Pool de consommables aléatoires |
| `EquipmentLootTable` | Pool d'équipements aléatoires |
| `RandomEvents` | Fallback events par MapData — assigné dans l'Inspector de `NavigationManager` |
| `EventDatabase` | Liste globale d'EventData, `GetByID(string)` |

**`EffectTrigger.None = 0`** est la valeur par défaut — les assets dont le trigger était `OnPlayerTurnStart` (anciennement index 0) doivent être reconfigurés manuellement.

**Editors custom :** `EffectDataEditor` (labels contextuels sur EffectData), `EventEffectDrawer` (champs conditionnels sur EventEffect), `NavEffectDrawer` (champs conditionnels sur NavEffect), `MapEditorWindow` (éditeur de carte).

---

### Prefabs

| Prefab | Usage |
|---|---|
| `ChoiceButton` | Boutons de choix (Event) — 360×65px, TMP auto-sizing |
| `SkillButtonPrefab` | Compétences (Combat) |
| `LootCard` | Cartes équipement post-victoire |
| `ConsumableButton` | Icône consommable dans les 3 scènes |
| `ModuleIcon` | Icône module HUD (48×48px) |

**ConsumableButton :** `SetInteractable(false)` grise ET réduit la taille d'un tiers. `tailleNormale` lue dans `Awake()` (fallback 50×50 si sizeDelta = 0). Désactiver `ChildControlSize` sur le container pour éviter le fallback.

**ModuleHUD :** enfant direct du Canvas (jamais du `mapContainer`). `ModuleIconContainer` avec HorizontalLayoutGroup + ContentSizeFitter.

---

## Bus d'événements — GameEvents.cs

| Trigger | Appelé dans | Écouté par |
|---|---|---|
| `TriggerPlayerTurnStarted()` | `CombatManager.StartPlayerTurn()` | `ModuleManager` → `OnPlayerTurnStart` |
| `TriggerPlayerTurnEnded()` | `CombatManager.OnEndTurn()` | `ModuleManager` → `OnPlayerTurnEnd` |
| `TriggerPlayerDealtDamage(dmg)` | `CombatManager.ApplyEffect()` | `ModuleManager` → `OnPlayerDealtDamage` |
| `TriggerPlayerDamaged(dmg)` | `CombatManager.ApplyEnemyEffect()` | `ModuleManager` → `OnPlayerDamaged` |
| `TriggerEnemyDied()` | `CombatManager.EndCombat(victory)` | `ModuleManager` → `OnEnemyDied` |

`OnRoomEntered`, `OnChestOpened`, `OnShopEntered` définis mais pas encore écoutés.

---

## État du développement

### Fonctionnel ✅
- Système de combat complet (tours, énergie, armure, cooldowns, statuts, crits, regen, lifesteal)
- IA ennemie avec file d'actions circulaire
- Équipement : résolution stats effectives + compétences
- Loot post-combat + gestion slots bras
- Navigation : brouillard de guerre, déplacement clavier, sauvegarde état
- Modules : triggers via GameEvents, effets, HUD, `OnFightStart` après reset d'armure
- Consommables : système complet dans les 3 scènes (grisés si non utilisables)
- Événements narratifs : effets `ModifyHP`, `ModifyMaxHP`, `HealToFull`, `GainConsumable`, `GainModule`, `GainEquipment`, `SetEventFlag`
- Pool d'events par salle (ManualList / FromPool), anti-doublon, fallback `RandomEvents`
- `EquipmentOfferController` partagé Combat/Event
- Effets de navigation complets (`NavEffect`, `AppliquerEffetsNav`, preview hover `RevealZoneChoice`)
- Skills de navigation exclus du combat (`isNavigationSkill`)
- Cases Event en orange sur la carte, cases NonNavigable transparentes
- Editors custom : `EffectDataEditor`, `EventEffectDrawer`, `NavEffectDrawer`, `MapEditorWindow`

### À faire 🔧
- **Scène MainMenu** ← priorité : doit appeler `StartNewRun()` + `GoToNavigation()`. Sans elle, le seeding de départ (skills jambes, modules, consommables) ne se fait pas avant le premier combat.
- `passiveEffects` sur `EquipmentData` : champ défini, non branché dans `CombatManager`
- `ModifyStat` (EffectAction non implémenté)
- Boss, salles spéciales
- Icônes graphiques par type de case (`CellType`) dans `MapRenderer`
- Sons, animations, retours visuels
- Popup "Utiliser / Jeter" pour les consommables
- Icône consommable grisée visuellement (après intégration sprites)
- Cooldown des skills de navigation hors combat

### Localisation 🌍
À implémenter après la première version jouable. Utiliser `com.unity.localization`. Ne jamais hardcoder du texte visible dans le code C#.

---

## Pièges connus

1. **Prefab dans la scène** : référencer le prefab asset depuis `Assets/Prefab/`, jamais une instance glissée dans la scène.
2. **RunManager / ModuleManager null** : toujours utiliser `Instance?.` en test d'une scène isolée.
3. **Destroy() asynchrone** : `Destroy(go)` ne prend effet qu'à la fin du frame.
4. **VerticalLayoutGroup + ContentSizeFitter** : largeur fixe + ContentSizeFitter vertical uniquement. Ne pas cumuler `ChildControlWidth` et largeur fixe.
5. **Layout Group + taille des enfants** : décocher `Control Child Size` pour respecter la taille du prefab.
6. **HUD Navigation dans mapContainer** : tout élément fixe à l'écran doit être enfant direct du Canvas.
7. **ModuleManager statique vs instance** : `OnModulesChanged` statique → HUD fonctionne sans instance. Mais les effets GameEvents nécessitent une instance.
8. **OnFightStart et reset d'armure** : modules `OnFightStart` appliqués après `currentPlayerArmor = 0` grâce à `isFirstTurn`. Ne pas déplacer cet appel.
9. **GUILayout Begin/End** : tout `BeginHorizontal()` doit avoir son `EndHorizontal()` dans tous les chemins, y compris avant un `break`.
10. **`[Header]` dans un CustomPropertyDrawer** : Unity le rend dans `OnGUI` mais pas dans `GetPropertyHeight` → chevauchements. Ne jamais faire ça.
11. **`SetActive(true)` enfant d'un GO inactif** : activer tous les parents d'abord (pattern `MontrerContinueButton()`).
12. **`interactable = true` ≠ visible** : toujours faire `SetActive(true)` en plus si le GO peut être inactif.
13. **EquipmentOfferController + bouton "Continuer"** : le controller se désactive via `Awake()`. Bouton "Continuer" doit être frère, jamais enfant. `lootContinueButton` masqué dans `Start()`.
14. **RevealZoneChoice + clics UI simultanés** : `Input.GetMouseButtonDown(0)` capte aussi les clics sur boutons UI. Utiliser `EventSystem.current.IsPointerOverGameObject()` si conflits.
15. **`baseVisionRange` (ex-`visionRange`)** : valeur réinitialisée à `1` après mise à jour du script — re-saisir manuellement dans la scène Navigation.
16. **ConsumableButton.SetInteractable() + sizeDelta nul** : désactiver `ChildControlSize` sur le container, définir la taille dans le prefab.
17. **ConsumableButton + callback null** : `Setup(data, null)` est valide (Event). Pas de crash, le clic ne fait rien.
