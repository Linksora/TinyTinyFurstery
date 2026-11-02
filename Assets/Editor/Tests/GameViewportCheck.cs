using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class GameViewportCheck
{
    [MenuItem("TapJam/Tests/Check 16:9 Viewport")]
    public static void Run()
    {
        if (Application.isPlaying) throw new Exception("Run outside Play mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        GameObject host = null;
        try
        {
            foreach (string name in new[] { "TitleScene", "Level1", "Level2", "Level3", "Credits" })
            {
                EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                var originalPositions = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(rect => rect.parent != null && rect.GetComponent<Canvas>() == null)
                    .ToDictionary(rect => rect, rect => rect.anchoredPosition3D);
                host = new GameObject("Viewport Test");
                GameViewport controller = host.AddComponent<GameViewport>();
                foreach (Vector2 size in new[] { new Vector2(1280, 720), new(2559, 1340), new(1024, 768), new(2560, 1080), new(800, 1200), new(1280, 720) })
                {
                    Rect fit = GameViewport.Fit((int)size.x, (int)size.y);
                    Require(Mathf.Abs(fit.width * size.x / (fit.height * size.y) - 16f / 9f) < 0.0001f, "Aspect ratio");
                    Require(Mathf.Abs(fit.center.x - 0.5f) < 0.0001f && Mathf.Abs(fit.center.y - 0.5f) < 0.0001f, "Centering");
                    Require(fit.xMin >= 0 && fit.yMin >= 0 && fit.xMax <= 1 && fit.yMax <= 1, "Bounds");
                    controller.Apply((int)size.x, (int)size.y);
                    foreach (var original in originalPositions)
                        Require(Vector3.Distance(original.Key.anchoredPosition3D, original.Value) < 0.01f,
                            $"{name}: preserve authored position of {original.Key.name}");
                    foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                        Require(camera.rect == fit, $"{name}: camera viewport");
                    foreach (Canvas canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (canvas.transform.IsChildOf(host.transform) || canvas.GetComponentInChildren<MouseCursorController>(true) != null ||
                            canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null) continue;
                        RectTransform content = canvas.transform.Find("16:9 Content") as RectTransform;
                        Require(content != null && Mathf.Approximately(content.sizeDelta.x / content.sizeDelta.y, 16f / 9f), $"{name}: UI content dimensions");
                        Vector2 reference = canvas.GetComponent<CanvasScaler>().referenceResolution;
                        Require(content.sizeDelta == reference, $"{name}: preserve authored UI resolution");
                        Require(Mathf.Approximately(canvas.scaleFactor, Mathf.Min(size.x / reference.x, size.y / reference.y)), $"{name}: uniform UI scale");
                        Require(canvas.transform.childCount == 1, $"{name}: wrapper is not duplicated on resize");
                    }
                }
                Debug.Log($"16:9 viewport PASS: {name}, six resize steps");
                UnityEngine.Object.DestroyImmediate(host);
                host = null;
            }
        }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (setup.Length > 0 && setup.Any(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Debug.Log("16:9 viewport PASS: all scenes");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
