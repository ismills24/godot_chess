extends SceneTree
## Render the real wooden pawn and each existing module, without changing the game.
func _initialize() -> void:
    capture.call_deferred()

func capture() -> void:
    var output := "res://artifacts/asset_harness/ps1_pawn/references"
    DirAccess.make_dir_recursive_absolute(output)
    var viewport := SubViewport.new()
    viewport.size = Vector2i(640, 640)
    viewport.transparent_bg = true
    viewport.own_world_3d = true
    viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
    root.add_child(viewport)
    var piece = load("res://assets/pieces/wood/LightPawn.tscn").instantiate()
    piece.depth_tested_splats = true
    viewport.add_child(piece)
    var camera := Camera3D.new()
    camera.projection = Camera3D.PROJECTION_ORTHOGONAL
    viewport.add_child(camera)
    camera.current = true
    for module in ["assembled", "base", "body", "head"]:
        piece.get_node("Base").visible = module in ["assembled", "base"]
        piece.get_node("Body").visible = module in ["assembled", "body"]
        piece.get_node("Head").visible = module in ["assembled", "head"]
        piece.get_node("ThreadedDowel").visible = module == "assembled"
        var center: Vector3 = {"assembled": Vector3(0, 0.75, 0), "base": Vector3(0, 0.06, 0), "body": Vector3(0, 0.5275, 0), "head": Vector3(0, 1.205, 0)}[module]
        camera.size = {"assembled": 1.8, "base": 1.04, "body": 1.02, "head": 0.76}[module]
        camera.position = center + Vector3(1.0, 0.65, 2.3)
        camera.look_at(center)
        for frame in 8:
            await process_frame
        await RenderingServer.frame_post_draw
        var error := viewport.get_texture().get_image().save_png(output + "/" + module + ".png")
        assert(error == OK)
        print("[PawnReference] ", module)
    quit()
