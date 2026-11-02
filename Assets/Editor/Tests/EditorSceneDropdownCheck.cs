using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class EditorSceneDropdownCheck
{
    [MenuItem("TapJam/Tests/Check Scene Dropdown (Play Mode)")]
    public static void Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
        var overlays = UnityEngine.Object.FindObjectsByType<EditorSceneDropdown>(FindObjectsSortMode.None);
        Require(overlays.Length == 1, "Expected exactly one scene dropdown overlay.");
        Dropdown dropdown = overlays[0].GetComponentInChildren<Dropdown>();
        int sceneCount = EditorBuildSettings.scenes.Select(scene => scene.path)
            .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null).Distinct().Count();
        Require(dropdown != null && dropdown.options.Count == sceneCount + 1,
            "The dropdown must include all valid build-list scenes, including disabled entries.");
        Require(EventSystem.current != null, "No EventSystem available for clicks.");

        Canvas.ForceUpdateCanvases();
        RectTransform rect = (RectTransform)dropdown.transform;
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
        };
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Require(hits.Count > 0 && hits[0].gameObject.transform.IsChildOf(overlays[0].transform),
            "The dropdown is obscured or cannot receive pointer clicks.");
        try
        {
            dropdown.Show();
            Transform list = dropdown.transform.Find("Dropdown List");
            Require(list != null, "The dropdown did not expand.");
            Canvas.ForceUpdateCanvases();
            Text[] labels = list.GetComponentsInChildren<Text>()
                .Where(text => text.gameObject.name == dropdown.itemText.gameObject.name).ToArray();
            Require(labels.Length == dropdown.options.Count, "Missing scene labels.");
            foreach (Text label in labels)
            {
                Require(!string.IsNullOrEmpty(label.text), "Empty scene label.");
                var settings = label.GetGenerationSettings(label.rectTransform.rect.size);
                var generator = new TextGenerator();
                generator.Populate(label.text, settings);
                Require(generator.verts.Count > 4,
                    $"Scene label '{label.text}' has no rendered glyphs; check font and row height.");
            }
            Require(overlays[0].transform.Find("Blocker") != null,
                "The expanded menu must block clicks to the game underneath.");
        }
        finally
        {
            dropdown.Hide();
        }
        Debug.Log("Scene dropdown PASS: installation, scene list, rendered labels, pointer raycast, expansion, input blocker.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
