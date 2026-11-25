using RogueChess.Engine.Core;
using RogueChess.Engine.Resolution;

namespace RogueChess.Engine.Content
{
    /// <summary>
    /// A relic: a passive global effect owned by one side (the prototype's <c>Relic</c> listeners). Relics live in
    /// <see cref="GameState.Globals"/> like board effects, so their hooks run in AI search too, and any state they keep
    /// is a global variable (hashed and undone). Defs are immutable and built per owner; the id carries the owner
    /// (<c>"time-bank:white"</c>).
    /// </summary>
    public abstract class RelicDef : GlobalDef
    {
        protected RelicDef(string baseId, string name, string description, Side owner, int varCount = 0)
            : base($"{baseId}:{owner.ToString().ToLowerInvariant()}", name, varCount)
        {
            BaseId = baseId;
            Description = description;
            Owner = owner;
        }

        /// <summary>The relic's id without the owner (<c>"time-bank"</c>).</summary>
        public string BaseId { get; }

        public string Description { get; }
        public Side Owner { get; }
    }
}

namespace RogueChess.Engine.Content.Relics
{
    /// <summary>
    /// Hourglass Cache (prototype <c>TimeBankRelic</c>): the owner gains +3 s on the clock after each of its moves.
    /// "A move" is the owner's own action moving a piece (a <see cref="FactKinds.Moved"/> with
    /// <see cref="Causes.Action"/>); a move blocked by a Guardian, or a consumable use, earns nothing.
    /// </summary>
    public sealed class TimeBankRelic : RelicDef
    {
        public const int BonusMs = 3000;
        public const string Cause = "time-bank";

        public static readonly TimeBankRelic White = new(Side.White);
        public static readonly TimeBankRelic Black = new(Side.Black);

        public static TimeBankRelic For(Side owner) => owner == Side.White ? White : Black;

        private TimeBankRelic(Side owner) : base("time-bank", "Hourglass Cache", "Gain +3s after each of your moves.", owner) { }

        public override FactKinds Triggers => FactKinds.Moved;

        public override void OnFact(TriggerContext ctx, in Fact fact)
        {
            if (ctx.Phase != Game.ActionPhase || fact.Cause != Causes.Action || fact.Piece?.Side != Owner) return;
            ctx.Propose(Proposal.Clock(Owner, BonusMs, Cause));
        }
    }

    /// <summary>
    /// Crown of Ascension (prototype <c>RoyalPromotionRelic</c>): any friendly piece that reaches the owner's last rank
    /// becomes a Queen. Royal pieces, pawns (they promote by the normal rule) and queens are left alone. The piece keeps
    /// its abilities and history (only its def changes), and it promotes in the wave after it arrives.
    /// </summary>
    public sealed class RoyalPromotionRelic : RelicDef
    {
        public const string Cause = "royal-promotion";

        public static readonly RoyalPromotionRelic White = new(Side.White);
        public static readonly RoyalPromotionRelic Black = new(Side.Black);

        public static RoyalPromotionRelic For(Side owner) => owner == Side.White ? White : Black;

        private RoyalPromotionRelic(Side owner)
            : base("royal-promotion", "Crown of Ascension", "Any friendly piece that reaches the last rank becomes a Queen.", owner) { }

        public override FactKinds Triggers => FactKinds.Moved;

        public override void OnFact(TriggerContext ctx, in Fact fact)
        {
            var p = fact.Piece;
            if (p == null || p.Side != Owner || p.Square != fact.To) return;
            if (p.Def.IsRoyal || p.Def.IsPawn || p.Def == StandardPieces.Queen) return;

            var board = ctx.Board;
            int lastRank = Owner == Side.White ? board.Height - 1 : 0;
            if (board.RankOf(fact.To) != lastRank) return;
            ctx.Propose(Proposal.ChangeDef(p, StandardPieces.Queen, Cause));
        }
    }
}
