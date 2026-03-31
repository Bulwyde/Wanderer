# CLAUDE.md — Contexte du projet "Petit turn based"

> Ce fichier est lu automatiquement par Claude au démarrage de chaque conversation.
> Il contient tout le contexte nécessaire pour reprendre le développement sans perdre l'historique.
> **Mettre à jour ce fichier après chaque session de travail significative.**

> **Stratégie de lecture :** Ce fichier ne duplique pas les détails d'implémentation déjà dans le code (signatures, champs, valeurs enum). Pour ces détails, Claude lit directement les scripts concernés. Ce fichier se concentre sur : conventions, architecture, décisions de design, pièges et état du dev.

---

## Consignes pour Claude

### Stratégie de travail optimisée
- **Avant tout travail sur un script**, lire le fichier concerné (même brièvement) pour connaître l'état réel du code — ne jamais supposer depuis ce fichier seul.
- **Avant de proposer une solution**, identifier la cause racine dans le code existant, pas seulement les symptômes.
- **Pour tout nouveau système**, chercher d'abord si un mécanisme existant peut être étendu plutôt que recréé (ex : guards `IsSlotFree`, flags `seeded`, fallbacks Inspector).

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
| `MainMenu` | Écran d'accueil — Continuer / Nouvelle Partie / Paramètres / Quitter |
| `Navigation` | Carte du donjon — déplacement case par case |
| `Combat` | Combat au tour par tour |
| `Event` | Événement narratif avec choix |

**Transitions :** toutes via `SceneLoader` (singleton DontDestroyOnLoad) — `GoToNavigation()`, `GoToCombat()`, `GoToEvent()`, `GoToMainMenu()`.

---

## Scripts principaux

### Singletons persistants (DontDestroyOnLoad)

**`RunManager.cs`** — État central de la run. Stocke : `selectedCharacter` (CharacterData), `hasActiveRun`, HP, position, équipement, modules, consommables, flags d'events, état nav sauvegardé.
- `StartNewRun(CharacterData, string)` : reset complet + appelle `SeedDonneesDepart()` → l'équipement, le module, les consommables de départ ET les stats (HP max/courant) sont initialisés **dès le lancement**, avant d'entrer en Navigation.
- `EndRun()` : pose `hasActiveRun = false` — appelé par CombatManager sur défaite et MainMenuManager sur abandon.
- `InitialiserStats()` : calcule `maxHP = characterData.maxHP + Σ bonusHP équipements`, pose `currentHP = maxHP`. Prévu pour accueillir les futures ressources (or, etc.).
- `AddModule()` appelle automatiquement `ModuleManager.NotifyModulesChanged()`.
- ⚠️ `EnterRoom(CellData)` ne set plus `currentSpecificEventID` — c'est `NavigationManager` qui l'assigne après le tirage aléatoire.

**`SceneLoader.cs`** — Singleton simple, wrappe `SceneManager.LoadScene()`.

**`ModuleManager.cs`** — Singleton DontDestroyOnLoad, **doit être dans la scène Navigation** (scène de démarrage). S'abonne aux `GameEvents`, déclenche les effets des modules. `OnModulesChanged` est statique — le HUD fonctionne sans instance, mais les effets ne se déclenchent pas sans instance.

---

### Scène MainMenu

**`MainMenuManager.cs`** — Gère les 4 boutons du menu principal.
- `Continuer` : actif seulement si `RunManager.hasActiveRun == true`.
- `Nouvelle Partie` : si run en cours → popup de confirmation "Abandonner ?" (Oui appelle `EndRun()` puis `DemarrerNouvellePartie()`). Sinon → `DemarrerNouvellePartie()` directement.
- `Paramètres` : placeholder (log), à implémenter.
- `Quitter` : `Application.Quit()` + stop Play Mode en éditeur.
- Champ Inspector `defaultCharacter` (CharacterData) : personnage par défaut jusqu'à l'implémentation de la sélection. **TODO** : remplacer par le résultat de `GoToCharacterSelection()`.
- ⚠️ Si `defaultCharacter` n'est pas assigné dans l'Inspector, log warning et les stats/équipement ne seront pas chargés.

**Layout scène :**
```
Canvas
├── KeyArt          (Image, Stretch full screen, premier enfant = derrière tout)
├── Logo            (Image/Panel, ancré haut-centre)
├── ButtonPanel     (VerticalLayoutGroup, ancré gauche-centre)
│   ├── ContinuerButton
│   ├── NouvellePartieButton
│   ├── ParamètresButton
│   └── QuitterButton
└── ConfirmationPopup (désactivé par défaut)
    ├── DarkOverlay (Image full screen, noir semi-transparent)
    └── PopupPanel
        ├── MessageText
        ├── OuiButton
        └── NonButton
```

---

### Scène Navigation

**`NavigationManager.cs`** — Déplacements clavier, brouillard de guerre, 3 sets (`visibleCells`, `visitedCells`, `exploredCells`). Portée effective = `baseVisionRange + RunManager.visionRangeBonus`, lue depuis `RunManager.selectedCharacter` (fallback : champ Inspector local pour tests isolés). Gère les effets de navigation (`AppliquerEffetsNav`), la sélection de zone (`RevealZoneChoice`), et le tirage d'events aléatoires (`ChoisirEventAleatoire` avec fallback `RandomEvents`).
- ⚠️ `baseVisionRange` remplace l'ancien `visionRange` — re-saisir la valeur dans l'Inspector après mise à jour du script.

**`MapRenderer.cs`** — Affiche les tiles selon les sets de visibilité. Cases `NonNavigable` toujours `Color.clear`. Gère le preview hover `RevealZoneChoice` via `PreviewZone()` / `ClearPreview()` / `RefreshSingleCell()`.

**`MapCameraController.cs`** — Déplace/zoome le `mapContainer` (RectTransform). ⚠️ Tout élément HUD fixe doit être **enfant direct du Canvas**, jamais du `mapContainer`.

**`NavigationHUD.cs`** — Affiche HP joueur, mis à jour chaque frame. Enfant direct du Canvas.

**`MapData.cs`** — ScriptableObject grille. Cases `Event` utilisent `EventCellMode` (ManualList ou FromPool). Case par défaut : `NonNavigable`. Helper `AUnVoisinNavigable(x, y)`.

---

### Scène Combat

**`CombatManager.cs`** — Machine à états `PlayerTurn → EnemyTurn → Victory/Defeat`. Stats effectives calculées une fois au démarrage via `ResolveEquipment()` — lit `RunManager.selectedCharacter` en priorité, fallback sur le champ Inspector local (tests isolés). Armure style StS (reset début de tour, ne bloque pas le poison). Modules `OnFightStart` appliqués après le reset d'armure initial (flag `isFirstTurn`). Loot délégué à `EquipmentOfferController.StartOffresSimultanées()`.
- Le seeding de l'équipement/modules/consommables dans `ResolveEquipment()` est désormais un **no-op** en jeu normal (tout est déjà seedé par `RunManager.StartNewRun()`). Les guards (`IsSlotFree`, `HasModule`, `startingConsumablesSeeded`) le garantissent.
- Sur défaite : `OnEndButtonClicked()` appelle `RunManager.EndRun()` avant `GoToMainMenu()`.
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
- **Scène MainMenu** : Continuer / Nouvelle Partie (popup abandon) / Paramètres (placeholder) / Quitter. Key art plein écran derrière les boutons.
- **CharacterData centralisé** : `RunManager.selectedCharacter` est la source unique. `CombatManager` et `NavigationManager` lisent depuis RunManager avec fallback Inspector pour les tests isolés.
- **Seeding au lancement de la run** : équipement, module, consommables de départ ET stats (HP) initialisés dans `RunManager.StartNewRun()` — la Navigation affiche tout correctement dès l'entrée, sans attendre le premier combat.
- **Boutons passifs d'équipement** : `EffectData` a un champ `displayName` (fallback `effectID`). `SkillButton.SetupPassif(EffectData)` affiche "Passif" au lieu d'un coût énergie et grise le bouton. `CombatManager.SpawnPassifsBras()` génère ces boutons pour Arm1/Arm2. `NavigationManager.SpawnPassifsJambes()` fait de même pour les Legs, avec le `navSkillPrefab` et le label `"(Passif)"` suffixé.
- **Déclenchement des passifs d'équipement** : `ModuleManager.ApplyModulesWithTrigger()` itère aussi sur les `passiveEffects` de toutes les pièces équipées (tous slots), même pattern que les modules. Torse et tête : pas de bouton d'affichage pour l'instant, mais les effets se déclenchent déjà.
- **RecalculerMaxHP** : `RunManager.EquipItem()` appelle automatiquement `RecalculerMaxHP()` si les stats sont déjà initialisées (`maxHP > 0`). Recalcule maxHP = base + Σ bonusHP équipements, et ajuste currentHP du même delta (gain d'équipement = gain de HP courant, style StS). Pendant le seeding initial (`maxHP == 0`), l'appel est ignoré — `InitialiserStats()` prend le relai.
- **EventManager HP** : `EventManager` a un champ optionnel `hpText` (TMP) mis à jour au `Start()` et après chaque `ApplyEffects()`. À assigner dans l'Inspector si un affichage HP existe dans la scène Event.

### À faire 🔧
- **Scène de sélection de personnage** — `MainMenuManager.defaultCharacter` est le placeholder en attendant. Quand elle existera : passer le `CharacterData` choisi à `StartNewRun()` et appeler `GoToNavigation()`.
- `passiveEffects` torse et tête : effets déclenchés, mais pas de bouton d'affichage pour l'instant.
- `ModifyStat` (EffectAction non implémenté)
- Boss, salles spéciales
- Icônes graphiques par type de case (`CellType`) dans `MapRenderer`
- Sons, animations, retours visuels
- Popup "Utiliser / Jeter" pour les consommables
- Icône consommable grisée visuellement (après intégration sprites)
- Cooldown des skills de navigation hors combat
- Paramètres (panel à construire)
- Ressources futures (or, etc.) : ajouter champ dans `RunManager` + ligne dans `InitialiserStats()`

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
16. **ConsumableButton.SetInteractable() + sizeDelta nul** : désactiver `ChildControlSize` sur le container, définir la taille dans le prefab. `rectTransform` peut être null si le container parent est inactif au moment de l'instantiation (Awake différé) — guard null ajouté dans `SetInteractable()`.
17. **ConsumableButton + callback null** : `Setup(data, null)` est valide (Event). Pas de crash, le clic ne fait rien.
18. **CharacterData fallback Inspector** : `CombatManager` et `NavigationManager` ont chacun un champ `characterData` en Inspector qui sert uniquement pour les tests de scène isolée. En jeu normal, il est écrasé/ignoré au profit de `RunManager.selectedCharacter`. Ne pas s'étonner si le champ Inspector semble "ignoré" en jeu complet.
19. **Seeding déjà fait à `StartNewRun()`** : le bloc de seeding de `CombatManager.ResolveEquipment()` est un no-op en jeu normal — tous les guards (`IsSlotFree`, `HasModule`, `startingConsumablesSeeded`) sont déjà vérifiés. Ne pas dupliquer la logique ailleurs.
20. **`EquipItem()` appelle `RecalculerMaxHP()` automatiquement** : mid-run, équiper une pièce met à jour maxHP et currentHP immédiatement. Pendant le seeding, `maxHP == 0` bloque l'appel — `InitialiserStats()` fait le calcul une seule fois après. Ne pas appeler `RecalculerMaxHP()` manuellement sauf cas exceptionnel.
21. **`EffectDataEditor` custom** : tout nouveau champ ajouté à `EffectData` doit aussi être ajouté dans `EffectDataEditor.cs` (section Identité ou Valeurs selon le champ), sinon il n'apparaît pas dans l'Inspector.
