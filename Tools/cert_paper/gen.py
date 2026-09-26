# -*- coding: utf-8 -*-
"""The certificate's paper, generated (2026-09-26, the author: "Sertifika arkaplanini
pixellabden uretebilirsin") — three stocks for the seven grades, the way the bill's
sheet was made. One candidate per job (the author's one-alternative rule).

  queue  — queue the three create_image_pro jobs (512x304, ~20 generations each)
  fetch  — poll get_image until all three land in raw/
  ship   — trim, posterize to a small tone count like bill_sheet, verify, write out/

Grades: worn docket (rungs 0-1) / clean stock (rungs 2-3) / ivory premium (rungs 4-6).
"""
import base64
import json
import os
import sys
import time
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
TOKEN_PATH = os.path.join(HERE, "..", "pixellab_token.txt")
URL = "https://api.pixellab.ai/mcp"
STATE = os.path.join(HERE, "state.json")
RAW = os.path.join(HERE, "raw")
BILL = os.path.join(HERE, "..", "..", "Assets", "Resources", "Items", "bill_sheet.png")

W, H = 512, 304
assert W % 4 == 0 and H % 4 == 0

BLANK = ("The sheet is completely EMPTY AND BLANK: no writing, no letters, no words, "
         "no border, no frame, no ruled lines, no stamp, no seal, no illustration - "
         "only the bare paper surface itself. ")
SUBJECT = ("the paper sheet itself is the single subject and fills the whole frame "
           "edge to edge, an upright rectangle with softly deckled hand-cut edges. ")
LIGHT = "Flat even ambient light, no beams, no glow, no cast shadow. "

JOBS = {
    "cert_paper_worn": (
        "An old yellowed BLANK sheet of certificate paper, " + SUBJECT + BLANK +
        "Aged and handled for years in a drawer: warm yellowed cream stock, subtle "
        "fibre grain, gentle tonal mottling, small brown foxing specks gathered "
        "toward the edges and corners, the outermost few pixels of every edge "
        "browned darker, one faint soft horizontal fold crease across the middle "
        "and one faint vertical fold crease at one third. " + LIGHT +
        "Muted warm palette: yellowed cream midtones around #E6DCC0, shading around "
        "#C9BCA8, foxing and browned edges around #8F5A1E."),
    "cert_paper_clean": (
        "A clean BLANK sheet of warm white certificate paper, " + SUBJECT + BLANK +
        "Fresh stationery stock: warm white paper, even fibre grain, very subtle "
        "tonal variation, a few pale fibre flecks, unmarked and unhandled, edges "
        "clean and even. " + LIGHT +
        "Palette: warm white around #F6F1E2, paler highlights around #FBF7EA, the "
        "faintest shading around #E0D7C2."),
    "cert_paper_ivory": (
        "A luxurious BLANK sheet of premium ivory laid paper, " + SUBJECT +
        "The sheet is completely EMPTY AND BLANK: no writing, no letters, no words, "
        "no border, no frame, no stamp, no seal, no illustration - only the bare "
        "paper surface itself. The finest stationery a grand bar can buy: pale "
        "ivory stock with delicate horizontal laid lines pressed faintly into the "
        "surface, a soft satin sheen, immaculate and unhandled, fine even deckled "
        "edges. " + LIGHT +
        "Palette: pale ivory around #FBF7EA, laid lines around #EFE6D0, the "
        "faintest golden warmth around #F5C97B in the sheen."),
}
SEEDS = {"cert_paper_worn": 11, "cert_paper_clean": 22, "cert_paper_ivory": 33}


def token():
    with open(TOKEN_PATH, "r", encoding="utf-8") as f:
        return f.read().strip()


def rpc(name, arguments, timeout=240):
    payload = {"jsonrpc": "2.0", "id": 1, "method": "tools/call",
               "params": {"name": name, "arguments": arguments}}
    req = urllib.request.Request(URL, data=json.dumps(payload).encode("utf-8"))
    req.add_header("Authorization", "Bearer " + token())
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    req.add_header("User-Agent", "Mozilla/5.0")
    with urllib.request.urlopen(req, timeout=timeout) as r:
        body = r.read().decode("utf-8", "replace")
    if "data:" in body:
        for line in body.splitlines():
            if line.startswith("data:"):
                body = line[5:].strip()
                break
    return json.loads(body)


def content_of(resp):
    return resp.get("result", {}).get("content", [])


def load_state():
    if os.path.exists(STATE):
        with open(STATE, "r", encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_state(st):
    with open(STATE, "w", encoding="utf-8") as f:
        json.dump(st, f, indent=1)


def queue():
    st = load_state()
    with open(BILL, "rb") as f:
        bill64 = base64.b64encode(f.read()).decode("ascii")
    ref = [{"base64": bill64,
            "usage": "the game's till receipt paper: match its pixel rendering "
                     "style, its flat pale tones and its deckled edge treatment - "
                     "this certificate sheet is the same paper family"}]
    for name, desc in JOBS.items():
        if len(desc) > 2000:
            raise SystemExit("brief too long for %s: %d" % (name, len(desc)))
        if name in st and st[name].get("job_id"):
            print("already queued:", name, st[name]["job_id"])
            continue
        resp = rpc("create_image_pro", {
            "description": desc, "width": W, "height": H,
            "no_background": True, "seed": SEEDS[name],
            "reference_images": ref,
        })
        text = " ".join(c.get("text", "") for c in content_of(resp))
        jid = None
        for tok_ in text.replace('"', " ").replace(",", " ").split():
            if len(tok_) >= 32 and "-" in tok_:
                jid = tok_
        st[name] = {"job_id": jid, "raw_text": text[:400]}
        save_state(st)
        print("queued", name, "->", jid or text[:200])


def fetch():
    os.makedirs(RAW, exist_ok=True)
    st = load_state()
    pending = {n: j for n, j in st.items() if j.get("job_id") and not j.get("file")}
    rounds = 0
    while pending and rounds < 60:
        rounds += 1
        for name in list(pending):
            jid = st[name]["job_id"]
            resp = rpc("get_image", {"job_id": jid})
            parts = content_of(resp)
            texts = " ".join(c.get("text", "") for c in parts if c.get("type") == "text")
            imgs = [c for c in parts if c.get("type") == "image"]
            if imgs:
                for i, c in enumerate(imgs):
                    path = os.path.join(RAW, "%s_%d.png" % (name, i))
                    with open(path, "wb") as f:
                        f.write(base64.b64decode(c["data"]))
                st[name]["file"] = os.path.join(RAW, name + "_0.png")
                st[name]["candidates"] = len(imgs)
                save_state(st)
                print("landed", name, "candidates:", len(imgs))
                del pending[name]
            else:
                print(name, "status:", texts[:160])
                if "failed" in texts.lower():
                    st[name]["error"] = texts[:300]
                    save_state(st)
                    del pending[name]
        if pending:
            time.sleep(15)
    print("done; state:", json.dumps({k: {kk: vv for kk, vv in v.items() if kk != "raw_text"}
                                      for k, v in st.items()}, indent=1))


if __name__ == "__main__":
    {"queue": queue, "fetch": fetch}[sys.argv[1]]()
