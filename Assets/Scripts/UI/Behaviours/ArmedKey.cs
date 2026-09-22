using UnityEngine;

namespace LastCall.UI
{
    /// <summary>
    /// A KEY THAT ASKS FIRST (2026-09-22, the author's eighth list: "Çöp butonuna tıklayınca butonun üstünde emin
    /// misin? sorusu sorsun tekrar basıldığında çöpe atılsın"). The first press only ARMS it and hangs the question
    /// over it; a second press while the question stands does the thing. Left alone, or the moment the bench closes,
    /// the question goes and the key is back to asking - so a drink is never thrown away by one stray click, and never
    /// by a click that was answering something the player has stopped looking at.
    /// </summary>
    public sealed class ArmedKey : MonoBehaviour
    {
        /// <summary>The question hung over the key; shown while armed.</summary>
        public RectTransform Ask;
        /// <summary>How long the question stands, in real seconds.</summary>
        public float Window = 2.6f;
        /// <summary>What the second press does.</summary>
        public System.Action Confirmed;

        private float _until = -1f;

        public bool Armed => _until > 0f && Time.unscaledTime < _until;

        /// <summary>The key's click: arms it, or - if it already is - disarms it and does the thing.</summary>
        public void Press()
        {
            if (Armed)
            {
                Disarm();
                Confirmed?.Invoke();
                return;
            }
            _until = Time.unscaledTime + Window;
            if (Ask != null)
            {
                Ask.gameObject.SetActive(true);
                Ask.SetAsLastSibling();              // over everything the bench laid down after the key
                var pop = Ask.GetComponent<PopIn>();
                if (pop != null) pop.Play();         // it opens like the drinkers' balloons, every time it asks
            }
        }

        public void Disarm()
        {
            _until = -1f;
            if (Ask != null && Ask.gameObject.activeSelf) Ask.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_until > 0f && Time.unscaledTime >= _until) Disarm();
        }

        private void OnDisable() => Disarm();
    }
}
