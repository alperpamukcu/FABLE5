# -*- coding: utf-8 -*-
"""The PixelLab MCP helper, rebuilt 2026-08-17 (the original lived in a session
scratchpad and died with it - see memory pixellab-mcp-constraints).

Speaks MCP streamable-HTTP against https://api.pixellab.ai/mcp. The server is
sessionless (no mcp-session-id header as of server 0.2.0) and needs
'Authorization: Bearer <token>' on tools/call - initialize and tools/list are
open, which is how an unauthenticated probe can still enumerate the tools.

Token comes from PIXELLAB_SECRET in the environment, else Tools/pixellab_token.txt
(git-ignored). Interface matches what the older Tools scripts import:
    call(tool, args, timeout)  -> prints text content, returns parsed messages
    post(payload, sid, timeout)-> (status, body_text)
    sse(body)                  -> [parsed json message, ...]
    _sid()                     -> None (kept for the old call sites)
"""
import io, json, os, ssl, urllib.request

URL = 'https://api.pixellab.ai/mcp'
HERE = os.path.dirname(os.path.abspath(__file__))


def _token():
    tok = os.environ.get('PIXELLAB_SECRET')
    if not tok:
        p = os.path.join(HERE, 'pixellab_token.txt')
        if os.path.exists(p):
            tok = io.open(p, encoding='utf-8').read().strip()
    if not tok:
        raise SystemExit('No PixelLab token: set PIXELLAB_SECRET or write '
                         'Tools/pixellab_token.txt (git-ignored).')
    return tok


def _sid():
    return None  # server 0.2.0 is sessionless


def post(payload, sid=None, timeout=900):
    req = urllib.request.Request(URL, data=json.dumps(payload).encode('utf-8'), headers={
        'Content-Type': 'application/json',
        'Accept': 'application/json, text/event-stream',
        'Authorization': 'Bearer ' + _token(),
    })
    with urllib.request.urlopen(req, timeout=timeout,
                                context=ssl.create_default_context()) as r:
        return r.status, r.read().decode('utf-8', 'replace')


def sse(body):
    out = []
    for line in body.splitlines():
        if line.startswith('data:'):
            try:
                out.append(json.loads(line[5:].strip()))
            except ValueError:
                pass
    return out


def call(tool, args, timeout=900):
    """tools/call; prints every text content line (the old scripts parse stdout
    for 'id:' lines) and returns the parsed SSE messages."""
    _, body = post({'jsonrpc': '2.0', 'id': 1, 'method': 'tools/call',
                    'params': {'name': tool, 'arguments': args}}, _sid(), timeout)
    msgs = sse(body)
    for m in msgs:
        for c in ((m.get('result') or {}).get('content') or []):
            if c.get('type') == 'text':
                print(c['text'])
    return msgs


def fetch_url(url, timeout=300):
    """A no-auth download URL from get_image/get_object -> bytes."""
    with urllib.request.urlopen(url, timeout=timeout,
                                context=ssl.create_default_context()) as r:
        return r.read()
