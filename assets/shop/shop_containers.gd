extends RefCounted
## Factories for the same textured, procedural containers used by ShopPreview.
## Returned nodes are unparented, with meshes and attachment anchors already built.
const ContainerMesh := preload("res://assets/shop/shop_container.gd")

static func create_wine_crate(size := Vector3(1.8, 1.3, 1.0), seed_value := 17,
		wear_amount := 0.6) -> ContainerMesh:
	return _create(ContainerMesh.Kind.WINE_CRATE, size, seed_value, wear_amount)

static func create_cardboard_box(size := Vector3(1.6, 0.8, 1.0), seed_value := 17,
		wear_amount := 0.6) -> ContainerMesh:
	return _create(ContainerMesh.Kind.CARDBOARD, size, seed_value, wear_amount)

static func _create(kind: ContainerMesh.Kind, size: Vector3, seed_value: int,
		wear_amount: float) -> ContainerMesh:
	var container := ContainerMesh.new()
	container.name = "WineCrate" if kind == ContainerMesh.Kind.WINE_CRATE else "CardboardBox"
	container.configure(kind, size, seed_value, wear_amount)
	return container
