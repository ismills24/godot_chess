extends SceneTree
## Run with Godot --headless --path . --script res://experiments/wood_set/verify_wood_set.gd
func _init() -> void:
    call_deferred("_verify")

func _verify() -> void:
    var kinds = ["pawn", "rook", "knight", "bishop", "queen", "king"]
    for dark in [false, true]:
        for kind in kinds:
            var prefix := "Dark" if dark else "Light"
            var path: String = "res://assets/pieces/wood/" + prefix + kind.capitalize() + ".tscn"
            var piece = load(path).instantiate()
            root.add_child(piece)
            assert(piece.piece_kind == kind and piece.dark_wood == dark)
            assert(piece.modules.size() == 3)
            assert(piece.has_node("Base") and piece.has_node("Body") and piece.has_node("Head/SmoothSocket"))
            for module in piece.modules:
                assert(module.mesh.get_surface_count() == 1)
                var vertices = module.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
                assert(vertices.size() > 300)
                for vertex in vertices:
                    assert(vertex.is_finite())
            piece.set_head_lift(0.15)
            assert(absf(piece.head.position.y - piece.seat_height - 0.15) < 0.00001)
            assert(absf(piece.head.rotation.y - 1.5 * TAU) < 0.00001)
            piece.set_head_lift(0.65)
            assert(piece.head.position.y > piece.seat_height + 0.30)
            piece.set_head_lift(0.0)
            assert(is_equal_approx(piece.head.position.y, piece.seat_height))
            piece.free()
    var preview = load("res://experiments/wood_set/WoodSetPreview.tscn").instantiate()
    root.add_child(preview)
    assert(preview.pieces.size() == 12)
    preview.full_armies = true
    preview._populate()
    assert(preview.pieces.size() == 32)
    for dark in [false, true]:
        var counts := {}
        for piece in preview.pieces:
            if piece.dark_wood == dark:
                counts[piece.piece_kind] = counts.get(piece.piece_kind, 0) + 1
        assert(counts == {"pawn": 8, "rook": 2, "knight": 2, "bishop": 2, "queen": 1, "king": 1})
    preview.focused = 2
    preview.full_armies = false
    preview._populate()
    assert(preview.pieces.size() == 2)
    assert(preview.pieces[0].piece_kind == "knight")
    print("PASS: 12 reusable scenes, finite geometry, module hierarchy, joint travel, reseating, 32-piece inventory, pair inspector")
    quit()
