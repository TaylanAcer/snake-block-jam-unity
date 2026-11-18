using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    // Level sonu pencereleri: TAMAMLANDI (Devam) ve HAMLE YOK! (Tekrar Dene).
    // Referanstaki coin ödülü, +5 hamle ve kalp kapsam dışı (spec §13).
    // R-TRN-04: pencereye sert kesme; geçiş animasyonu yok. Koyu overlay
    // arkadaki tahtaya dokunuşu da keser (InputHandler.IsOverUI).
    public sealed class ResultView : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text buttonText;

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
            panel.SetActive(true);
        }
    }
}
