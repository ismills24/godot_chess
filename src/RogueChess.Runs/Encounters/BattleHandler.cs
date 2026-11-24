using System;
using System.Collections.Generic;
using System.Linq;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Encounters;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;

namespace RogueChess.Runs;

public sealed record BattleParameters
{
    public string? Fen { get; init; }
    public bool StandardRules { get; init; }
    public int Width { get; init; } = 6;
    public int Height { get; init; } = 6;
    public ArmySpec Player { get; init; } = new() { PieceValue = 10 };
    public ArmySpec Enemy { get; init; } = new() { PieceValue = 8 };
    public AgentProfile Ai { get; init; } = AgentProfile.Casual with { TimeBudgetMs = 0 };
    public bool AllowUndo { get; init; } = true;
}
public sealed class BattleState
{
    public GameStateSnapshot Position { get; set; } = new();
    public List<GameStateSnapshot> UndoPositions { get; set; } = new();
}
public interface IBattleUndoPolicy
{
    string Version { get; }
    void Apply(EncounterContext context);
}
public sealed class FreeBattleUndo : IBattleUndoPolicy
{
    public string Version => "free-v1";
    public void Apply(EncounterContext context) { }
}

public sealed class BattleHandler : IEncounterHandler
{
    private readonly Catalog _catalog = ContentLibrary.CreateCatalog();
    private readonly IBattleUndoPolicy _undoPolicy;
    private readonly bool _requireInnBoard;
    public string Version => $"battle-v1/{_undoPolicy.Version}/inn={_requireInnBoard}";
    public BattleHandler(IBattleUndoPolicy? undoPolicy = null, bool requireInnBoard = false)
        { _undoPolicy = undoPolicy ?? new FreeBattleUndo(); _requireInnBoard = requireInnBoard; }

    public EncounterDefinition Resolve(EncounterDefinition definition)
    {
        var parameters = RunJson.Decode<BattleParameters>(definition.Parameters);
        if (parameters.Width < 2 || parameters.Width > 12 || parameters.Height < 2 || parameters.Height > 12
            || parameters.Player.PieceValue < 0 || parameters.Player.PieceValue > 100
            || parameters.Enemy.PieceValue < 0 || parameters.Enemy.PieceValue > 100
            || parameters.Player.AbilityValue < 0 || parameters.Player.AbilityValue > 100
            || parameters.Enemy.AbilityValue < 0 || parameters.Enemy.AbilityValue > 100
            || parameters.Player.MinimumPawns < 0 || parameters.Player.MinimumPawns > 20
            || parameters.Enemy.MinimumPawns < 0 || parameters.Enemy.MinimumPawns > 20)
            throw new FormatException("Invalid battle dimensions or army budget.");
        // The resolved profile, including disabled wall-clock cutoff, is embedded in the run header.
        var ai = parameters.Ai with { TimeBudgetMs = 0 };
        if (ai.MaxDepth < 1 || ai.MaxDepth > 12 || ai.NodeBudget < 1 || ai.NodeBudget > 2_000_000
            || ai.TableBits < 8 || ai.TableBits > 20 || ai.QuiescenceDepth < 0 || ai.QuiescenceDepth > 12)
            throw new FormatException("AI requires bounded deterministic work limits.");
        _ = new AgentFactory().Create(ai);
        parameters = parameters with { Ai = ai };
        var resolved = definition with { Parameters = RunJson.Data(parameters) };
        // Validate FEN and the inn's fixed 8x8 presentation before a run can start.
        if (parameters.Fen != null)
        {
            var state = Fen.Parse(parameters.Fen, parameters.StandardRules ? RuleSet.Standard : RuleSet.Rogue);
            if (state.Width > 12 || state.Height > 12 || (_requireInnBoard && (state.Width != 8 || state.Height != 8)))
                throw new FormatException("The bartender scene requires an 8x8 board.");
        }
        else if (_requireInnBoard && (parameters.Width != 8 || parameters.Height != 8))
            throw new FormatException("The bartender scene requires an 8x8 board.");
        return resolved;
    }
    public StatePayload Generate(EncounterDefinition definition, ulong seed)
    {
        var p = RunJson.Decode<BattleParameters>(definition.Parameters);
        var rules = p.StandardRules ? RuleSet.Standard : RuleSet.Rogue;
        var state = p.Fen != null ? Fen.Parse(p.Fen, rules) : EncounterGenerator.BuildBattle(
            EncounterGenerator.GenerateArmy(p.Player, _catalog, RunRandom.Derive(seed, "player")),
            EncounterGenerator.GenerateArmy(p.Enemy, _catalog, RunRandom.Derive(seed, "enemy")),
            p.Width, p.Height, RunRandom.Derive(seed, "placement"), rules);
        state.SeedRandom(RunRandom.Derive(seed, "battle"));
        Game.BeginGame(state);
        state.ClearHistory();
        return StatePayload.From("battle", new BattleState { Position = state.Capture() });
    }
    public void Enter(EncounterContext context)
    {
        var battle = context.Encounter.Data.As<BattleState>("battle");
        var board = GameState.Restore(battle.Position, _catalog);
        Advance(context, board);
        battle.Position = board.Capture();
        context.Encounter.Data = StatePayload.From("battle", battle);
    }
    public void Execute(EncounterContext context, RunCommand command)
    {
        var battle = context.Encounter.Data.As<BattleState>("battle");
        var board = GameState.Restore(battle.Position, _catalog);
        var parameters = RunJson.Decode<BattleParameters>(context.Encounter.Definition.Parameters);
        switch (command.Type)
        {
            case "battle.move":
                if (board.SideToMove != Side.White) throw new CommandRejectedException("Wait for your turn.");
                var requested = RunJson.Decode<BattleMove>(command.Parameters);
                var legal = new List<Move>();
                MoveGenerator.GenerateLegal(board, legal);
                var candidates = legal.Where(m => m.From == requested.From && m.To == requested.To
                    && (requested.Promotion < 0 || m.Promotion == requested.Promotion)).ToList();
                if (candidates.Count == 0) throw new CommandRejectedException("That move is not legal.");
                battle.UndoPositions.Add(board.Capture());
                Play(context, board, candidates[0]);
                Advance(context, board);
                break;
            case "battle.undo":
                if (!parameters.AllowUndo || battle.UndoPositions.Count == 0) throw new CommandRejectedException("No move to undo.");
                _undoPolicy.Apply(context); // Inventory costs survive board restoration, and transaction rollback covers rejection.
                board = GameState.Restore(battle.UndoPositions[^1], _catalog);
                battle.UndoPositions.RemoveAt(battle.UndoPositions.Count - 1);
                context.State.UndosUsed++;
                break;
            case "battle.resign":
                context.Complete("enemy");
                break;
            default: throw new CommandRejectedException($"Unsupported battle command '{command.Type}'.");
        }
        battle.Position = board.Capture();
        context.Encounter.Data = StatePayload.From("battle", battle);
    }
    private void Advance(EncounterContext context, GameState board)
    {
        var outcome = board.Rules.Evaluate(board);
        if (outcome == GameOutcome.Ongoing && board.SideToMove == Side.Black)
        {
            var p = RunJson.Decode<BattleParameters>(context.Encounter.Definition.Parameters);
            var profile = p.Ai with { TimeBudgetMs = 0, Seed = RunRandom.Derive(context.Seed, $"ai/{context.Encounter.Definition.Id}") };
            var decision = new AgentFactory().Create(profile).Decide(board.Clone(), context.Cancellation);
            context.Cancellation.ThrowIfCancellationRequested();
            if (decision.Move is not { } move) throw new InvalidOperationException("AI did not return a move for an ongoing game.");
            Play(context, board, move);
            outcome = board.Rules.Evaluate(board);
        }
        if (outcome != GameOutcome.Ongoing) context.Complete(outcome switch
        {
            GameOutcome.WhiteWins => "player", GameOutcome.BlackWins => "enemy", _ => "draw",
        });
    }
    private static void Play(EncounterContext context, GameState board, Move move)
    {
        bool enemy = board.SideToMove == Side.Black;
        var result = Game.PlayTurn(board, move);
        if (!result.Accepted) throw new InvalidOperationException($"Battle resolution failed: {result.RejectReason}");
        board.ClearHistory();
        context.Effects.Add(new BattleStep(board.Capture(), move.From, move.To, enemy));
    }
}
