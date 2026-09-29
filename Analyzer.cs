using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AchievementTracker
{
    public enum ReqKind
    {
        Stat,
        Enemy,
        ItemPickup,
        ItemCraft,
        Pickable,
        FoodEaten,
        PiecePlaced,
        KnownWorld,
        KnownWorldKey,
        KnownCommand,
        OtherAchievement
    }

    /// <summary>Можно ли прямо сейчас продвинуться по конкретному условию.</summary>
    public enum Avail
    {
        Met,
        Available,
        Unknown,
        Locked
    }

    public enum AchStatus
    {
        Done,
        Available,
        Partial,
        Locked
    }

    public class Requirement
    {
        public ReqKind Kind;
        public string Key;
        public PlayerStatType Stat;
        public KillModifiers Modifier;
        public RequirementOperator Op = RequirementOperator.AboveEquals;
        public float Target;
        public Achievement Other;
    }

    public class ReqState
    {
        public Requirement Req;
        public bool HasValue;
        public float Current;
        public bool Met;
        public Avail Avail;
        public string Reason = "";
        public string Name = "";
    }

    public class AchState
    {
        public Achievement Ach;
        public List<ReqState> Reqs = new List<ReqState>();
        public AchStatus Status;
        public bool Hidden;
        public int MetCount;
        public float Fraction;
        public string ProgressText = "";
        public string Summary = "";
        public string DifficultyLock;
    }

    public static class Analyzer
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static bool Ready =>
            Achievements.m_instance != null && Game.instance != null && Game.instance.GetPlayerProfile() != null;

        public static IEnumerable<Achievement> All()
        {
            if (Achievements.m_instance == null) yield break;
            foreach (AchievementList list in Achievements.m_instance.m_achievementLists)
            {
                if (list == null) continue;
                foreach (Achievement a in list.m_achievements)
                {
                    if (a != null) yield return a;
                }
            }
        }

        public static Achievement Find(string id) => All().FirstOrDefault(a => a.m_id == id);

        public static bool IsHidden(Achievement a) => a.m_isSecret && !a.m_unlocked && !Plugin.RevealSecrets.Value;

        public static List<Requirement> GetRequirements(Achievement a)
        {
            var list = new List<Requirement>();
            foreach (Achievement.PlayerStatRequirement r in a.m_statTrigger)
            {
                list.Add(new Requirement { Kind = ReqKind.Stat, Key = r.m_stat.ToString(), Stat = r.m_stat, Target = r.m_amountAboveEquals });
            }
            foreach (Achievement.EnemyStatRequirement r in a.m_enemyStatsTriggers)
            {
                list.Add(new Requirement { Kind = ReqKind.Enemy, Key = r.m_stat, Modifier = r.m_modifier, Op = r.m_operator, Target = r.m_amount });
            }
            AddDict(list, a.m_itemPickupTriggers, ReqKind.ItemPickup);
            AddDict(list, a.m_itemCraftTriggers, ReqKind.ItemCraft);
            AddDict(list, a.m_pickableTriggers, ReqKind.Pickable);
            AddDict(list, a.m_foodEatenTriggers, ReqKind.FoodEaten);
            AddDict(list, a.m_piecePlacedTriggers, ReqKind.PiecePlaced);
            AddDict(list, a.m_knownWorldTriggers, ReqKind.KnownWorld);
            AddDict(list, a.m_knownWorldKeysTriggers, ReqKind.KnownWorldKey);
            AddDict(list, a.m_knownCommandsTriggers, ReqKind.KnownCommand);
            foreach (Achievement other in a.m_otherAchievementTriggers)
            {
                if (other == null) continue;
                list.Add(new Requirement { Kind = ReqKind.OtherAchievement, Key = other.m_id, Other = other, Target = 1f });
            }
            return list;
        }

        public static int RequirementCount(Achievement a) =>
            a.m_statTrigger.Count + a.m_enemyStatsTriggers.Count + a.m_itemPickupTriggers.Count + a.m_itemCraftTriggers.Count +
            a.m_pickableTriggers.Count + a.m_foodEatenTriggers.Count + a.m_piecePlacedTriggers.Count + a.m_knownWorldTriggers.Count +
            a.m_knownWorldKeysTriggers.Count + a.m_knownCommandsTriggers.Count + a.m_otherAchievementTriggers.Count;

        private static void AddDict(List<Requirement> list, List<Achievement.DictStatRequirement> src, ReqKind kind)
        {
            foreach (Achievement.DictStatRequirement r in src)
            {
                list.Add(new Requirement { Kind = kind, Key = r.m_stat, Op = r.m_operator, Target = r.m_amount });
            }
        }

        public static PlayerProfile.PlayerStats StatsFor(Achievement a)
        {
            PlayerProfile.PlayerStats[] all = Game.instance.GetPlayerProfile().m_playerStats;
            int idx = (int)a.m_difficultyRequirement;
            return idx >= 0 && idx < all.Length ? all[idx] : all[(int)DifficultyRequirement.Any];
        }

        public static bool TryGetValue(Requirement r, PlayerProfile.PlayerStats s, out float value)
        {
            value = 0f;
            switch (r.Kind)
            {
                case ReqKind.Stat: return s.m_stats.TryGetValue(r.Stat, out value);
                case ReqKind.Enemy:
                    int m = (int)r.Modifier;
                    return m >= 0 && m < s.m_enemyStats.Length && s.m_enemyStats[m].TryGetValue(r.Key, out value);
                case ReqKind.ItemPickup: return s.m_itemPickupStats.TryGetValue(r.Key, out value);
                case ReqKind.ItemCraft: return s.m_itemCraftStats.TryGetValue(r.Key, out value);
                case ReqKind.Pickable: return s.m_pickableStats.TryGetValue(r.Key, out value);
                case ReqKind.FoodEaten: return s.m_foodEatenStats.TryGetValue(r.Key, out value);
                case ReqKind.PiecePlaced: return s.m_piecesPlacedStats.TryGetValue(r.Key, out value);
                case ReqKind.KnownWorld: return s.m_knownWorlds.TryGetValue(r.Key, out value);
                case ReqKind.KnownWorldKey: return s.m_knownWorldKeys.TryGetValue(r.Key, out value);
                case ReqKind.KnownCommand: return s.m_knownCommands.TryGetValue(r.Key, out value);
                case ReqKind.OtherAchievement:
                    value = r.Other != null && r.Other.m_unlocked ? 1f : 0f;
                    return true;
            }
            return false;
        }

        /// <summary>Та же логика, что в Achievement.CheckUnlocked: без записи в словаре условие не выполнено.</summary>
        public static bool IsMet(Requirement r, bool has, float v)
        {
            if (!has) return false;
            switch (r.Op)
            {
                case RequirementOperator.BelowEquals: return v <= r.Target;
                case RequirementOperator.Equals: return v == r.Target;
                case RequirementOperator.NotEquals: return v != r.Target;
                default: return v >= r.Target;
            }
        }

        /// <summary>Идёт ли сейчас в статистику этого достижения прогресс (сложность мира совпадает).</summary>
        public static string DifficultyLock(Achievement a, List<Requirement> reqs)
        {
            DifficultyRequirement need = a.m_difficultyRequirement;
            if (need <= DifficultyRequirement.Any) return null;
            DifficultyRequirement cur = Achievements.GetCurrentAchievementDifficulty();
            bool onlyEnemies = reqs.Count > 0 && reqs.All(r => r.Kind == ReqKind.Enemy);
            // Убийства засчитываются во все сложности от Casual до текущей, остальное — только в текущую
            bool ok = onlyEnemies ? cur >= need : cur == need;
            string n = Names.Difficulty(need), c = Names.Difficulty(cur);
            return ok ? null : Loc.S($"нужна сложность мира «{n}» (сейчас «{c}»)", $"requires world difficulty \"{n}\" (now \"{c}\")");
        }

        public static AchState Evaluate(Achievement a, bool withAvailability)
        {
            var st = new AchState { Ach = a, Hidden = IsHidden(a) };
            List<Requirement> reqs = GetRequirements(a);
            PlayerProfile.PlayerStats stats = StatsFor(a);
            float fracSum = 0f;
            foreach (Requirement r in reqs)
            {
                var rs = new ReqState { Req = r };
                rs.HasValue = TryGetValue(r, stats, out rs.Current);
                rs.Met = IsMet(r, rs.HasValue, rs.Current);
                if (rs.Met) st.MetCount++;
                fracSum += rs.Met ? 1f : ReqFraction(r, rs.Current);
                if (withAvailability)
                {
                    rs.Name = Names.Requirement(r);
                    if (rs.Met)
                    {
                        rs.Avail = Avail.Met;
                    }
                    else
                    {
                        Progression.Check(r, out rs.Avail, out rs.Reason);
                    }
                }
                st.Reqs.Add(rs);
            }

            int total = st.Reqs.Count;
            if (total == 1 && reqs[0].Kind != ReqKind.OtherAchievement && reqs[0].Op == RequirementOperator.AboveEquals)
            {
                ReqState only = st.Reqs[0];
                st.ProgressText = Num(Math.Min(only.Current, only.Req.Target)) + " / " + Num(only.Req.Target);
            }
            else
            {
                st.ProgressText = st.MetCount + " / " + total;
            }
            st.Fraction = total == 0 ? (a.m_unlocked ? 1f : 0f) : fracSum / total;

            if (a.m_unlocked)
            {
                st.Status = AchStatus.Done;
                st.Fraction = 1f;
                return st;
            }

            st.DifficultyLock = DifficultyLock(a, reqs);
            if (!withAvailability) return st;

            if (st.DifficultyLock != null)
            {
                st.Status = AchStatus.Locked;
                st.Summary = st.DifficultyLock;
                return st;
            }

            List<ReqState> open = st.Reqs.Where(r => !r.Met).ToList();
            bool anyLocked = open.Any(r => r.Avail == Avail.Locked);
            bool anyOpen = open.Any(r => r.Avail == Avail.Available || r.Avail == Avail.Unknown);
            if (!anyLocked) st.Status = AchStatus.Available;
            else if (anyOpen) st.Status = AchStatus.Partial;
            else st.Status = AchStatus.Locked;

            if (st.Status == AchStatus.Available)
            {
                ReqState unknown = open.FirstOrDefault(r => r.Avail == Avail.Unknown);
                st.Summary = unknown != null && open.All(r => r.Avail == Avail.Unknown) ? unknown.Reason : "";
            }
            else
            {
                IEnumerable<string> reasons = open.Where(r => r.Avail == Avail.Locked && !string.IsNullOrEmpty(r.Reason))
                    .Select(r => r.Reason).Distinct().Take(2);
                st.Summary = string.Join("; ", reasons);
                if (st.Status == AchStatus.Partial)
                {
                    int canNow = open.Count(r => r.Avail != Avail.Locked);
                    st.Summary = Loc.S($"сейчас можно закрыть {canNow} из {open.Count} оставшихся. ", $"{canNow} of {open.Count} remaining doable now. ") + st.Summary;
                }
            }
            return st;
        }

        public static float ReqFraction(Requirement r, float cur)
        {
            if (r.Op != RequirementOperator.AboveEquals || r.Target <= 0f) return 0f;
            return Math.Max(0f, Math.Min(1f, cur / r.Target));
        }

        public static string Num(float v)
        {
            if (Math.Abs(v - Math.Round(v)) < 0.01f || Math.Abs(v) >= 100f)
            {
                return Math.Round(v).ToString("#,0", Ru).Replace(' ', ' ');
            }
            return v.ToString("0.#", Ru);
        }
    }
}
