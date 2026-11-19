using SnakeBlockJam.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SnakeBlockJam.EditorTools
{
    // Level Editor'deki "▶ Oyna": seçili level'ı Game.unity'de açar. Oyun koduna
    // dokunmaz: Play Mode başlayınca GameController.LoadLevel(level) çağrılır
    // (Restart ve "Devam" da aynı public metodu kullanıyor).
    [InitializeOnLoad]
    public static class LevelEditorPlay
    {
        private const string LevelPathKey = "SnakeBlockJam.LevelEditor.PlayLevel";

        static LevelEditorPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static void Play(LevelData level)
        {
            if (level == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            AssetDatabase.SaveAssetIfDirty(level);

            if (EditorSceneManager.GetActiveScene().path != GameSceneBuilder.ScenePath)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;

                EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath);
            }

            // Domain reload'dan sağ çıkar (statik alan çıkmaz).
            SessionState.SetString(LevelPathKey, AssetDatabase.GetAssetPath(level));
            EditorApplication.EnterPlaymode();
        }

        // Start'tan önce de sonra da çalışsa sonuç aynı: LoadLevel(level) alanı
        // değiştirir, Start'ın LoadLevel()'ı da o alanı kullanır.
        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            string path = SessionState.GetString(LevelPathKey, "");

            if (path.Length == 0)
                return;

            SessionState.EraseString(LevelPathKey);

            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            var controller = Object.FindAnyObjectByType<GameController>();

            if (level != null && controller != null)
                controller.LoadLevel(level);
        }
    }
}
