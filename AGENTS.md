# AGENTS.md — LAST CALL

Agent working rules for this repository. Read `CLAUDE.md` for architecture,
verification workflow and gotchas; `Docs/GDD/` is the design source of truth.

## Asset generation rules

- Asset generation goes ONLY through the `lastcall-assets` MCP server tools
  (`Tools/AssetPipeline/server.py`). Never call image APIs directly.
- Review every generated asset with `view_asset` against
  `Docs/GDD/14_art_bible.md` before approving.
- Respect max 4 rerolls per asset; after 2 failed rerolls reconsider the
  subject wording.
- You may call `approve()` autonomously when an asset passes the art bible
  checklist.
