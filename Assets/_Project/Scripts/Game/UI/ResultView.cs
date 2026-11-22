using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    // Level sonu pencereleri: TAMAMLANDI (Devam) ve HAMLE YOK! (Tekrar Dene).
    // Referanstaki coin ödülü, +5 hamle ve kalp kapsam dışı (spec §13).
    // R-TRN-04: pencereye sert kesme (crossfade yok); kart ~0,1 sn içinde
    // büyükten yerine oturur (scale-in, M). Koyu overlay arkadaki tahtaya
    // dokunuşu da keser (InputHandler.IsOverUI).
    public sealed class ResultView : MonoBehaviour
    {
        public const float ScaleInTime = 0.1f;
        public const float ScaleInStart = 1.25f;

        [SerializeField] private GameController controller;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text buttonText;

        [SerializeField] private GameObject paper;
        [SerializeField] private Sprite completedRibbon;
        [SerializeField] private GameObject trophy;
        [SerializeField] private GameObject retrySymbol;
        private Sprite blueHeader;

        private float _shownTime = -1f;

        private void OnEnable()
        {
            controller.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            controller.Changed -= Refresh;
        }

        public void Refresh()
        {
            switch (controller.Phase)
            {
                case GamePhase.Completed:
                    Show("TAMAMLANDI", $"Seviye {controller.Level.levelNumber}", "Devam");
                    break;

                case GamePhase.OutOfMoves:
                    Show("HAMLE YOK!", "Hamlelerin bitti.", "Tekrar Dene");
                    break;

                default:
                    panel.SetActive(false);
                    break;
            }
        }

        // Pencerenin tek butonu.
        public void OnButton()
        {
            if (controller.Phase == GamePhase.Completed)
                controller.Continue();
            else if (controller.Phase == GamePhase.OutOfMoves)
                controller.Restart();
        }

        private void Show(string title, string message, string button)
        {
            titleText.text = title;
            messageText.text = message;
            buttonText.text = button;

            if (!panel.activeSelf)
            {
                _shownTime = 0f;
                if (paper != null)
                    StyleCard(controller.Phase == GamePhase.Completed);
            }

            panel.SetActive(true);
            ApplyScale();
        }

        // Büyükten (1,25) yerine (1) ~0,1 sn'de; overlay ölçeklenmez, sadece kart.
        public static float ScaleIn(float time)
        {
            return Mathf.Lerp(ScaleInStart, 1f, Mathf.Clamp01(time / ScaleInTime));
        }

        private void StyleCard(bool completed)
        {
            var header = (RectTransform)titleText.transform.parent;
            var skin = header.GetComponent<Image>();
            if (blueHeader == null)
                blueHeader = skin.sprite;
            skin.sprite = completed ? completedRibbon : blueHeader;
            skin.type = !completed && skin.hasBorder ? Image.Type.Sliced : Image.Type.Simple;
            header.sizeDelta = completed ? new Vector2(840f, 210f) : new Vector2(560f, 110f);
            ((RectTransform)Card).sizeDelta = new Vector2(900f, completed ? 1120f : 1010f);
            ((RectTransform)buttonText.transform.parent).anchoredPosition = new Vector2(0f, completed ? 68f : 120f);
            Card.GetComponent<Image>().enabled = !completed;
            paper.SetActive(!completed);
            if (retrySymbol != null)
                retrySymbol.SetActive(!completed);
            if (trophy != null)
            {
                trophy.SetActive(completed);
                ((RectTransform)messageText.transform.parent).anchoredPosition =
                    new Vector2(0f, completed ? -245f : -80f);
            }
            messageText.color = completed ? Color.white : new Color32(51, 58, 72, 255);
        }

        private Transform Card => panel.transform.childCount > 0 ? panel.transform.GetChild(0) : null;

        private void Update()
        {
            if (_shownTime < 0f || !panel.activeSelf)
                return;

            _shownTime += Time.unscaledDeltaTime;
            ApplyScale();

            if (_shownTime >= ScaleInTime)
                _shownTime = -1f;
        }

        private void ApplyScale()
        {
            Transform card = Card;

            // _shownTime < 0: animasyon bitti veya yok → tam boy.
            if (card != null)
                card.localScale = Vector3.one * ScaleIn(_shownTime < 0f ? ScaleInTime : _shownTime);
        }
    }
}
