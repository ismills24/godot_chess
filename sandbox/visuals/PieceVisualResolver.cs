using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;
using RogueChess.Engine.Core;
using Side = RogueChess.Engine.Core.Side;

namespace GodotChess.Sandbox.Visuals;

/// <summary>Presentation data only: changing a recipe never changes the piece's rules.</summary>
public sealed record PieceVisualRecipe
{
    public string? Scene { get; init; }
    public string Base { get; init; } = "pawn";
    public string Body { get; init; } = "pawn";
    public string Head { get; init; } = "pawn";
    public float Scale { get; init; } = 1;
    public float YawDegrees { get; init; }
}

public sealed record ResolvedPieceVisual(PieceVisualRecipe? Recipe, bool Dark, string Symbol);

public sealed class PieceVisualResolver
{
    private readonly Dictionary<string, PieceVisualRecipe> _recipes;

    public PieceVisualResolver(string path = "res://assets/pieces/visual_catalog.json")
    {
        try
        {
            _recipes = JsonSerializer.Deserialize<Dictionary<string, PieceVisualRecipe>>(
                FileAccess.GetFileAsString(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (JsonException e)
        {
            GD.PushWarning($"Visual catalog unavailable; using tokens: {e.Message}");
            _recipes = new();
        }
    }

    public ResolvedPieceVisual Resolve(Piece piece) => new(
        _recipes.GetValueOrDefault(piece.Def.Id), piece.Side == Side.Black,
        char.ToUpperInvariant(piece.Def.Symbol).ToString());
}
