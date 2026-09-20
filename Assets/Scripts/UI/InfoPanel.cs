using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PetShop.UI
{
    /// <summary>
    /// The popup that shows what is on a shelf, who lives in a pen, or the day's books.
    /// Closes on its button or on Escape.
    /// </summary>
    public class InfoPanel : MonoBehaviour
    {
        private GameObject _root;
        private TMP_Text   _body;

        public bool IsOpen => _root != null && _root.activeSelf;

        public void Build(Transform canvas)
        {
            _root = UIFactory.Panel("InfoPanel", canvas,
                                    new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                                    UIFactory.PanelBg,
                                    new Vector2(16f, -170f), new Vector2(370f, 170f));

            UIFactory.Panel("Accent", _root.transform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                            UIFactory.Accent, new Vector2(0f, -3f), Vector2.zero);

            _body = UIFactory.Label("Body", _root.transform, "",
                new Vector2(0.06f, 0.16f), new Vector2(0.95f, 0.94f), 16f, UIFactory.Ink,
                TextAlignmentOptions.TopLeft);

            var close = UIFactory.Button("Close", _root.transform, "Close  (Esc)",
                new Vector2(0.30f, 0.035f), new Vector2(0.70f, 0.14f), 15f);
            close.onClick.AddListener(Hide);

            _root.SetActive(false);
        }

        public void Show(string text)
        {
            if (_root == null || string.IsNullOrEmpty(text)) return;
            _body.text = text;
            _root.SetActive(true);
        }

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }
    }
}
