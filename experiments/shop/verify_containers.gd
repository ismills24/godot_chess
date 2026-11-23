extends SceneTree
const ShopContainer := preload("res://assets/shop/shop_container.gd")
const Containers := preload("res://assets/shop/shop_containers.gd")

func _init() -> void:
	call_deferred("_verify")

func _verify() -> void:
	for texture in [ShopContainer.WOOD, ShopContainer.CARD]:
		assert(texture.get_width() == 128 and texture.get_height() == 128, "Runtime textures must be 128px")
	var sizes := [ShopContainer.MIN_SIZE, Vector3(1.8, 1.3, 1.0), ShopContainer.MAX_SIZE, Vector3(3.2, 0.45, 0.45), Vector3(0.55, 2.6, 2.0)]
	for kind in [ShopContainer.Kind.WINE_CRATE, ShopContainer.Kind.CARDBOARD]:
		for size: Vector3 in sizes:
			var box := ShopContainer.new()
			box.kind = kind
			box.dimensions = size
			root.add_child(box)
			assert(box.scale == Vector3.ONE)
			assert(box.usable_size().x > 0 and box.usable_size().y > 0 and box.usable_size().z > 0)
			assert(is_equal_approx(box.tag_anchor.position.z, size.z * 0.5 + 0.04))
			var mesh: ArrayMesh = box.get_node("ContainerMesh").mesh
			var data := mesh.surface_get_arrays(0)
			var vertices: PackedVector3Array = data[Mesh.ARRAY_VERTEX]
			var normals: PackedVector3Array = data[Mesh.ARRAY_NORMAL]
			var uvs: PackedVector2Array = data[Mesh.ARRAY_TEX_UV]
			assert(vertices.size() > 0 and vertices.size() % 3 == 0)
			for i in vertices.size():
				assert(vertices[i].is_finite() and normals[i].is_finite() and uvs[i].is_finite())
				assert(is_equal_approx(normals[i].length(), 1.0))
			for i in range(0, vertices.size(), 3):
				var cross := (vertices[i + 1] - vertices[i]).cross(vertices[i + 2] - vertices[i])
				assert(cross.length_squared() > 0.00000000001, "No collapsed triangles at extreme dimensions")
				assert(cross.dot(normals[i]) < 0, "Godot front faces use clockwise winding")
			box.rebuild()
			assert(vertices == box.get_node("ContainerMesh").mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX], "Seeded geometry is reproducible")
			assert(uvs == box.get_node("ContainerMesh").mesh.surface_get_arrays(0)[Mesh.ARRAY_TEX_UV])
			box.dimensions = Vector3(-1, 100, 0)
			box.rebuild()
			assert(box.dimensions == Vector3(0.55, 2.6, 0.45))
			box.dimensions = Vector3(NAN, 1, 1)
			box.rebuild()
			assert(box.dimensions.is_finite())
			box.free()
	_verify_helpers()
	print("PASS: 128px imports; both materials at five dimension extremes; finite vertices/UVs; clockwise triangles; anchors; deterministic rebuilds; invalid-size clamping; factory helpers and stable attachments")
	quit()

func _verify_helpers() -> void:
	var crate := Containers.create_wine_crate(Vector3(2.0, 1.4, 1.1), 42, 0.4)
	var box := Containers.create_cardboard_box(Vector3(1.6, 0.8, 1.0), 43, 0.8)
	for container in [crate, box]:
		assert(container.get_parent() == null)
		assert(container.has_node("ContainerMesh") and container.item_anchor != null and container.tag_anchor != null)
		var item := Node3D.new()
		var tag := Node3D.new()
		container.item_anchor.add_child(item)
		container.tag_anchor.add_child(tag)
		var item_anchor_id: int = container.item_anchor.get_instance_id()
		var tag_anchor_id: int = container.tag_anchor.get_instance_id()
		var child_count: int = container.get_child_count()
		container.position = Vector3(2, 3, 4)
		container.rotation.y = 0.3
		var original_transform: Transform3D = container.transform
		# Detached callers can resize and attach content before parenting.
		container.resize(Vector3(2.4, 1.1, 1.3))
		assert(container.dimensions == Vector3(2.4, 1.1, 1.3))
		assert(is_equal_approx(container.tag_anchor.position.z, 0.69))
		root.add_child(container)
		container.resize(Vector3(1.4, 1.7, 0.7))
		container.set_wear(81, 0.9)
		assert(container.wear_seed == 81 and is_equal_approx(container.wear, 0.9))
		assert(container.item_anchor.get_instance_id() == item_anchor_id)
		assert(container.tag_anchor.get_instance_id() == tag_anchor_id)
		assert(item.get_parent() == container.item_anchor and tag.get_parent() == container.tag_anchor)
		assert(container.get_child_count() == child_count)
		assert(container.transform == original_transform)
	assert(crate.kind == ShopContainer.Kind.WINE_CRATE and box.kind == ShopContainer.Kind.CARDBOARD)
	crate.free()
	box.free()
