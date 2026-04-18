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

**`RunManager.cs`** — État central de la run (character, HP, position, équipement, modules, consommables, flags, état nav).
- `StartNewRun()` → `SeedDonneesDepart()` : équipement/module/consommables de départ + stats initialisés avant d'entrer en Navigation.
- `EndRun()` : appelé sur défaite, victoire boss, et abandon MainMenu.
- `EnterRoom(CellData)` : assigne `currentCellType`, résolution de l'ennemi/groupe (priorité : `cell.specificGroup` > `cell.specificEnemy` > `EnemyPool.PickRandom()` — retourne `EnemyPoolEntry` qui peut être groupe ou solo).
- `ResolveEnemyPool(CellType)` : retourne `normalEnemyPool`/`eliteEnemyPool`/`bossEnemyPool` depuis `currentMapData`. Null si `currentMapData` null.
- `currentMapData` : assigné par `NavigationManager` avant tout `GoToCombat()` ou `GoToShop()` — requis pour `ResolveEnemyPool()` et `ShopManager`.
- `currentEnemyData` : ennemi solo — lu par `CombatManager` si `currentEnemyGroup` est null.
- `currentEnemyGroup` : groupe d'ennemis (EnemyGroup SO) — prioritaire sur `currentEnemyData`. Resetté dans `StartNewRun()`.
- `EquipItem()` appelle automatiquement `RecalculerMaxHP()` si `maxHP > 0` (ignoré pendant le seeding initial).
- `AddModule()` appelle automatiquement `ModuleManager.NotifyModulesChanged()`.
- `GetOrCreateShopState(CellData, ShopData)` : génère l'inventaire shop à la 1ère visite, persiste dans `shopStates[x,y]`, reset dans `StartNewRun()`.
- **Cooldowns nav** : `combatsTermines`/`eventsTermines` (compteurs run), `navSkillCooldowns` (dict skillID → `NavSkillCooldownState`). `SetNavSkillCooldown(skillID, type, count, tag?)` · `IsNavSkillReady(skillID)` · `TickCooldownsDe(NavCooldownType)` · `TickCooldownsAvecTag(List<TagData>)`. Tout resetté dans `StartNewRun()`. Classe helper `NavSkillCooldownState` définie en bas du fichier.

**`ModuleManager.cs`** — DDOL, **doit être dans la scène Navigation**. `OnModulesChanged` statique (HUD sans instance) mais les effets GameEvents nécessitent une instance.

### Navigation

**`NavigationManager.cs`** — Déplacements clavier, brouillard de guerre (3 sets), `AppliquerEffetsNav`, `RevealZoneChoice`, tirage d'events (`ChoisirEventAleatoire` + fallback `RandomEvents`). Assigne `currentMapData` avant `EnterRoom()` pour les cases Classic/Elite/Boss et avant `GoToShop()`. Gère les cooldowns des skills de navigation : `SpawnSkillsJambes()` grise les boutons selon `IsNavSkillReady`, `UtiliserSkillJambes()` appelle `SetNavSkillCooldown` après effet. Détecte la première visite d'un shop (`GetShopState == null`) pour déclencher `TickCooldownsDe(ShopDecouvert)`.

**`MapData.cs`** — ScriptableObject grille. `CellData` a `specificEnemy` (EnemyData), `specificGroup` (EnemyGroup — prioritaire sur `specificEnemy`), `eventList`/`eventPool`, `shopData`. `MapData` a `normalEnemyPool`, `eliteEnemyPool`, `bossEnemyPool` (EnemyPool), `defaultCombatLootTable` (EquipmentLootTable fallback si l'ennemi/groupe n'a pas de lootPool), `defaultLootOfferCount` (int, 1–4).

**`MapRenderer.cs`** — Affichage visuel de la grille. Classe `AssociationIconeCase` (`typeDCase` + `icone` Sprite) ; champ `[SerializeField] iconesParType[]` mappant chaque `CellType` à une icône. Chaque case possède une `Image` enfant dédiée à l'icône, visible **uniquement sur les cases révélées**. ⚠️ Composant sur le GameObject `Canvas` dans la scène Navigation — assigner les associations dans l'Inspector de ce composant.

**`MapCameraController.cs`** — Zoom et pan de la carte. `HandleZoom()` ajuste `anchoredPosition` après le changement de scale pour maintenir le point sous le curseur fixe ("zoom vers la souris"). Champ `[SerializeField] canvasParent` (RectTransform du conteneur de la carte) **obligatoire** — à assigner dans l'Inspector du GameObject `Canvas` racine de la scène Navigation. Sans ce champ, le zoom recentre sur l'origine au lieu du curseur.

**`CellType` enum (ordre fixe — ne jamais insérer au milieu) :**
`Empty(0)` `Start(1)` `Boss(2)` `Classic(3)` `Event(4)` `NonNavigable(5)` `Shop(6)` `Elite(7)`

### Combat

**`CombatManager.cs`** — États `PlayerTurn → EnemyTurn → Victory/Defeat`. Refactorisé pour le **multi-ennemis** (1 à 4 ennemis simultanés).
- `List<EnemyInstance> enemies` : liste des ennemis actifs. `EnemyInstance` est une classe interne portant `data`, `currentHP/Armor`, `statuses` (Dictionary propre), `combatStatBonuses` (Dictionary<StatType,float> — miroir du `combatStatModifiers` joueur, jamais modifié sur le SO), `ai` (EnemyAI), `uiRoot`, `canvasGroup`, `spriteImage`, `animator`, TMP texts (hp/armor/nextAction), `targetButton`, `statusIconContainer` (Transform), `spawnedStatusIcons` (List<StatusIcon>).
- `BuildEnemyList()` : construit les instances depuis `RunManager.currentEnemyGroup` (groupe, jusqu'à 4) ou `RunManager.currentEnemyData` (solo). Fallback Inspector : `enemyGroup` > `enemyData`.
- `InitializeCombat()` : après `BuildEnemyList()`, applique les `spawnEffects` de chaque ennemi via `ApplyEnemyEffect` (avant le premier tour).
- `SpawnEnemyUI(EnemyInstance)` : instancie `enemyUIPrefab` dans `enemiesContainer` (HorizontalLayoutGroup). Ajoute/récupère un `CanvasGroup` (blocksRaycasts=false par défaut). Cherche `EnemySprite` par nom exact → `spriteImage` (preserveAspect=true) + `animator`. Cherche TMP par nom : "EnemyHPText", "EnemyArmorText", "EnemyNextActionText". Le nom `EnemyNameText` est supprimé — les noms ne sont plus affichés.
- **Ciblage** : `RequiertCiblage(skill)` → false si ≤ 1 ennemi vivant (auto-cible). Sinon → `EnterTargetSelectionMode()` (CanvasGroup.blocksRaycasts=true sur vivants). Flèche : `_rootCanvas` mis en cache depuis `arrowTransform` (pas depuis `this` — CombatManager peut être hors Canvas). `sizeDelta.x = distance / scaleFactor` pour corriger pixels écran → unités canvas. Clic détecté dans `Update()` via `RectTransformUtility.RectangleContainsScreenPoint` sur `spriteImage.rectTransform` — pas via `Button.onClick` (le root élargi par HorizontalLayoutGroup causait un décalage d'une position). Annulation RMB/Escape rembourse énergie/cooldown.
- **Tour ennemi** : `EnemyTurnRoutine()` — foreach séquentiel sur ennemis vivants avec `WaitForSeconds(EnemyActionDelay)` entre chaque.
- `ApplyEffect(EffectData, source, EnemyInstance explicitTarget, SkillData sourceSkill = null)` → `GetEffectTargets(EffectTarget, EnemyInstance)` : SingleEnemy = explicit ou premier vivant, AllEnemies = tous vivants, RandomEnemy = aléatoire parmi vivants. `sourceSkill` passé depuis `UseSkill` (chemin direct) et `OnEnemyCibleClique` (chemin ciblé) — utilisé pour calculer les bonus stacks `SkillUtilise` dans `case ApplyStatus`.
- `CompterEquipementsAvecTag(TagData)` : helper privé — itère tous les `EquipmentSlot` via `System.Enum.GetValues`, comparaison par `tagName`. Retourne 0 si tag null ou RunManager indisponible.
- `ObtenirBonusStacksModules(StatusData, SkillData)` : helper privé — cherche dans modules actifs + passifs d'équipement les effets `ApplyStatus + SkillUtilise` dont `statusToApply` et `tagName` correspondent. Cumule et retourne le total de stacks bonus à injecter. Retourne 0 si `sourceSkill` null ou RunManager indisponible.
- `ApplyEnemyEffect(EffectData, source, EnemyInstance attacker)` : gère `DealDamage`, `Heal`, `AddArmor`, `ApplyStatus`, **`ModifyStat`** (Self → `attacker.combatStatBonuses`, autre cible → `combatStatModifiers` joueur).
- Statuts/effets par tour : méthodes séparées joueur vs ennemi (`ApplyPlayerPerTurnEffects()` / `ApplyEnemyPerTurnEffects(EnemyInstance)`). Les deux gèrent désormais `perTurnAction = ModifyStat` (accumulation cumulative dans le dict de stats).
- `GetEnemyStatModifiers(EnemyInstance, StatType)` → `(flat, pct)` : combine `combatStatBonuses` + statuts `ModifyStat` passifs de l'ennemi. Miroir de `GetPlayerStatModifiers`.
- `GetEnemyAttack(EnemyInstance)` : `(data.attack + flat) × (1 + pct)`. **Toujours utiliser à la place de `ennemi.data.attack` direct.**
- `CheckEnemyDeath(EnemyInstance)` : exclut l'ennemi, déclenche `GameEvents.TriggerEnemyDied()`, tick cooldowns nav, **applique les `deathEffects`** (avant `AllEnemiesDead()`), lance `MortEnnemiRoutine()`.
- `MortEnnemiRoutine(EnemyInstance)` : coroutine — `animator.SetTrigger("Death")` → pause 0.3s → fade `CanvasGroup.alpha` 1→0 sur `fadeDureeMort` secondes. **Pas de `SetActive(false)`** : le bloc reste dans le HorizontalLayoutGroup pour que les survivants ne se redistribuent pas. `fadeDureeMort` exposé dans l'Inspector (défaut 3.5s).
- `combatEnded` bool : garde contre double-appel à `EndCombat()` (notamment sur AoE).
- **Loot** : priorité `currentGroup.lootPool` > `enemies[0].data.lootPool` (solo) > `RunManager.currentMapData.defaultCombatLootTable`. Crédits : `currentGroup.creditsLoot` OU somme des `creditsLoot` individuels.
- `ResolveEquipment()` au démarrage : lit `RunManager.selectedCharacter`. Seeding = no-op en jeu normal. Sur victoire boss → `EndRun()` + `GoToMainMenu()` sans `ClearCurrentRoom()`.
- **Champs Inspector** : `enemiesContainer` (Transform), `enemyUIPrefab` (GameObject), `arrowTransform` (RectTransform), `playerSpriteTransform` (RectTransform), `fadeDureeMort` (float), `statusIconContainer` (Transform), `statusIconPrefab` (GameObject), `statusIconsPerRow` (int, défaut 5). Fallbacks test : `enemyGroup` (EnemyGroup), `enemyData` (EnemyData).
- `RefreshPlayerStatusIcons()` : recrée les icônes `StatusIcon` dans `statusIconContainer` (GridLayoutGroup) depuis `playerStatuses`. Appelée automatiquement par `UpdatePlayerUI()`.
- **Distribution d'items par `EffectData`** : `EffectAction.DonnerConsommable/DonnerModule` branchés dans `ApplyEffect` **et** `ApplyModuleEffect`. `DonnerConsommable` appelle `SpawnConsumableButtons()` après `AddConsumable()` réussi pour rafraîchir l'UI immédiatement. `DonnerEquipement` alimente `equipementsLootDifféré` (List<EquipmentData>), fusionné dans `PickLootOffers()` → `BuildLootOffresBase()` au moment du loot panel post-combat. `ObtenirFiltreTag(EffectData)` : helper privé qui retourne `selectedCharacter.tags[0]` si `filtreParTagHero`, sinon `filtreTag`.

**`EquipmentOfferController.cs`** — Partagé Combat (simultané) / Event et Shop (séquentiel). Se désactive dans `Awake()`.
- **Structure LootPanel** — identique dans les 3 scènes (Combat, Event, Shop) :
  ```
  LootPanel
    LootContinueButton   ← frère de EquipmentOfferArea (géré par le manager externe)
    EquipmentOfferArea   ← EquipmentOfferController ici
      LootCardContainer
      SkipButton         ← enfant (géré par EquipmentOfferController, caché avec le parent)
      ArmSelectionPanel  ← enfant (idem)
        Arm1Button
        Arm2Button
  ```
- `LootContinueButton` = **frère**, jamais enfant — sinon caché par `SetActive(false)` de l'area.
- `SkipButton` + `ArmSelectionPanel` = **enfants** — se cachent automatiquement avec l'area.
- **Combat** : `LootContinueButton` → aller en navigation. `skipButton` non assigné (mode simultané).
- **Event** : `LootContinueButton` → `OnContinueClicked` (même effet que l'ancien `continueButton`). `EventManager` expose `lootPanel` + `lootContinueButton`.
- **Shop** : pas de `LootContinueButton` affiché — le `LootPanel` se ferme automatiquement après résolution. `ShopManager` expose `lootPanel` (champ `lootContinueButton` ignoré en pratique).

### Shop / Event

**`ShopManager.cs`** — Résout `ShopData` (`cell.shopData` > `currentMapData.defaultShopData`). Deux modes UI : `RafraichirArticles()` (destroy+recreate, après achat) et `MettreAJourDisponibilite()` (in-place, après crédit seul — évite le flash interactable). Champs `lootPanel` + `lootContinueButton` présents mais `lootContinueButton` non utilisé — le panel se ferme directement dans `OnRemplacementResolu()`.

**`EventManager.cs`** — Système d'effets propre (`EventEffect`/`EventEffectType`), indépendant de `EffectData`. `MontrerContinueButton()` remonte toute la hiérarchie avant d'activer. Champs `lootPanel` + `lootContinueButton` : si assignés, le `LootPanel` s'ouvre pour les offres et `lootContinueButton` déclenche `OnContinueClicked` après résolution.

---

## Données (ScriptableObjects)

| Script | Rôle |
|---|---|
| `CharacterData` | Stats de base, équipement/module/consommables départ, `baseVisionRange`, `startingCredits` |
| `EnemyData` | Stats, actions IA, `spawnEffects` (List<EffectData> — appliqués à l'init du combat), `deathEffects` (List<EffectData> — déclenchés à 0 HP), lootPool, consumableLootPool, `creditsLoot` |
| `EnemyGroup` | Groupe multi-ennemis (1 à 4 EnemyData). Loot propre : `lootPool`, `lootOfferCount`, `consumableLootPool`, `creditsLoot`. Tags. Prioritaire sur EnemyData dans CellData. |
| `EnemyPool` | Pool mixte (`EnemyData` ou `EnemyGroup`) via `List<EnemyPoolEntry>`. `PickRandom()` → `EnemyPoolEntry`. Tirage pondéré si `weight > 0`. ⚠️ Migration : anciens assets (List\<EnemyData\>) à ré-assigner dans l'Inspector. |
| `SkillData` | Compétence (coût énergie, cooldown, `List<EffectData> effects`). Navigation : `isNavigationSkill`, `navEffects`, `navCooldownType` (`NavCooldownType` enum), `navCooldownCount`, `navCooldownTag` |
| `EffectData` | Effet universel (trigger, action, target, value) — skills/consommables/modules/passifs. **Condition de tag (catégorie A)** : `conditionTag` (TagData), `conditionCible` (enum `ConditionCible` : `Aucune/EnnemiCible/CarteActuelle`), `bonusConditionnel` (float), `typeBonusConditionnel` (enum `TypeBonusConditionnel` : `Pourcentage/Flat`). `CarteActuelle` = réservé, log uniquement. **Distribution d'items (catégorie B)** : `EffectAction.DonnerConsommable/DonnerEquipement/DonnerModule` + `consommableLootTable` / `equipementLootTable` / `moduleLootTable` (loot tables) + `filtreTag` (TagData) + `filtreParTagHero` (bool — utilise `selectedCharacter.tags[0]`). `DonnerEquipement` → file `equipementsLootDifféré`, proposé au loot panel post-combat. **Scaling par tag (catégorie C)** : `scalingSource` (enum `EffectScalingSource` : `Aucune/EquipementEquipe/SkillUtilise`) + `comptageTag` (TagData). `EquipementEquipe` : valeur effective = `value × CompterEquipementsAvecTag(comptageTag)`, appliqué sur `ModifyStat` dans `ApplyEffect` et `ApplyModuleEffect`. `SkillUtilise` : condition binaire sur le skill source — si le skill n'a pas `comptageTag`, l'effet est skippé ; pour `ApplyStatus`, les stacks bonus sont injectés inline dans `ApplyEffect` via `ObtenirBonusStacksModules`. |
| `StatusData` | Statut (behavior, perTurnAction, decayPerTurn, decayTiming, maxStacks) |
| `EquipmentData` | Slot, bonus stats, skills, passiveEffects |
| `ModuleData` | moduleID, `List<EffectData> effects` (chaque effet porte son propre trigger) |
| `ConsumableData` | `List<EffectData> effects`, `usableInCombat`/`usableOnMap`/`usableInEvents` (false par défaut) |
| `EventData` | eventID, title, choices avec `List<EventEffect>` |
| `NavEffect` | TeleportRandom, RevealZoneRandom/Choice, IncreaseVisionRange, IncrementCounter |
| `EventPool` | Pool d'events (filtre déjà joués) |
| `ShopData` | Loot tables, quantités, fourchettes de prix |
| `EventDatabase` | Liste globale `GetByID(string)` |
| `TagData` | Tag sémantique — `tagName`, `[Flags] TagCategorie categorie`, `Color couleur`. Assets dans `Assets/ScriptableObjects/Tags/` |

**Système de tags :** tous les Data principaux (EquipmentData, EnemyData, EventData, ConsumableData, ModuleData, SkillData, CharacterData, MapData) portent un `List<TagData> tags`. Sert aux conditions d'effets et au filtrage de loot — **pas d'affichage joueur** (≠ KeywordData). `TagCategorie` est un `[Flags]` enum : cocher plusieurs catégories pour les tags cross-types (ex : `Feu` sur Ennemi + Équipement). `EquipmentData.isUnique` supprimé — remplacé par un tag `Tag_Unique`.

**`TagData` ↔ nom d'asset (sync automatique) :** `OnValidate` dans `TagData.cs` remplit `tagName` depuis le nom du fichier à la création. `TagDataWatcher` (AssetPostprocessor) répercute tout renommage Project → `tagName`. `TagDataEditor` (DelayedTextField) répercute `tagName` → nom d'asset. `[CanEditMultipleObjects]` activé.

**`KeywordData` ≠ `TagData` :** KeywordData = tooltips UI pour le joueur (description, couleur de surbrillance, tooltips imbriqués). TagData = logique interne. Ne pas fusionner.

**`EffectTrigger.None = 0`** — valeur par défaut. Les assets avec l'ancien trigger `OnPlayerTurnStart` (index 0) doivent être reconfigurés.
**`EffectDataEditor` custom** : tout nouveau champ `EffectData` doit aussi être ajouté dans `EffectDataEditor.cs` sinon invisible dans l'Inspector.

**Editors custom :** `EffectDataEditor`, `EventEffectDrawer`, `NavEffectDrawer`, `MapEditorWindow`, `TagDataEditor`, `SkillDataEditor`.
**Editors Data (filtrage tags) :** `CharacterDataEditor`, `ConsumableDataEditor`, `EquipmentDataEditor`, `EventDataEditor`, `EnemyDataEditor`, `EnemyGroupEditor`, `MapDataEditor`, `ModuleDataEditor` — tous s'appuient sur `TagListFilterUtil.DrawFilteredTagList(prop, categorie)` pour ne proposer que les tags de la catégorie correspondante (+ tags "Everything" = toutes catégories cochées). Cache statique par catégorie, invalidé à chaque recompilation. `EnemyGroupEditor` utilise `TagCategorie.Ennemi`, `[CanEditMultipleObjects]`.
**`EnemyDataEditor`** : réécrit avec `FindProperty` explicite pour chaque champ — ordre forcé : Identité → Stats → Tags → Actions → Effets (spawnEffects/deathEffects) → Loot. Ne pas revenir au loop générique (perd le contrôle de l'ordre).

**Prefabs :** `ChoiceButton` (360×65px), `SkillButtonPrefab`, `LootCard`, `ConsumableButton`, `ModuleIcon` (48×48px), `StatusIconPrefab` (ex : 32×32px — Image + TMP StackText).
`ConsumableButton.SetInteractable(false)` grise + réduit taille d'un tiers. Désactiver `ChildControlSize` sur le container. `ModuleHUD` enfant direct du Canvas (jamais du `mapContainer`).
**Setup `StatusIconPrefab`** : racine avec composant `StatusIcon`, enfant `IconImage` (Image, full size), enfant `StackText` (TMP, ancré bas-droite). Le container parent doit avoir un `GridLayoutGroup` (Constraint = Fixed Column Count — le `constraintCount` est écrasé au runtime par `statusIconsPerRow`). ⚠️ Le prefab **doit être créé depuis la hiérarchie Canvas** (clic droit → UI → Empty), pas depuis le Project panel — sinon la racine a un `Transform` 3D au lieu d'un `RectTransform`, et le `GridLayoutGroup` ne peut pas positionner les icônes (toutes superposées à `(0,0)`).

---

## Systèmes clés — décisions non-évidentes

**ModifyStat — 3 cas distincts (joueur) :**
- *Permanent run* (events/modules hors-combat) → `RunManager.AddStatBonus()`, stocké dans `runStatBonuses`, intégré dans `ResolveEquipment()` et `RecalculerMaxHP()`.
- *Temporaire ce combat* (skills/consommables/modules en combat) → `combatStatModifiers`, lu dynamiquement par `GetCurrentAttack()` etc.
- *Statut ModifyStat* → actif tant que stacks > 0. `valueScalesWithStacks=false` : valeur fixe, stacks = durée. `valueScalesWithStacks=true` : valeur = `effectPerStack × stacks`, durée infinie.

**ModifyStat ennemi — système symétrique au joueur :**
- `EnemyInstance.combatStatBonuses: Dictionary<StatType, float>` — équivalent de `combatStatModifiers` joueur.
- `GetEnemyStatModifiers(EnemyInstance, StatType)` → `(flat, pct)` — combine `combatStatBonuses` + statuts `ModifyStat` passifs.
- `GetEnemyAttack(EnemyInstance)` — **point d'entrée unique** pour lire l'attaque ennemi. Ne jamais lire `ennemi.data.attack` directement.
- `EffectAction.ModifyStat` dans `ApplyEnemyEffect` : `Self` → `combatStatBonuses`, autre → `combatStatModifiers` joueur.

**`PerTurnStart + perTurnAction ModifyStat`** (joueur ET ennemi) : chaque tour, ajoute `effectPerStack × stacks` dans le dict de stats correspondant. Permet une croissance cumulative (ex : +1 attaque/tour). Fonctionne via le case `ModifyStat` ajouté dans `ApplyPlayerPerTurnEffects` et `ApplyEnemyPerTurnEffects`.

**Formule dégâts joueur :** `(skillValue + effectiveAttack + flat) × (1 + pct)`. Crit appliqué après, défense soustraite avant le crit.

**StatusDecayTiming :** `OnTurnStart` (défaut) ou `OnTurnEnd`. `ApplyPerTurnEffects` = effets seuls. `DecayStatuses(timing)` = décroissance filtrée. Nettoyage des stacks à 0 intégré dans `DecayStatuses`.

**Crédits :** `RunManager.credits`, initialisé depuis `CharacterData.startingCredits`. `AddCredits(int)` (plancher 0). `HasEnoughCredits(int)`. `EffectAction.AddCredits` branché partout. `EventEffectType.ModifyCredits` : EventManager désactive le bouton si solde insuffisant + suffixe `[Cout : X credits]`.

**Bus d'événements (`GameEvents.cs`) :**
`TriggerPlayerTurnStarted/Ended`, `TriggerPlayerDealtDamage(dmg)`, `TriggerPlayerDamaged(dmg)`, `TriggerEnemyDied()`, `TriggerSkillUsed(SkillData)` → tous écoutés par `ModuleManager`. `TriggerSkillUsed` déclenché après exécution directe (`UseSkill`) ET après ciblage (`OnEnemyCibleClique`) — avant `UpdatePlayerUI()`. `OnRoomEntered`/`OnChestOpened`/`OnShopEntered` définis mais pas encore écoutés.

**Cooldowns de skills de navigation :**
`NavCooldownType` enum dans `SkillData.cs` : `None | ShopDecouvert | CombatsTermines | EventsTermines | MondeTermine | CombatEnnemisAvecTag`. Le type détermine l'événement qui recharge le skill. Points de déclenchement : `CombatManager.EndCombat` (→ `TickCooldownsAvecTag` sur les tags de l'ennemi) · `CombatManager.OnLootContinueClicked` (→ `TickCooldownsDe(CombatsTermines)` ou `MondeTermine` selon boss ou non, + `combatsTermines++`) · `EventManager.OnContinueClicked` (→ `TickCooldownsDe(EventsTermines)` + `eventsTermines++`) · `NavigationManager.OnRoomEntered` Shop (→ `TickCooldownsDe(ShopDecouvert)` si première visite). Le bouton de navigation est grisé si `!IsNavSkillReady(skillID)`. ⚠️ `skillID` doit être renseigné sur le `SkillData` sinon le cooldown est ignoré.

---

## État du développement

### Fonctionnel ✅
Combat complet (tours, énergie, armure StS, cooldowns, statuts, crits, regen, lifesteal) · IA ennemie circulaire · Équipement (stats effectives, skills, passifs) · Loot post-combat · Navigation (brouillard, clavier, sauvegarde) · Modules (GameEvents, HUD, OnFightStart) · Consommables (3 scènes) · Events narratifs (tous effets) · Pool d'events (ManualList/FromPool, anti-doublon) · EquipmentOfferController partagé · NavEffect complets · MainMenu complet · Seeding au lancement · Boutons passifs équipement · Crédits · Marchand (ShopData, persistance par case) · Boss (victoire → EndRun + MainMenu) · EnemyPool + CellType.Elite · Sélection ennemi par case/pool · **Système de tags** (TagData sur tous les Data, sync nom asset ↔ tagName, multi-édition) · **Cooldowns skills de navigation** (5 types, bouton grisé, persist RunManager) · **Filtrage tags par catégorie dans l'Inspector** (9 editors Data + TagListFilterUtil) · **Combat multi-ennemis** (jusqu'à 4, ciblage flèche souris, tour séquentiel, statuts par ennemi, loot priorité groupe > solo > MapData fallback, EnemyGroup SO, EnemyPool mixte EnemyData+EnemyGroup) · **Mort d'ennemi mid-combat** (exclusion immédiate, fade CanvasGroup 3.5s, positions preservées, hook Animator "Death" prêt) · **spawnEffects / deathEffects sur EnemyData** (effets à l'apparition et à la mort, via `ApplyEnemyEffect`) · **Système de stats ennemi** (`combatStatBonuses`, `GetEnemyAttack`, `GetEnemyStatModifiers` — symétrique joueur) · **`PerTurnStart + ModifyStat`** fonctionnel joueur ET ennemi (croissance cumulative par tour) · **Icônes de statuts joueur** (`StatusIcon.cs`, `RefreshPlayerStatusIcons`, GridLayoutGroup avec `statusIconsPerRow` configurable) · **Icônes par type de case** (`MapRenderer` — `AssociationIconeCase`, `iconesParType[]`, image enfant dédiée, visible sur cases révélées uniquement) · **Zoom vers la souris** (`MapCameraController` — `anchoredPosition` corrigé après scale, champ `canvasParent`) · **Conditions de tag sur `EffectData` — catégorie A** (`conditionTag/conditionCible/bonusConditionnel/typeBonusConditionnel`, modes `Pourcentage` et `Flat`, base toujours appliquée) · **Distribution d'items filtrés par tag — catégorie B** (`DonnerConsommable/DonnerEquipement/DonnerModule`, `GetRandomAvecTag` par `tagName` dans les 3 loot tables, `filtreParTagHero`, file `equipementsLootDifféré` post-combat, `SpawnConsumableButtons()` après ajout réussi) · **Scaling par comptage de tags — catégorie C** (C1 : `EquipementEquipe` — `ModifyStat` scalé par `CompterEquipementsAvecTag` dans `ApplyEffect` et `ApplyModuleEffect` ; C2 : `SkillUtilise` — `OnSkillUsed` event, `ApplyModulesAvecSkill`, bonus stacks `ApplyStatus` injectés inline par `ObtenirBonusStacksModules` sur la même cible que le skill, skip dans `ApplyModulesAvecSkill` pour éviter double application)

### À faire 🔧
- Scène de sélection de personnage (`MainMenuManager.defaultCharacter` = placeholder)
- Passifs torse/tête : effets actifs mais pas de bouton d'affichage
- ~~Icônes par type de case dans `MapRenderer`~~ ✅ fait
- Sons, animations, retours visuels
- Popup "Utiliser / Jeter" consommables
- Paramètres (panel)
- Mécaniques boss spéciales (phases, actions uniques) dans `EnemyData`/`EnemyAI`
- Événements de craft (à définir)
- Localisation (`com.unity.localization`) — après 1ère version jouable
- Conditions de tags dans `EffectData` — catégories A, B et C implémentées. Restante :
  - ~~**A** : Condition on/off~~ ✅ fait (`conditionTag/conditionCible`, modes `Pourcentage`/`Flat`)
  - ~~**B** : Filtrer ce qu'on reçoit par tag~~ ✅ fait (`DonnerConsommable/Equipement/Module`, `GetRandomAvecTag`, `filtreParTagHero`)
  - ~~**C** : Scaling par comptage de tags~~ ✅ fait (C1 : `EquipementEquipe` — valeur × nb équipements tagués ; C2 : `SkillUtilise` — bonus stacks si skill tagué, injectés inline dans `ApplyEffect`)
  - **D** : Influence sur la navigation/génération — ex : "+10% chances ennemi Shiny", "prochain event de type Ultra rare" (touche EnemyPools et NavigationManager, pas EffectData)
  - Ne pas tout implémenter d'un coup — attaquer catégorie par catégorie quand un besoin concret se présente en jeu

---

## Pièges connus

1. **Prefab dans la scène** : référencer l'asset depuis `Assets/Prefab/`, jamais une instance scène.
2. **RunManager/ModuleManager null** : `Instance?.` en test de scène isolée.
3. **Destroy() asynchrone** : effet à la fin du frame. `SetActive(false)` avant `Destroy()` pour retirer immédiatement du LayoutGroup.
4. **VerticalLayoutGroup + ContentSizeFitter** : largeur fixe + ContentSizeFitter vertical seulement. Décocher `ChildControlWidth` et `ChildControlSize`.
5. **HUD dans mapContainer** : tout élément fixe → enfant direct du Canvas.
6. **ModuleManager** : `OnModulesChanged` statique (HUD OK sans instance) mais GameEvents nécessitent l'instance.
7. **OnFightStart + armure** : modules appliqués après `currentPlayerArmor = 0` via `isFirstTurn`. Ne pas déplacer.
8. **GUILayout Begin/End** : `BeginHorizontal()` → `EndHorizontal()` dans tous les chemins, y compris avant `break`.
9. **`[Header]` + CustomPropertyDrawer** : ne jamais combiner — chevauchements Inspector.
10. **`SetActive(true)` enfant d'un GO inactif** : activer tous les parents d'abord.
11. **EquipmentOfferController** : se désactive via `Awake()`. `LootContinueButton` = frère de `EquipmentOfferArea`, jamais enfant (sinon caché par le `SetActive(false)`). `SkipButton` et `ArmSelectionPanel` = enfants de `EquipmentOfferArea` (se cachent avec le parent). `lootContinueButton` masqué dans `Start()` du manager.
12. **RevealZoneChoice + clics UI** : `Input.GetMouseButtonDown(0)` capte les clics UI → `EventSystem.current.IsPointerOverGameObject()` si conflits.
13. **CharacterData/EnemyData fallback Inspector** : champs Inspector de `CombatManager` = tests isolés uniquement. En jeu normal, écrasés par RunManager.
14. **Seeding no-op** : `CombatManager.ResolveEquipment()` ne seed rien en jeu normal — tout fait par `StartNewRun()`. Ne pas dupliquer.
15. **`EffectDataEditor`** : tout nouveau champ `EffectData` → ajouter dans `EffectDataEditor.cs` sinon invisible.
16. **`StatType.MaxHP` via `AddStatBonus()`** : met à jour `maxHP`+`currentHP` ET stocke dans `runStatBonuses`. Préférer `ModifyStat` à l'ancien `ModifyMaxHP` pour cohérence combat.
17. **`effect` → `effects` (List)** : SkillData/ConsumableData/ModuleData. Réassigner les assets existants dans `effects[0]`.
18. **ConsumableButton** : `SetInteractable()` → `rectTransform` peut être null si container inactif à l'Awake (guard null ajouté). `tailleNormale` lue dans `Awake()`.
19. **ConsomableContainer désactivé en Event** : `SpawnConsomableButtons()` appelle `SetActive(true)` — ne pas désactiver définitivement.
20. **`Canvas.ForceUpdateCanvases()`** : appeler après destroy+recreate de ContentSizeFitter/LayoutGroup pour recalcul synchrone.
21. **Flash interactable Shop** : ne pas appeler `RafraichirArticles()` si seul l'état interactable change → `MettreAJourDisponibilite()`.
22. **`CellType` enum** : ne jamais insérer au milieu (valeurs sérialisées en int). Ajouter **à la fin**. Ordre actuel : Empty(0) Start(1) Boss(2) Classic(3) Event(4) NonNavigable(5) Shop(6) Elite(7).
23. **`currentMapData` avant combat** : `NavigationManager` doit l'assigner avant `EnterRoom()` pour Classic/Elite/Boss, sinon `ResolveEnemyPool()` = null → fallback Inspector.
24. **Cases Shop jamais `cleared`** : ne pas appeler `ClearCurrentRoom()` pour les shops. `MapRenderer` vérifie `cellType != Shop` avant couleur "vide".
25. **`ShopItemButton` prefab** : `Button` + `ShopItemButton` à la racine, `ItemNameText` + `PriceText` (TMP) assignés dans l'Inspector.
26. **LOS DDA** : algorithme DDA dans `HasClearLineOfSight` — avance par frontière cardinale, jamais en diagonal pur. Coin exact = bloquer si l'un des deux murs cardinaux adjacents est présent.
27. **`NavCooldownType.CombatEnnemisAvecTag`** : le `skillID` doit être renseigné sur le `SkillData` — sans lui, `SetNavSkillCooldown` est ignoré silencieusement.
28. **`TagListFilterUtil` cache statique** : les tags filtrés sont mis en cache par catégorie jusqu'à la recompilation. Si un nouveau `TagData` est créé, rouvrir l'asset concerné après recompilation pour voir la liste à jour.
29. **Tags déjà assignés hors catégorie** : si un asset avait un tag non conforme avant l'ajout des editors filtrés, il apparaîtra comme `(aucun)` dans le popup et sera mis à null à la prochaine sauvegarde — vérifier les assets existants.
30. **CanvasScaler résolution** : référence cible = 640×360, mode `Scale with Screen Size`, `Match = 0.5`. Ne pas utiliser `Constant Pixel Size` (aucun scaling) ni une référence 4:3 (800×600) ou 1080p.
31. **`NavigationHUD`** : peut être mis en composant sur `NavigationManager` s'il n'y a pas de GO dédié — l'Update() fonctionne pareil. Les champs `hpText`/`creditsText` doivent être assignés dans l'Inspector.
32. **`EnemyPool` migration** : `List<EnemyData> enemies` est devenu `List<EnemyPoolEntry> entries`. Les assets EnemyPool existants auront leur liste vide — ré-assigner dans l'Inspector (champ `enemyData` ou `enemyGroup` de chaque entrée).
33. **`combatEnded` guard** : `EndCombat()` est protégé par un bool. Ne pas le contourner ni le reset manuellement — il est remis à false dans `Start()` du prochain combat.
34. **Ciblage flèche + énergie** : l'énergie et le cooldown sont déduits AVANT d'entrer en mode ciblage. Si le joueur annule (RMB/Escape), ils sont remboursés. Ne jamais déduire à nouveau dans `OnEnemyCibleClique()`.
35. **`EnemyInstance.uiRoot`** : instancié dynamiquement depuis `enemyUIPrefab` dans `enemiesContainer`. Ne pas le chercher par `FindFirstObjectByType` — utiliser `instance.uiRoot` directement.
36. **`enemyUIPrefab` noms TMP** : `SpawnEnemyUI` cherche les TMP par `GetComponentsInChildren` et compare `name`. Les noms exacts attendus : "EnemyNameText", "EnemyHPText", "EnemyArmorText", "EnemyNextActionText". Un nom mal orthographié → champ null → NullRef silencieux.
37. **`using System.Linq`** : ajouté dans `CombatManager.cs` pour `enemies.Sum(e => e.data?.creditsLoot ?? 0)`. Ne pas retirer.
38. **Loot multi-ennemis** : la priorité est `currentGroup.lootPool` > `enemies[0].data.lootPool` (seulement si combat solo) > `MapData.defaultCombatLootTable`. Si aucun de ces trois n'est renseigné, aucune offre d'équipement n'est faite (pas d'erreur — juste pas de loot).
39. **`??` operator avec objets Unity** : `GetComponent<T>()` retourne un "Unity null" (pas un C# null) quand le composant est absent. L'opérateur `??` utilise le null C# → ne détecte pas le Unity null → `AddComponent` n'est jamais appelé. Toujours utiliser `if (x == null)` avec les objets Unity.
40. **`GetComponentInParent<Canvas>()` depuis CombatManager** : si CombatManager est sur un GO hors hiérarchie Canvas, retourne null. Toujours chercher le Canvas depuis un GO qui EST sous le Canvas (ex : `arrowTransform.GetComponentInParent<Canvas>()`).
41. **`sizeDelta` vs pixels écran** : `sizeDelta` est en unités canvas, `Input.mousePosition` est en pixels écran. Diviser par `Canvas.scaleFactor` pour convertir. Le scaleFactor dépend de la résolution d'affichage vs la résolution de référence du CanvasScaler.
42. **Ciblage ennemi — `Button.onClick` vs `RectangleContainsScreenPoint`** : le Button sur le root élargi par `HorizontalLayoutGroup` couvre plus de surface que le sprite visible → décalage de cible. Préférer `RectTransformUtility.RectangleContainsScreenPoint(spriteImage.rectTransform, ...)` dans `Update()`.
43. **Mort d'ennemi — `SetActive(false)` vs `CanvasGroup.alpha = 0`** : `SetActive(false)` retire l'enfant du HorizontalLayoutGroup et redistribue les positions. Pour garder les positions des survivants fixes, utiliser `CanvasGroup.alpha = 0` uniquement — le bloc reste dans le layout mais est invisible.
44. **`EnemySprite` — nom exact requis dans le prefab** : `SpawnEnemyUI` cherche l'enfant par `root.transform.Find("EnemySprite")`. Un nom différent → `spriteImage` et `animator` null → sprite non affiché, fade non fonctionnel, ciblage impossible.
45. **`ennemi.data.attack` direct — interdit** : toujours passer par `GetEnemyAttack(ennemi)` pour tenir compte des `combatStatBonuses` et statuts `ModifyStat`. La lecture directe ignore tous les buffs/debuffs runtime.
46. **`deathEffects` — ordre dans `CheckEnemyDeath`** : les `deathEffects` sont appliqués APRÈS l'exclusion du ciblage mais AVANT `AllEnemiesDead()`. Si un deathEffect tue le joueur, `combatEnded` devient true → le `return` après la boucle évite de déclencher faussement la victoire.
47. **`spawnEffects` et `deathEffects` — `ApplyEnemyEffect`** : `Self` désigne l'ennemi lui-même (ex : se donner de l'armure). Pour cibler le joueur (ex : explosion), utiliser `SingleEnemy`.
48. **`StatusIconContainer` — `GridLayoutGroup` requis** : le container doit avoir un `GridLayoutGroup` dans la scène. `RefreshPlayerStatusIcons()` et `RefreshEnemyStatusIcons()` écrasent `constraintCount` au runtime — inutile de le configurer à la main, mais le composant doit être présent.
49. **`StatusIcon` prefab — composant `StatusIcon` à la racine** : `RefreshPlayerStatusIcons` et `RefreshEnemyStatusIcons` font `go.GetComponent<StatusIcon>()` — si le composant est sur un enfant et non sur la racine, il retourne null et l'icône n'est pas enregistrée.
50. **`EnemyStatusContainer` — nom exact requis dans l'EnemyUIPrefab** : `SpawnEnemyUI` cherche le container par `root.transform.Find("EnemyStatusContainer")`. Un nom différent → `statusIconContainer` null → icônes ennemis silencieusement absentes (pas d'erreur).
51. **`StatusIconPrefab` — `RectTransform` obligatoire à la racine** : le prefab doit être créé depuis la hiérarchie Canvas (UI → Empty), pas depuis le Project panel. Un prefab créé hors Canvas a un `Transform` 3D à la racine — le `GridLayoutGroup` ne peut pas le positionner et toutes les icônes se superposent à `(0,0)`. Symptôme : icônes présentes dans la hiérarchie mais visuellement au même endroit. `RefreshPlayerStatusIcons` et `RefreshEnemyStatusIcons` appellent `SetActive(false)` avant `Destroy()` pour retrait immédiat du layout (Piège #3).
52. **Test RunManager-dépendant** : tout test impliquant des données de run (`selectedCharacter`, tags du héros, modules, équipements, cooldowns nav) **doit être lancé depuis le MainMenu** — lancer directement une scène Navigation ou Combat laisse `RunManager` non initialisé, ce qui peut faire échouer silencieusement les effets qui dépendent de ces données (ex : `filtreParTagHero`, `DonnerModule`, `ObtenirFiltreTag`).
