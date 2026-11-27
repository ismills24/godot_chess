using System.Collections.Generic;
using Godot;

namespace GodotChess.Sandbox.Visuals;

/// <summary>Temporary, per-instance tint. Never mutates the assets' shared materials.</summary>
public sealed class PieceSelectionHighlight
{
    private readonly List<(MeshInstance3D Mesh, Material? Original, bool Overlay)> _changed = new();

    public void Clear()
    {
        foreach (var (mesh, original, overlay) in _changed)
        {
            if (!GodotObject.IsInstanceValid(mesh)) continue;
            if (overlay) mesh.MaterialOverlay = original;
            else mesh.MaterialOverride = original;
        }
        _changed.Clear();
    }

    public void Apply(Node node)
    {
        if (node is MeshInstance3D mesh && node.Name != "ContactShadow")
        {
            if (mesh.MaterialOverride is ShaderMaterial wood &&
                (wood.Shader.ResourcePath == "res://assets/pieces/wood/wood_surface_depth.gdshader" ||
                 wood.Shader.ResourcePath == "res://assets/pieces/wood/wood_surface.gdshader" ||
                 wood.Shader.ResourcePath == "res://assets/pieces/wood/wood_solid.gdshader" ||
                 wood.Shader.ResourcePath.StartsWith("res://assets/pieces/wood_hd/")))
            {
                var tinted = (ShaderMaterial)wood.Duplicate();
                tinted.SetShaderParameter("selection_amount", 0.2f);
                _changed.Add((mesh, wood, false));
                mesh.MaterialOverride = tinted;
            }
            else
            {
                _changed.Add((mesh, mesh.MaterialOverlay, true));
                mesh.MaterialOverlay = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.4f, 0.85f, 1, 0.16f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
                };
            }
        }
        foreach (var child in node.GetChildren()) Apply(child);
    }
}
