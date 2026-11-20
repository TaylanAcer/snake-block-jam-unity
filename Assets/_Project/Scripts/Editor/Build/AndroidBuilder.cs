using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SnakeBlockJam.EditorTools
{
    // Android APK üretir. Player ayarlarını build'den önce koddan uygular:
    // Unity açıkken ProjectSettings.asset dışarıdan yazılırsa editör kendi
    // bellekteki değeriyle ezebilir, bu yüzden ayarlar Unity'nin içinden verilir.
    public static class AndroidBuilder
    {
        public const string PackageName = "com.oreoncase.snakeblockjam";
        public const string ProductName = "Snake Block Jam";
        public const string CompanyName = "Oreon Case";
        public const string OutputDirectory = "Builds/Android";

        [MenuItem("Snake Block Jam/Build/Android APK (Development)")]
        public static void BuildDevelopmentMenu() => BuildFromMenu(development: true);

        [MenuItem("Snake Block Jam/Build/Android APK (Release)")]
        public static void BuildReleaseMenu() => BuildFromMenu(development: false);

        [MenuItem("Snake Block Jam/Setup/Apply Android Settings")]
        public static void ApplyAndroidSettings()
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);

            // IL2CPP, ARMv7 + ARM64. Sadece ARM64 yetmiyor: test cihazı Galaxy A13
            // (SM-A135F) 64-bit işlemciye rağmen 32-bit Android çalıştırıyor
            // (ro.product.cpu.abilist = armeabi-v7a) ve ARM64 APK'yı kurmuyor.
            // Tek APK iki kütüphaneyi de taşır; cihaz kendi ABI'sini seçer.
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            // Oyun sadece portre.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Çentik/kamera deliği altına çizme: HUD ve Restart butonu
            // kesilmesin. Ayrı bir SafeArea bileşeninden daha basit.
            PlayerSettings.Android.renderOutsideSafeArea = false;

            // Sabit 30 FPS (GameController.MobileFrameRate) için kare temposu: Swappy
            // kapalıyken Galaxy A13'te kareler 16/33/50 ms karışık geliyordu (titreme).
            PlayerSettings.Android.optimizedFramePacing = true;

            // Teslim ve emulator için APK (Play Store AAB'si gerekmiyor).
            EditorUserBuildSettings.buildAppBundle = false;

            AssetDatabase.SaveAssets();
        }

        // Komut satırı (-quit verme; metot çıkış kodunu kendisi döndürür):
        // Unity.exe -batchmode -projectPath <proje> -buildTarget Android
        //   -executeMethod SnakeBlockJam.EditorTools.AndroidBuilder.BuildFromCommandLine
        //   [-development]
        public static void BuildFromCommandLine()
        {
            bool development = Array.IndexOf(Environment.GetCommandLineArgs(), "-development") >= 0;

            try
            {
                BuildReport report = Build(development);
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static BuildReport Build(bool development)
        {
            ApplyAndroidSettings();

            string[] scenes = Array.ConvertAll(
                Array.FindAll(EditorBuildSettings.scenes, s => s.enabled), s => s.path);

            if (scenes.Length == 0)
                throw new InvalidOperationException(
                    "Build listesinde sahne yok. Önce Snake Block Jam → Setup → Build Game Scene çalıştır.");

            Directory.CreateDirectory(OutputDirectory);
            string fileName = development ? "SnakeBlockJam-dev.apk" : "SnakeBlockJam.apk";

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputDirectory, fileName),
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = development ? BuildOptions.Development : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            // summary.totalSize APK boyutu değil (ara dosyaları da sayıyor); dosyaya bak.
            float apkMegabytes = File.Exists(summary.outputPath)
                ? new FileInfo(summary.outputPath).Length / (1024f * 1024f)
                : 0f;

            Debug.Log($"[AndroidBuilder] {summary.result}: {summary.outputPath} " +
                      $"({apkMegabytes:F1} MB, {summary.totalTime.TotalSeconds:F0} sn, " +
                      $"{summary.totalErrors} hata)");
            return report;
        }

        private static void BuildFromMenu(bool development)
        {
            BuildReport report = Build(development);

            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(report.summary.outputPath);
        }
    }
}
