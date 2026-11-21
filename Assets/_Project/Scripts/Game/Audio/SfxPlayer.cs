using SnakeBlockJam.Core;
using UnityEngine;

namespace SnakeBlockJam.Game
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private GameController controller;
        [SerializeField] private FeedbackView feedback;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

        private AudioSource _source;
        private AudioClip _arrowSlide;
        private readonly System.Collections.Generic.Dictionary<string, AudioClip> _clips = new();

        // Son Ã§alÄ±nan sesin adÄ± (testler iÃ§in; batchmode'da ses aygÄ±tÄ± olmayabilir).
        public string LastPlayed { get; private set; }
        public float RevealDuration => _clips["Reveal"].length;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            foreach (var clip in Resources.LoadAll<AudioClip>("GameSounds"))
                _clips.Add(clip.name, clip);
            _arrowSlide = SfxSynth.ToClip("ArrowSlide", SfxSynth.ArrowSlide());
        }

        private void OnEnable()
        {
            controller.MovePlayed += OnMovePlayed;
            controller.BoardView.RevealStarted += OnReveal;
            controller.BoardView.TunnelEntered += OnTunnel;
            controller.BoardView.EscapeStarted += OnEscape;
            controller.BoardView.HawkShown += OnHawk;
            controller.ToolSelected += PlayButton;
            controller.LevelCleared += OnLevelCleared;

            controller.BoardView.ArrowPassed += OnArrowPassed;
            controller.BoardView.ShovelChipped += OnShovelChipped;
            controller.BoardView.BlockedContact += OnBlockedContact;

            if (feedback != null)
                feedback.StreakShown += OnStreakShown;
        }

        private void OnDisable()
        {
            controller.MovePlayed -= OnMovePlayed;
            controller.ToolSelected -= PlayButton;
            controller.LevelCleared -= OnLevelCleared;

            if (controller.BoardView != null)
            {
                controller.BoardView.RevealStarted -= OnReveal;
                controller.BoardView.TunnelEntered -= OnTunnel;
                controller.BoardView.EscapeStarted -= OnEscape;
                controller.BoardView.HawkShown -= OnHawk;
                controller.BoardView.BlockedContact -= OnBlockedContact;
                controller.BoardView.ArrowPassed -= OnArrowPassed;
                controller.BoardView.ShovelChipped -= OnShovelChipped;
            }

            if (feedback != null)
                feedback.StreakShown -= OnStreakShown;
        }

        private void OnMovePlayed(MoveResult result)
        {
            if (result.Outcome == MoveOutcome.Blocked)
                Play(_clips["Tap"]);
        }

        private void OnReveal() => Play(_clips["Reveal"]);
        private void OnTunnel() => Play(_clips["Tunnel"]);
        private void OnEscape(int segments) => Play(_clips[segments <= 3 ? "ShortEscape" : "Escape"]);
        private void OnHawk() => Play(_clips["Hawk"]);
        private void OnArrowPassed() => Play(_arrowSlide);
        private void OnShovelChipped() => Play(_clips["ShovelChip"]);
        // Çarpma: ses ve titreşim temas anında birlikte (R-COL-08).
        private void OnBlockedContact(MoveResult result)
        {
            Play(_clips["Bump"]);
            Haptics.Pulse(BumpVibrationMilliseconds, BumpVibrationAmplitude);
        }

        // ASSUMED: ERM motorlu ucuz cihazlarda (Galaxy A13) 40 ms altı zor hissedilir.
        public const long BumpVibrationMilliseconds = 60;
        public const int BumpVibrationAmplitude = 255;
        private void OnLevelCleared() => Play(_clips["AllClear"]);
        private void OnStreakShown(int index)
        {
            int style = Mathf.Min(index, FeedbackView.Streaks.Length - 1);
            Play(_clips[FeedbackView.Streaks[style].Text.TrimEnd('!')]);
        }

        public void PlayButton() => Play(_clips["Button"]);
        public void PlayTutorial() => Play(_clips["Tutorial"]);

        private void Play(AudioClip clip)
        {
            LastPlayed = clip.name;
            _source.PlayOneShot(clip, volume);
        }

        private void OnDestroy()
        {
            if (_arrowSlide != null)
                Destroy(_arrowSlide);
        }
    }
}
