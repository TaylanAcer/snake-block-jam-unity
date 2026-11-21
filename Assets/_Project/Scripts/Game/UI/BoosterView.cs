using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    // Alt bölümdeki İpucu ve Kürek butonları ve Kürek hedef seçme bandı.
    // Kural bilmez: GameController.Changed gelince durumu okur (HudView gibi).
    //
    // R-HNT-03 / R-SHV-04: butonlar altta, sayı rozetli. L13–L17'de sadece İpucu
    // ortada (Level_13.png), L18+ ikisi yan yana (Level_20.png).
    // R-SHV-02: Kürek modunda butonların yerinde başlık, açıklama ve kırmızı X'li
    // bant (image(20261116-205144)). Stok yokken referansta yeşil "+" rozeti var
    // (L17, L19: mağazaya gider); mağaza kapsam dışı, buton pasif.
    public sealed class BoosterView : MonoBehaviour
    {
        // İki buton yan yanayken merkezden uzaklık (1080 genişlikli referans).
        public const float ButtonSpacing = 94f;

        public const string EmptyBadgeText = "+";
        private static readonly Color CountBadge = new Color32(0x2F, 0x6F, 0xE0, 0xFF);
        private static readonly Color EmptyBadge = new Color32(0x3D, 0xB5, 0x4A, 0xFF);

        [SerializeField] private GameController controller;
        [SerializeField] private RectTransform hintButton;
        [SerializeField] private Text hintCount;
        [SerializeField] private RectTransform shovelButton;
        [SerializeField] private Text shovelCount;
        [SerializeField] private GameObject shovelBanner;
        [SerializeField] private Image hintFlash;
        private SnakeBlockJam.Core.BoardState state;
        private float hintAt;
        private int hintTarget;

        private void OnEnable()
        {
            controller.Changed += Refresh;
            controller.HintUsed += AnimateHint;
            Refresh();
        }

        private void OnDisable()
        {
            controller.Changed -= Refresh;
            controller.HintUsed -= AnimateHint;
        }

        public void Refresh()
        {
            if (state != controller.State)
            {
                state = controller.State;
                StopHintAnimation();
            }
            else if (hintTarget != 0 && hintTarget != controller.HintedSnakeId)
                StopHintAnimation();

            bool targeting = controller.IsShovelTargeting;
            bool showHint = controller.IsHintUnlocked && !targeting;
            bool showShovel = controller.IsShovelUnlocked && !targeting;

            hintButton.gameObject.SetActive(showHint);
            shovelButton.gameObject.SetActive(showShovel);
            shovelBanner.SetActive(targeting);

            float offset = showHint && showShovel ? ButtonSpacing : 0f;
            SetX(hintButton, -offset);
            SetX(shovelButton, offset);

            SetBadge(hintCount, controller.HintsLeft);
            SetBadge(shovelCount, controller.ShovelsLeft);

            hintButton.GetComponent<Button>().interactable = controller.CanUseBoosters && controller.HasHint;
            shovelButton.GetComponent<Button>().interactable = controller.CanUseBoosters && controller.HasShovel;
        }

        private void AnimateHint(int snakeId)
        {
            hintAt = Time.time;
            hintTarget = snakeId;
        }

        private void StopHintAnimation()
        {
            hintTarget = 0;
            hintButton.localScale = Vector3.one;
            hintFlash.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (hintTarget == 0)
                return;
            float t = (Time.time - hintAt) / controller.Feel.hintActivationDuration;
            if (t >= 1f || !controller.CanUseBoosters || controller.IsShovelTargeting)
            {
                StopHintAnimation();
                return;
            }

            float pop = GuideMotion.EaseOutBack(t);
            hintButton.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, pop);
            if (!controller.BoardView.TryGetSnakeHead(hintTarget, out Vector3 head))
                return;
            hintFlash.gameObject.SetActive(true);
            RectTransform ring = hintFlash.rectTransform;
            ring.anchoredPosition = GuideMotion.CanvasPoint((RectTransform)ring.parent, controller.GameCamera, head);
            ring.localScale = Vector3.one * Mathf.Lerp(0.63f, 1.54f, Mathf.SmoothStep(0f, 1f, t));
            hintFlash.color = new Color(1f, 1f, 0.68f, 1f - t);
        }

        public void OnHint()
        {
            controller.UseHint();
        }

        public void OnShovel()
        {
            controller.BeginShovel();
        }

        public void OnCancelShovel()
        {
            controller.CancelShovel();
        }

        // Rozet: sayı (mavi) veya stok yoksa "+" (yeşil). Rozet görseli sayının ebeveyni.
        private static void SetBadge(Text count, int value)
        {
            count.text = value > 0 ? value.ToString() : EmptyBadgeText;

            var badge = count.transform.parent.GetComponent<Image>();

            if (badge != null)
                badge.color = value > 0 ? CountBadge : EmptyBadge;
        }

        private static void SetX(RectTransform rect, float x)
        {
            Vector2 position = rect.anchoredPosition;
            position.x = x;
            rect.anchoredPosition = position;
        }
    }
}
