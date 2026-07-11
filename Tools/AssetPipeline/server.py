"""
LAST CALL — Asset Pipeline MCP Server
Fully-automated AI asset generation, driven by Claude via MCP.

Flow per asset:  content JSON -> locked prompt template -> Replicate (FLUX+LoRA / Recraft)
                 -> background removal -> palette grade -> resize -> staging
                 -> Claude vision review (view_asset) -> approve/reject
                 -> approved PNG lands in Assets/Art/<Category>/ (Unity AssetPostprocessor
                    applies import settings automatically).

Run:  uv run --with fastmcp --with replicate --with pillow --with numpy server.py
Env:  REPLICATE_API_TOKEN must be set.
"""

import json, os, io, time, urllib.request
from pathlib import Path

import numpy as np
import replicate
from PIL import Image as PILImage, ImageEnhance
from fastmcp import FastMCP
from fastmcp.utilities.types import Image

ROOT = Path(__file__).parent
CFG = json.loads((ROOT / "pipeline_config.json").read_text(encoding="utf-8"))

mcp = FastMCP("lastcall-asset-pipeline")


# ---------- helpers ----------

def _log(entry: dict):
    entry["ts"] = time.strftime("%Y-%m-%dT%H:%M:%S")
    with open(ROOT / CFG["log_file"], "a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")


def _load_content(category: str) -> list[dict]:
    src = (ROOT / CFG["content_sources"][category]).resolve()
    if not src.exists():
        raise FileNotFoundError(f"Content source missing: {src}")
    data = json.loads(src.read_text(encoding="utf-8"))
    if isinstance(data, list):
        items = data
    else:
        # Game data files keep their list under a schema-specific key.
        items = next((data[k] for k in ("items", "patrons", "vips", "cards", "tools")
                      if isinstance(data.get(k), list)), [])
    out, seen = [], set()
    for it in items:
        iid = it.get("id") or it.get("name", "unknown").lower().replace(" ", "_")
        if iid in seen:  # deck files repeat ids for duplicate cards; one asset each
            continue
        seen.add(iid)
        # art_prompt is the authored art direction; the name beats the mechanical
        # rules description as a fallback subject.
        out.append({
            "id": iid,
            "subject": it.get("art_prompt") or it.get("name") or it.get("description", ""),
        })
    return out


def _build_prompt(category: str, subject: str) -> str:
    spec = CFG["categories"][category]
    return spec["template"].format(
        trigger=CFG["trigger_word"], style=CFG["style_block"], subject=subject
    )


def _download(url: str) -> bytes:
    with urllib.request.urlopen(url) as r:
        return r.read()


def _run_model(category: str, prompt: str, seed: int) -> bytes:
    spec = CFG["categories"][category]
    w, h = spec["size"]
    if spec["model"] == "recraft":
        out = replicate.run(CFG["models"]["recraft"], input={
            "prompt": prompt, "size": f"{w}x{h}",
            "style": spec.get("recraft_style", "digital_illustration"),
        })
    else:
        out = replicate.run(CFG["models"]["flux_lora"], input={
            "prompt": prompt, "width": w, "height": h,
            "seed": seed, "num_inference_steps": 28,
            "guidance_scale": 3.5, "output_format": "png",
        })
    url = out[0] if isinstance(out, list) else out
    return _download(str(url))


def _remove_bg(png: bytes) -> bytes:
    out = replicate.run(CFG["models"]["remove_bg"], input={"image": io.BytesIO(png)})
    url = out[0] if isinstance(out, list) else out
    return _download(str(url))


def _grade(img: PILImage.Image) -> PILImage.Image:
    """Light palette-compliance grade: warm highlights, teal-shifted shadows."""
    g = CFG["palette_grade"]
    rgba = img.convert("RGBA")
    a = rgba.getchannel("A")
    arr = np.asarray(rgba.convert("RGB"), dtype=np.float32)
    lum = arr.mean(axis=2, keepdims=True) / 255.0
    arr[..., 0] *= 1 + (g["warm_gain"] - 1) * lum[..., 0]          # warm the lights (R)
    shadow = (1 - lum[..., 0])
    arr[..., 1] += g["teal_shadow_shift"] * shadow                  # teal in shadows (G/B)
    arr[..., 2] += g["teal_shadow_shift"] * shadow
    out = PILImage.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")
    out = ImageEnhance.Color(out).enhance(g["saturation"])
    out = out.convert("RGBA"); out.putalpha(a)
    return out


def _finalize(png: bytes, category: str) -> PILImage.Image:
    spec = CFG["categories"][category]
    img = PILImage.open(io.BytesIO(png)).convert("RGBA")
    if spec["grade"]:
        img = _grade(img)
    tw, th = spec["target"]
    img.thumbnail((tw * 2, th * 2), PILImage.LANCZOS)  # keep 2x for retina, ratio preserved
    return img


def _staging_path(category: str, asset_id: str, seed: int) -> Path:
    d = ROOT / CFG["staging_dir"] / category
    d.mkdir(parents=True, exist_ok=True)
    return d / f"{asset_id}__seed{seed}.png"


# ---------- MCP tools ----------

@mcp.tool()
def pipeline_status() -> str:
    """Counts of staged/approved assets per category, and total logged generations."""
    lines = []
    for cat, spec in CFG["categories"].items():
        stag = ROOT / CFG["staging_dir"] / cat
        appr = (ROOT / CFG["project_assets_dir"] / spec["out_subdir"]).resolve()
        n_s = len(list(stag.glob("*.png"))) if stag.exists() else 0
        n_a = len(list(appr.glob("*.png"))) if appr.exists() else 0
        lines.append(f"{cat}: staged={n_s} approved={n_a}")
    logf = ROOT / CFG["log_file"]
    n_gen = sum(1 for _ in open(logf, encoding="utf-8")) if logf.exists() else 0
    lines.append(f"total generations logged: {n_gen}")
    return "\n".join(lines)


@mcp.tool()
def preview_prompts(category: str, limit: int = 5) -> str:
    """Show the exact prompts that would be sent for a category (no generation, no cost)."""
    items = _load_content(category)[:limit]
    return "\n\n".join(f"[{it['id']}]\n{_build_prompt(category, it['subject'])}" for it in items)


@mcp.tool()
def generate(category: str, ids: list[str] | None = None, seed_offset: int = 0) -> str:
    """Generate assets for a category (all items, or only the listed ids).
    seed_offset: bump when rerolling a rejected asset (max_rerolls_per_asset enforced)."""
    if category not in CFG["categories"]:
        return f"Unknown category. Valid: {list(CFG['categories'])}"
    if seed_offset > CFG["max_rerolls_per_asset"]:
        return f"Reroll limit ({CFG['max_rerolls_per_asset']}) reached — switch to a Kontext edit or ask the human."
    spec, results = CFG["categories"][category], []
    items = _load_content(category)
    if ids:
        items = [it for it in items if it["id"] in set(ids)]
    for i, it in enumerate(items):
        seed = CFG["base_seed"] + hash(it["id"]) % 10000 + seed_offset * 100000
        prompt = _build_prompt(category, it["subject"])
        try:
            raw = _run_model(category, prompt, seed)
            if spec["remove_bg"]:
                raw = _remove_bg(raw)
            img = _finalize(raw, category)
            p = _staging_path(category, it["id"], seed)
            img.save(p)
            _log({"event": "generate", "category": category, "id": it["id"],
                  "seed": seed, "prompt": prompt, "file": str(p)})
            results.append(f"OK  {it['id']} -> {p.name}")
        except Exception as e:
            _log({"event": "error", "category": category, "id": it["id"], "error": str(e)})
            results.append(f"ERR {it['id']}: {e}")
    return "\n".join(results) or "Nothing matched."


@mcp.tool()
def list_staging(category: str) -> str:
    """List staged (not yet reviewed) files for a category."""
    d = ROOT / CFG["staging_dir"] / category
    files = sorted(p.name for p in d.glob("*.png")) if d.exists() else []
    return "\n".join(files) or "(empty)"


@mcp.tool()
def view_asset(category: str, filename: str) -> Image:
    """Return a staged image so the reviewing agent can SEE it and judge it against
    the art bible checklist (palette, upper-left key light, framing, no AI artifacts)."""
    p = ROOT / CFG["staging_dir"] / category / filename
    return Image(path=str(p))


@mcp.tool()
def approve(category: str, filename: str) -> str:
    """Move a staged asset into the Unity Assets folder (import settings auto-apply)."""
    spec = CFG["categories"][category]
    src = ROOT / CFG["staging_dir"] / category / filename
    dst_dir = (ROOT / CFG["project_assets_dir"] / spec["out_subdir"]).resolve()
    dst_dir.mkdir(parents=True, exist_ok=True)
    clean = filename.split("__seed")[0] + ".png"
    dst = dst_dir / clean
    src.replace(dst)
    _log({"event": "approve", "category": category, "file": str(dst)})
    return f"Approved -> {dst}"


@mcp.tool()
def reject(category: str, filename: str, reason: str) -> str:
    """Delete a staged asset and log WHY (reason feeds the next reroll's judgement)."""
    p = ROOT / CFG["staging_dir"] / category / filename
    if p.exists():
        p.unlink()
    _log({"event": "reject", "category": category, "file": filename, "reason": reason})
    return f"Rejected ({reason}). Reroll with generate(category, ids=[...], seed_offset+1)."


if __name__ == "__main__":
    mcp.run()
