using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.BoardEffects;
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
    /// <summary>Board effects active for the whole battle, in the map format
    /// (<c>{ "id", "name", "condition": {...}, "effect": { "type", "params" } }</c>), e.g. the bartender's beer spill.
    /// Left out of the JSON when absent, so runs without effects keep their hashes.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<JsonElement>? BoardEffects { get; init; }
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
    /// <summary>The largest board side any battle may use, scene boards included.</summary>
    public const int MaxBoardSide = 10;
    // Kept so the handler version (and existing run manifests) still name scene encounters.
    private readonly bool _requireInnBoard;
    public string Version => $"battle-v1/{_undoPolicy.Version}/inn={_requireInnBoard}";
    public BattleHandler(IBattleUndoPolicy? undoPolicy = null, bool requireInnBoard = false)
        { _undoPolicy = undoPolicy ?? new FreeBattleUndo(); _requireInnBoard = requireInnBoard; }

    public EncounterDefinition Resolve(EncounterDefinition definition)
    {
        var parameters = RunJson.Decode<BattleParameters>(definition.Parameters);
        if (parameters.Width < 2 || parameters.Width > MaxBoardSide || parameters.Height < 2 || parameters.Height > MaxBoardSide
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
        try { _ = CreateBoardEffects(_catalog, parameters); }
        catch (Exception e) when (e is not FormatException) { throw new FormatException($"Invalid board effect: {e.Message}", e); }
        parameters = parameters with { Ai = ai };
        var resolved = definition with { Parameters = RunJson.Data(parameters) };
        // Validate a FEN before a run can start. Every board, scene or not, is at most 10x10.
        if (parameters.Fen != null)
        {
            var state = Fen.Parse(parameters.Fen, parameters.StandardRules ? RuleSet.Standard : RuleSet.Rogue);
            if (state.Width > MaxBoardSide || state.Height > MaxBoardSide)
                throw new FormatException($"Boards can be at most {MaxBoardSide}x{MaxBoardSide}.");
        }
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
        foreach (var (def, vars) in CreateBoardEffects(_catalog, p)) state.AddGlobal(def, vars);
        state.SeedRandom(RunRandom.Derive(seed, "battle"));
        Game.BeginGame(state);
        state.ClearHistory();
        return StatePayload.From("battle", new BattleState { Position = state.Capture() });
    }
    public void Enter(EncounterContext context)
    {
        var battle = context.Encounter.Data.As<BattleState>("battle");
        CreateBoardEffects(_catalog, RunJson.Decode<BattleParameters>(context.Encounter.Definition.Parameters));
        var board = GameState.Restore(battle.Position, _catalog);
        Advance(context, board);
        battle.Position = board.Capture();
        context.Encounter.Data = StatePayload.From("battle", battle);
    }
    public void Execute(EncounterContext context, RunCommand command)
    {
        var battle = context.Encounter.Data.As<BattleState>("battle");
        var parameters = RunJson.Decode<BattleParameters>(context.Encounter.Definition.Parameters);
        CreateBoardEffects(_catalog, parameters);
        var board = GameState.Restore(battle.Position, _catalog);
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
    /// <summary>Build the battle's board effects and register them in <paramref name="catalog"/> so saved positions
    /// that hold them can be restored. Effect ids are derived from their condition and effect, so this is stable.</summary>
    public static List<(GlobalDef Def, int[] Vars)> CreateBoardEffects(Catalog catalog, BattleParameters parameters)
    {
        var effects = new List<(GlobalDef, int[])>();
        foreach (var entry in parameters.BoardEffects ?? new())
        {
            string type = entry.TryGetProperty("effect", out var effect) && effect.TryGetProperty("type", out var t)
                ? t.GetString() ?? "" : "";
            var factory = catalog.FindBoardEffect(type) ?? throw new FormatException($"Unknown board effect '{type}'.");
            var (def, vars) = factory(entry);
            if (catalog.FindGlobal(def.Id) == null) catalog.AddGlobal(def);
            effects.Add((catalog.FindGlobal(def.Id)!, vars));
        }
        return effects;
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
