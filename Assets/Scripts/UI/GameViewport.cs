using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Keeps world rendering and overlay UI inside the same centered 16:9 viewport.</summary>
[DefaultExecutionOrder(10000)]
public sealed class GameViewport : MonoBehaviour
{
    public static Rect Fit(int width, int height)
    {
        float aspect = width / (float)Mathf.Max(1, height);
        const float target = 16f / 9f;
        return aspect > target
            ? new Rect((1f - target / aspect) / 2f, 0, target / aspect, 1)
            : new Rect(0, (1f - aspect / target) / 2f, 1, aspect / target);
    }

    private Vector2Int lastSize;
    private bool dirty = true;
    private RectTransform[] bars;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<GameViewport>() != null) return;
        var root = new GameObject("16:9 Game Viewport");
        DontDestroyOnLoad(root);
        root.AddComponent<GameViewport>();
    }

    private void CreateBars()
    {
        var canvasObject = new GameObject("Letterbox", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32766; // Above game UI, below the custom mouse cursor.
        bars = new RectTransform[4];
        for (int i = 0; i < bars.Length; i++)
        {
            var bar = new GameObject("Black Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(canvas.transform, false);
            bar.GetComponent<Image>().color = Color.black;
            bars[i] = (RectTransform)bar.transform;
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += SceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= SceneLoaded;
    private void SceneLoaded(Scene scene, LoadSceneMode mode) => dirty = true;

    private void LateUpdate()
    {
        Vector2Int size = new(Screen.width, Screen.height);
        if (!dirty && size == lastSize) return;
        dirty = false;
        lastSize = size;
        Apply(size.x, size.y);
    }

    public void Apply(int width, int height)
    {
        if (bars == null) CreateBars();
        Rect viewport = Fit(width, height);
        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (camera.targetTexture == null && camera.targetDisplay == 0)
                camera.rect = viewport;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas.transform.parent != null && canvas.transform.parent.GetComponentInParent<Canvas>() != null)
                continue;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                canvas.transform.IsChildOf(transform) || canvas.GetComponentInChildren<MouseCursorController>(true) != null)
                continue;
            FitCanvas(canvas, width, height);
        }

        SetBar(bars[0], Vector2.zero, new Vector2(viewport.xMin, 1));
        SetBar(bars[1], new Vector2(viewport.xMax, 0), Vector2.one);
        SetBar(bars[2], Vector2.zero, new Vector2(1, viewport.yMin));
        SetBar(bars[3], new Vector2(0, viewport.yMax), Vector2.one);
    }

    private static void FitCanvas(Canvas canvas, int width, int height)
    {
        Transform existing = canvas.transform.Find("16:9 Content");
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (existing == null)
        {
            var content = new GameObject("16:9 Content", typeof(RectTransform));
            RectTransform rect = (RectTransform)content.transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                ? scaler.referenceResolution : new Vector2(1280, 720);
            // Move existing UI without changing its anchors, offsets or sibling ordering.
            while (canvas.transform.childCount > 1)
            {
                Transform child = canvas.transform.GetChild(0);
                RectTransform childRect = child as RectTransform;
                Vector3 position = childRect != null ? childRect.anchoredPosition3D : child.localPosition;
                child.SetParent(rect, false);
                if (childRect != null) childRect.anchoredPosition3D = position;
            }
        }
        if (scaler != null) scaler.enabled = false;
        Vector2 reference = ((RectTransform)canvas.transform.Find("16:9 Content")).sizeDelta;
        canvas.scaleFactor = Mathf.Min(width / reference.x, height / reference.y);
    }

    private static void SetBar(RectTransform bar, Vector2 min, Vector2 max)
    {
        bar.anchorMin = min;
        bar.anchorMax = max;
        bar.offsetMin = bar.offsetMax = Vector2.zero;
    }
}
