using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AchievementTracker
{
    /// <summary>Место, где встречается существо: официальная метка локации (может не быть) и её биом.</summary>
    internal sealed class Place
    {
        public string Label;
        public Heightmap.Biome Biome;
    }

    /// <summary>
    /// Кто живёт в подземельях и локациях. Префабы локаций и комнат подземелий в этой версии игры лежат в отдельных
    /// пакетах и загружаются по требованию, поэтому один раз проходим по ним в фоне — асинхронно, по одному за раз, —
    /// читаем спавнеры и отпускаем. Результат кэшируем на диск до обновления игры.
    /// </summary>
    internal static class LocationScanner
    {
        private const float LoadTimeout = 15f;

        // Тема комнат подземелья → официальное название из локализации игры. Только однозначные соответствия;
        // для Crypt, Hole, GoblinCamp и прочих подходящего названия нет или их несколько — не подставляем.
        private static readonly Dictionary<Room.Theme, string> ThemeLabels = new Dictionary<Room.Theme, string>
        {
            { Room.Theme.SunkenCrypt, "$location_sunkencrypt" },
            { Room.Theme.ForestCrypt, "$location_forestcrypt" },
            { Room.Theme.Cave, "$location_mountaincave" },
            { Room.Theme.DvergerTown, "$location_dvergrtown" },
            { Room.Theme.DvergerBoss, "$location_dvergrboss" },
            { Room.Theme.ForestCryptHildir, "$hud_pin_hildir1" },
            { Room.Theme.CaveHildir, "$hud_pin_hildir2" },
            { Room.Theme.PlainsFortHildir, "$hud_pin_hildir3" },
            { Room.Theme.FortressRuins, "$charredfortress" },
            { Room.Theme.MorkHalla, "$location_morkhalla" },
        };

        private static readonly Dictionary<string, List<Place>> Homes = new Dictionary<string, List<Place>>();
        public static readonly List<string> Debug = new List<string>();
        private static bool s_running;
        private static bool s_failedLogged;
        private static ZoneSystem s_for;

        public static bool Ready { get; private set; }

        private static string CacheFile => Path.Combine(BepInEx.Paths.CachePath, "AchievementTracker_places.txt");

        public static List<Place> Get(string creature) =>
            creature != null && Homes.TryGetValue(creature, out List<Place> list) ? list : null;

        public static IEnumerable<KeyValuePair<string, List<Place>>> All() => Homes;

        /// <summary>Вызывается каждый кадр; запускает скан один раз на мир, если нет свежего кэша.</summary>
        public static void Tick(MonoBehaviour host)
        {
            if (s_running) return;
            if (Ready && s_for == ZoneSystem.instance) return;
            // Начинаем, как только игра подготовила список локаций — ещё за экраном загрузки мира
            if (ZoneSystem.instance == null || DungeonDB.instance == null || ZoneSystem.instance.m_locations.Count == 0) return;
            if (s_for != ZoneSystem.instance)
            {
                s_for = ZoneSystem.instance;
                Ready = false;
                Homes.Clear();
                if (TryLoadCache())
                {
                    Ready = true;
                    return;
                }
            }
            s_running = true;
            host.StartCoroutine(Scan());
        }

        /// <summary>achtracker rescan — забыть кэш и просканировать заново.</summary>
        public static void ForceRescan()
        {
            try
            {
                if (File.Exists(CacheFile)) File.Delete(CacheFile);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not delete location cache: " + e.Message);
            }
            if (!s_running)
            {
                Ready = false;
                Homes.Clear();
                s_for = null;
            }
        }

        private static string CacheKey() =>
            $"v2|{(global::Version.GetVersionString())}|{ZoneSystem.instance.m_locations.Count}|{DungeonDB.GetRooms().Count}";

        private static bool TryLoadCache()
        {
            try
            {
                if (!File.Exists(CacheFile)) return false;
                string[] lines = File.ReadAllLines(CacheFile, Encoding.UTF8);
                if (lines.Length == 0 || lines[0] != CacheKey()) return false;
                foreach (string line in lines.Skip(1))
                {
                    string[] p = line.Split('\t');
                    if (p.Length != 3 || !int.TryParse(p[2], out int biome)) continue;
                    Add(p[0], p[1].Length == 0 ? null : p[1], (Heightmap.Biome)biome);
                }
                Plugin.Log.LogInfo($"Location data loaded from cache: {Homes.Count} creatures");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Location cache unreadable, rescanning: " + e.Message);
                Homes.Clear();
                return false;
            }
        }

        private static void SaveCache()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(CacheKey());
                foreach (KeyValuePair<string, List<Place>> kv in Homes)
                {
                    foreach (Place p in kv.Value) sb.AppendLine($"{kv.Key}\t{p.Label ?? ""}\t{(int)p.Biome}");
                }
                Directory.CreateDirectory(BepInEx.Paths.CachePath);
                File.WriteAllText(CacheFile, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Could not save location cache: " + e.Message);
            }
        }

        private static void Add(string creature, string label, Heightmap.Biome biome)
        {
            if (string.IsNullOrEmpty(creature)) return;
            if (!Homes.TryGetValue(creature, out List<Place> list)) Homes[creature] = list = new List<Place>();
            if (list.Any(p => p.Label == label && p.Biome == biome)) return;
            list.Add(new Place { Label = label, Biome = biome });
        }

        private static IEnumerable<string> CreaturesIn(GameObject go)
        {
            if (go == null) yield break;
            foreach (CreatureSpawner cs in go.GetComponentsInChildren<CreatureSpawner>(true))
            {
                Character ch = cs.m_creaturePrefab != null ? cs.m_creaturePrefab.GetComponent<Character>() : null;
                if (ch != null) yield return ch.m_name;
            }
            foreach (SpawnArea sa in go.GetComponentsInChildren<SpawnArea>(true))
            {
                foreach (SpawnArea.SpawnData sd in sa.m_prefabs)
                {
                    Character ch = sd?.m_prefab != null ? sd.m_prefab.GetComponent<Character>() : null;
                    if (ch != null) yield return ch.m_name;
                }
            }
        }

        private static IEnumerable<Room.Theme> Flags(Room.Theme themes)
        {
            foreach (Room.Theme t in Enum.GetValues(typeof(Room.Theme)))
            {
                if (t != Room.Theme.None && (themes & t) != 0) yield return t;
            }
        }

        /// <summary>Загрузка в полёте: сам объект, ссылка на префаб и время начала загрузки.</summary>
        private sealed class InFlight<T>
        {
            public T Item;
            public SoftReferenceableAssets.SoftReference<GameObject> Ref;
            public float Started;
        }

        /// <summary>
        /// Асинхронно грузит префабы и отдаёт их в read. Пока идёт экран загрузки мира (персонажа ещё нет) —
        /// по 8 одновременно, это не заметно; когда персонаж уже в мире — по одному, чтобы не было подвисаний.
        /// </summary>
        private static IEnumerator Pipeline<T>(ZoneSystem world, List<T> items,
            Func<T, SoftReferenceableAssets.SoftReference<GameObject>> refOf, Action<T, GameObject> read, Action onDone)
        {
            var inflight = new List<InFlight<T>>();
            int next = 0;
            while (next < items.Count || inflight.Count > 0)
            {
                if (world != ZoneSystem.instance)
                {
                    foreach (InFlight<T> f in inflight) f.Ref.Release();
                    yield break; // вышли из мира
                }
                int limit = Player.m_localPlayer == null ? 8 : 1;
                while (inflight.Count < limit && next < items.Count)
                {
                    var f = new InFlight<T> { Item = items[next++], Started = Time.realtimeSinceStartup };
                    f.Ref = refOf(f.Item);
                    f.Ref.LoadAsync();
                    inflight.Add(f);
                }
                for (int i = inflight.Count - 1; i >= 0; i--)
                {
                    InFlight<T> f = inflight[i];
                    bool loaded = f.Ref.IsLoaded;
                    if (!loaded && Time.realtimeSinceStartup - f.Started < LoadTimeout) continue;
                    try
                    {
                        if (loaded) read(f.Item, f.Ref.Asset);
                    }
                    catch (Exception e)
                    {
                        LogOnce(e);
                    }
                    finally
                    {
                        f.Ref.Release();
                    }
                    inflight.RemoveAt(i);
                    onDone();
                }
                yield return null;
            }
        }

        private static IEnumerator Scan()
        {
            float started = Time.realtimeSinceStartup;
            Debug.Clear();
            ZoneSystem world = ZoneSystem.instance;
            var themePlaces = new Dictionary<Room.Theme, List<Place>>();
            int n = 0;

            List<ZoneSystem.ZoneLocation> locations = world.m_locations
                .Where(zl => zl != null && zl.m_enable && zl.m_prefab.IsValid).ToList();
            yield return Pipeline(world, locations, zl => zl.m_prefab, (zl, go) => ReadLocation(zl, go, themePlaces), () => n++);
            if (world != ZoneSystem.instance) { s_running = false; yield break; }

            // Комнаты тем, которых нет ни в одном подземелье, грузить незачем
            List<DungeonDB.RoomData> rooms = DungeonDB.GetRooms()
                .Where(rd => rd != null && rd.m_enabled && rd.m_prefab.IsValid && Flags(rd.m_theme).Any(themePlaces.ContainsKey)).ToList();
            yield return Pipeline(world, rooms, rd => rd.m_prefab, (rd, go) => ReadRoom(rd, go, themePlaces), () => n++);
            if (world != ZoneSystem.instance) { s_running = false; yield break; }

            SaveCache();
            Ready = true;
            s_running = false;
            Plugin.Log.LogInfo($"Location scan done in {Time.realtimeSinceStartup - started:0.0}s: {Homes.Count} creatures in {n} locations/rooms");
        }

        private static void ReadLocation(ZoneSystem.ZoneLocation zl, GameObject go, Dictionary<Room.Theme, List<Place>> themePlaces)
        {
            if (go == null) return;
            var locs = go.GetComponentsInChildren<Location>(true).ToList();
            GameObject interior = locs.Select(l => l.m_interiorPrefab).FirstOrDefault(p => p != null);
            if (interior != null) locs.AddRange(interior.GetComponentsInChildren<Location>(true));
            string label = locs.Select(l => l.m_discoverLabel).FirstOrDefault(s => !string.IsNullOrEmpty(s));

            var gens = go.GetComponentsInChildren<DungeonGenerator>(true).ToList();
            if (interior != null) gens.AddRange(interior.GetComponentsInChildren<DungeonGenerator>(true));
            Room.Theme themes = gens.Aggregate(Room.Theme.None, (acc, g) => acc | g.m_themes);
            if (label == null)
            {
                // Однозначная тема подземелья с официальным названием
                List<string> byTheme = Flags(themes).Where(ThemeLabels.ContainsKey).Select(t => ThemeLabels[t]).Distinct().ToList();
                if (byTheme.Count == 1) label = byTheme[0];
            }

            var place = new Place { Label = label, Biome = zl.m_biome };
            List<string> creatures = CreaturesIn(go).Concat(CreaturesIn(interior)).Distinct().ToList();
            foreach (string c in creatures) Add(c, place.Label, place.Biome);
            foreach (Room.Theme t in Flags(themes))
            {
                if (!themePlaces.TryGetValue(t, out List<Place> list)) themePlaces[t] = list = new List<Place>();
                list.Add(place);
            }
            Debug.Add($"{zl.m_prefabName}: biome={zl.m_biome} label='{label}' themes={themes} creatures=[{string.Join(",", creatures)}]");
        }

        private static void ReadRoom(DungeonDB.RoomData rd, GameObject go, Dictionary<Room.Theme, List<Place>> themePlaces)
        {
            if (go == null) return;
            List<string> creatures = CreaturesIn(go).Distinct().ToList();
            if (creatures.Count == 0) return;
            foreach (Room.Theme t in Flags(rd.m_theme))
            {
                if (!themePlaces.TryGetValue(t, out List<Place> places)) continue;
                foreach (Place p in places)
                {
                    foreach (string c in creatures) Add(c, p.Label, p.Biome);
                }
            }
        }

        private static void LogOnce(Exception e)
        {
            if (s_failedLogged) return;
            s_failedLogged = true;
            Plugin.Log.LogWarning("Location scan: some prefabs could not be read: " + e.Message);
        }

        private static string Clean(string s) => Regex.Replace(s ?? "", "<[^>]+>", "");

        /// <summary>
        /// Где встречается существо, без повторов: «водится: …; в локациях: «Затонувшие склепы» (Болото); в локациях биома «Горы»».
        /// Безымянные локации в биомах, где существо и так водится или уже назван конкретное место, не упоминаем.
        /// </summary>
        public static string Describe(Heightmap.Biome wild, List<Place> places, bool boss)
        {
            var parts = new List<string>();
            if (wild != Heightmap.Biome.None)
            {
                parts.Add((boss ? Loc.S("босс: ", "boss: ") : Loc.S("водится: ", "lives in: ")) + Names.Biomes(wild));
            }
            if (places == null || places.Count == 0) return string.Join("; ", parts);

            var named = new List<string>();
            Heightmap.Biome namedBiomes = Heightmap.Biome.None;
            foreach (IGrouping<string, Place> g in places.Where(p => p.Label != null).GroupBy(p => p.Label))
            {
                Heightmap.Biome b = g.Aggregate(Heightmap.Biome.None, (acc, p) => acc | p.Biome);
                namedBiomes |= b;
                string name = Clean(Names.L(g.Key));
                named.Add(b == Heightmap.Biome.None ? $"«{name}»" : $"«{name}» ({Names.Biomes(b)})");
            }
            if (named.Count > 0) parts.Add(Loc.S("в локациях: ", "in locations: ") + string.Join(", ", named));

            Heightmap.Biome unnamed = places.Where(p => p.Label == null).Aggregate(Heightmap.Biome.None, (acc, p) => acc | p.Biome);
            unnamed &= ~(wild | namedBiomes);
            if (unnamed != Heightmap.Biome.None)
            {
                int count = Names.Split(unnamed).Count();
                parts.Add(count == 1
                    ? Loc.S($"в локациях биома «{Names.Biomes(unnamed)}»", $"in locations of the {Names.Biomes(unnamed)} biome")
                    : Loc.S($"в локациях биомов: {Names.Biomes(unnamed)}", $"in locations of these biomes: {Names.Biomes(unnamed)}"));
            }
            return string.Join("; ", parts);
        }

        public static Heightmap.Biome Biomes(List<Place> places) =>
            places.Aggregate(Heightmap.Biome.None, (acc, p) => acc | p.Biome);
    }
}
