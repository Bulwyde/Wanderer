# CLAUDE.md — Contexte du projet "Petit turn based"

> Mettre à jour après chaque session significative.
> Ce fichier ne duplique pas les détails d'implémentation déjà dans le code. Il se concentre sur : conventions, architecture, décisions de design, pièges et état du dev. Pour les signatures/champs/valeurs enum → lire les scripts directement.

---

## Consignes pour Claude

- **Avant tout travail sur un script**, lire le fichier concerné — ne jamais supposer depuis ce fichier seul.
- **Avant de proposer une solution**, identifier la cause racine dans le code existant.
- **Pour tout nouveau système**, chercher d'abord si un mécanisme existant peut être étendu.
- **Avant tout chantier ≥ 3 fichiers**, demander validation à Elisyo avec un résumé — ne jamais partir directement dans l'implémentation. Les petits fixes (1–2 fichiers) peuvent être faits directement.
- Expliquer **pourquoi** (cause + logique du fix), pas seulement quoi.
- Pour le setup Unity, donner les **noms exacts** (champs, composants, valeurs, hiérarchie).
- Ne pas hardcoder de caractères Unicode spéciaux dans les textes TMP.
- Utiliser `FindFirstObjectByType<T>()` (pas `FindObjectOfType<T>()` déprécié).

**Conventions de code :** scripts en français, bloc `<summary>` en tête, sections `// ---...---`, logs préfixés `[RunManager]`/`[Combat]`/etc., `Mathf.Max(1,...)` pour éviter dégâts nuls, `Mathf.Clamp(x,0,9999)` pour plafonner. ScriptableObjects jamais modifiés au runtime.

---

## Vue d'ensemble

Roguelike tour par tour Unity/C#, inspiré Slay the Spire + Darkest Dungeon. Carte → salles (combat, event, shop, boss) → équipement → progression.

**Scènes :** `MainMenu` | `Navigation` | `Combat` | `Event` | `Shop`
**Transitions :** toutes via `SceneLoader` (DDOL) — `GoToNavigation/Combat/Event/Shop/MainMenu()`.
**Résolution cible :** 640×360 (16:9). Tous les CanvasScaler → `Scale with Screen Size`, référence `640×360`, `Match Width or Height = 0.5`.

---

## Scripts principaux

### Singletons DDOL

**`RunManager.cs`** — État central de la run (character, HP, crédits, position, équipement, modules, consommables, flags, état nav).
- `GetEffectiveCellType(CellData)` : **source de vérité unique** pour le type runtime d'une case. Priorité : `postVisitCellTypes` > `overridesMaximum` > `resolvedAleatoireCells` (si Aleatoire) > `cell.cellType`. Utilisé partout — ne jamais lire `cell.cellType` directement en runtime.
- `navEffectsEnAttente : List<NavEffect>` — NavEffects déclenchés hors scène Navigation (ex : depuis scène Event). Appliqués et vidés dans `Start()` de NavigationManager. Réinitialisés dans `StartNewRun()`.
- `EnterRoom(CellData)` : résolution ennemi — priorité `specificGroup` > `specificEnemy` > pool MapData. `currentMapData` doit être assigné avant par NavigationManager.
- `ClearCurrentRoom()` → `DeterminerPostVisitType(currentCellType)` → `SetPostVisitType`. `currentCellType` = type effectif via `GetEffectiveCellType`. Résultat : `Ferrailleur→FerailleurUtilise`, `Teleporteur→TeleporteurUtilise`, `Shop→Shop`, autres → `Empty`.
- `GetOrCreateShopState(CellData, ShopData)` : génère l'inventaire shop à la 1ère visite, persiste dans `shopStates[x,y]`. Reset dans `StartNewRun()`.
- **Cooldowns nav** : `SetNavSkillCooldown` · `IsNavSkillReady` · `TickCooldownsDe(NavCooldownType)` · `TickCooldownsAvecTag`. Tout resetté dans `StartNewRun()`. `skillID` vide = ignoré silencieusement.
- `StartNewRun()` → `SeedDonneesDepart()` : équipement/module/consommables/stats initialisés avant Navigation.

**`ModuleManager.cs`** — DDOL, **doit être dans la scène Navigation**. `OnModulesChanged` statique (HUD sans instance) mais les effets GameEvents nécessitent une instance.

**`InventoryUIManager.cs`** — DDOL, **ne doit pas être dans une scène spécifique** (DDOL).
Canvas créé dynamiquement au démarrage si aucun Canvas n'est assigné dans l'Inspector.
Pour les détails complets : voir **INVENTAIRE.md**.
- `Open()` / `Close()` / `Toggle()` : gestion de l'interface. Raccourci : touche `I`, Escape ferme.
- `RefreshUI()` : rebuild complet du panneau gauche (équipements portés + skill slots) ET du panneau droit (inventaires).
- `SetDragDropEnabled(bool)` : désactivé en combat (`InitializeCombat`), réactivé au loot (`ShowLootPanel`).
- `Open()` / `Close()` appellent `MapCameraController.SetInputEnabled(bool)` et `NavigationManager.SetInputEnabled(bool)` pour bloquer tous les inputs de navigation quand l'inventaire est ouvert.
- **Panneau gauche** : Legs + Arm1/Arm2 (selon `selectedCharacter.maxEquippedArms`). Chaque équipement affiché avec ses skill slots. Slots `Unavailable` masqués.
- **Panneau droit** : GridLayout équipements (64×64) + GridLayout skills (48×48).

### Navigation

**`NavigationManager.cs`** — Déplacements, brouillard de guerre, NavEffects, tirage d'events.
- `InitialiserCarte()` : Étape 1 = enforcement maximums par type (Fisher-Yates → `SetOverrideMaximum`). Étape 2 = résolution cases `Aleatoire` via `CellAleaPool.TirerAleatoire` (Fisher-Yates, comptes actuels en paramètre pour respecter `maxOccurrences`).
- `VerifierBloqueurLD()` : convertit BloqueurLD en Empty via `SetPostVisitType` si condition remplie. Skip si `HasPostVisitType` déjà posé. Appelée dans `Start()` avant placement du joueur.
- `PlacePlayerOnStart()` : utilise `GetEffectiveCellType` — corrige le bug où deux cases Start avec maximum=1 donnaient toujours la même.
- `OnRoomEntered()` : switch sur `GetEffectiveCellType()` — ne reçoit jamais le type SO brut. Teleporteur : priorité `cell.specificEvent` → fallback `mapData.defaultTeleportEvent` → warning si les deux null. Ferrailleur : placeholder `[Chantier futur]`, flux `ChoisirEventAleatoire`.
- `Start()` : applique `RunManager.navEffectsEnAttente` (effets différés depuis scène Event), puis vide la liste.
- Cooldowns skills nav : `SpawnSkillsJambes()` grise selon `IsNavSkillReady`. Première visite shop : `TickCooldownsDe(ShopDecouvert)` avant `GoToShop()`.

**`MapData.cs`** — ScriptableObject grille.
- `defaultTeleportEvent` (EventData) : fallback pour les cases Teleporteur sans `specificEvent` (ex : cas issu d'une résolution Aléatoire). Pattern identique à `defaultShopData`.
- `maximumsParType` + `typeDeRemplacement` : enforcement des maximums à l'initialisation de la carte.
- `BloqueurCondition` : `type` (CompteurNomme/CombatsTermines/EventsTermines) + `compteurID` + `valeurCible`.
- `CellAleaPool` : tirage pondéré avec `maxOccurrences`. Retourne `Empty` si tout le quota est atteint.

**`MapRenderer.cs`** — Affichage visuel de la grille. `iconesParType[]` à assigner dans l'Inspector du composant sur le GameObject `Canvas`.
- `ObtenirTypeAffiche()` : utilise `HasPostVisitType(x, y)` (pas `IsRoomCleared`) — couvre aussi les BloqueurLD débloqués, qui ont un postVisitType mais ne sont jamais dans `clearedRooms`.

**`MapCameraController.cs`** — Zoom/pan. `canvasParent` (RectTransform du conteneur de la carte) **obligatoire** dans l'Inspector — sans lui, zoom recentre sur l'origine au lieu du curseur.

**`CellType` enum (ordre fixe — ne jamais insérer au milieu) :**
`Empty(0)` `Start(1)` `Boss(2)` `CombatSimple(3)` `Event(4)` `NonNavigable(5)` `Shop(6)` `Elite(7)` `BloqueurLD(8)` `PointInteret(9)` `Ferrailleur(10)` `Radar(11)` `Coffre(12)` `Teleporteur(13)` `Aleatoire(14)` `FerailleurUtilise(15)` `TeleporteurUtilise(16)`

### Combat

**`CombatManager.cs`** — États `PlayerTurn → EnemyTurn → Victory/Defeat`. Multi-ennemis (1 à 4 simultanés).
- `BuildEnemyList()` : depuis `RunManager.currentEnemyGroup` (groupe) ou `currentEnemyData` (solo). Fallback champs Inspector = tests isolés uniquement.
- `InitializeCombat()` : applique `spawnEffects` de chaque ennemi via `ApplyEnemyEffect` avant le premier tour.
- **Ciblage** : `RequiertCiblage` → false si ≤ 1 ennemi vivant (auto-cible). Flèche : `_rootCanvas` depuis `arrowTransform` (pas depuis `this`). `sizeDelta.x = distance / scaleFactor`. Clic via `RectTransformUtility.RectangleContainsScreenPoint(spriteImage.rectTransform)` dans `Update()` — pas `Button.onClick` (root élargi par HLG → décalage). Annulation RMB/Escape rembourse énergie/cooldown.
- `CheckEnemyDeath` : exclut → `TriggerEnemyDied()` → `deathEffects` (avant `AllEnemiesDead()`) → `MortEnnemiRoutine`. Guard `combatEnded` contre double-appel sur AoE.
- `MortEnnemiRoutine` : `animator.SetTrigger("Death")` → pause → fade `CanvasGroup.alpha`. **Pas `SetActive(false)`** — bloc reste dans HLG, positions survivants préservées.
- `GetEnemyAttack(EnemyInstance)` — **toujours utiliser** à la place de `ennemi.data.attack` direct (ignore buffs runtime).
- Loot : priorité `currentGroup.lootPool` > `enemies[0].data.lootPool` (solo) > `defaultCombatLootTable`. `DonnerEquipement` → file `equipementsLootDifféré`, proposé au loot panel post-combat.
- `InitialiserEquipementEtSkills` : itère tous les `EquipmentSlot` via `Enum.GetValues(typeof(EquipmentSlot))` — couvre Arm1–Arm4 automatiquement. Chemin RunManager : `GetEquipped(slot)` uniquement (pas de fallback SO). Chemin sans RunManager (test scène isolée) : fallback `characterData.startingX`. Ne jamais mélanger les deux chemins — écrire sur un SO depuis ce chemin corrompt l'asset.
- `SpawnPassifsBras` : même double chemin RunManager / fallback SO. Couvre Arm1–Arm4 selon `maxEquippedArms`.
- Stats ennemi : `EnemyInstance.combatStatBonuses` + `GetEnemyStatModifiers(EnemyInstance, StatType)` — symétrique joueur.

**`EquipmentOfferController.cs`** — Partagé Combat/Event/Shop. Se désactive dans `Awake()`.
- `LootContinueButton` = **frère** de `EquipmentOfferArea` (jamais enfant — caché par `SetActive(false)`). `SkipButton`/`ArmSelectionPanel` = **enfants** (se cachent avec le parent).

### Shop / Event

**`ShopManager.cs`** — Résout `ShopData` (`cell.shopData` > `currentMapData.defaultShopData`). `RafraichirArticles()` = destroy+recreate. `MettreAJourDisponibilite()` = in-place (évite flash interactable).
- Disposition configurable dans l'Inspector : `equipmentParLigne`, `moduleParLigne`, `consommableParLigne`, `skillParLigne` (défaut 3) + spacings correspondants (`equipmentSpacingColumns`, etc.). Les 4 catégories utilisent des rangées horizontales via `CreerRangee()`. Espacement entre rangées géré dans l'Inspector (VLG spacing — non écrasé par le code).

**`EventManager.cs`** — Système d'effets propre (`EventEffect`/`EventEffectType`), indépendant de `EffectData`.
- `TriggerNavEffect` : `IncrementCounter` → directement via `RunManager.IncrementCounter()` (disponible dans toutes les scènes, pas besoin de NavigationManager). Autres NavEffects : si NavigationManager absent → `RunManager.navEffectsEnAttente` (pas warning + abandon).
- `MontrerContinueButton()` remonte toute la hiérarchie avant d'activer (parent inactif = enfant invisible).

---

## Données (ScriptableObjects)

| Script | Rôle | Notes non-évidentes |
|---|---|---|
| `CharacterData` | Stats base, équipement/module/consommables départ, `baseVisionRange`, `startingCredits` | `maxEquippedArms` (nb de slots bras affichés dans l'inventaire UI) |
| `EnemyData` | Stats, actions IA, `spawnEffects`, `deathEffects`, lootPool, `creditsLoot` | `deathEffects` appliqués avant `AllEnemiesDead()` |
| `EnemyGroup` | Groupe multi-ennemis (1–4 EnemyData). Loot propre. | Prioritaire sur `EnemyData` dans `CellData` |
| `EnemyPool` | Pool mixte `EnemyData`/`EnemyGroup` via `List<EnemyPoolEntry>`. `PickRandom()` pondéré. | |
| `SkillData` | Skills combat + nav (`isNavigationSkill`, `navEffects`, `navCooldownType/Count/Tag`, `skillID`) | `skillID` vide = cooldown ignoré silencieusement |
| `EffectData` | Effet universel — A (conditionTag), B (DonnerItems filtrés par tag), C (scalingSource/comptageTag) | Tout nouveau champ → aussi dans `EffectDataEditor.cs` sinon invisible |
| `StatusData` | Statut (behavior, perTurnAction, decayPerTurn, decayTiming, maxStacks) | |
| `EquipmentData` | Slot, bonus stats, skillSlots (List<SkillSlot>), passiveEffects (Zone 1), skillModifiers (Zone 2) | isUnique remplace par Tag_Unique |
| `ModuleData` | moduleID, `List<EffectData> effects` (chaque effet porte son propre trigger) | |
| `ConsumableData` | effects, `usableInCombat/OnMap/InEvents` (false par défaut) | |
| `EventData` | eventID, title, choices avec `List<EventEffect>` | |
| `CellAleaPool` | Pool types cases Aléatoires, tirage pondéré avec `maxOccurrences`. Retourne `Empty` si épuisé. | |
| `NavEffect` | TeleportRandom, RevealZoneRandom/Choice, IncreaseVisionRange, IncrementCounter | |
| `EventPool` | Pool d'events (filtre déjà joués) | |
| `ShopData` | Loot tables, quantités, fourchettes de prix | Inclut `skillLootTable` + `skillCount` + `skillPriceRange` |
| `EventDatabase` | Liste globale `GetByID(string)` | |
| `TagData` | Tag sémantique — `tagName`, `[Flags] TagCategorie`, `Color`. Sync auto nom asset ↔ `tagName`. | Ne pas fusionner avec `KeywordData` (= tooltips joueur) |
| `MapData` | Grille SO — `defaultShopData`, `defaultTeleportEvent`, `aleatoirePool`, `maximumsParType`, pools ennemis | `defaultTeleportEvent` = fallback Teleporteur sans `specificEvent` |

**Système de tags :** tous les Data portent `List<TagData> tags`. `TagCategorie` est `[Flags]`. `EffectTrigger.None = 0` — valeur par défaut ; les assets avec l'ancien trigger `OnPlayerTurnStart(0)` doivent être reconfigurés.

**Editors custom :** `EffectDataEditor`, `EventEffectDrawer`, `NavEffectDrawer`, `MapEditorWindow`, `TagDataEditor`, `SkillDataEditor`.
**Editors Data (filtrage tags) :** 9 editors (`CharacterDataEditor`… `ModuleDataEditor`) — s'appuient sur `TagListFilterUtil.DrawFilteredTagList(prop, categorie)`. Cache statique par catégorie, invalidé à recompilation. `EnemyDataEditor` : ordre forcé par `FindProperty` — ne pas revenir au loop générique.
**`MapEditorWindow`** : section "Configuration des événements" (`eventCellMode`, `eventList`, `eventPool`) affichée pour `Event`, `Ferrailleur`, `Radar` et `Coffre` (tous utilisent `ChoisirEventAleatoire` en runtime).

**Prefabs :** `ChoiceButton` (360×65px), `SkillButtonPrefab`, `LootCard`, `ConsumableButton`, `ModuleIcon` (48×48px), `StatusIconPrefab`.
`StatusIconPrefab` : racine `StatusIcon` + enfant `IconImage` + enfant `StackText` (TMP, bas-droite). Container : `GridLayoutGroup`. ⚠️ Créer depuis la hiérarchie Canvas (UI → Empty) — sinon Transform 3D → icônes superposées à `(0,0)`.

---

## Systèmes clés — décisions non-évidentes

**ModifyStat — 3 cas distincts (joueur) :**
- *Permanent run* → `RunManager.AddStatBonus()`, stocké dans `runStatBonuses`, intégré dans `ResolveEquipment()` et `RecalculerMaxHP()`.
- *Temporaire ce combat* → `combatStatModifiers`, lu dynamiquement.
- *Statut ModifyStat* → actif tant que stacks > 0. `valueScalesWithStacks=false` : valeur fixe, stacks = durée. `valueScalesWithStacks=true` : valeur = `effectPerStack × stacks`, durée infinie.

**ModifyStat ennemi :** `EnemyInstance.combatStatBonuses` + `GetEnemyStatModifiers` — symétrique joueur. `GetEnemyAttack` = point d'entrée unique. `Self` dans `ApplyEnemyEffect` → `combatStatBonuses`, autre → `combatStatModifiers` joueur.

**`PerTurnStart + ModifyStat`** (joueur ET ennemi) : accumulation cumulative dans le dict de stats chaque tour — permet croissance continue (ex : +1 ATK/tour).

**Formule dégâts joueur :** `(skillValue + effectiveAttack + flat) × (1 + pct)`. Crit après, défense avant crit.

**StatusDecayTiming :** `OnTurnStart` (défaut) ou `OnTurnEnd`. `ApplyPerTurnEffects` = effets. `DecayStatuses(timing)` = décroissance filtrée.

**Crédits :** `RunManager.credits`. `AddCredits(int)` plancher 0. `EventEffectType.ModifyCredits` : désactive le bouton si solde insuffisant + suffixe `[Cout : X credits]`.

**Bus d'événements (`GameEvents.cs`) :** `TriggerPlayerTurnStarted/Ended`, `TriggerPlayerDealtDamage`, `TriggerPlayerDamaged`, `TriggerEnemyDied`, `TriggerSkillUsed(SkillData)` → tous écoutés par `ModuleManager`. `TriggerSkillUsed` déclenché après exécution directe ET après ciblage, avant `UpdatePlayerUI()`.

**Cooldowns nav :** `NavCooldownType` : `None | ShopDecouvert | CombatsTermines | EventsTermines | MondeTermine | CombatEnnemisAvecTag`. Points de tick : `CombatManager.EndCombat` (→ `TickCooldownsAvecTag`) · `OnLootContinueClicked` (→ `TickCooldownsDe(CombatsTermines/MondeTermine)` + `combatsTermines++`) · `EventManager.OnContinueClicked` (→ `TickCooldownsDe(EventsTermines)` + `eventsTermines++`) · `NavigationManager.OnRoomEntered Shop` si première visite. ⚠️ `skillID` vide = ignoré silencieusement.

**Systeme SkillModifier (Zone 2 des equipements) :**
- Zone 1 `passiveEffects` : `EffectData` declenchees par des evenements globaux (trigger). Comportement inchange.
- Zone 2 `skillModifiers` : `List<SkillModifier>` embarquee dans `EquipmentData` (class `[Serializable]`, pas un SO). Modifie le comportement des skills equipes sur CET equipement au moment de l'execution.
- Types disponibles : `ForceAoE`, `BaseDamageMultiplier`, `DamageMultiplier`, `CritChanceBonus`, `RepeatExecution`, `EnergyCostModifier`, `BonusStatusStacks`.
- `conditionTag` sur chaque SkillModifier : filtre par `skill.tags` (comparaison `tagName`). Null = s'applique a tous les skills de l'equipement.
- Pipeline : `UseSkill` reçoit `(SkillData, EquipmentData)` via le callback `Action<SkillData, EquipmentData>` du bouton → `ObtenirContexteExecution` → `ExecuterEffetsSkill` (1 + repetitions fois). L'équipement source est tracké à la création du bouton (liste parallèle `_availableSkillSources`), jamais recherché au clic. Le contexte (`ContexteExecutionSkill`) est passé jusqu'à `AppliquerDegatsEnnemi`. Boucle `RepeatExecution` : garde `!combatEnded` + break si cible morte (`SingleEnemy` uniquement).
- `ForceAoE` pose `ctx.overrideTarget = EffectTarget.AllEnemies` → lu via `ctx.overrideTarget ?? effect.target` dans `DealDamage` ET `ApplyStatus` (ciblant des ennemis). Exception : le branchement `if (effect.target == EffectTarget.Self)` est évalué avant la boucle ennemie et n'est jamais redirigé par `ForceAoE`.
- `TriggerSkillUsed` se declenche une seule fois apres toutes les repetitions (les modules ne se declenchent pas N fois).
- **Nouvelles stats `StatType`** : `ArmorGainMultiplier`, `HealGainMultiplier`, `DamageGainMultiplier` — pourcentages additifs (0.1 = +10%). Pas de champ `effective*` dans CombatManager — lues dynamiquement via `GetPlayerStatModifiers` au moment de l'application. Câblées dans : `ApplyEffect` (`Heal`, `AddArmor`), `AppliquerDegatsEnnemi` (après crit, avant `multiplicateurDegatsFinal`), `ApplyModuleEffect` (`Heal`/`AddArmor` branche `targetsSelf`). Le `switch` `ApplyModuleEffect → ModifyStat` contient un cas explicite `break` pour ces trois stats (évite warning).
- **`EffectScalingSource.SkillEquipeSurCetObjet`** : `value × nb de skills avec comptageTag` équipés sur l'équipement source. Nécessite `ContexteExecutionSkill.sourceEquipment` renseigné. Pour les skills joueur : `ObtenirContexteExecution` assigne `ctx.sourceEquipment = equipSource`. Pour les passifs d'équipement : `ModuleManager` passe `equip` en 3ᵉ argument à `ApplyModuleEffect` — les modules (sans équipement source) passent `null` par défaut. Helper dédié : `CompterSkillsAvecTagSurEquipement(EquipmentData, TagData)`.

---

## État du développement

### Fonctionnel ✅
- **Combat** : tours, énergie, armure StS, cooldowns, statuts (decay, stacks, perTurn), crits, regen, lifesteal, IA circulaire. Multi-ennemis (1–4) : ciblage flèche, tour séquentiel, mort fade CanvasGroup, spawnEffects/deathEffects, stats symétrique joueur, icônes statuts par ennemi. SkillModifier Zone 2 (ForceAoE, BaseDamageMultiplier, DamageMultiplier, CritChanceBonus, RepeatExecution, EnergyCostModifier, BonusStatusStacks). Multi-bras Arm1–Arm4 (InitialiserEquipementEtSkills + SpawnPassifsBras via enum auto-coverage).
- **Navigation** : brouillard de guerre, clavier, sauvegarde position, NavEffects complets, BloqueurLD conditionnel, cases Aléatoires (CellAleaPool, maxOccurrences, Fisher-Yates), maximums par type, GetEffectiveCellType source de vérité, postVisitTypes automatiques. OnRoomEntered couvre tous les types (PointInteret/Teleporteur→specificEvent/defaultTeleportEvent, Radar/Coffre/Ferrailleur→ChoisirEventAleatoire, FerailleurUtilise/TeleporteurUtilise→log). NavEffects différés (navEffectsEnAttente).
- **Équipement** : stats effectives, skills, passifs, loot post-combat, EquipmentOfferController partagé 3 scènes.
- **Events** : tous EventEffectType, pool anti-doublon, offres équipement interactives, consommables utilisables en event.
- **Shop** : ShopData, persistance par case, deux modes UI (destroy+recreate / in-place). Modules, consommables, crédits.
- **Divers** : Seeding au StartNewRun, boss (EndRun+MainMenu), icônes par type de case, zoom vers la souris, EnemyPool mixte, tags (TagData, filtrage catégorie dans 9 editors), cooldowns nav (5 types), scaling EffectData catégories A/B/C, MapEditorWindow (section événements pour Event/Ferrailleur/Radar/Coffre).
- **Inventaire** : 8 slots équipements + 24 slots skills, drag'n'drop complet avec placement indexé, clonage automatique (loot + départ via `SeedSlotSiVide`), deep copy des skillSlots, tags hérités, validation isNavigationSkill (nav→Legs / combat→Arms), maxEquippedArms pour Arm3/4, modal blocker inputs navigation, InventoryUIManager DDOL overlay, désactivation drag'n'drop en combat, skills à vendre dans le shop. Fix drag/drop slot Used (UnequipSkill avant SwapSkill).

### À faire 🔧
- Scène de sélection de personnage (`MainMenuManager.defaultCharacter` = placeholder)
- Passifs torse/tête : effets actifs mais pas de bouton d'affichage
- Sons, animations, retours visuels
- Popup "Utiliser / Jeter" consommables
- Paramètres (panel)
- Mécaniques boss spéciales (phases, actions uniques)
- Événements de craft
- Localisation (`com.unity.localization`) — après 1ère version jouable
- **D** : Conditions tags → influence navigation/génération (EnemyPools + NavigationManager)

---

## Système d'inventaire et drag'n'drop

> **Voir INVENTAIRE.md pour l'architecture complète, les bugs corrigés et les pièges spécifiques.**

### Structures clés
- **SkillSlot** : `enum SlotState` (Available/Used/Unavailable/LockedInUse) + `SkillData equippedSkill`
- **EquipmentData.skillSlots** : `List<SkillSlot>` configurables par équipement
- **EquipmentSlot** : Head, Torso, Legs, Arm1, Arm2, Arm3, Arm4 (`maxEquippedArms` détermine l'accès Arm3/4)
- **EquipmentType** : Head, Torso, Legs, Arm (validation via `IsSlotCompatible()`)

### Inventaires indexés
- `inventoryEquipments` / `inventorySkills` : `List<T>` de taille fixe (`maxInventoryEquipments` / `maxInventorySkills`), remplies de nulls
- `AddEquipmentToInventory(equipment)` : place au premier slot null
- `SetEquipmentToInventorySlot(index, equipment)` : place à un index spécifique (refuse si occupé, sauf null pour vider)
- `RemoveEquipmentFromInventory(equipment)` : trouve et set à null
- Symétrique pour les skills

### Clonage et indépendance
- `CloneEquipmentForLoot(original)` : `Instantiate` + deep copy des `skillSlots`
- Tous les équipements loots/achetés/de départ passent par ce clone
- Chaque clone a une `List<SkillSlot>` indépendante → modifier un clone n'affecte pas les autres

### Drag'n'drop et validation
- **InventoryDragDropController** : `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`, `Setup*` méthodes trackent l'origine (`_originEquipmentSlot`, `_originInventoryEquipmentSlotIndex`, etc.)
- **InventoryDropZone** : marque les zones de drop (`targetSlotIndex`, `targetEquipment`, `targetEquipmentSlot`, `zoneType`)
- **TraiterDrop** : essaie le placement AVANT de modifier l'état (atomicité — si placement échoue, origine inchangée)
- **Validation** : `IsSlotCompatible()` + `isNavigationSkill` check (nav→Legs, combat→Arms) + `maxEquippedArms` check

### InventoryUIManager — Container-per-slot
- `[SerializeField] RectTransform legsContainer`, `arm1Container`… (icône équipement)
- `[SerializeField] RectTransform legsSkillGrid`, `arm1SkillGrid`… (grille skills)
- `RafraichirContenuSlot()` : peuple le container existant, ne crée pas la hiérarchie
- `RafraichirEquipementsPortes()` : masque arm3/4 containers si `maxEquippedArms < 3/4`
- `RafraichirInventaire*` : n'ajoute PAS de `DragDropController` au panel parent
- `[SerializeField] Image modalBlocker` : panel transparent bloquant les raycasts arrière-plan (activé à `Open()`, désactivé à `Close()`)

---

## Pièges connus

1. **Prefab dans la scène** : référencer l'asset depuis `Assets/Prefab/`, jamais une instance scène.
2. **RunManager/ModuleManager null** : `Instance?.` en test de scène isolée.
3. **HUD dans mapContainer** : tout élément fixe → enfant direct du Canvas.
4. **ModuleManager** : `OnModulesChanged` statique (HUD OK sans instance) mais GameEvents nécessitent l'instance.
5. **OnFightStart + armure** : modules appliqués après `currentPlayerArmor = 0` via `isFirstTurn`. Ne pas déplacer.
6. **GUILayout Begin/End** : `BeginHorizontal()` → `EndHorizontal()` dans tous les chemins, y compris avant `break`.
7. **`[Header]` + CustomPropertyDrawer** : ne jamais combiner — chevauchements Inspector.
8. **EquipmentOfferController** : se désactive dans `Awake()`. `LootContinueButton` = frère de `EquipmentOfferArea`, jamais enfant. `SkipButton`/`ArmSelectionPanel` = enfants. `lootContinueButton` masqué dans `Start()` du manager.
9. **RevealZoneChoice + clics UI** : `Input.GetMouseButtonDown(0)` capte les clics UI → `EventSystem.current.IsPointerOverGameObject()` si conflits.
10. **CharacterData/EnemyData fallback Inspector** : champs Inspector de `CombatManager` = tests isolés. En jeu normal, écrasés par RunManager.
11. **Seeding no-op** : `CombatManager` ne seed rien — `SeedSlotIfFree` entièrement supprimé. Le seeding se fait uniquement dans `StartNewRun()` → `SeedDonneesDepart()` → `SeedSlotSiVide()`. Ne jamais rajouter de seeding dans `CombatManager`.
12. **`EffectDataEditor`** : tout nouveau champ `EffectData` → ajouter dans `EffectDataEditor.cs` sinon invisible.
13. **`StatType.MaxHP` via `AddStatBonus()`** : met à jour `maxHP`+`currentHP` ET stocke dans `runStatBonuses`. Préférer `ModifyStat` à l'ancien `ModifyMaxHP`.
14. **ConsumableButton** : `rectTransform` peut être null si container inactif à l'Awake (guard null ajouté). `tailleNormale` lue dans `Awake()`.
15. **ConsommableContainer en Event** : `SpawnConsomableButtons()` appelle `SetActive(true)` — ne pas désactiver définitivement.
16. **Flash interactable Shop** : ne pas appeler `RafraichirArticles()` si seul l'état interactable change → `MettreAJourDisponibilite()`.
17. **`CellType` enum** : ne jamais insérer au milieu (valeurs sérialisées en int). Ajouter **à la fin**.
18. **`currentMapData` avant combat** : NavigationManager doit l'assigner avant `EnterRoom()` — sinon `ResolveEnemyPool()` = null.
19. **Cases Shop jamais `cleared`** : ne pas appeler `ClearCurrentRoom()` pour les shops.
20. **LOS DDA** : avance par frontière cardinale. Coin exact = bloquer si l'un des deux murs cardinaux adjacents est présent.
21. **`NavCooldownType.CombatEnnemisAvecTag`** : `skillID` vide → `SetNavSkillCooldown` ignoré silencieusement.
22. **`TagListFilterUtil` cache statique** : rouvrir l'asset après recompilation pour voir un nouveau `TagData`.
23. **Tags hors catégorie** : un tag non conforme apparaît comme `(aucun)` et est mis à null à la prochaine sauvegarde — vérifier les assets existants.
24. **`combatEnded` guard** : ne pas resetter manuellement — remis à false dans `Start()` du prochain combat.
25. **Ciblage flèche + énergie** : énergie/cooldown déduits AVANT mode ciblage. Annulation → remboursement. Ne pas déduire à nouveau dans `OnEnemyCibleClique()`.
26. **`EnemyInstance.uiRoot`** : utiliser `instance.uiRoot` directement — pas `FindFirstObjectByType`.
27. **`enemyUIPrefab` noms TMP** : cherchés par nom exact (`EnemyHPText`, `EnemyArmorText`, `EnemyNextActionText`). Nom mal orthographié → null silencieux.
28. **Loot multi-ennemis** : priorité `currentGroup.lootPool` > `enemies[0].data.lootPool` > `defaultCombatLootTable`. Aucun des trois → pas de loot (pas d'erreur).
29. **`??` operator avec objets Unity** : `GetComponent<T>()` retourne "Unity null" — `??` ne le détecte pas. Toujours `if (x == null)`.
30. **`GetComponentInParent<Canvas>()` depuis CombatManager** : chercher depuis un GO sous le Canvas (ex : `arrowTransform`).
31. **`sizeDelta` vs pixels écran** : diviser par `Canvas.scaleFactor` pour convertir.
32. **Ciblage ennemi — `Button.onClick`** : root élargi par HLG → décalage. Préférer `RectTransformUtility.RectangleContainsScreenPoint(spriteImage.rectTransform)` dans `Update()`.
33. **Mort d'ennemi — fade vs `SetActive`** : `SetActive(false)` redistribue le HLG. `CanvasGroup.alpha = 0` uniquement pour préserver les positions.
34. **`EnemySprite` — nom exact** : `SpawnEnemyUI` cherche par `Find("EnemySprite")`. Nom différent → sprite null, fade non fonctionnel, ciblage impossible.
35. **`ennemi.data.attack` direct — interdit** : toujours `GetEnemyAttack(ennemi)` (tient compte de `combatStatBonuses` et statuts).
36. **`deathEffects` — ordre** : appliqués APRÈS exclusion du ciblage, AVANT `AllEnemiesDead()`. Si deathEffect tue le joueur, `combatEnded=true` → pas de victoire fausse.
37. **`spawnEffects`/`deathEffects` — `Self`** : désigne l'ennemi lui-même. Pour cibler le joueur → `SingleEnemy`.
38. **`StatusIconContainer` — `GridLayoutGroup` requis** : `constraintCount` écrasé au runtime, mais le composant doit être présent.
39. **`StatusIcon` prefab — composant à la racine** : `GetComponent<StatusIcon>()` cherché sur la racine. Sur un enfant → null silencieux.
40. **`EnemyStatusContainer` — nom exact** : `SpawnEnemyUI` cherche par `Find("EnemyStatusContainer")`. Nom différent → icônes ennemis absentes (pas d'erreur).
41. **`StatusIconPrefab` — `RectTransform` obligatoire** : créer depuis la hiérarchie Canvas (UI → Empty), pas Project panel. Transform 3D → icônes superposées à `(0,0)`. `SetActive(false)` avant `Destroy()` pour retrait immédiat du layout.
42. **Test RunManager-dépendant** : lancer depuis le MainMenu — scène Navigation/Combat isolée laisse RunManager non initialisé (effets silencieusement échoués).
43. **`EquipmentData` — ne plus référencer `.skills`** : le champ a été supprimé. Utiliser `.skillSlots` et filtrer les états `Used`/`LockedInUse` pour obtenir les skills actifs.
44. **`CloneEquipmentForLoot` obligatoire** : tout équipement obtenu en jeu (loot, event, shop) doit passer par `RunManager.CloneEquipmentForLoot()` avant d'être équipé ou ajouté à l'inventaire. Ne jamais modifier un SO asset directement.
45. **`InventoryUIManager` — setup scène** : ajouter un GameObject vide avec le composant `InventoryUIManager` dans la première scène chargée. Il se place en DDOL seul. Pas besoin de Canvas dans la scène.
46. **`armsContainer` — GridLayoutGroup fixe** : les panneaux bras ont `cellSize = 80×80`. Si un équipement a beaucoup de skill slots, le contenu peut être clippé visuellement. Ajuster `cellSize` ou `preferredHeight` selon les assets.
47. **`SkillData.inheritedTags`** : liste runtime uniquement (`[HideInInspector]`). Ne jamais la pré-remplir dans l'Inspector. Remplie par `EquipSkill()`, vidée par `UnequipSkill()`.
48. **Parent panels sans DragDropController** : `panelSkillInventory` et `panelEquipmentInventory` NE DOIVENT PAS avoir de `InventoryDragDropController` — sinon le parent entier devient draggable. Seuls les slots individuels (enfants créés par `RafraichirInventaire*`) ont leur propre contrôleur.
49. **Slots vides → DropZone détectable** : les slots vides n'ont pas de `DragDropController` mais ont une `InventoryDropZone` → les drops sur slots vides fonctionnent. Le raycast traverse l'`Image` du slot vide jusqu'à la `DropZone`.
50. **Unequip ≠ suppression** : `UnequipSkill()` renvoie le skill en inventaire. Pour supprimer, appeler `RemoveSkillFromInventory()` après. `ExecuterSuppression()` gère la séquence complète.
51. **Floating icon taille** : 32×32 pour skills, 64×64 pour équipements. Défini dans `OnBeginDrag()` via `floatingRT.sizeDelta`.
52. **`EquipmentData` — type par défaut `Arm`** : tout nouvel asset EquipmentData est créé avec `equipmentType = Arm`. Changer si Head/Torso/Legs.
53. **`CloneEquipmentForLoot` — auto-correction slot state** : si un skill est assigné à un slot mais que son état est `Available`, il est automatiquement passé à `Used` au clonage (log warning). Corriger l'asset source pour éviter le warning.
54. **`SeedSlotSiVide` — clone obligatoire** : les équipements de départ (`startingArm1`, `startingLegs`, etc.) sont clonés via `CloneEquipmentForLoot()` avant équipement. Si 2 slots pointent le même asset, ils deviennent 2 clones indépendants.
55. **`MapCameraController` / `NavigationManager` — `SetInputEnabled(bool)`** : appelé par `InventoryUIManager.Open()` / `Close()`. Si une autre feature doit bloquer les inputs navigation, utiliser la même méthode pour cohérence.
56. **`SkillModifier.conditionTag`** : compare `skill.tags` par `tagName`, pas par reference objet. N'inclut pas `inheritedTags` — seulement les tags propres du skill.
57. **`RepeatExecution + combatEnded`** : la boucle de repetition est gardee par `&& !combatEnded`. Ne pas retirer cette garde — si le premier cast tue tous les ennemis, le deuxieme cast ne doit pas s'executer.
58. **`SkillButton.EffectiveCost`** : calculé dans `SpawnSkillButtons` (base + `EnergyCostModifier`), stocké sur le bouton. `UpdateSkillButtons` et `CancelTargetSelection` (remboursement) lisent `sb.EffectiveCost` — jamais `skill.energyCost` directement.
59. **`SeedSlotIfFree` supprimé** : méthode entièrement retirée de `CombatManager`. Le seeding se fait uniquement dans `StartNewRun()` → `SeedDonneesDepart()` → `SeedSlotSiVide()`. Ne jamais rajouter de seeding dans `CombatManager`.
60. **Fallback SO interdit si RunManager présent** : ne jamais écrire `GetEquipped(slot) ?? characterData.startingX` quand RunManager est actif. Le SO asset serait modifié en runtime → mutations persistantes entre sessions. Deux chemins stricts : RunManager OU fallback SO (jamais mixés).
61. **Arm3/Arm4 dans CombatManager** : `InitialiserEquipementEtSkills` et `SpawnPassifsBras` utilisent `Enum.GetValues(typeof(EquipmentSlot))` — couvre tous les slots présents et futurs automatiquement. Ne jamais hardcoder de liste de slots.
62. **Source équipement via callback** : `SkillButton` stocke `_sourceEquipment` à la création (parallèle à `availableSkills` via `_availableSkillSources`), transmis par le callback `Action<SkillData, EquipmentData>`. Ne jamais chercher l'équipement par référence `SkillData` au clic — deux équipements peuvent partager le même asset skill.
63. **`ForceAoE` et `ApplyStatus`** : `ForceAoE` override `effect.target` via `ctx.overrideTarget`. Valable pour `DealDamage` ET `ApplyStatus` (ciblant des ennemis). Exception : `effect.target == Self` est résolu avant la boucle ennemie — jamais redirigé. Si tu veux un AoE dégâts-seulement, utilise le système de tags plutôt qu'un `ForceAoE`.
64. **`DamageGainMultiplier` et ordre des multiplicateurs** : appliqué après le crit et avant `ctx.multiplicateurDegatsFinal` dans `AppliquerDegatsEnnemi`. Ordre intentionnel et fixe : conditionnel de tag → crit → `DamageGainMultiplier` → `multiplicateurDegatsFinal` → clamp 9999. Ne pas réordonner.
65. **`SkillEquipeSurCetObjet` sur un module** : un `ModuleData` n'a pas d'équipement source — `sourceEquipment` sera toujours `null`. `CompterSkillsAvecTagSurEquipement(null, tag)` retourne 0 → `ModifyStat` vaudra 0. Ne jamais utiliser `SkillEquipeSurCetObjet` sur un module, uniquement sur des passifs d'équipement.
