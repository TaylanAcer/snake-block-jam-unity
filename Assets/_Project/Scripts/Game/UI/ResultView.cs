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
                _shownTime = 0f;

            panel.SetActive(true);
            ApplyScale();
        }

        // Büyükten (1,25) yerine (1) ~0,1 sn'de; overlay ölçeklenmez, sadece kart.
        public static float ScaleIn(float time)
        {
            return Mathf.Lerp(ScaleInStart, 1f, Mathf.Clamp01(time / ScaleInTime));
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
