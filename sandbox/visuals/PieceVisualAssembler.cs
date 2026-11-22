using System;
using System.Collections.Generic;
using Godot;

namespace GodotChess.Sandbox.Visuals;

/// <summary>Builds a resolved recipe. Owns no gameplay state and emits no gameplay events.</summary>
public static class PieceVisualAssembler
{
    private static readonly HashSet<string> WoodKinds = new() { "pawn", "knight", "bishop", "rook", "queen", "king" };

    public static Node3D Assemble(ResolvedPieceVisual visual)
    {
        var root = new Node3D();
        root.AddChild(new MeshInstance3D
        {
            Name = "ContactShadow", Mesh = new PlaneMesh { Size = new Vector2(1.45f, 1.45f) },
            Position = new Vector3(0, 0.004f, 0),
            MaterialOverride = new ShaderMaterial { Shader = GD.Load<Shader>("res://experiments/wood_set/contact_shadow.gdshader") }
        });
        var recipe = visual.Recipe;
        Node3D? model = null;
        if (recipe is { Scene: { Length: > 0 } path } && ResourceLoader.Exists(path))
        {
            if (GD.Load<Resource>(path) is PackedScene scene)
            {
                var instance = scene.Instantiate();
                if (instance is Node3D spatial) model = spatial;
                else instance.Free();
            }
        }
        else if (recipe != null && string.IsNullOrEmpty(recipe.Scene)
            && WoodKinds.Contains(recipe.Base) && WoodKinds.Contains(recipe.Body) && WoodKinds.Contains(recipe.Head))
        {
            model = GD.Load<PackedScene>("res://assets/pieces/wood/LightPawn.tscn").Instantiate<Node3D>();
            model.Set("base_kind", recipe.Base);
            model.Set("body_kind", recipe.Body);
            model.Set("head_kind", recipe.Head);
            model.Set("dark_wood", visual.Dark);
            model.Set("depth_tested_splats", true);
        }

        if (model != null && recipe != null && float.IsFinite(recipe.Scale) && recipe.Scale > 0
            && float.IsFinite(recipe.YawDegrees))
        {
            model.Scale = Vector3.One * recipe.Scale;
            model.Rotation = new Vector3(0, Mathf.DegToRad(recipe.YawDegrees + (visual.Dark ? 180 : 0)), 0);
            root.AddChild(model);
        }
        else
        {
            model?.Free();
            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.37f, BottomRadius = 0.45f, Height = 0.35f },
                Position = new Vector3(0, 0.175f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = visual.Dark ? new Color("795337") : new Color("e6cca1") }
            });
            root.AddChild(new Label3D
            {
                Text = visual.Symbol, Position = new Vector3(0, 0.65f, 0), FontSize = 80, PixelSize = 0.009f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                Modulate = visual.Dark ? new Color("ffe0ac") : Colors.White, OutlineSize = 12
            });
        }
        return root;
    }
}
