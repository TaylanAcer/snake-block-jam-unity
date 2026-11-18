using UnityEngine;
using UnityEngine.UI;

namespace SnakeBlockJam.Game
{
    // Üst HUD ve ALL CLEAR yazısı. Kural bilmez; GameController.Changed gelince
    // durumu okur ve gösterir.
    //
    // R-MV-01: `Hamle: N` kutusu, altında `Seviye N`. R-MV-05: L1–L5'te sayaç
    // yok; referansta bu level'larda kutu hiç görünmüyor (Level_02.png).
    // R-HRD-01: Hard level'larda HUD kırmızı (kafatası ve giriş animasyonu yok).
    public sealed class HudView : MonoBehaviour
    {
        // R-TRN-02 (MEASURED, M): küçükten büyür, ~0,04–0,1 sn'de tam boya gelir,
        // hafif aşıp yerine oturur.
        private const float PopGrowTime = 0.07f;
        private const float PopSettleTime = 0.06f;
        private const float PopStartScale = 0.3f;
        private const float PopOvershootScale = 1.1f;

        [SerializeField] private GameController controller;
        [SerializeField] private GameObject movesPanel;
        [SerializeField] private Image movesBackground;
        [SerializeField] private Text movesText;
        [SerializeField] private Text levelText;
        [SerializeField] private RectTransform allClear;
        [SerializeField] private Color normalColor = new Color32(0x45, 0xB4, 0xCB, 0xFF);
        [SerializeField] private Color hardColor = new Color32(0xE5, 0x48, 0x4D, 0xFF);

        private float _allClearTime = -1f;

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
            LevelData level = controller.Level;
            bool hasCounter = controller.State != null && controller.State.RemainingMoves.HasValue;

            movesPanel.SetActive(hasCounter);

            if (hasCounter)
            {
                movesText.text = $"Hamle: {controller.State.RemainingMoves.Value}";
                levelText.text = $"Seviye {level.levelNumber}";
                movesBackground.color = level.isHard ? hardColor : normalColor;
            }

            bool showAllClear = controller.Phase == GamePhase.AllClear;

            if (showAllClear && !allClear.gameObject.activeSelf)
                _allClearTime = 0f;

            allClear.gameObject.SetActive(showAllClear);
        }

        private void Update()
        {
            if (_allClearTime < 0f || !allClear.gameObject.activeSelf)
                return;

            _allClearTime += Time.deltaTime;
            allClear.localScale = Vector3.one * PopScale(_allClearTime);

            if (_allClearTime >= PopGrowTime + PopSettleTime)
                _allClearTime = -1f;
        }

        public static float PopScale(float time)
        {
            if (time < PopGrowTime)
                return Mathf.Lerp(PopStartScale, PopOvershootScale, time / PopGrowTime);

            return Mathf.Lerp(PopOvershootScale, 1f, Mathf.Clamp01((time - PopGrowTime) / PopSettleTime));
        }
    }
}
