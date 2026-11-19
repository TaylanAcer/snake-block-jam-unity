using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    // Oyun olaylarını efekt seslerine bağlar (spec §12 uygulama notu: minimum
    // liste dokunma, çarpma, kaçış, ALL CLEAR, seri yazısı, booster). Sesler
    // SfxSynth ile açılışta üretilir. Kural bilmez; sadece olayları dinler.
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private FeedbackView feedback;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

        private AudioSource _source;
        private AudioClip _tap, _bump, _escape, _allClear, _streak, _booster, _button, _arrowSlide;

        // Son çalınan sesin adı (testler için; batchmode'da ses aygıtı olmayabilir).
        public string LastPlayed { get; private set; }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            _tap = SfxSynth.ToClip("Tap", SfxSynth.Tap());
            _bump = SfxSynth.ToClip("Bump", SfxSynth.Bump());
            _arrowSlide = SfxSynth.ToClip("ArrowSlide", SfxSynth.ArrowSlide());
            _escape = SfxSynth.ToClip("Escape", SfxSynth.Escape());
            _allClear = SfxSynth.ToClip("AllClear", SfxSynth.AllClear());
            _streak = SfxSynth.ToClip("Streak", SfxSynth.Streak());
            _button = SfxSynth.ToClip("Button", SfxSynth.Button());
            _booster = SfxSynth.ToClip("Booster", SfxSynth.Booster());
        }

        private void OnEnable()
        {
            controller.MovePlayed += OnMovePlayed;
            controller.LevelCleared += OnLevelCleared;
            controller.BoosterUsed += OnBoosterUsed;
            controller.BoardView.ArrowPassed += OnArrowPassed;
            controller.BoardView.BlockedContact += OnBlockedContact;

            if (feedback != null)
                feedback.StreakShown += OnStreakShown;
        }

        private void OnDisable()
        {
            controller.MovePlayed -= OnMovePlayed;
            controller.LevelCleared -= OnLevelCleared;
            controller.BoosterUsed -= OnBoosterUsed;

            if (controller.BoardView != null)
            {
                controller.BoardView.BlockedContact -= OnBlockedContact;
                controller.BoardView.ArrowPassed -= OnArrowPassed;
            }

            if (feedback != null)
                feedback.StreakShown -= OnStreakShown;
        }

        // Her kabul edilen dokunuş yumuşak bir tık; kaçışsa üstüne kaçış sesi.
        // Çarpma sesi temas anında gelir (R-FX-11: ikinci, yüksek vuruş).
        private void OnMovePlayed(MoveResult result)
        {
            Play(_tap);

            if (result.IsEscaped)
                Play(_escape);
        }

        private void OnArrowPassed() => Play(_arrowSlide);
        private void OnBlockedContact(MoveResult result) => Play(_bump);
        private void OnLevelCleared() => Play(_allClear);
        private void OnStreakShown(int index) => Play(_streak);
        private void OnBoosterUsed() => Play(_booster);

        public void PlayButton() => Play(_button);

        private void Play(AudioClip clip)
        {
            LastPlayed = clip.name;
            _source.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            foreach (var clip in new[] { _tap, _bump, _escape, _allClear, _streak, _booster, _button, _arrowSlide })
            {
                if (clip != null)
                    Destroy(clip);
            }
        }
    }
}
