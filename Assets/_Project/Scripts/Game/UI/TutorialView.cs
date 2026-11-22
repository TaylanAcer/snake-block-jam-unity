using SnakeBlockJam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    public sealed class TutorialView : MonoBehaviour
    {
        // Sıra TutorialKind ile aynı (None hariç): Bridge, Burrow, Hint, Shovel, Arrow.
        // Hangi bölümde hangi kartın çıkacağı LevelData.tutorial'dadır.
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
        private readonly bool[] seen = new bool[Titles.Length];
        [SerializeField] private RectTransform guide;
        [SerializeField] private RectTransform hintButton;
        [SerializeField] private RectTransform shovelButton;
        [SerializeField] private RectTransform card;
        [SerializeField] private RectTransform freeHand;
        [SerializeField] private Image freeRing;
        [SerializeField] private GameObject tapBanner;
        [SerializeField] private RectTransform tapMarker;
        [SerializeField] private RectTransform tapHand;
        [SerializeField] private Image tapRing;
        private BoardState previous;
        private RectTransform focus;
        private CanvasGroup cardVisibility;
        private float showAt;
        private float dismissAt;
        private float guideAt;
        private float tapAt;
        private float tapRevealAt;
        private int tapTarget;
        private bool tapHasEscape;
        private SfxPlayer sounds;
        private enum CardPhase { Hidden, Waiting, Visible, Closing }
        private CardPhase cardPhase;

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
            sounds = controller.GetComponent<SfxPlayer>();
            cardVisibility = overlay.GetComponent<CanvasGroup>();
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

        private void OnMove(MoveResult result)
        {
            HideGuide();
            if (controller.Level.tutorial == TutorialKind.Tap && result.IsEscaped)
            {
                tapHasEscape = true;
                HideTap();
                SetTapTarget();
            }
        }

        private void HideGuide()
        {
            guide.gameObject.SetActive(false);
            focus = null;
        }

        private void Refresh()
        {
            if (focus != null && controller.IsShovelTargeting)
                HideGuide();

            if (controller.State != null && controller.State.Status != GameStatus.Playing)
                HideTap();

            if (controller.State == null || controller.State == previous)
                return;

            HideGuide();
            HideTap();
            previous = controller.State;
            tapHasEscape = false;
            tapRevealAt = Time.time + (sounds != null ? sounds.RevealDuration : 0f);
            SetTapTarget();

            int index = (int)controller.Level.tutorial - 1;
            bool show = index >= 0 && index < Titles.Length && !seen[index];
            cardPhase = show ? CardPhase.Waiting : CardPhase.Hidden;
            overlay.SetActive(show);
            if (!show)
                return;

            seen[index] = true;
            cardVisibility.alpha = 0f;
            cardVisibility.interactable = false;
            card.localScale = Vector3.one * 0.72f;
            showAt = tapRevealAt;
            title.text = Titles[index];
            description.text = Descriptions[index];
            picture.sprite = illustrations[index];
        }

        public void Dismiss()
        {
            if (cardPhase != CardPhase.Visible)
                return;

            cardPhase = CardPhase.Closing;
            dismissAt = Time.time;
            cardVisibility.interactable = false;
        }

        private void FinishDismiss()
        {
            cardPhase = CardPhase.Hidden;
            overlay.SetActive(false);

            // R-HNT-01 / R-SHV-01: tanıtım "Free" işaretçisiyle bir ücretsiz kullanım verir.
            if (controller.Level.tutorial == TutorialKind.Hint)
            {
                controller.GrantFreeHint();
                focus = hintButton;
            }
            else if (controller.Level.tutorial == TutorialKind.Shovel)
            {
                controller.GrantFreeShovel();
                focus = shovelButton;
            }

            if (focus != null)
            {
                guide.position = focus.position;
                guideAt = Time.time;
                guide.gameObject.SetActive(true);
            }
        }

        private void SetTapTarget()
        {
            if (controller.Level.tutorial != TutorialKind.Tap || !controller.CanUseBoosters)
                return;
            tapTarget = Solver.PickHint(controller.State);
            tapAt = Time.time + (tapHasEscape ? controller.Feel.tutorialRetargetDelay : 0f);
        }

        private void HideTap()
        {
            tapTarget = 0;
            tapBanner.SetActive(false);
            tapMarker.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            FeelSettings feel = controller.Feel;
            if (cardPhase == CardPhase.Waiting && Time.time >= showAt)
            {
                cardPhase = CardPhase.Visible;
                showAt = Time.time;
                sounds?.PlayTutorial();
            }

            if (cardPhase == CardPhase.Visible)
            {
                float t = Mathf.Clamp01((Time.time - showAt) / feel.tutorialCardInDuration);
                cardVisibility.alpha = Mathf.SmoothStep(0f, 1f, t * 2f);
                cardVisibility.interactable = t >= 1f;
                card.localScale = Vector3.one * Mathf.LerpUnclamped(0.72f, 1f, GuideMotion.EaseOutBack(t));
                card.anchoredPosition = Vector2.down * (56f * (1f - GuideMotion.EaseOutBack(t)));
            }
            else if (cardPhase == CardPhase.Closing)
            {
                float t = Mathf.Clamp01((Time.time - dismissAt) / feel.tutorialCardOutDuration);
                float shrink = Mathf.SmoothStep(0f, 1f, (t - 0.32f) / 0.68f);
                float lift = Mathf.SmoothStep(0f, 1f, t / 0.32f);
                cardVisibility.alpha = 1f - shrink;
                card.localScale = Vector3.one * (1f + 0.035f * lift - 0.20f * shrink);
                if (t >= 1f)
                    FinishDismiss();
            }

            if (focus != null)
            {
                guide.position = focus.position;
                GuideMotion.AnimatePress(freeHand, new Vector2(34f, 36f), freeRing,
                    Time.time - guideAt, feel.freeGuidePeriod);
            }

            float tapStartsAt = Mathf.Max(tapAt, tapRevealAt);
            if (tapTarget != 0 && Time.time >= tapStartsAt &&
                controller.BoardView.TryGetSnakeHead(tapTarget, out Vector3 head))
            {
                tapBanner.SetActive(!tapHasEscape);
                tapMarker.gameObject.SetActive(true);
                tapMarker.anchoredPosition = GuideMotion.CanvasPoint((RectTransform)tapMarker.parent,
                    controller.GameCamera, head);
                GuideMotion.AnimatePress(tapHand, new Vector2(19f, 17f), tapRing,
                    Time.time - tapStartsAt, feel.tutorialTapPeriod);
            }
        }
    }
}
