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


def _pause():
    """Low-credit accounts get 6 requests/min with burst 1; space out prediction
    creates so the second call of an asset (remove_bg) doesn't 429 and waste the
    first. Set throttle_pause_s to 0 once the account holds >= $5."""
    time.sleep(CFG.get("throttle_pause_s", 0))


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
                _pause()  # second prediction of the asset; see _pause docstring
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
        _pause()
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


# ---------- style LoRA bootstrap (golden set + training) ----------

GOLDEN_DIR = ROOT / CFG["staging_dir"] / "golden_set"


@mcp.tool()
def golden_set_generate(indices: list[int] | None = None, seed_offset: int = 0) -> str:
    """Generate style-reference candidates with base FLUX (no LoRA) from
    pipeline_config golden_set_subjects. These train the cozy-noir style LoRA;
    they are NOT game assets. Files land in staging/golden_set/."""
    GOLDEN_DIR.mkdir(parents=True, exist_ok=True)
    subjects = CFG["golden_set_subjects"]
    picks = indices if indices else list(range(len(subjects)))
    results = []
    for i in picks:
        seed = CFG["base_seed"] + 7000 + i + seed_offset * 100000
        prompt = f"{CFG['style_block']}, {subjects[i]}"
        try:
            out = replicate.run(CFG["models"]["flux_base"], input={
                "prompt": prompt, "aspect_ratio": "1:1", "seed": seed,
                "num_inference_steps": 28, "guidance": 3.5, "output_format": "png",
            })
            url = out[0] if isinstance(out, list) else out
            p = GOLDEN_DIR / f"ref_{i:02d}__seed{seed}.png"
            p.write_bytes(_download(str(url)))
            _log({"event": "golden_set", "index": i, "seed": seed, "prompt": prompt, "file": str(p)})
            results.append(f"OK  ref_{i:02d} -> {p.name}")
        except Exception as e:
            _log({"event": "error", "category": "golden_set", "index": i, "error": str(e)})
            results.append(f"ERR ref_{i:02d}: {e}")
        _pause()
    return "\n".join(results)


@mcp.tool()
def golden_set_pack(min_images: int = 15) -> str:
    """Zip the reviewed golden-set images for the LoRA trainer."""
    import zipfile
    pngs = sorted(GOLDEN_DIR.glob("*.png"))
    if len(pngs) < min_images:
        return f"Only {len(pngs)} refs staged; need at least {min_images}."
    zpath = GOLDEN_DIR / "golden_set.zip"
    with zipfile.ZipFile(zpath, "w") as z:
        for p in pngs:
            z.write(p, p.name)
    return f"Packed {len(pngs)} images -> {zpath}"


@mcp.tool()
def train_style_lora(steps: int = 1000) -> str:
    """Launch the cozy-noir style LoRA training on Replicate from the packed golden
    set. Creates the destination model when missing. Returns the training id."""
    zpath = GOLDEN_DIR / "golden_set.zip"
    if not zpath.exists():
        return "golden_set.zip missing - run golden_set_pack first."

    owner, name = CFG["lora_destination"].split("/")
    try:
        replicate.models.create(owner=owner, name=name, visibility="private",
                                hardware="cpu", description="LAST CALL cozy-noir style LoRA")
    except Exception as e:  # already exists is fine
        _log({"event": "model_create_note", "error": str(e)})

    trainer = replicate.models.get(CFG["models"]["lora_trainer"])
    version = trainer.latest_version.id
    with open(zpath, "rb") as f:
        training = replicate.trainings.create(
            model=CFG["models"]["lora_trainer"], version=version,
            destination=CFG["lora_destination"],
            input={"input_images": f, "trigger_word": CFG["trigger_word"],
                   "steps": steps, "is_style": True},
        )
    _log({"event": "training_started", "id": training.id, "steps": steps})
    return f"Training started: {training.id} (status: {training.status})"


@mcp.tool()
def training_status(training_id: str) -> str:
    """Poll a LoRA training. When it succeeds, returns the model:version to paste
    into pipeline_config.json models.flux_lora."""
    t = replicate.trainings.get(training_id)
    if t.status == "succeeded":
        version = (t.output or {}).get("version", "?")
        return f"succeeded - flux_lora value: {version}"
    return f"{t.status}" + (f" - {t.error}" if t.error else "")


if __name__ == "__main__":
    mcp.run()
