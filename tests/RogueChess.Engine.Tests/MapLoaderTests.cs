using System.Collections.Generic;
using System.IO;
using System.Linq;
using RogueChess.Engine.Content;
using RogueChess.Engine.Core;
using RogueChess.Engine.Notation;
using RogueChess.Engine.Rules;
using Xunit;

namespace RogueChess.Engine.Tests;

public class MapLoaderTests
{
    internal static string LegacyMapsDir
    {
        get
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "maps", "legacy"))) dir = dir.Parent;
            return Path.Combine(dir!.FullName, "maps", "legacy");
        }
    }

    [Fact]
    public void Standard_chess_map_matches_its_fen_layout()
    {
        var map = MapLoader.Load(File.ReadAllText(Path.Combine(LegacyMapsDir, "standard chess.json")), Catalog.CreateStandard());
        // The prototype's "standard chess" map has kings on the d-file and queens on the e-file (mirrored from orthodox chess).
        var fen = Fen.Parse("rnbkqbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBKQBNR w - - 0 1", RuleSet.Rogue);

        Assert.Equal(RuleSet.Rogue, map.Rules);
        Assert.Equal(Side.White, map.SideToMove);
        for (int sq = 0; sq < fen.Area; sq++)
        {
            Assert.Equal(fen.PieceAt(sq)?.Def, map.PieceAt(sq)?.Def);
            Assert.Equal(fen.PieceAt(sq)?.Side, map.PieceAt(sq)?.Side);
        }
    }

    /// <summary>
    /// Every legacy map parses; the only problems are content ids that M3 still has to port. This list is the
    /// M3 content checklist: when a def is registered in the catalog, remove it here.
    /// </summary>
    [Fact]
    public void Legacy_maps_only_lack_content_still_to_be_ported()
    {
        var unknown = new SortedSet<string>();
        int loaded = 0;
        foreach (var file in Directory.GetFiles(LegacyMapsDir, "*.json"))
        {
            try
            {
                MapLoader.Load(File.ReadAllText(file), ContentLibrary.CreateCatalog());
                loaded++;
            }
            catch (MapLoadException e)
            {
                Assert.All(e.Problems, p => Assert.StartsWith("unknown ", p));
                unknown.UnionWith(e.UnknownIds);
            }
        }

        Assert.Equal(8, loaded);
        Assert.Equal(new SortedSet<string>
        {
            // abilities
            "Bouncer", "Cannibal", "FireMage", "FreezeMage", "Frozen", "Marksman", "OnFire",
            "Piercing", "Revenant", "WallPlacement",
            // tiles
            "FireTile", "FreezeTile", "SlipperyTile",
            // board effects
            "CheckerboardWipe", "WallsPlacement", "WindGust",
        }, unknown);
    }

    [Fact]
    public void Abilities_and_tiles_from_the_catalog_are_applied()
    {
        var catalog = Catalog.CreateStandard()
            .AddAbility(ProbeExploding.Def, "Exploding")
            .AddTile(TestPositions.Wall, "WallTile");
        const string json = """
            { "width": 4, "height": 4, "startingPlayer": "Black",
              "pieces": [ { "type": "Pawn", "color": "White", "x": 1, "y": 1, "decorators": ["Exploding"] } ],
              "tiles": [ { "type": "WallTile", "x": 2, "y": 2 } ] }
            """;

        var s = MapLoader.Load(json, catalog);

        Assert.True(s.PieceAt(1, 1)!.HasAbility(ProbeExploding.Def));
        Assert.Same(TestPositions.Wall, s.TileAt(s.Sq(2, 2)).Def);
        Assert.Equal(Side.Black, s.SideToMove);
        Assert.Equal(0, s.UndoDepth);
    }
}
