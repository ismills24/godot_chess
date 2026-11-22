# Wooden board

`WoodBoard.tscn` is a reusable presentation-only board: 64 flush maple/walnut
squares, a plain walnut frame, and a fine inset border. Its script builds shared
tile geometry at runtime. It has no chess rules, picking, or move handling.

Open `res://experiments/wood_set/WoodBoardPreview.tscn` and press F6 to view all
32 wooden pieces in the standard starting position. Drag to orbit, scroll to
zoom, or use White's view, Black's view, and Overhead view. Pieces and pixel
rendering can be toggled separately. Auto orbit provides a continuous-motion
performance check. Player presets stop orbit and align the view with the board
edges. The camera is orthographic, without DOF or lens distortion. The main sandbox
now instantiates this board for playable 3D games.

## Integration contract

- Board origin is the center of the playing surface, at Y=0.
- Files run along +X; ranks run along -Z from White's side.
- `square_center(file, rank)` takes zero-based file/rank and returns local position.
- `board_width` and `board_height` default to 8; set them before adding the board
  to the tree for other sizes. Instance method `cell_center(file, rank)` uses these
  dimensions. Static `square_center` preserves the 8x8 preview API.
- `SQUARE_SIZE` is 1.45 units; the playing field is 11.6 units wide.
- The frame is 0.42 units wide, giving an outer span of 12.44 units.
- a1 is dark and h1 is light. Queens start on their matching color at d1/d8.
- `Squares` contains named meshes a1 through h8. `Frame` contains the foundation,
  rails, and inlay. No preview stands are used on the board.
- The preview supplies static soft contact-shadow planes beneath the pieces.
  These are grounding aids, not dynamic piece shadows or a gameplay effect.

Geometry/placement checks verified 64 squares, 32 unique occupied squares,
standard ranks, queen-square colors, and opposing knight orientations. The board
was visually checked both with pieces and from overhead without pieces. The
initial idle benchmark missed a serious moving-camera cost: CPU depth sorting
dropped a 5-second continuous-orbit test to 36 FPS average / 15 FPS 1% low.

The board now sets `depth_tested_splats = true` on all pieces. Gaussian footprints
use an alpha-scissor cutout and the GPU depth buffer, with no per-frame CPU sorting
or mesh uploads. This keeps the sampled shapes/materials but gives edges a crisper
finish than the soft alpha blending retained in the separate piece gallery.
The same orbit test improved to 120 FPS average / 115 FPS 1% low (worst frame 9 ms)
on the RTX 3080 Ti under GL Compatibility. Test motion, not just idle frames, when
changing rendering or assessing gameplay suitability.
