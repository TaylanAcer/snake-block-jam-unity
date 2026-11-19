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
        public const float ButtonSpacing = 105f;

        public const string EmptyBadgeText = "+";
        private static readonly Color CountBadge = new Color32(0x2F, 0x6F, 0xE0, 0xFF);
        private static readonly Color EmptyBadge = new Color32(0x3D, 0xB5, 0x4A, 0xFF);

        [SerializeField] private GameController controller;
        [SerializeField] private RectTransform hintButton;
        [SerializeField] private Text hintCount;
        [SerializeField] private RectTransform shovelButton;
        [SerializeField] private Text shovelCount;
        [SerializeField] private GameObject shovelBanner;

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

            hintButton.GetComponent<Button>().interactable = controller.CanUseBoosters && controller.HintsLeft > 0;
            shovelButton.GetComponent<Button>().interactable = controller.CanUseBoosters && controller.ShovelsLeft > 0;
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
