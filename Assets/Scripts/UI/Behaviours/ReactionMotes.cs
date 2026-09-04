using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// WHAT A DRINKER GIVES BACK, AS MOTES INSTEAD OF A LINE OF TEXT (2026-09-04, the
    /// author: "müşteriler içkilerini içtikten sonra tepkilerini emoji efektleriyle
    /// verecek … müşterinin assetinin arkasından küçük küçük partiküller olarak yukarı
    /// gidecek … mükemmelde 20 adet").
    ///
    /// Small faces that come up from BEHIND the drinker: world sprites on the patrons'
    /// own sorting layer, one order under the bodies, so they rise out of the shoulders
    /// instead of landing on the face. HOW MANY is the whole reading — a handful for a
    /// drink that missed, twenty for one that landed — so the count is the caller's word
    /// and everything else in here is scatter.
    ///
    /// EVERY MOTE IS ITS OWN (the author: "birbirinden bağımsız yükselecek"). Twenty of
    /// them on one clock is a curtain rising, not a cheer: each leaves at its own moment,
    /// climbs its own distance, sways at its own rate and fades on its own. Nothing is
    /// shared but the face and the tint.
    ///
    /// It rides the room rather than the screen. The motes follow the body they came
    /// from — a drinker whose stool lifts with the cellar takes their reaction up with
    /// them — and a burst fired at somebody already walking out is pinned where it was
    /// thrown, because a cloud that chases a leaver across the bar reads as a comet.
    /// </summary>
    public sealed class ReactionMotes : MonoBehaviour
    {
        /// <summary>Stage units tall. The face art is 14 px and is drawn at one unit a
        /// pixel — two screen pixels apiece at 720p — so it never lands off the grid.</summary>
        private const float MoteUnits = 14f;

        /// <summary>The band they are made on: the patrons' own sorting layer, which is
        /// orders 22..29. Where in it they sit is decided every frame — one under the body
        /// they came from, whatever that body is doing (a seated drinker draws at 25, one
        /// walking out at 22), so they are BEHIND their own customer and never in front of
        /// the one storming off.</summary>
        private const int MoteOrder = 24;

        private const float Stagger = 0.05f;     // between one mote leaving and the next
        private const float LifeMin = 0.95f, LifeMax = 1.5f;
        private const float RiseMin = 58f, RiseMax = 104f;   // stage units climbed
        private const float SpreadX = 30f;       // how wide they leave the shoulders
        private const float SwayMin = 4f, SwayMax = 11f;
        private const float LeanMax = 24f;       // how far apart they have drifted by the top

        private sealed class Mote
        {
            public SpriteRenderer Sr;
            public Vector3 Home;
            public float Delay, Life, Rise, Sway, Freq, Phase, Lean, Scale, T;
            public bool Done;
        }

        private Mote[] _motes;
        private SpriteRenderer _body;
        private bool _follow;
        private Vector3 _followFrom;
        private Color _tint;

        /// <summary>
        /// Throws <paramref name="count"/> motes from <paramref name="at"/> (stage
        /// coordinates), behind <paramref name="body"/>. With <paramref name="follow"/> they
        /// take that body's movement with them; without it they stay where they were thrown.
        /// </summary>
        public static void Burst(DiegeticStage stage, Vector3 at, SpriteRenderer body,
            bool follow, Sprite face, Color tint, int count)
        {
            if (stage == null || face == null || count <= 0) return;
            var host = new GameObject("ReactionMotes");
            var motes = host.AddComponent<ReactionMotes>();
            motes.Build(stage, at, body, follow, face, tint, count);
        }

        private void Build(DiegeticStage stage, Vector3 at, SpriteRenderer body, bool follow,
            Sprite face, Color tint, int count)
        {
            _tint = tint;
            _body = body;
            _follow = follow && body != null;
            _followFrom = body != null ? body.transform.position : Vector3.zero;
            bool calm = Motion.Reduced;
            float scale = MoteUnits / Mathf.Max(0.0001f, face.bounds.size.y);
            _motes = new Mote[count];
            for (int i = 0; i < count; i++)
            {
                var sr = stage.NewStageSprite("Mote" + i, MoteOrder);
                sr.sprite = face;
                sr.color = new Color(tint.r, tint.g, tint.b, 0f);
                sr.transform.SetParent(transform, false);
                sr.transform.localScale = Vector3.one * scale;
                // Behind the shoulders, not in a line: a mote leaves from its own point
                // along them, and the ones that leave later start nearer the middle so the
                // cloud narrows as it empties rather than draining from one side.
                float lane = Random.Range(-SpreadX, SpreadX) * (1f - 0.25f * i / Mathf.Max(1, count - 1));
                _motes[i] = new Mote
                {
                    Sr = sr,
                    Home = at + new Vector3(lane, Random.Range(-4f, 4f), 0f),
                    Delay = i * Stagger + (calm ? 0f : Random.Range(0f, Stagger)),
                    Life = calm ? LifeMin : Random.Range(LifeMin, LifeMax),
                    Rise = calm ? RiseMin : Random.Range(RiseMin, RiseMax),
                    Sway = calm ? 0f : Random.Range(SwayMin, SwayMax),
                    Freq = Random.Range(0.7f, 1.4f),
                    Phase = Random.Range(0f, Mathf.PI * 2f),
                    // A mote leans AWAY from the shoulder it left, so the cloud opens as it
                    // climbs instead of rising as one column (measured in play).
                    Lean = calm ? 0f : Mathf.Sign(lane) * Random.Range(LeanMax * 0.25f, LeanMax),
                    Scale = scale,
                };
                sr.transform.position = _motes[i].Home;
            }
            if (_motes.Length > 0 && _motes[0].Sr != null)
                transform.SetParent(_motes[0].Sr.transform.parent, false);
        }

        private void Update()
        {
            if (_motes == null) { Destroy(gameObject); return; }
            var shift = _follow && _body != null ? _body.transform.position - _followFrom : Vector3.zero;
            int order = _body != null ? _body.sortingOrder - 1 : MoteOrder;
            float dt = Time.deltaTime;
            bool anyLeft = false;
            foreach (var m in _motes)
            {
                if (m.Done || m.Sr == null) continue;
                m.T += dt;
                float t = m.T - m.Delay;
                if (t < 0f) { anyLeft = true; m.Sr.enabled = false; continue; }
                float k = t / m.Life;
                if (k >= 1f)
                {
                    m.Done = true;
                    if (m.Sr != null) Destroy(m.Sr.gameObject);
                    continue;
                }
                anyLeft = true;
                m.Sr.enabled = true;
                m.Sr.sortingOrder = order;
                // Out of the shoulder quickly, then easing off as it climbs — a mote is
                // light, and light things slow down going up.
                float climb = 1f - (1f - k) * (1f - k);
                float sway = Mathf.Sin(k * m.Freq * Mathf.PI * 2f + m.Phase) * m.Sway;
                m.Sr.transform.position = m.Home + shift
                    + new Vector3(sway + m.Lean * k, m.Rise * climb, 0f);
                // A short pop on the way out and a long fade on the way up: the eye is
                // meant to catch the leaving, and count what is left.
                float pop = k < 0.14f ? Mathf.Lerp(0.62f, 1f, k / 0.14f) : 1f;
                m.Sr.transform.localScale = Vector3.one * (m.Scale * pop);
                float a = k < 0.1f ? k / 0.1f : (k > 0.6f ? 1f - (k - 0.6f) / 0.4f : 1f);
                m.Sr.color = new Color(_tint.r, _tint.g, _tint.b, a);
            }
            if (!anyLeft) Destroy(gameObject);
        }
    }
}
