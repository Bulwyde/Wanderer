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
- `EnterRoom(CellData)` : assigne `currentCellType`, `currentEnemyData` (priorité : `cell.specificEnemy` > `ResolveEnemyPool()` > null/fallback Inspector), logge l'ennemi résolu.
- `ResolveEnemyPool(CellType)` : retourne `normalEnemyPool`/`eliteEnemyPool`/`bossEnemyPool` depuis `currentMapData`. Null si `currentMapData` null.
- `currentMapData` : assigné par `NavigationManager` avant tout `GoToCombat()` ou `GoToShop()` — requis pour `ResolveEnemyPool()` et `ShopManager`.
- `currentEnemyData` : lu par `CombatManager.Start()` en priorité sur le champ Inspector.
- `EquipItem()` appelle automatiquement `RecalculerMaxHP()` si `maxHP > 0` (ignoré pendant le seeding initial).
- `AddModule()` appelle automatiquement `ModuleManager.NotifyModulesChanged()`.
- `GetOrCreateShopState(CellData, ShopData)` : génère l'inventaire shop à la 1ère visite, persiste dans `shopStates[x,y]`, reset dans `StartNewRun()`.
- **Cooldowns nav** : `combatsTermines`/`eventsTermines` (compteurs run), `navSkillCooldowns` (dict skillID → `NavSkillCooldownState`). `SetNavSkillCooldown(skillID, type, count, tag?)` · `IsNavSkillReady(skillID)` · `TickCooldownsDe(NavCooldownType)` · `TickCooldownsAvecTag(List<TagData>)`. Tout resetté dans `StartNewRun()`. Classe helper `NavSkillCooldownState` définie en bas du fichier.

**`ModuleManager.cs`** — DDOL, **doit être dans la scène Navigation**. `OnModulesChanged` statique (HUD sans instance) mais les effets GameEvents nécessitent une instance.

### Navigation

**`NavigationManager.cs`** — Déplacements clavier, brouillard de guerre (3 sets), `AppliquerEffetsNav`, `RevealZoneChoice`, tirage d'events (`ChoisirEventAleatoire` + fallback `RandomEvents`). Assigne `currentMapData` avant `EnterRoom()` pour les cases Classic/Elite/Boss et avant `GoToShop()`. Gère les cooldowns des skills de navigation : `SpawnSkillsJambes()` grise les boutons selon `IsNavSkillReady`, `UtiliserSkillJambes()` appelle `SetNavSkillCooldown` après effet. Détecte la première visite d'un shop (`GetShopState == null`) pour déclencher `TickCooldownsDe(ShopDecouvert)`.

**`MapData.cs`** — ScriptableObject grille. `CellData` a `specificEnemy` (EnemyData), `eventList`/`eventPool`, `shopData`. `MapData` a `normalEnemyPool`, `eliteEnemyPool`, `bossEnemyPool` (EnemyPool).

**`CellType` enum (ordre fixe — ne jamais insérer au milieu) :**
`Empty(0)` `Start(1)` `Boss(2)` `Classic(3)` `Event(4)` `NonNavigable(5)` `Shop(6)` `Elite(7)`

### Combat

**`CombatManager.cs`** — États `PlayerTurn → EnemyTurn → Victory/Defeat`. `ResolveEquipment()` au démarrage : lit `RunManager.selectedCharacter` + `currentEnemyData` (fallback Inspector). Seeding dans `ResolveEquipment()` = no-op en jeu normal (déjà fait par `StartNewRun()`). Sur victoire boss → `EndRun()` + `GoToMainMenu()` sans `ClearCurrentRoom()`.

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
| `EnemyData` | Stats, actions IA, lootPool, consumableLootPool, `creditsLoot` |
| `EnemyPool` | Pool d'ennemis aléatoires (`PickRandom()`). Assigné sur `MapData` (3 pools) |
| `SkillData` | Compétence (coût énergie, cooldown, `List<EffectData> effects`). Navigation : `isNavigationSkill`, `navEffects`, `navCooldownType` (`NavCooldownType` enum), `navCooldownCount`, `navCooldownTag` |
| `EffectData` | Effet universel (trigger, action, target, value) — skills/consommables/modules/passifs |
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
**Editors Data (filtrage tags) :** `CharacterDataEditor`, `ConsumableDataEditor`, `EquipmentDataEditor`, `EventDataEditor`, `EnemyDataEditor`, `MapDataEditor`, `ModuleDataEditor` — tous s'appuient sur `TagListFilterUtil.DrawFilteredTagList(prop, categorie)` pour ne proposer que les tags de la catégorie correspondante (+ tags "Everything" = toutes catégories cochées). Cache statique par catégorie, invalidé à chaque recompilation.

**Prefabs :** `ChoiceButton` (360×65px), `SkillButtonPrefab`, `LootCard`, `ConsumableButton`, `ModuleIcon` (48×48px).
`ConsumableButton.SetInteractable(false)` grise + réduit taille d'un tiers. Désactiver `ChildControlSize` sur le container. `ModuleHUD` enfant direct du Canvas (jamais du `mapContainer`).

---

## Systèmes clés — décisions non-évidentes

**ModifyStat — 3 cas distincts :**
- *Permanent run* (events/modules hors-combat) → `RunManager.AddStatBonus()`, stocké dans `runStatBonuses`, intégré dans `ResolveEquipment()` et `RecalculerMaxHP()`.
- *Temporaire ce combat* (skills/consommables/modules en combat) → `combatStatModifiers`, lu dynamiquement par `GetCurrentAttack()` etc.
- *Statut ModifyStat* → actif tant que stacks > 0. `valueScalesWithStacks=false` : valeur fixe, stacks = durée. `valueScalesWithStacks=true` : valeur = `effectPerStack × stacks`, durée infinie.

**Formule dégâts joueur :** `(skillValue + effectiveAttack + flat) × (1 + pct)`. Crit appliqué après, défense soustraite avant le crit.

**StatusDecayTiming :** `OnTurnStart` (défaut) ou `OnTurnEnd`. `ApplyPerTurnEffects` = effets seuls. `DecayStatuses(timing)` = décroissance filtrée. Nettoyage des stacks à 0 intégré dans `DecayStatuses`.

**Crédits :** `RunManager.credits`, initialisé depuis `CharacterData.startingCredits`. `AddCredits(int)` (plancher 0). `HasEnoughCredits(int)`. `EffectAction.AddCredits` branché partout. `EventEffectType.ModifyCredits` : EventManager désactive le bouton si solde insuffisant + suffixe `[Cout : X credits]`.

**Bus d'événements (`GameEvents.cs`) :**
`TriggerPlayerTurnStarted/Ended`, `TriggerPlayerDealtDamage(dmg)`, `TriggerPlayerDamaged(dmg)`, `TriggerEnemyDied()` → tous écoutés par `ModuleManager`. `OnRoomEntered`/`OnChestOpened`/`OnShopEntered` définis mais pas encore écoutés.

**Cooldowns de skills de navigation :**
`NavCooldownType` enum dans `SkillData.cs` : `None | ShopDecouvert | CombatsTermines | EventsTermines | MondeTermine | CombatEnnemisAvecTag`. Le type détermine l'événement qui recharge le skill. Points de déclenchement : `CombatManager.EndCombat` (→ `TickCooldownsAvecTag` sur les tags de l'ennemi) · `CombatManager.OnLootContinueClicked` (→ `TickCooldownsDe(CombatsTermines)` ou `MondeTermine` selon boss ou non, + `combatsTermines++`) · `EventManager.OnContinueClicked` (→ `TickCooldownsDe(EventsTermines)` + `eventsTermines++`) · `NavigationManager.OnRoomEntered` Shop (→ `TickCooldownsDe(ShopDecouvert)` si première visite). Le bouton de navigation est grisé si `!IsNavSkillReady(skillID)`. ⚠️ `skillID` doit être renseigné sur le `SkillData` sinon le cooldown est ignoré.

---

## État du développement

### Fonctionnel ✅
Combat complet (tours, énergie, armure StS, cooldowns, statuts, crits, regen, lifesteal) · IA ennemie circulaire · Équipement (stats effectives, skills, passifs) · Loot post-combat · Navigation (brouillard, clavier, sauvegarde) · Modules (GameEvents, HUD, OnFightStart) · Consommables (3 scènes) · Events narratifs (tous effets) · Pool d'events (ManualList/FromPool, anti-doublon) · EquipmentOfferController partagé · NavEffect complets · MainMenu complet · Seeding au lancement · Boutons passifs équipement · Crédits · Marchand (ShopData, persistance par case) · Boss (victoire → EndRun + MainMenu) · EnemyPool + CellType.Elite · Sélection ennemi par case/pool · **Système de tags** (TagData sur tous les Data, sync nom asset ↔ tagName, multi-édition) · **Cooldowns skills de navigation** (5 types, bouton grisé, persist RunManager) · **Filtrage tags par catégorie dans l'Inspector** (8 editors Data + TagListFilterUtil)

### À faire 🔧
- Scène de sélection de personnage (`MainMenuManager.defaultCharacter` = placeholder)
- Passifs torse/tête : effets actifs mais pas de bouton d'affichage
- Icônes par type de case dans `MapRenderer`
- Sons, animations, retours visuels
- Popup "Utiliser / Jeter" consommables
- Paramètres (panel)
- Mécaniques boss spéciales (phases, actions uniques) dans `EnemyData`/`EnemyAI`
- Événements de craft (à définir)
- Localisation (`com.unity.localization`) — après 1ère version jouable
- Conditions de tags dans `EffectData` — système de tags posé, vérification à implémenter. Les cas d'usage identifiés couvrent 4 catégories distinctes qui nécessitent chacune un mécanisme différent :
  - **A** : Condition on/off (l'effet se déclenche si un objet a le tag) — ex : "+50% dégâts sur les ennemis Boss", "bonus si carte World1"
  - **B** : Filtrer ce qu'on reçoit par tag — ex : "obtenez un consommable de type Soin", "obtenez un équipement de type Handgun"
  - **C** : Scaling par comptage de tags — ex : "+1 attaque par équipement Saber équipé", "skills Poison appliquent 1 stack supplémentaire"
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
