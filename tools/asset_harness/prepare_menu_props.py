"""Prepare the existing alley props and textures for the real-time Godot menu."""
import json
import shutil
import struct
from io import BytesIO
from pathlib import Path

from PIL import Image, ImageEnhance, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "artifacts/polyhaven_sources"
DESTINATION = ROOT / "menu/props"


def copy_texture(source: Path, destination: Path, size: int = 1024, *, soften: bool = False) -> None:
    with Image.open(source) as image:
        image = image.convert("RGB")
        image.thumbnail((size, size), Image.Resampling.LANCZOS)
        if soften:
            image = image.filter(ImageFilter.GaussianBlur(0.95))
            image = ImageEnhance.Contrast(image).enhance(0.68)
            image = ImageEnhance.Color(image).enhance(0.84)
        image.save(destination, quality=91)


def pack_handsaw() -> None:
    source = SOURCES / "handsaw_wood"
    scene = json.loads((source / "handsaw_wood_2k.gltf").read_text(encoding="utf-8"))
    data = bytearray((source / scene["buffers"][0]["uri"]).read_bytes())
    for image in scene["images"]:
        with Image.open(source / image.pop("uri")) as texture:
            texture = texture.convert("RGB")
            texture.thumbnail((512, 512), Image.Resampling.LANCZOS)
            compressed = BytesIO()
            texture.save(compressed, format="JPEG", quality=87)
        payload = compressed.getvalue()
        data.extend(b"\0" * (-len(data) % 4))
        image["bufferView"] = len(scene["bufferViews"])
        image["mimeType"] = "image/jpeg"
        scene["bufferViews"].append({"buffer": 0, "byteOffset": len(data), "byteLength": len(payload)})
        data.extend(payload)
    scene["buffers"][0] = {"byteLength": len(data)}
    scene_json = json.dumps(scene, separators=(",", ":")).encode()
    scene_json += b" " * (-len(scene_json) % 4)
    data.extend(b"\0" * (-len(data) % 4))
    header = struct.pack("<4sII", b"glTF", 2, 28 + len(scene_json) + len(data))
    json_chunk = struct.pack("<I4s", len(scene_json), b"JSON") + scene_json
    bin_chunk = struct.pack("<I4s", len(data), b"BIN\0") + data
    (DESTINATION / "handsaw.glb").write_bytes(header + json_chunk + bin_chunk)


def main() -> None:
    DESTINATION.mkdir(parents=True, exist_ok=True)
    copy_texture(SOURCES / "cobbles/Diffuse.jpg", DESTINATION / "cobbles.jpg")
    copy_texture(SOURCES / "cobbles/nor_gl.jpg", DESTINATION / "cobbles_normal.jpg")
    copy_texture(
        SOURCES / "hidden_alley/textures/wall_bricks_plaster_diff_1k.jpg",
        DESTINATION / "brick.jpg",
        size=512,
        soften=True,
    )
    optimized = ROOT / "artifacts/victorian_alley_v2/optimized_textures"
    copy_texture(optimized / "154_wall_bricks_plaster_nor_gl_1k.png", DESTINATION / "brick_normal.jpg")
    copy_texture(optimized / "155_wall_bricks_plaster_rough_1k.png", DESTINATION / "brick_rough.jpg")
    copy_texture(optimized / "033_modular_factory_facade_brick_diff.png", DESTINATION / "exposed_brick.jpg",
                 size=512, soften=True)
    copy_texture(optimized / "036_modular_factory_facade_brick_nor_gl.png", DESTINATION / "exposed_brick_normal.jpg")
    copy_texture(optimized / "037_modular_factory_facade_brick_rough.png", DESTINATION / "exposed_brick_rough.jpg")
    shutil.copyfile(
        ROOT / "artifacts/asset_harness/8c2492536794/model_00001.glb",
        DESTINATION / "barrel.glb",
    )
    shutil.copyfile(
        ROOT / "experiments/ps1_pawn/generated/PawnPS1.glb",
        DESTINATION / "chess_pawn.glb",
    )
    pack_handsaw()


if __name__ == "__main__":
    main()
