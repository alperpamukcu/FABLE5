# LAST CALL — GDD Module: Automated Asset Production Pipeline

> Canonical production method for all shipped 2D art. Complements `14_art_bible.md` (which owns the look); this module owns the process. v1.1 addition.

## 1. Architecture
A custom MCP server (`Tools/AssetPipeline/server.py`) exposes the generation pipeline as agent-callable tools. Claude (Antigravity) is the operator; the developer is the art director.

```
content JSON → locked prompt template (art bible clauses embedded)
→ Replicate: FLUX.1[dev] + `lstcll` cozy-noir style LoRA  (portraits, VIPs, ingredients, tools, backgrounds)
             Recraft V3                                    (UI, icons)
→ recraft-remove-background → palette grade → resize (2× target)
→ staging/ → agent vision review vs art bible → human final approve
→ Assets/Art/<Category>/ → AssetPostprocessor auto-import (sprite, PPU 100, no mipmaps, alpha)
```

## 2. Models & versions (record every change here)
| Purpose | Model | Notes |
|---|---|---|
| Painterly art | FLUX.1 [dev] + style LoRA `lstcll` | LoRA version hash pinned in pipeline_config.json |
| UI / vector | Recraft V3 | digital_illustration style |
| Isolation | recraft-remove-background | BiRefNet fallback for glass edges |
| Detail fixes / variants | FLUX Kontext | instead of full rerolls after 2 fails |

## 3. Review protocol
Agent checklist (every asset, via view_asset): palette compliance (amber/plum/teal, ≤1 neon accent) · single warm key light from upper-left · correct framing per category spec · silhouette readable at card size · no real brands, no text artifacts, no extra fingers/AI tells.
Rules: max 4 rerolls per asset, then Kontext edit or human decision. Rejects logged with reason. **approve(): superseded 2026-07-11 by the repo-root `AGENTS.md` ruling — the agent may approve autonomously when an asset passes the art bible checklist; every approval stays in the audit log.**

## 4. Reproducibility & audit
Deterministic seeds per asset id; every generate/approve/reject logged to `generation_log.jsonl` (prompt, seed, file, reason). This log is also the Steam AI-disclosure evidence trail: all categories produced here ship as **Pre-Generated AI content** and are disclosed in the Steam Content Survey; agent coding assistance is exempt (Jan 2026 policy).

## 5. Bottle & pour animation spec (engine-side, no AI frames)
- Bottle sprites: pivot at the neck/mouth. Glass sprites: liquid-fill bounds defined per sprite.
- Pour sequence (DOTween free): tilt −55° / 0.4s OutBack → particle stream (ingredient-tinted) → Shader Graph 2D liquid `_Fill` 0→1 / 0.6s with sine surface wobble → glass DOPunchScale 0.08 / 0.25s. SFX keys: stream-start, fill-tick (pitch rises), set-down thunk.
- Rationale: AI sprite-sheet animation drifts frame-to-frame on painterly art (2026 state); Balatro-class juice is tween-driven on static sprites.
