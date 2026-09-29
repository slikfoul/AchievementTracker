using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace AchievementTracker
{
    /// <summary>
    /// Что игроку уже доступно на текущем этапе: открытые биомы, рецепты, инструменты, побеждённые боссы.
    /// Справочники (кто где водится, кто что роняет) строятся один раз на сцену.
    /// </summary>
    public static class Progression
    {
        private static readonly AccessTools.FieldRef<Player, HashSet<string>> KnownBiomeRef =
            AccessTools.FieldRefAccess<Player, HashSet<string>>("m_knownBiome");

        // Боссы не спавнятся через SpawnSystem — задаём биом вручную по имени префаба
        private static readonly Dictionary<string, Heightmap.Biome> BossPrefabs = new Dictionary<string, Heightmap.Biome>
        {
            { "Eikthyr", Heightmap.Biome.Meadows },
            { "gd_king", Heightmap.Biome.BlackForest },
            { "Bonemass", Heightmap.Biome.Swamp },
            { "Dragon", Heightmap.Biome.Mountain },
            { "GoblinKing", Heightmap.Biome.Plains },
            { "SeekerQueen", Heightmap.Biome.Mistlands },
            { "Fader", Heightmap.Biome.AshLands },
        };

        private static readonly Dictionary<string, string> BossTokens = new Dictionary<string, string>
        {
            { "Eikthyr", "$enemy_eikthyr" },
            { "Elder", "$enemy_gdking" },
            { "Bonemass", "$enemy_bonemass" },
            { "Moder", "$enemy_dragon" },
            { "Yagluth", "$enemy_goblinking" },
            { "Queen", "$enemy_seekerqueen" },
            { "Ashlands", "$enemy_fader" },
        };

        private static readonly Dictionary<string, string> PowerBossKeys = new Dictionary<string, string>
        {
            { "Eikthyr", "defeated_eikthyr" },
            { "Elder", "defeated_gdking" },
            { "Bonemass", "defeated_bonemass" },
            { "Moder", "defeated_dragon" },
            { "Yagluth", "defeated_goblinking" },
            { "Queen", "defeated_queen" },
            { "Ashlands", "defeated_fader" },
        };

        private static readonly Dictionary<PlayerStatType, Heightmap.Biome> StatBiomes = new Dictionary<PlayerStatType, Heightmap.Biome>
        {
            { PlayerStatType.TreeSwamp, Heightmap.Biome.Swamp },
            { PlayerStatType.TreeSnowFir, Heightmap.Biome.Mountain },
            { PlayerStatType.TreeSnowPine, Heightmap.Biome.Mountain },
            { PlayerStatType.TreeYggdrasilShoot, Heightmap.Biome.Mistlands },
            { PlayerStatType.TreeAshlands, Heightmap.Biome.AshLands },
            { PlayerStatType.TreePine, Heightmap.Biome.BlackForest },
            { PlayerStatType.DeathByFreezing, Heightmap.Biome.Mountain },
            { PlayerStatType.LavaLeviathanSink, Heightmap.Biome.AshLands },
            { PlayerStatType.DeathByAshlandsLava, Heightmap.Biome.AshLands },
            { PlayerStatType.DeathByAshlandsOcean, Heightmap.Biome.AshLands },
            { PlayerStatType.DeathByCinderFire, Heightmap.Biome.AshLands },
            { PlayerStatType.DeathByIncinerator, Heightmap.Biome.AshLands },
            { PlayerStatType.DeathByCatapult, Heightmap.Biome.AshLands },
            { PlayerStatType.LeviathanSink, Heightmap.Biome.Ocean },
        };

        private static ZNetScene s_builtFor;
        private static readonly Dictionary<string, Heightmap.Biome> CreatureBiomes = new Dictionary<string, Heightmap.Biome>();
        private static readonly HashSet<string> Bosses = new HashSet<string>();
        private static readonly Dictionary<string, List<string>> ItemDroppers = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, Recipe> RecipesByItem = new Dictionary<string, Recipe>();
        private static readonly Dictionary<string, Piece> PiecesByName = new Dictionary<string, Piece>();

        private static void EnsureBuilt()
        {
            if (s_builtFor != null && s_builtFor == ZNetScene.instance) return;
            if (ZNetScene.instance == null || ObjectDB.instance == null) return;
            s_builtFor = ZNetScene.instance;
            CreatureBiomes.Clear();
            Bosses.Clear();
            ItemDroppers.Clear();
            RecipesByItem.Clear();
            PiecesByName.Clear();

            SpawnDebug.Clear();
            try
            {
                // Какая погода в каком биоме бывает — по таблице самой игры
                var envBiomes = new Dictionary<string, Heightmap.Biome>(StringComparer.OrdinalIgnoreCase);
                if (EnvMan.instance != null)
                {
                    foreach (BiomeEnvSetup setup in EnvMan.instance.m_biomes)
                    {
                        foreach (EnvEntry e in setup.m_environments)
                        {
                            if (string.IsNullOrEmpty(e?.m_environment)) continue;
                            envBiomes.TryGetValue(e.m_environment, out Heightmap.Biome eb);
                            envBiomes[e.m_environment] = eb | setup.m_biome;
                        }
                    }
                }

                var narrow = new Dictionary<string, Heightmap.Biome>();
                var wide = new Dictionary<string, Heightmap.Biome>();
                var keyed = new Dictionary<string, Heightmap.Biome>();
                var lists = new HashSet<SpawnSystemList>();
                SpawnSystem ss = ZoneSystem.instance?.m_zoneCtrlPrefab?.GetComponent<SpawnSystem>();
                if (ss != null) lists.UnionWith(ss.m_spawnLists.Where(l => l != null));
                lists.UnionWith(Resources.FindObjectsOfTypeAll<SpawnSystemList>());
                foreach (SpawnSystemList list in lists)
                {
                    foreach (SpawnSystem.SpawnData sd in list.m_spawners)
                    {
                        if (sd?.m_prefab == null || !sd.m_enabled) continue;
                        Character ch = sd.m_prefab.GetComponent<Character>();
                        if (ch == null || string.IsNullOrEmpty(ch.m_name)) continue;
                        Heightmap.Biome b = HomeBiomes(sd, envBiomes, out string why);
                        SpawnDebug.Add($"{sd.m_prefab.name} ({ch.m_name}): biome={sd.m_biome} area={sd.m_biomeArea} " +
                                       $"key='{sd.m_requiredGlobalKey}' env=[{string.Join(",", sd.m_requiredEnvironments)}] " +
                                       $"event='{sd.m_requiredPersistentEvent}' -> {b} ({why})");
                        if (b == Heightmap.Biome.None) continue;
                        // Спавн после победы над боссом (defeated_*) — дополнительный: драугры в тумане на Лугах, фулинги
                        // в Черном лесу и т.п. Основной биом берём из спавна без условий, дополнительные — только если основного нет.
                        Dictionary<string, Heightmap.Biome> target = Names.Split(b).Count() >= 5 ? wide
                            : string.IsNullOrEmpty(sd.m_requiredGlobalKey) ? narrow : keyed;
                        target.TryGetValue(ch.m_name, out Heightmap.Biome prev);
                        target[ch.m_name] = prev | b;
                    }
                }
                // Широкие маски (5+ биомов) — это особый спавн «везде»; учитываем их, только если точнее данных нет —
                // а если есть только они, честно говорим «в особых местах»
                foreach (KeyValuePair<string, Heightmap.Biome> kv in keyed) CreatureBiomes[kv.Key] = kv.Value;
                foreach (KeyValuePair<string, Heightmap.Biome> kv in narrow) CreatureBiomes[kv.Key] = kv.Value;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Failed to read spawn lists: " + e.Message);
            }

            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                Character ch = prefab.GetComponent<Character>();
                if (ch == null || string.IsNullOrEmpty(ch.m_name)) continue;
                if (BossPrefabs.TryGetValue(prefab.name, out Heightmap.Biome bossBiome))
                {
                    CreatureBiomes[ch.m_name] = bossBiome;
                    Bosses.Add(ch.m_name);
                }
                CharacterDrop cd = prefab.GetComponent<CharacterDrop>();
                if (cd == null) continue;
                foreach (CharacterDrop.Drop d in cd.m_drops)
                {
                    ItemDrop item = d?.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>() : null;
                    if (item == null) continue;
                    string name = item.m_itemData.m_shared.m_name;
                    if (!ItemDroppers.TryGetValue(name, out List<string> who)) ItemDroppers[name] = who = new List<string>();
                    if (!who.Contains(ch.m_name)) who.Add(ch.m_name);
                }
            }

            BuildOrigins();

            foreach (Recipe r in ObjectDB.instance.m_recipes)
            {
                if (r?.m_item == null) continue;
                string name = r.m_item.m_itemData.m_shared.m_name;
                if (!RecipesByItem.ContainsKey(name)) RecipesByItem[name] = r;
            }

            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                PieceTable table = go?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces;
                if (table == null) continue;
                foreach (GameObject pgo in table.m_pieces)
                {
                    Piece p = pgo != null ? pgo.GetComponent<Piece>() : null;
                    if (p != null && !PiecesByName.ContainsKey(p.m_name)) PiecesByName[p.m_name] = p;
                }
            }
        }

        internal enum OriginKind
        {
            Fight,  // появляется в бою с другим существом или при его смерти (призыв, снаряды, эффекты смерти)
            Breed,  // рождается у прирученных (Procreation)
            Hatch,  // вылупляется из предмета-яйца (EggGrow)
            GrowUp  // вырастает из детёныша (Growup)
        }

        internal sealed class Origin
        {
            public OriginKind Kind;
            public string Source; // имя существа-источника или предмета-яйца
        }

        // существо → откуда оно берётся, если само в мире не спавнится
        private static readonly Dictionary<string, List<Origin>> Origins = new Dictionary<string, List<Origin>>();

        private static void AddOrigin(string child, OriginKind kind, string source)
        {
            if (string.IsNullOrEmpty(child) || string.IsNullOrEmpty(source) || child == source) return;
            if (!Origins.TryGetValue(child, out List<Origin> list)) Origins[child] = list = new List<Origin>();
            if (!list.Any(o => o.Kind == kind && o.Source == source)) list.Add(new Origin { Kind = kind, Source = source });
        }

        /// <summary>Существа, которых порождает префаб: призыв, снаряды, спавн при уроне, спавнеры внутри эффектов.</summary>
        private static void CollectSpawned(GameObject go, int depth, HashSet<GameObject> seen, List<string> result)
        {
            if (go == null || depth > 3 || !seen.Add(go)) return;
            if (depth > 0)
            {
                Character ch = go.GetComponent<Character>();
                if (ch != null)
                {
                    if (!string.IsNullOrEmpty(ch.m_name)) result.Add(ch.m_name);
                    return; // внутрь другого существа не идём
                }
            }
            foreach (SpawnAbility sa in go.GetComponentsInChildren<SpawnAbility>(true))
            {
                if (sa.m_spawnPrefab == null) continue;
                foreach (GameObject p in sa.m_spawnPrefab) CollectSpawned(p, depth + 1, seen, result);
            }
            foreach (Projectile pr in go.GetComponentsInChildren<Projectile>(true)) CollectSpawned(pr.m_spawnOnHit, depth + 1, seen, result);
            foreach (SpawnOnDamaged sd in go.GetComponentsInChildren<SpawnOnDamaged>(true)) CollectSpawned(sd.m_spawnOnDamage, depth + 1, seen, result);
            if (depth > 0)
            {
                foreach (CreatureSpawner cs in go.GetComponentsInChildren<CreatureSpawner>(true)) CollectSpawned(cs.m_creaturePrefab, depth + 1, seen, result);
            }
        }

        private static void CollectAttack(Attack attack, HashSet<GameObject> seen, List<string> result)
        {
            if (attack == null) return;
            CollectSpawned(attack.m_attackProjectile, 1, seen, result);
            CollectSpawned(attack.m_spawnOnTrigger, 1, seen, result);
        }

        private static void BuildOrigins()
        {
            Origins.Clear();
            foreach (GameObject prefab in ZNetScene.instance.m_prefabs)
            {
                Character ch = prefab != null ? prefab.GetComponent<Character>() : null;
                if (ch == null || string.IsNullOrEmpty(ch.m_name)) continue;
                try
                {
                    var seen = new HashSet<GameObject>();
                    var spawned = new List<string>();
                    CollectSpawned(prefab, 0, seen, spawned);
                    if (ch.m_deathEffects?.m_effectPrefabs != null)
                    {
                        foreach (EffectList.EffectData e in ch.m_deathEffects.m_effectPrefabs) CollectSpawned(e?.m_prefab, 1, seen, spawned);
                    }
                    if (ch is Humanoid h)
                    {
                        var items = new List<GameObject>();
                        if (h.m_defaultItems != null) items.AddRange(h.m_defaultItems);
                        if (h.m_randomWeapon != null) items.AddRange(h.m_randomWeapon);
                        if (h.m_randomArmor != null) items.AddRange(h.m_randomArmor);
                        if (h.m_randomShield != null) items.AddRange(h.m_randomShield);
                        if (h.m_randomSets != null) items.AddRange(h.m_randomSets.Where(s => s?.m_items != null).SelectMany(s => s.m_items));
                        foreach (GameObject it in items)
                        {
                            ItemDrop.ItemData.SharedData sh = it != null ? it.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                            if (sh == null) continue;
                            CollectAttack(sh.m_attack, seen, spawned);
                            CollectAttack(sh.m_secondaryAttack, seen, spawned);
                        }
                    }
                    foreach (string child in spawned) AddOrigin(child, OriginKind.Fight, ch.m_name);

                    Procreation pc = prefab.GetComponent<Procreation>();
                    if (pc != null)
                    {
                        foreach (GameObject off in new[] { pc.m_offspring, pc.m_noPartnerOffspring })
                        {
                            Character oc = off != null ? off.GetComponent<Character>() : null;
                            if (oc != null) AddOrigin(oc.m_name, OriginKind.Breed, ch.m_name);
                        }
                    }
                    Growup gu = prefab.GetComponent<Growup>();
                    Character adult = gu?.m_grownPrefab != null ? gu.m_grownPrefab.GetComponent<Character>() : null;
                    if (adult != null) AddOrigin(adult.m_name, OriginKind.GrowUp, ch.m_name);
                }
                catch (Exception e)
                {
                    Plugin.Log.LogWarning($"Failed to read what {prefab.name} spawns: {e.Message}");
                }
            }

            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                EggGrow egg = go != null ? go.GetComponent<EggGrow>() : null;
                Character hatch = egg?.m_grownPrefab != null ? egg.m_grownPrefab.GetComponent<Character>() : null;
                ItemDrop id = go.GetComponent<ItemDrop>();
                if (hatch != null && id != null) AddOrigin(hatch.m_name, OriginKind.Hatch, id.m_itemData.m_shared.m_name);
            }
        }

        /// <summary>Сырые данные спавна и то, как мод их понял — для achtracker dump.</summary>
        public static readonly List<string> SpawnDebug = new List<string>();

        public static IEnumerable<string> SpawnReport()
        {
            EnsureBuilt();
            return SpawnDebug;
        }

        public static IEnumerable<string> OriginReport()
        {
            EnsureBuilt();
            foreach (KeyValuePair<string, List<Origin>> kv in Origins.OrderBy(k => k.Key))
            {
                yield return $"{kv.Key} ({Names.L(kv.Key)}): " + string.Join("; ", kv.Value.Select(o => $"{o.Kind} <- {o.Source}"));
            }
        }

        public static IEnumerable<KeyValuePair<string, Heightmap.Biome>> CreatureHomes()
        {
            EnsureBuilt();
            return CreatureBiomes;
        }

        /// <summary>
        /// «Родные» биомы записи спавна. Спавн только во время события — не родной. Если запись требует погоды,
        /// оставляем только биомы, где такая погода бывает; погода, не привязанная ни к одному биому (боссы, события), — особый спавн.
        /// </summary>
        private static Heightmap.Biome HomeBiomes(SpawnSystem.SpawnData sd, Dictionary<string, Heightmap.Biome> envBiomes, out string why)
        {
            if (!string.IsNullOrEmpty(sd.m_requiredPersistentEvent))
            {
                why = "event only";
                return Heightmap.Biome.None;
            }
            Heightmap.Biome b = sd.m_biome;
            if (sd.m_requiredEnvironments == null || sd.m_requiredEnvironments.Count == 0)
            {
                why = "regular";
                return b;
            }
            Heightmap.Biome envB = Heightmap.Biome.None;
            foreach (string env in sd.m_requiredEnvironments)
            {
                if (env != null && envBiomes.TryGetValue(env, out Heightmap.Biome eb)) envB |= eb;
            }
            if (envB == Heightmap.Biome.None)
            {
                why = "weather not tied to any biome";
                return Heightmap.Biome.None;
            }
            b &= envB;
            why = b == Heightmap.Biome.None ? "weather never occurs there" : "narrowed by weather";
            return b;
        }

        public static void Check(Requirement r, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            Player p = Player.m_localPlayer;
            if (p == null) return;
            EnsureBuilt();
            switch (r.Kind)
            {
                case ReqKind.Stat:
                    CheckStat(p, r.Stat, out avail, out reason);
                    return;
                case ReqKind.Enemy:
                    CheckCreature(p, r.Key, out avail, out reason);
                    return;
                case ReqKind.ItemPickup:
                    CheckItemSource(p, r.Key, out avail, out reason);
                    return;
                case ReqKind.ItemCraft:
                    CheckRecipe(p, r.Key, out avail, out reason);
                    return;
                case ReqKind.FoodEaten:
                    if (p.IsMaterialKnown(r.Key) || p.IsRecipeKnown(r.Key)) return;
                    if (RecipesByItem.ContainsKey(r.Key)) CheckRecipe(p, r.Key, out avail, out reason);
                    else CheckItemSource(p, r.Key, out avail, out reason);
                    return;
                case ReqKind.Pickable:
                    if (p.IsMaterialKnown(Names.PickableItem(r.Key))) return;
                    avail = Avail.Unknown;
                    reason = Loc.S("ещё не попадалось — ищи в новых местах", "not found yet — explore new places");
                    return;
                case ReqKind.PiecePlaced:
                    CheckPiece(p, r.Key, out avail, out reason);
                    return;
                case ReqKind.OtherAchievement:
                    avail = Avail.Locked;
                    reason = Loc.S("сначала: ", "first: ") + Names.Achievement(r.Other);
                    return;
            }
        }

        private static void CheckStat(Player p, PlayerStatType stat, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            string name = stat.ToString();

            if (name.StartsWith("Explore"))
            {
                CheckExplore(p, name, out avail, out reason);
                return;
            }

            if (name.StartsWith("TreeTier") || name.StartsWith("MineTier"))
            {
                int need = name[name.Length - 1] - '0';
                bool axe = name.StartsWith("Tree");
                int have = MaxToolTier(p, axe ? Skills.SkillType.Axes : Skills.SkillType.Pickaxes);
                if (have < need)
                {
                    avail = Avail.Locked;
                    reason = Loc.S((axe ? "нужен топор" : "нужна кирка") + $" уровня {need} (сейчас {(have < 0 ? "нет" : have.ToString())})", (axe ? "need an axe" : "need a pickaxe") + $" of tier {need} (now {(have < 0 ? "none" : have.ToString())})");
                }
                return;
            }

            foreach (KeyValuePair<string, string> kv in PowerBossKeys)
            {
                if (name == "SetPower" + kv.Key || name == "UsePower" + kv.Key)
                {
                    if (!HasKey(p, kv.Value))
                    {
                        avail = Avail.Locked;
                        reason = Loc.S("нужно победить босса: ", "defeat the boss: ") + Names.L(BossTokens[kv.Key]);
                    }
                    return;
                }
            }
            if (name.StartsWith("SetPower") || name.StartsWith("UsePower"))
            {
                avail = Avail.Unknown;
                reason = Loc.S("нужна сила соответствующего босса", "requires that boss's power");
                return;
            }
            if (stat == PlayerStatType.SetGuardianPower || stat == PlayerStatType.UseGuardianPower)
            {
                if (!PowerBossKeys.Values.Any(k => HasKey(p, k)))
                {
                    avail = Avail.Locked;
                    reason = Loc.S("нужно победить хотя бы одного босса", "defeat at least one boss");
                }
                return;
            }

            if (StatBiomes.TryGetValue(stat, out Heightmap.Biome biome) && !IsBiomeKnown(p, biome))
            {
                avail = Avail.Locked;
                reason = Loc.S("нужно открыть биом: ", "discover biome: ") + Names.Biomes(biome);
            }
        }

        /// <summary>Порог и оси — как в Player (игра считает «восток» отрицательным X, «запад» — положительным).</summary>
        public const float ExploreDistance = 10350f;

        private static void CheckExplore(Player p, string stat, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            Vector3 pos = p.transform.position;
            float d = stat.StartsWith("ExploreNorth") ? pos.z
                : stat.StartsWith("ExploreSouth") ? -pos.z
                : stat.StartsWith("ExploreEast") ? -pos.x
                : pos.x;
            float left = ExploreDistance - d;
            reason = Loc.S(
                $"ты сейчас в {Analyzer.Num(Mathf.Max(0f, d))} м в эту сторону, нужно дальше {Analyzer.Num(ExploreDistance)} м — осталось {Analyzer.Num(Mathf.Max(0f, left))} м",
                $"you are {Analyzer.Num(Mathf.Max(0f, d))} m out this way, need beyond {Analyzer.Num(ExploreDistance)} m — {Analyzer.Num(Mathf.Max(0f, left))} m to go");
            if (stat.EndsWith("NoMap") && !Achievements.IsCleanNoMap())
            {
                avail = Avail.Locked;
                reason = Loc.S("нужен мир с модификатором «", "requires a world with the \"") + Names.L("$menu_nomap") + Loc.S("»", "\" modifier");
            }
        }

        private static void CheckCreature(Player p, string creature, out Avail avail, out string reason) =>
            CheckCreature(p, creature, 0, out avail, out reason);

        private static void CheckCreature(Player p, string creature, int depth, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            // Алтарь (OfferingBowl) из скана локаций: этого босса призывают предметом
            string altarItem = LocationScanner.AltarItem(creature);
            bool boss = Bosses.Contains(creature) || altarItem != null;
            CreatureBiomes.TryGetValue(creature, out Heightmap.Biome wild);
            // Подземелья, локации и алтари — из фонового скана префабов (LocationScanner)
            List<Place> places = LocationScanner.Get(creature);
            string summon = altarItem == null ? "" :
                Loc.S("призывается на алтаре предметом «", "summoned at the altar with \"") +
                Regex.Replace(Names.L(altarItem), "<[^>]+>", "") + Loc.S("»", "\"");
            Heightmap.Biome all = wild | (places != null ? LocationScanner.Biomes(places) : Heightmap.Biome.None);
            if (all == Heightmap.Biome.None)
            {
                // Сам не спавнится — ищем, из кого или из чего появляется (бой, разведение, яйцо, взросление)
                if (depth < 3 && Origins.TryGetValue(creature, out List<Origin> origins) && origins.Count > 0)
                {
                    DescribeOrigins(p, origins, depth, out avail, out reason);
                    return;
                }
                avail = Avail.Unknown;
                reason = LocationScanner.Ready
                    ? Loc.S("не встречается в мире сам по себе: появляется только при особых условиях (события)",
                            "doesn't appear in the world on its own: only under special conditions (events)")
                    : Loc.S("ищу, где встречается (идёт поиск по локациям)…", "looking up where it lives (scanning locations)…");
                return;
            }

            if (IsBiomeKnown(p, all))
            {
                string where = LocationScanner.Describe(wild, places, boss);
                reason = string.Join("; ", new[] { where, summon }.Where(s => s.Length > 0));
                return;
            }
            // Биом ещё не открыт: пишем, какой открыть, и только конкретные названные места — без повтора того же биома
            avail = Avail.Locked;
            string named = LocationScanner.Describe(Heightmap.Biome.None, places, boss, skipUnnamed: all);
            reason = Loc.S("нужно открыть биом: ", "discover biome: ") + Names.Biomes(all) +
                     string.Concat(new[] { named, summon }.Where(s => s.Length > 0).Select(s => " — " + s));
        }

        private static void DescribeOrigins(Player p, List<Origin> origins, int depth, out Avail avail, out string reason)
        {
            avail = Avail.Unknown;
            reason = "";
            var texts = new List<string>();
            bool anyAvailable = false, anyUnknown = false;
            foreach (Origin o in origins)
            {
                Avail a;
                string why;
                string text;
                if (o.Kind == OriginKind.Hatch)
                {
                    // Яйцо — предмет: доступно, если игрок его уже находил
                    a = p.IsMaterialKnown(o.Source) ? Avail.Available : Avail.Unknown;
                    text = Loc.S("вылупляется из предмета «", "hatches from \"") + Names.L(o.Source) + Loc.S("»", "\"");
                }
                else
                {
                    CheckCreature(p, o.Source, depth + 1, out a, out why);
                    string src = Regex.Replace(Names.L(o.Source), "<[^>]+>", "");
                    switch (o.Kind)
                    {
                        case OriginKind.Fight: text = Loc.S($"появляется в бою с «{src}»", $"appears in the fight with \"{src}\""); break;
                        case OriginKind.Breed: text = Loc.S($"рождается у прирученных «{src}»", $"born to tamed \"{src}\""); break;
                        default: text = Loc.S($"вырастает из «{src}»", $"grows up from \"{src}\""); break;
                    }
                    if (!string.IsNullOrEmpty(why)) text += " (" + why + ")";
                }
                texts.Add(text);
                anyAvailable |= a == Avail.Available;
                anyUnknown |= a == Avail.Unknown;
            }
            avail = anyAvailable ? Avail.Available : anyUnknown ? Avail.Unknown : Avail.Locked;
            reason = string.Join("; ", texts.Distinct());
        }

        private static void CheckItemSource(Player p, string item, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            if (p.IsMaterialKnown(item)) return;
            if (ItemDroppers.TryGetValue(item, out List<string> who) && who.Count > 0)
            {
                string best = null;
                foreach (string c in who)
                {
                    CheckCreature(p, c, out Avail a, out _);
                    if (a == Avail.Available) { best = c; break; }
                }
                if (best != null)
                {
                    reason = Loc.S("выпадает с: ", "drops from: ") + Names.L(best);
                    return;
                }
                CheckCreature(p, who[0], out avail, out string why);
                reason = Loc.S("выпадает с: ", "drops from: ") + Names.L(who[0]) + (string.IsNullOrEmpty(why) ? "" : " — " + why);
                return;
            }
            avail = Avail.Unknown;
            reason = Loc.S("ещё не попадалось", "not found yet");
        }

        private static void CheckRecipe(Player p, string item, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            if (p.IsRecipeKnown(item)) return;
            avail = Avail.Locked;
            if (!RecipesByItem.TryGetValue(item, out Recipe recipe))
            {
                reason = Loc.S("рецепт ещё не открыт", "recipe not unlocked yet");
                return;
            }
            reason = Loc.S("рецепт не открыт", "recipe not unlocked") + MissingSuffix(p, recipe.m_resources, recipe.m_craftingStation != null ? recipe.m_craftingStation.m_name : null);
        }

        private static void CheckPiece(Player p, string piece, out Avail avail, out string reason)
        {
            avail = Avail.Available;
            reason = "";
            if (p.IsRecipeKnown(piece)) return;
            avail = Avail.Locked;
            if (!PiecesByName.TryGetValue(piece, out Piece pc))
            {
                reason = Loc.S("чертёж ещё не открыт", "blueprint not unlocked yet");
                return;
            }
            reason = Loc.S("чертёж не открыт", "blueprint not unlocked") + MissingSuffix(p, pc.m_resources, pc.m_craftingStation != null ? pc.m_craftingStation.m_name : null);
        }

        private static string MissingSuffix(Player p, Piece.Requirement[] resources, string station)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(station) && !p.IsRecipeKnown(station)) parts.Add(Loc.S("станция ", "station ") + Names.L(station));
            foreach (Piece.Requirement res in resources)
            {
                ItemDrop drop = res?.m_resItem;
                if (drop == null) continue;
                string n = drop.m_itemData.m_shared.m_name;
                if (!p.IsMaterialKnown(n)) parts.Add(Names.L(n));
            }
            return parts.Count == 0 ? "" : Loc.S("; не хватает: ", "; missing: ") + string.Join(", ", parts);
        }

        public static int MaxToolTier(Player p, Skills.SkillType skill)
        {
            int best = -1;
            foreach (GameObject go in ObjectDB.instance.m_items)
            {
                ItemDrop id = go != null ? go.GetComponent<ItemDrop>() : null;
                if (id == null) continue;
                ItemDrop.ItemData.SharedData sh = id.m_itemData.m_shared;
                if (sh.m_skillType != skill || sh.m_toolTier <= best) continue;
                if (p.IsRecipeKnown(sh.m_name) || p.GetInventory().HaveItem(sh.m_name)) best = sh.m_toolTier;
            }
            return best;
        }

        public static bool HasKey(Player p, string key)
        {
            return (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key)) || p.HaveUniqueKey(key);
        }

        public static bool IsBiomeKnown(Player p, Heightmap.Biome biomes)
        {
            if ((biomes & Heightmap.Biome.Meadows) != 0) return true;
            HashSet<string> known = KnownBiomeRef(p);
            if (known == null) return false;
            foreach (Heightmap.Biome b in Names.Split(biomes))
            {
                string loc = Names.GameL(BiomeSector.GetBiomeName(b));
                // В наборе лежат локализованные имена, иногда с префиксами альт-биомов
                if (known.Any(k => k != null && k.IndexOf(loc, StringComparison.OrdinalIgnoreCase) >= 0)) return true;
            }
            return false;
        }
    }
}
