using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using RogueChess.Engine.AI;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Content.Consumables;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using RogueChess.Runs;
using Xunit;
using Xunit.Abstractions;

namespace RogueChess.Runs.Tests;

public class RunTests
{
    private readonly ITestOutputHelper _output;
    public RunTests(ITestOutputHelper output) => _output = output;
    private static RunDefinition Definition(string? fen = null) => new()
    {
        Width = 3, Height = 3, StartX = 0,
        Inventory = StatePayload.From("tokens", new Tokens(5)),
        Encounters = new()
        {
            new() { Id = "inn", Name = "Inn", Kind = "bartender", X = 1, Y = 1,
                Parameters = RunJson.Data(new BattleParameters { Fen = fen ?? Fen.StartPosition, StandardRules = true,
                    Ai = AgentProfile.Casual with { MaxDepth = 1, NodeBudget = 1000, QuiescenceDepth = 1 } }) },
            new() { Id = "later", Kind = "battle", X = 2, Y = 2,
                Parameters = RunJson.Data(new BattleParameters { Ai = AgentProfile.Novice with { NodeBudget = 1000 } }) },
        },
    };
    private static RunSession Start(EncounterRegistry? registry = null) => RunSession.Start(42, Definition(), registry);
    private static RunCommand Enter => RunCommand.Create("world.move", new WorldMove(1, 1));
    private static RunCommand Move => RunCommand.Create("battle.move", new BattleMove(12, 28), "inn");
    private static RunCommand Undo => RunCommand.Create("battle.undo", new { }, "inn");
    private static BattleState Battle(RunSession s) => s.State.Encounters.Single(e => e.Definition.Id == "inn").Data.As<BattleState>("battle");
    private static string Hash(RunSession s) => RunJson.Hash(s.State);

    [Fact] public void Seeds_config_and_generated_encounters_round_trip_without_precision_loss()
    {
        var a = RunSession.Start(ulong.MaxValue, Definition());
        var b = RunSession.Start(ulong.MaxValue, Definition());
        Assert.Equal(Hash(a), Hash(b));
        Assert.NotEqual(Hash(a), Hash(Start()));
        var trace = RunJson.Read<RunTrace>(RunJson.Write(a.Trace));
        Assert.Equal(ulong.MaxValue, trace.Header.Seed);
        Assert.Equal(Hash(a), Hash(RunSession.Replay(trace)));
        var profile = RunJson.Decode<BattleParameters>(trace.Header.Definition.Encounters[0].Parameters).Ai;
        Assert.Equal(0, profile.TimeBudgetMs);
    }
    [Fact] public void A_player_command_records_one_entry_and_derives_the_enemy_reply()
    {
        var session = Start();
        Assert.True(session.Execute(Enter).Accepted);
        var result = session.Execute(Move, 1234);
        Assert.True(result.Accepted);
        Assert.Equal(2, result.Effects.OfType<BattleStep>().Count());
        Assert.Equal(2, session.Trace.Commands.Count);
        Assert.Equal(1234, session.Trace.Commands[^1].DecisionMilliseconds);
        Assert.Equal(Side.White, Battle(session).Position.SideToMove);
        Assert.Equal(2, Battle(session).Position.History.Length);
        Assert.Equal(Hash(session), Hash(RunSession.Replay(session.Trace)));
        // Shared reference for the Windows/Linux CI matrix. A semantics change requires deliberate versioning.
        Assert.Equal("4E66D8F0CC056770D252B26B138456D9D166BC92771826D19731369E61756280", Hash(session));
    }
    [Fact] public void Illegal_stale_and_wrong_context_commands_do_not_change_state_or_history()
    {
        var session = Start();
        string initial = Hash(session);
        Assert.False(session.Execute(RunCommand.Create("world.move", new WorldMove(0, 0))).Accepted);
        Assert.False(session.Execute(RunCommand.Create("world.move", new WorldMove(int.MinValue, 1))).Accepted);
        Assert.Equal(initial, Hash(session));
        session.Execute(Enter);
        string entered = Hash(session);
        Assert.False(session.Execute(Move, expectedRevision: 0).Accepted);
        Assert.False(session.Execute(Move with { EncounterId = "later" }).Accepted);
        Assert.False(session.Execute(RunCommand.Create("battle.move", new BattleMove(12, 60), "inn")).Accepted);
        Assert.False(session.Execute(Undo).Accepted);
        Assert.Single(session.Trace.Commands);
        Assert.Equal(entered, Hash(session));
    }
    [Fact] public void Undo_is_in_the_trace_and_repeating_a_move_repeats_the_AI()
    {
        var session = Start();
        session.Execute(Enter);
        string before = RunJson.Hash(Battle(session).Position);
        session.Execute(Move);
        string played = RunJson.Hash(Battle(session).Position);
        Assert.True(session.Execute(Undo).Accepted);
        Assert.Equal(before, RunJson.Hash(Battle(session).Position));
        session.Execute(Move);
        Assert.Equal(played, RunJson.Hash(Battle(session).Position));
        Assert.Equal(new[] { "world.move", "battle.move", "battle.undo", "battle.move" },
            session.Trace.Commands.Select(e => e.Command.Type));
        Assert.Equal(Hash(session), Hash(RunSession.Replay(session.Trace)));
    }
    private sealed record Tokens(int Count);
    private sealed class PaidUndo : IBattleUndoPolicy
    {
        public string Version => "paid-test-v1";
        public void Apply(EncounterContext context)
        {
            var tokens = context.State.Inventory.As<Tokens>("tokens");
            if (tokens.Count == 0) throw new CommandRejectedException("No tokens.");
            context.State.Inventory = StatePayload.From("tokens", tokens with { Count = tokens.Count - 1 });
        }
    }
    private static EncounterRegistry PaidRegistry() => new EncounterRegistry()
        .Register("bartender", new BattleHandler(new PaidUndo(), true)).Register("battle", new BattleHandler());
    [Fact] public void Repeated_gameplay_undo_does_not_refund_costs()
    {
        var session = Start(PaidRegistry());
        session.Execute(Enter);
        for (int i = 0; i < 2; i++) { session.Execute(Move); Assert.True(session.Execute(Undo).Accepted); }
        Assert.Equal(3, session.State.Inventory.As<Tokens>("tokens").Count);
        Assert.Equal(2, session.State.UndosUsed);
        Assert.Equal(Hash(session), Hash(RunSession.Replay(session.Trace, registry: PaidRegistry())));
        var old = RunSession.Replay(session.Trace, through: 1, registry: PaidRegistry());
        Assert.Equal(5, old.State.Inventory.As<Tokens>("tokens").Count);
        Assert.Equal(0, old.State.UndosUsed);
    }
    [Fact] public void Saving_in_a_battle_preserves_undo_targets_before_the_checkpoint()
    {
        var session = Start();
        session.Execute(Enter);
        session.Execute(Move);
        var restored = RunSession.Restore(RunJson.Read<RunSave>(RunJson.Write(session.CaptureSave())));
        Assert.Equal(Hash(session), Hash(restored));
        Assert.True(restored.Execute(Undo).Accepted);
        session.Execute(Undo);
        Assert.Equal(Hash(session), Hash(restored));
        Assert.Equal(Hash(restored), Hash(RunSession.Replay(restored.Trace)));
    }
    [Fact] public void Loading_an_earlier_checkpoint_replays_its_tail()
    {
        var session = Start();
        session.Execute(Enter);
        var saved = session.CaptureSave();
        session.Execute(Move);
        session.Execute(Undo);
        saved = saved with { Trace = session.Trace };
        Assert.Equal(Hash(session), Hash(RunSession.Restore(saved)));
    }
    [Fact] public void Compatibility_and_divergence_are_reported_explicitly()
    {
        var session = Start();
        Assert.Throws<FormatException>(() => RunSession.Replay(session.Trace with
            { Header = session.Trace.Header with { FormatVersion = 999 } }));
        Assert.Throws<FormatException>(() => RunSession.Replay(session.Trace with
            { Header = session.Trace.Header with { ConfigurationHash = "changed" } }));
        session.Execute(Enter);
        var tampered = session.Trace;
        tampered.Commands[0] = tampered.Commands[0] with { StateHash = "wrong" };
        Assert.Equal(1, Assert.Throws<ReplayDivergenceException>(() => RunSession.Replay(tampered)).Sequence);
        var save = session.CaptureSave();
        save.Checkpoint.State.X = 0;
        Assert.Throws<FormatException>(() => RunSession.Restore(save));
    }
    [Fact] public void Cancellation_and_external_mutation_cannot_change_committed_state()
    {
        var session = Start();
        string initial = Hash(session);
        Assert.Throws<OperationCanceledException>(() => session.Execute(Enter, cancellation: new CancellationToken(true)));
        var exposed = session.State;
        exposed.X = 999;
        exposed.Encounters.Clear();
        session.Trace.Header.Definition.Encounters.Clear();
        Assert.Equal(initial, Hash(session));
        Assert.Empty(session.Trace.Commands);
    }
    private sealed class MemoryStore : IRunStore
    {
        public RunSave? Saved;
        public bool Fail;
        public int Writes;
        public void Write(RunSave save) { if (Fail) throw new IOException("disk full"); Saved = RunJson.Copy(save); Writes++; }
        public RunSave Read() => Saved!;
    }
    [Theory] [InlineData(SavePolicy.AfterCommand)] [InlineData(SavePolicy.OnOverworldReturn)]
    public void Save_policy_is_independent_of_recording(SavePolicy policy)
    {
        var session = Start();
        var store = new MemoryStore();
        var persistence = new RunPersistence(store) { Policy = policy };
        persistence.Flush(session);
        session.Execute(Enter);
        persistence.OnCommand(session, RunPhase.Overworld);
        Assert.Equal(policy == SavePolicy.AfterCommand ? 2 : 1, store.Writes);
        Assert.Equal(1, session.Sequence);
        session.Execute(RunCommand.Create("battle.resign", new { }, "inn"));
        persistence.OnCommand(session, RunPhase.Encounter);
        Assert.Equal(2, store.Read().Checkpoint.Sequence);
        Assert.False(persistence.Dirty);
    }
    [Fact] public void Failed_saves_retry_without_reapplying_the_last_command()
    {
        var session = Start();
        session.Execute(Enter);
        var store = new MemoryStore { Fail = true };
        var persistence = new RunPersistence(store);
        Assert.False(persistence.Flush(session));
        Assert.True(persistence.Dirty);
        Assert.Contains("disk full", persistence.LastError);
        store.Fail = false;
        Assert.True(persistence.Flush(session));
        Assert.Single(store.Read().Trace.Commands);
        Assert.Null(persistence.LastError);
    }
    [Fact] public void A_command_already_saved_before_its_notification_is_not_marked_dirty()
    {
        var session = Start();
        session.Execute(Enter);
        var store = new MemoryStore();
        var persistence = new RunPersistence(store);
        persistence.Flush(session);
        persistence.OnCommand(session, RunPhase.Overworld);
        Assert.False(persistence.Dirty);
        Assert.Equal(1, store.Writes);
        var loaded = new RunPersistence(store);
        loaded.MarkLoaded(store.Read().Checkpoint.Sequence);
        Assert.Equal(1, loaded.LastSavedSequence);
    }
    [Fact] public void Atomic_file_store_keeps_a_readable_previous_generation()
    {
        string directory = Path.Combine(Path.GetTempPath(), "rogue-runs-tests-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "run.json");
        try
        {
            var store = new FileRunStore(path);
            var session = Start();
            store.Write(session.CaptureSave());
            session.Execute(Enter);
            store.Write(session.CaptureSave());
            Assert.Equal(1, store.Read().Checkpoint.Sequence);
            Assert.Equal(0, RunJson.Read<RunSave>(File.ReadAllText(path + ".previous")).Checkpoint.Sequence);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            // Only these explicitly-created files; no recursive removal.
            foreach (string file in new[] { path, path + ".previous", path + ".tmp" }) if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }
    private sealed class EventHandler : IEncounterHandler
    {
        public string Version => "event-test-v1";
        public EncounterDefinition Resolve(EncounterDefinition definition) => definition;
        public StatePayload Generate(EncounterDefinition definition, ulong seed) => StatePayload.From("event", new { seed });
        public void Enter(EncounterContext context) { }
        public void Execute(EncounterContext context, RunCommand command)
        {
            if (command.Type == "noop") return;
            int roll = context.Random("offer", 10);
            context.State.Inventory = StatePayload.From("tokens", new Tokens(roll + 1));
            if (command.Type == "reject") throw new CommandRejectedException("rejected after mutation");
            context.Complete("claimed");
        }
    }
    [Fact] public void Extension_handlers_rollback_rng_and_inventory_and_complete_once()
    {
        var definition = new RunDefinition { StartX = 0,
            Encounters = new() { new() { Id = "event", Kind = "event", X = 1, Y = 1 } } };
        var registry = new EncounterRegistry().Register("event", new EventHandler());
        var session = RunSession.Start(73, definition, registry);
        session.Execute(Enter);
        string before = Hash(session);
        Assert.False(session.Execute(RunCommand.Create("noop", new { }, "event")).Accepted);
        Assert.False(session.Execute(RunCommand.Create("reject", new { }, "event")).Accepted);
        Assert.Equal(before, Hash(session));
        Assert.Single(session.Trace.Commands);
        Assert.True(session.Execute(RunCommand.Create("claim", new { }, "event")).Accepted);
        Assert.Equal(RunPhase.Ended, session.State.Phase);
        Assert.False(session.Execute(RunCommand.Create("claim", new { }, "event")).Accepted);
        Assert.Equal(Hash(session), Hash(RunSession.Replay(session.Trace, registry: registry)));
    }
    [Fact] public void Engine_snapshot_keeps_dead_pieces_vars_rng_rules_and_future_ids()
    {
        var catalog = ContentLibrary.CreateCatalog();
        var board = Fen.Parse("4k3/8/8/8/8/8/P7/4K3 w - - 0 1", RuleSet.Rogue);
        var pawn = board.PieceAt(0, 1)!;
        var ability = new StatefulAbility();
        var global = new StatefulGlobal();
        var tile = new TileDef("timed-test", "Timed test", varCount: 1);
        catalog.AddAbility(ability).AddGlobal(global).AddTile(tile);
        board.GrantAbility(pawn, ability, 7, 11);
        board.AddGlobal(global, 23);
        board.SetTile(3, tile, 13);
        board.RemoveFromBoard(pawn);
        board.SeedRandom(ulong.MaxValue);
        var restored = GameState.Restore(RunJson.Copy(board.Capture()), catalog);
        Assert.Equal(board.Dump(), restored.Dump());
        Assert.Equal(board.Hash, restored.Hash);
        Assert.Equal(7, restored.GetPiece(pawn.Id)!.Abilities[0].Var(0));
        Assert.Equal(23, restored.Globals[0].Var(0));
        Assert.Equal(13, restored.TileAt(3).Var(0));
        Assert.Equal(board.Place(StandardPieces.Rook, Side.White, 0).Id, restored.Place(StandardPieces.Rook, Side.White, 0).Id);
        Assert.Equal(0, GameState.Restore(restored.Capture(), catalog).UndoDepth);
    }
    [Fact] public void Engine_snapshot_preserves_clock_extra_turns_and_action_history()
    {
        var catalog = ContentLibrary.CreateCatalog().AddGlobal(ExtraTurnPotion.Instance);
        var board = Fen.Parse("k7/8/8/8/8/8/p7/R6K w - - 0 1", RuleSet.Rogue);
        board.GrantAbility(board.PieceAt(0, 0)!, ChronoCaptureAbility.Instance);
        ExtraTurnPotion.Instance.Give(board, 2, 1);
        Assert.True(Game.PlayTurn(board, RogueChess.Engine.Core.Move.UseConsumable(0)).Accepted);
        Assert.True(Game.PlayTurn(board, MoveText.FindLegal(board, "a1a2")!.Value).Accepted);
        Assert.True(Game.PlayTurn(board, RogueChess.Engine.Core.Move.UseConsumable(0)).Accepted);
        board.SetSideToMove(Side.Black);
        Assert.True(Game.PlayTurn(board, RogueChess.Engine.Core.Move.UseConsumable(0)).Accepted);
        Assert.Equal(5000, board.ClockMs(Side.White));
        Assert.Equal(1, board.ExtraTurns(Side.White));
        Assert.Equal(1, board.ExtraTurns(Side.Black));
        Assert.Equal(4, board.History.Count);

        var restored = GameState.Restore(RunJson.Copy(board.Capture()), catalog);
        Assert.Equal(board.Dump(), restored.Dump());
        Assert.Equal(board.History.ToArray(), restored.History.ToArray());
        Assert.Equal(board.Hash, restored.Hash);
        Assert.Equal(0, restored.UndoDepth);
        var move = MoveText.FindLegal(board, "a8b8")!.Value;
        Assert.True(Game.PlayTurn(board, move).Accepted);
        Assert.True(Game.PlayTurn(restored, move).Accepted);
        Assert.Equal(Side.Black, restored.SideToMove); // The restored banked turn is spent.
        Assert.Equal(RunJson.Hash(board.Capture()), RunJson.Hash(restored.Capture()));
    }
    [Fact] public void Previous_content_traces_are_rejected_before_replay_or_restore()
    {
        var session = Start();
        var trace = session.Trace with { Header = session.Trace.Header with { ContentVersion = "engine-content-v1" } };
        Assert.Throws<FormatException>(() => RunSession.Replay(trace));
        Assert.Throws<FormatException>(() => RunSession.Restore(session.CaptureSave() with { Trace = trace }));
    }
    private sealed class StatefulAbility : AbilityDef
    {
        public StatefulAbility() : base("stateful-test", "Stateful test", varCount: 2) { }
    }
    private sealed class StatefulGlobal : GlobalDef
    {
        public StatefulGlobal() : base("global-test", "Global test", varCount: 1) { }
    }
    [Fact] public void Complete_battle_round_trip_and_profile()
    {
        var session = RunSession.Start(11, Definition("4k3/8/8/8/8/8/4R3/4K3 w - - 0 1") with
        {
            Encounters = new() { Definition("4k3/8/8/8/8/8/4R3/4K3 w - - 0 1").Encounters[0] with
                { Parameters = RunJson.Data(new BattleParameters { Fen = "4k3/8/8/8/8/8/4R3/4K3 w - - 0 1", StandardRules = false }) } },
        });
        session.Execute(Enter);
        var result = session.Execute(RunCommand.Create("battle.move", new BattleMove(12, 60), "inn"));
        Assert.True(result.Accepted);
        Assert.Equal(RunPhase.Ended, session.State.Phase);
        Assert.Equal("player", session.State.Encounters[0].Outcome);
        var clock = Stopwatch.StartNew();
        var trace = RunJson.Write(session.Trace);
        var save = RunJson.Write(session.CaptureSave());
        var replayed = RunSession.Replay(RunJson.Read<RunTrace>(trace));
        _output.WriteLine($"Fixture trace {trace.Length} chars; save {save.Length} chars; serialization + replay {clock.ElapsedMilliseconds} ms.");
        Assert.Equal(Hash(session), Hash(replayed));
    }
    [Fact] public void Run_core_has_no_Godot_reference()
        => Assert.DoesNotContain(typeof(RunSession).Assembly.GetReferencedAssemblies(), a => a.Name!.StartsWith("Godot", StringComparison.Ordinal));

    private sealed class RelocatedWorld : IWorldGenerator
    {
        public string Version => "relocated-test-v1";
        public RunState Generate(RunDefinition definition, ulong seed)
        {
            var state = new ConfiguredWorldGenerator().Generate(definition, seed);
            state.Encounters.RemoveAt(1);
            state.Encounters[0].Definition = state.Encounters[0].Definition with { X = 1, Y = 0 };
            return state;
        }
    }
    [Fact] public void The_world_generator_owns_the_generated_layout_and_replays_by_version()
    {
        var session = RunSession.Start(42, Definition(), generator: new RelocatedWorld());
        Assert.Single(session.State.Encounters);
        Assert.True(session.Execute(RunCommand.Create("world.move", new WorldMove(1, 0))).Accepted);
        Assert.Equal("inn", session.State.ActiveEncounterId);
        Assert.Equal(Hash(session), Hash(RunSession.Replay(session.Trace, generator: new RelocatedWorld())));
        Assert.Throws<FormatException>(() => RunSession.Replay(session.Trace));
    }
    [Fact] public void Representative_battle_trace_and_checkpoint_profile()
    {
        var session = Start();
        session.Execute(Enter);
        var clock = Stopwatch.StartNew();
        for (int i = 0; i < 12 && session.State.Phase == RunPhase.Encounter; i++)
        {
            var board = GameState.Restore(Battle(session).Position, ContentLibrary.CreateCatalog());
            var moves = new List<Move>();
            MoveGenerator.GenerateLegal(board, moves);
            var move = moves[(i * 7) % moves.Count];
            Assert.True(session.Execute(RunCommand.Create("battle.move", new BattleMove(move.From, move.To, move.Promotion), "inn")).Accepted);
        }
        long playMs = clock.ElapsedMilliseconds;
        var traceJson = RunJson.Write(session.Trace);
        var save = session.CaptureSave();
        string path = Path.Combine(Path.GetTempPath(), "rogue-profile-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            clock.Restart();
            var store = new FileRunStore(path);
            store.Write(save);
            long writeMs = clock.ElapsedMilliseconds;
            clock.Restart();
            Assert.Equal(Hash(session), Hash(RunSession.Restore(store.Read())));
            long loadMs = clock.ElapsedMilliseconds;
            clock.Restart();
            Assert.Equal(Hash(session), Hash(RunSession.Replay(RunJson.Read<RunTrace>(traceJson))));
            _output.WriteLine($"{session.Sequence} commands: trace {traceJson.Length} chars, save {new FileInfo(path).Length} bytes; play {playMs} ms, write {writeMs} ms, load {loadMs} ms, replay {clock.ElapsedMilliseconds} ms.");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
