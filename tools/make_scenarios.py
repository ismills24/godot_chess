"""Generate sandbox scenario maps (maps/sandbox/*.json) from FEN plus abilities/tiles.

Run from the repo root:  python tools/make_scenarios.py
Squares are algebraic ("e4"); the output uses the prototype's map format (x = file, y = rank, 0-based).
"""
import json, os

NAMES = {'p': 'Pawn', 'n': 'Knight', 'b': 'Bishop', 'r': 'Rook', 'q': 'Queen', 'k': 'King'}

def xy(sq):
    return ord(sq[0]) - ord('a'), int(sq[1:]) - 1

def build(name, description, fen, abilities=None, tiles=None, rules="rogue"):
    rows, side = fen.split()[0].split('/'), fen.split()[1]
    height, pieces, width = len(rows), [], 0
    for r, row in enumerate(rows):
        y, x, run = height - 1 - r, 0, ''
        for ch in row + ' ':
            if ch.isdigit():
                run += ch; continue
            if run: x += int(run); run = ''
            if ch == ' ': break
            pieces.append({"type": NAMES[ch.lower()], "color": "White" if ch.isupper() else "Black", "x": x, "y": y, "decorators": []})
            x += 1
        width = max(width, x)
    for ability, squares in (abilities or {}).items():
        for sq in squares:
            px, py = xy(sq)
            next(p for p in pieces if p["x"] == px and p["y"] == py)["decorators"].append(ability)
    tile_list = [{"type": t, "x": xy(sq)[0], "y": xy(sq)[1]} for t, squares in (tiles or {}).items() for sq in squares]
    return {"name": name, "description": description, "rules": rules, "width": width, "height": height,
            "startingPlayer": "White" if side == 'w' else "Black", "pieces": pieces, "tiles": tile_list}

SCENARIOS = {
    "01_explosion_chain": build(
        "Explosion chain vs Scapegoat",
        "White to move. Play Rd1xd5: wave 0 the bomb on d5 dies; wave 1 its blast kills the rook and the bomb on c6, "
        "and would kill the bishop on e6 but the Scapegoat knight on f7 dies instead; wave 2 the c6 bomb kills b7.",
        "7k/1p3N2/2n1B3/3p4/8/8/PP6/K2R4 w",
        abilities={"Exploding": ["d5", "c6"], "Scapegoat": ["f7"]}),
    "02_guardian_fog_walls": build(
        "Guardian, fog and walls",
        "Black to move. Ra4xd4 is stopped by the Guardian tile under the queen (the rook is blocked and stays on a4, "
        "the tile is used up), so a second capture (Bg7xd4) goes through. The black king stands in fog: nothing can "
        "capture it. Walls stop sliding pieces but break when a piece lands on them.",
        "4k3/6b1/8/8/r2Q4/8/8/4K2R b",
        tiles={"GuardianTile": ["d4"], "FogTile": ["e8"], "WallTile": ["h5", "c6", "e3"]}),
    "03_phantom_ranks": build(
        "Phantom ranks",
        "Rooks and queens on both sides are Phantoms: from move one they slide straight through their own pawns. "
        "Walls on d4 and e5 still stop them. Try Qd1-d3 or Ra1xa7.",
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w",
        abilities={"Phantom": ["a1", "h1", "d1", "a8", "h8", "d8"]},
        tiles={"WallTile": ["d4", "e5"]}),
    "04_predator_hunt": build(
        "Predator hunt",
        "White's Predator pawn evolves with every kill: dxe5 (knight), Nxf7 (bishop), Bxb3 (rook), Rxb8 (queen); "
        "Black moves in between. Taking the exploding pawn on c5 instead ends the hunt with a bang.",
        "1q5k/5p2/8/2p1p3/3P4/1r6/8/K7 w",
        abilities={"Predator": ["d4"], "Exploding": ["c5"]}),
    "05_mixed_armies": build(
        "Mixed armies",
        "A full game with everything ported so far: exploding e-pawns, Scapegoat knights, Phantom rooks, Predator "
        "d-pawns, walls, fog and guardian tiles in the centre.",
        "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w",
        abilities={"Exploding": ["e2", "e7"], "Scapegoat": ["b1", "g8"], "Phantom": ["a1", "h8"], "Predator": ["d2", "d7"]},
        tiles={"WallTile": ["d4", "e5"], "FogTile": ["c5", "f4"], "GuardianTile": ["d5", "e4"]}),
    "06_empty_board": build(
        "Empty board",
        "Just two kings. Use Place, Tile and Ability modes to build your own test position, then play it.",
        "4k3/8/8/8/8/8/8/4K3 w"),
}

out = os.path.join(os.path.dirname(__file__), "..", "maps", "sandbox")
for key, data in SCENARIOS.items():
    with open(os.path.join(out, key + ".json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(data, f, indent=2)
        f.write("\n")
print("wrote", len(SCENARIOS), "scenarios")
