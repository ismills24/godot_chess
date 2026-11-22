using System;
using System.Linq;
using Godot;
using GodotChess.Sandbox;
using GodotChess.Sandbox.Visuals;
using RogueChess.Engine.Content;
using RogueChess.Engine.Content.Abilities;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Resolution;
using RogueChess.Engine.Rules;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Experiments;

/// <summary>Run with Godot --headless --path . res://experiments/gameplay/VerifyGameplayVisuals.tscn.</summary>
public partial class VerifyGameplayVisuals : Node
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public override async void _Ready()
    {
        try
        {
            var view = new BoardView3D { Size = new Vector2(880, 900) };
            AddChild(view);
            var state = Fen.Parse(Fen.StartPosition);
            view.State = state;
            view.RefreshVisuals();
            var world = view.GetChild<SubViewport>(0).GetChild<Node3D>(0);
            Node3D Visual(Piece p) => world.GetNode<Node3D>($"Piece_{p.Id}");
            Check(world.GetChildren().Count(n => n.Name.ToString().StartsWith("Piece_")) == 32, "Full inventory");
            foreach (var p in state.PiecesOnBoard())
                Check(Visual(p).GetChild<Node3D>(1).Get("head_kind").AsString() == p.Def.Id, "Standard head mapping");
            var pawn = state.PieceAt(4, 1)!;
            var original = Visual(pawn);
            var pawnBase = original.GetChild<Node3D>(1).GetNode<MeshInstance3D>("Base");
            var neighborBase = Visual(state.PieceAt(3, 1)!).GetChild<Node3D>(1).GetNode<MeshInstance3D>("Base");
            var originalMaterial = pawnBase.MaterialOverride;
            view.Selected = pawn.Square; view.RefreshVisuals();
            Check(pawnBase.MaterialOverride != originalMaterial && neighborBase.MaterialOverride == originalMaterial,
                "Selection tint is isolated from shared materials");
            Check(((ShaderMaterial)pawnBase.MaterialOverride).GetShaderParameter("selection_amount").AsSingle() > 0,
                "Selected piece is faintly tinted");
            view.Selected = -1; view.RefreshVisuals();
            Check(pawnBase.MaterialOverride == originalMaterial, "Deselection restores original material");
            var orbitCamera = world.GetChildren().OfType<Camera3D>().Single();
            foreach (var button in new[] { MouseButtonMask.Right, MouseButtonMask.Middle })
            {
                var before = orbitCamera.Position;
                view._GuiInput(new InputEventMouseMotion { ButtonMask = button, Relative = new Vector2(40, 0) });
                Check(orbitCamera.Position != before, "Drag rotates the board view");
            }
            int mark = state.UndoDepth;
            var result = Game.PlayTurn(state, new Move(state.Sq(4, 1), state.Sq(4, 3), MoveFlags.DoublePush));
            Check(result.Accepted, "Move accepted");
            view.RefreshVisuals();
            Check(Visual(pawn) == original && original.Position == view.SquareCenter(pawn.Square), "Move preserves visual identity");
            state.UndoTo(mark);
            view.RefreshVisuals();
            Check(original.Position == view.SquareCenter(state.Sq(4, 1)), "Undo restores position");

            state = Fen.Parse("5k/6/6/1p4/P5/K5 w - - 0 1", RuleSet.Rogue);
            view.State = state; view.RefreshVisuals();
            pawn = state.PieceAt(0, 1)!;
            original = Visual(pawn);
            var victim = state.PieceAt(1, 2)!;
            state.GrantAbility(pawn, PredatorAbility.Instance);
            mark = state.UndoDepth;
            result = Game.PlayTurn(state, new Move(pawn.Square, victim.Square, MoveFlags.Capture));
            Check(result.Accepted && pawn.Def.Id == "knight", "Predator capture evolves");
            view.RefreshVisuals();
            Check(!world.HasNode($"Piece_{victim.Id}"), "Capture removes visual");
            Check(Visual(pawn) == original, "Evolution preserves visual identity");
            Check(Visual(pawn).GetChild<Node3D>(1).Get("head_kind").AsString() == "knight", "Evolution follows state");
            state.UndoTo(mark); view.RefreshVisuals();
            Check(world.HasNode($"Piece_{victim.Id}") && Visual(pawn).GetChild<Node3D>(1).Get("head_kind").AsString() == "pawn", "Undo restores assembly and victim");

            var unknown = new PieceDef("unmapped", "Unmapped", 'u', 2, Array.Empty<Movement>());
            var custom = state.Place(unknown, Side.White, state.Sq(3, 2));
            view.RefreshVisuals();
            Check(Visual(custom).GetChildren().OfType<Label3D>().Single().Text == "U", "Unmapped piece has symbol token");
            foreach (var recipe in new[] { new PieceVisualRecipe { Head = "missing" }, new PieceVisualRecipe { Scene = "res://absent.tscn" } })
            {
                var fallback = PieceVisualAssembler.Assemble(new(recipe, false, "X"));
                AddChild(fallback);
                Check(fallback.GetChildren().OfType<Label3D>().Any(), "Missing asset uses placeholder");
                fallback.QueueFree();
            }
            var mixed = PieceVisualAssembler.Assemble(new(new PieceVisualRecipe { Base = "rook", Body = "pawn", Head = "knight" }, true, "N"));
            AddChild(mixed);
            var model = mixed.GetChild<Node3D>(1);
            Check(Mathf.IsEqualApprox(model.GetNode<Node3D>("Head").Position.Y, 0.91f), "Mixed head uses body socket");
            Check(model.Get("dark_wood").AsBool(), "Side finish");
            mixed.QueueFree();

            foreach (var dimensions in new[] { (8, 8), (6, 6), (5, 9) })
            {
                state = new GameState(dimensions.Item1, dimensions.Item2, RuleSet.Rogue);
                view.State = state; view.RefreshVisuals();
                var board = world.GetChildren().OfType<Node3D>().Single(n => n.HasNode("Squares"));
                Check(board.GetNode("Squares").GetChildCount() == state.Area, "Board dimensions");
                var camera = world.GetChildren().OfType<Camera3D>().Single();
                await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                for (int sq = 0; sq < state.Area; sq++)
                    Check(view.PickSquare(camera.UnprojectPosition(view.SquareCenter(sq))) == sq, "Projected square picking");
                var fresh = state.Place(StandardPieces.Queen, Side.Black, 0);
                view.RefreshVisuals();
                Check(Visual(fresh).GetChild<Node3D>(1).Get("head_kind").AsString() == "queen", "New game ID reuse resets visual");
            }
            GD.Print("[VisualTests] PASS: inventory, movement, capture, undo, evolution, mixed modules, fallbacks, rectangular boards, picking, state replacement");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
