using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SettingsOverlayCheck
{
    [MenuItem("TapJam/Tests/Check Settings Overlay")]
    public static void Run()
    {
        string[] scenes = { "TitleScene", "Level1", "Level2", "Level3" };
        Vector2[] sizes =
        {
            new(1280, 720), new(1920, 1080), new(2559, 1340),
            new(1024, 768), new(2560, 1080), new(800, 1200), new(1280, 720)
        };
        foreach (string name in scenes)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/Scenes/{name}.unity");
            try
            {
                SettingsPanel settings = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SettingsPanel>(true)).Single();
                Canvas rootCanvas = settings.transform.parent.GetComponentInParent<Canvas>();
                // World-space sizing exercises the actual RectTransform hierarchy without changing the Game view.
                rootCanvas.renderMode = RenderMode.WorldSpace;
                rootCanvas.GetComponent<CanvasScaler>().enabled = false;
                RectTransform viewport = (RectTransform)rootCanvas.transform;
                viewport.localScale = Vector3.one;
                Transform panel = settings.transform.Find("Panel");
                panel.gameObject.SetActive(true);
                RectTransform dim = (RectTransform)panel.Find("Dim");
                Image image = dim.GetComponent<Image>();
                if (!image.raycastTarget || image.color.a <= 0 || image.color.a >= 1 ||
                    panel.Find("PanelImage").GetSiblingIndex() <= dim.GetSiblingIndex())
                    throw new Exception($"{name}: invalid dimming/input/panel ordering");

                foreach (Vector2 size in sizes)
                {
                    viewport.sizeDelta = size;
                    Canvas.ForceUpdateCanvases();
                    var expected = new Vector3[4];
                    var actual = new Vector3[4];
                    viewport.GetWorldCorners(expected);
                    dim.GetWorldCorners(actual);
                    for (int i = 0; i < 4; i++)
                        if (Vector3.Distance(expected[i], actual[i]) > 0.01f)
                            throw new Exception($"{name} at {size}: overlay corner {i} is {actual[i]}, expected {expected[i]}");
                }
                Debug.Log($"Settings overlay PASS: {name}, {sizes.Length} resize steps");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        Debug.Log("Settings overlay PASS: all scenes");
    }
}
