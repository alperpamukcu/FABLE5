# LAST CALL — GDD Module: Automated Asset Production Pipeline (v2 — pixel)

> **STATUS 2026-07-27 — CURRENT.**
> The asset pipeline still governs how art enters the project.

> Canonical production method for all shipped 2D art. Complements `14_art_bible.md` v2
> (which owns the look); this module owns the process. **v2 pixel pivot** — the v1
> painterly/FLUX+LoRA path is deprecated (see §6). All banks tagged `legacy-cozy-noir`
> in `17_ui_inventory.md` are frozen and regenerated as pixel art.

## 1. Architecture
Two generators, both feeding one staging → review → approve loop:
- **PixelLab MCP** (registered MCP server `pixellab`, `https://api.pixellab.ai/mcp`) —
  PRIMARY. Purpose-built pixel-art generation (objects, tilesets, UI panels, animation
  frames) with direct agent tools (`create_map_object`, `create_ui_asset`, …).
- **Retro Diffusion** via `Tools/AssetPipeline/server.py` (`models.pixel`) — ALTERNATIVE
  when PixelLab is unavailable/insufficient, keeping the same staging loop.

## 2. Post-process chain (v2)
generate → **palette quantize** to the 14 v2 40-colour palette (nearest-ramp mapping)
→ **binary alpha cleanup** (no semi-transparent pixels) → **size verify** (exact sprite
spec from art bible §6; REJECT any off-size output — never rescale) → staging → review
at **1× and 3×** zoom → approve → `Assets/Art/<Category>/` → `LastCallImporter`
auto-import (point filter, uncompressed, no mipmaps, PPU 1).

## 3. Review criteria (v2 — replaces the painterly checklist)
Per asset, at 1× and 3×: palette compliance (40 colours only) · silhouette readable at
1× · 1 px outline in the darkest ramp step · neon rim present on foreground sprites ·
no anti-aliasing / no semi-alpha pixels · exact target size · consistent pixel density.
Max 4 rerolls per asset, then a Kontext-style edit or a human decision. Every
generate/approve/reject is logged (see §5). `approve()` autonomy per repo-root
`AGENTS.md` — the agent may approve when an asset passes this checklist.

## 4. Unity import (v2 — replaces v1 importer behaviour)
`LastCallImporter.cs` for `/Art/`: `filterMode = Point`, `textureCompression =
Uncompressed`, `mipmapEnabled = false`, `spritePixelsPerUnit = 1`. The project runs a
Unity **Pixel Perfect Camera** at a **640×360 reference, integer scaling only** (×2, ×3
= 1080p). No non-integer scaling anywhere.

## 5. Reproducibility & audit
Deterministic seeds per asset id; every generate/approve/reject logged to
`generation_log.jsonl` (prompt, seed, file, reason). This log is the Steam AI-disclosure
evidence trail: all categories produced here ship as **Pre-Generated AI content** and are
disclosed in the Steam Content Survey; agent coding assistance is exempt (Jan 2026 policy).

## 6. Deprecated (v1, do not use for v2)
FLUX.1[dev] + `lstcll` cozy-noir LoRA (do NOT train it); Recraft V3 vector UI; the
recraft-remove-background + palette-grade + 2× resize post chain. Consistency now comes
from palette quantization + fixed sprite sizes + prompt templates embedding art-bible §8
clauses — not from a style LoRA.

## 7. Bottle & pour animation (engine-side, no AI frames beyond authored tilt frames)
Slides/pours are DOTween-shaped translations on whole pixels (see `18_nightclub_scene_spec`
§3). Bottle tilt for pours uses **pre-authored sprite frames** (0° / −30° / −55°), never
runtime rotation. Glass fill uses authored fill frames, not a shader. SFX keys:
stream-start, fill-tick (pitch rises), set-down thunk.

## 8. Vessel art: the sheet, the drawing and the spout (2026-08-11)
A bottle sheet is not the bottle. The v3 masters are trimmed to their glass, but hand-drawn
art arrives however it arrives — the juice cartons came on 96×168 sheets with the carton
standing small inside (the orange one uses 46×88 of it) — and a UI Image lays a sprite out
on its **whole** sheet. So nothing about a vessel is taken from its sheet. `VesselArt`
measures the alpha and the game uses the DRAWING:

- **Size.** A vessel stands at the height its scene offers, measured on the drawing, so what
  a sheet was saved with cannot change how big a bottle is. Anything wider than **0.44 of
  its own height** is fitted by width instead — a carton is a carton beside a spirit bottle,
  not a wardrobe.
- **Standing.** Feet on the scene's line (plank, mat, wire shelf, product line), middle over
  the mark. No vessel floats on its own margin.
- **The spout** is where liquid leaves, and it is measured, never assumed. The top of the
  silhouette is right for a bottle, whose neck is the highest thing on the sheet; it is
  wrong for a carton, whose top is a flat roof with a stub set into it. So when a capless
  shot exists alongside the closed one on the same sheet, the cap is found as **what the two
  shots disagree about** — the pixels that changed, up in the vessel's top.
- **Level.** What is left in a bottle is drawn across the drawing's box, not the sheet's, and
  cut out by the art itself (`BottleFill`). Opaque art — a carton, a can — hides its own
  level by nature; the hover card and the market tile carry the number.

New art therefore needs no metadata, no pivot convention and no table: draw the vessel, keep
the closed and open shots on one sheet if the vessel has a cap, and the game measures it.
