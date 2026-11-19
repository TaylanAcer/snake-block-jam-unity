using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    public sealed class TutorialView : MonoBehaviour
    {
        private static readonly int[] Levels = { 5, 11, 13, 18, 20 };
        private static readonly string[] Titles = { "Köprü", "Tünel", "İpucu", "Kürek", "Ok" };
        private static readonly string[] Descriptions =
        {
            "Yılanlar köprünün üstünden veya altından geçebilir.",
            "Yılanlar kaçmak için tünele girebilir.",
            "Kaçabilecek yılanı vurgular!",
            "Seçilen yılanı kaldırır!",
            "Yılanlar okun yönüne döner."
        };

        [SerializeField] private GameController controller;
        [SerializeField] private GameObject overlay;
        [SerializeField] private Text title;
        [SerializeField] private Text description;
        [SerializeField] private Image picture;
        [SerializeField] private Sprite[] illustrations;
        private readonly bool[] seen = new bool[Levels.Length];
        [SerializeField] private RectTransform guide;
        [SerializeField] private RectTransform hintButton;
        [SerializeField] private RectTransform shovelButton;
        private SnakeBlockJam.Core.BoardState previous;
        private RectTransform focus;

        public void Configure(GameController game, GameObject panel, Text heading, Text body, Image icon, Sprite[] art)
        {
            controller = game;
            overlay = panel;
            title = heading;
            description = body;
            picture = icon;
            illustrations = art;
        }

        private void OnEnable()
        {
            controller.Changed += Refresh;
            controller.BoosterUsed += HideGuide;
            controller.MovePlayed += OnMove;
            Refresh();
        }

        private void OnDisable()
        {
            controller.Changed -= Refresh;
            controller.BoosterUsed -= HideGuide;
            controller.MovePlayed -= OnMove;
        }

        private void OnMove(SnakeBlockJam.Core.MoveResult result) => HideGuide();

        private void HideGuide()
        {
            guide.gameObject.SetActive(false);
            focus = null;
        }

        private void Refresh()
        {
            if (focus != null && controller.IsShovelTargeting)
                HideGuide();
            if (controller.State == null || controller.State == previous)
                return;

            HideGuide();
            previous = controller.State;
            int index = System.Array.IndexOf(Levels, controller.Level.levelNumber);
            bool show = index >= 0 && !seen[index];
            overlay.SetActive(show);
            if (!show)
                return;

            seen[index] = true;
            title.text = Titles[index];
            description.text = Descriptions[index];
            picture.sprite = illustrations[index];
        }

        public void Dismiss()
        {
            if (!overlay.activeSelf)
                return;

            overlay.SetActive(false);
            if (controller.Level.levelNumber == GameController.HintUnlockLevel && controller.HintsLeft > 0)
                focus = hintButton;
            else if (controller.Level.levelNumber == GameController.ShovelUnlockLevel && controller.ShovelsLeft > 0)
                focus = shovelButton;

            if (focus != null)
            {
                guide.position = focus.position;
                guide.gameObject.SetActive(true);
            }
        }

        private void LateUpdate()
        {
            if (focus != null)
                guide.position = focus.position;
        }
    }
}
