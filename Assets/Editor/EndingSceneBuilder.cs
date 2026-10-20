using Spotlight.Exploration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public static class EndingSceneBuilder
{
    [MenuItem("Spotlight/Framework/Build Ending Scene")]
    public static void Build()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Camera camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.tag = "MainCamera";
        EndingScenePlayer player = new GameObject("Ending Scene Player").AddComponent<EndingScenePlayer>();
        player.Configure(
            AssetDatabase.LoadAssetAtPath<StoryDefinition>("Assets/Modules/Story/Definitions/ending_01.asset"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/sprite/CG/Ending/2001.png"),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/sprite/CG/Ending/20011.png"));
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/ending.unity");
        var paths = new[]
        {
            "Assets/Scenes/Bootstrap.unity", "Assets/Scenes/explore/zero.unity",
            "Assets/Scenes/explore/first.unity", "Assets/Scenes/fight/fight_1.unity",
            "Assets/Scenes/explore/second.unity", "Assets/Scenes/fight/fight_2.unity",
            "Assets/Scenes/explore/third.unity", "Assets/Scenes/fight/fight_3.unity",
            "Assets/Scenes/ending.unity", "Assets/Scenes/Gameplay.unity",
            "Assets/Scenes/fight/fight_test.unity", "Assets/Scenes/SampleScene.unity"
        };
        EditorBuildSettings.scenes = System.Array.ConvertAll(paths,
            path => new EditorBuildSettingsScene(path, true));
        AssetDatabase.SaveAssets();
    }

    public static void BuildWindows()
    {
        Build();
        string output = Path.GetFullPath("Builds/Spotlight/Spotlight.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = System.Array.ConvertAll(EditorBuildSettings.scenes, scene => scene.path),
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        });
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new System.Exception("Windows build failed: " + report.summary.result);
    }
}
