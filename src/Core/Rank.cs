using System;
using System.Globalization;

namespace RotkAlive
{
    // "[rank:T.D]" from the kill feed: T is the tier (0 Placement .. 7 Royalty), D the division,
    // where division 1 is the best. Royalty division 1 is shown by the game as ROYALTY ONE.
    public struct RankInfo
    {
        public const int MaxTier = 7;

        static readonly string[] TierNames =
        {
            "PLACEMENT", "BRONZE", "SILVER", "GOLD", "PLATINUM", "DIAMOND", "MASTER", "ROYALTY"
        };

        static readonly string[] TierKeys =
        {
            "placement", "bronze", "silver", "gold", "platinum", "diamond", "master", "royalty"
        };

        public int Tier;
        public int Division;
        public bool OutOfRange;

        public bool IsRoyaltyOne { get { return Tier == MaxTier && Division == 1; } }

        public string TierName { get { return IsRoyaltyOne ? "ROYALTY ONE" : TierNames[Tier]; } }

        // Name of the badge: one per tier, plus a separate one for Royalty One.
        public string IconKey { get { return IsRoyaltyOne ? "royalty-one" : TierKeys[Tier]; } }

        // Divisions sort 1 (best) to 5; a missing division sorts after 5.
        public int DivisionSortKey { get { return Division >= 1 ? Division : 99; } }

        public static RankInfo Parse(string text)
        {
            RankInfo r = new RankInfo();
            if (string.IsNullOrEmpty(text)) return r;

            int dot = text.IndexOf('.');
            string whole = dot >= 0 ? text.Substring(0, dot) : text;
            string frac = dot >= 0 ? text.Substring(dot + 1) : "";

            int tier;
            if (!int.TryParse(whole, NumberStyles.Integer, CultureInfo.InvariantCulture, out tier) || tier < 0) tier = 0;
            if (tier > MaxTier)
            {
                r.OutOfRange = true;
                tier = MaxTier;
            }
            r.Tier = tier;

            int division = 0;
            if (frac.Length > 0 && frac[0] >= '0' && frac[0] <= '9') division = frac[0] - '0';
            r.Division = tier == 0 ? 0 : division;
            return r;
        }

        // Negative when a ranks above b.
        public static int Compare(RankInfo a, RankInfo b)
        {
            int c = b.Tier.CompareTo(a.Tier);
            return c != 0 ? c : a.DivisionSortKey.CompareTo(b.DivisionSortKey);
        }
    }
}
