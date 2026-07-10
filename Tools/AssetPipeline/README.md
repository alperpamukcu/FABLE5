# Last Call — Asset Pipeline MCP Server (setup)

## 1. Install & run (Windows, once)
```
pip install uv          # if not present
setx REPLICATE_API_TOKEN "r8_your_token_here"
```
Server is launched BY Antigravity (do not run it manually in a terminal).

## 2. Register in Antigravity (MCP config)
Antigravity → Agent panel ••• → MCP Servers → Add custom server (raw config):
```json
{
  "mcpServers": {
    "lastcall-assets": {
      "command": "uv",
      "args": ["run", "--with", "fastmcp", "--with", "replicate",
               "--with", "pillow", "--with", "numpy",
               "PATH_TO_PROJECT/Tools/AssetPipeline/server.py"],
      "env": { "REPLICATE_API_TOKEN": "r8_your_token_here" }
    }
  }
}
```

## 3. One-time prerequisites
- Train the cozy-noir style LoRA on Replicate (ostris/flux-dev-lora-trainer, is_style=true,
  trigger word `lstcll`, 15–25 golden-set images). Paste the resulting
  `owner/model:versionhash` into `pipeline_config.json → models.flux_lora`.
- Buy PREPAID Replicate credit to hard-cap spend (Replicate removed monthly spend limits).
- Content JSONs may include an optional `art_prompt` field per item; if present it
  overrides `description` as the subject text.

## 4. The autonomous loop Claude runs
1. `preview_prompts("portraits")`  → sanity-check prompts (free)
2. `generate("portraits", ids=["sailor_musa", ...])`  → batch of staged PNGs
3. For each: `view_asset(...)` → judge against art-bible checklist
   (palette, upper-left key light, 3/4 framing, no hands, no AI artifacts)
4. Fail → `reject(..., reason)` → `generate(..., ids=[id], seed_offset+1)` (max 4)
5. Pass → keep staged, and ONLY after the HUMAN eyeballs the batch → `approve(...)`
6. Approved PNGs land in Assets/Art/<Category>/ — the AssetPostprocessor
   (Assets/Editor/LastCallImporter.cs) applies sprite import settings automatically.

## 5. Human gates (non-negotiable, from the blueprint)
- Gate 1: the LoRA + golden set + prompt templates (the "look") — human sign-off.
- Gate 2: final approve of every shipped asset — human. Claude pre-filters, never ships alone.

## 6. Suggested .agent rule addition (append to your rules file / AGENTS.md)
> Asset generation goes ONLY through the `lastcall-assets` MCP server tools.
> Never call image APIs directly. Review every generated asset with view_asset
> against Docs/GDD/14_art_bible.md before proposing approval. Never call approve()
> without explicit human confirmation in chat. Respect max 4 rerolls per asset.
