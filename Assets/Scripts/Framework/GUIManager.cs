using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class GUIManager : MonoBehaviour
{
    public static GUIManager Instance { get; private set; }
    [Header("Root Canvas (optional)")]
    [SerializeField] private Canvas rootCanvas;

    [Header("Optional: modal mask prefab (UGUI Image full screen)")]
    [SerializeField] private GameObject modalMaskPrefab;

    private readonly Dictionary<string, UIWindow> _instances = new(); // key -> instance
    private readonly Stack<string> _stack = new();                   // key stack
    private readonly Dictionary<UILayer, Transform> _layers = new();

    private readonly AddressablesLoader<GameObject> _loader = new();

    private GameObject _modalMaskInstance;
    private int _modalCount;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        EnsureCanvasAndLayers();
        EnsureModalMask();
    }

    private void EnsureCanvasAndLayers()
    {
        if (rootCanvas == null)
        {
            var canvasGO = new GameObject("UIRootCanvas");
            rootCanvas = canvasGO.AddComponent<Canvas>();
            rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
        }

        CreateLayer(UILayer.Background, "Background");
        CreateLayer(UILayer.Normal, "Normal");
        CreateLayer(UILayer.Popup, "Popup");
        CreateLayer(UILayer.Top, "Top");
    }

    private void CreateLayer(UILayer layer, string name)
    {
        // 关键：显式创建 RectTransform，别用默认 Transform
        var go = new GameObject(name, typeof(RectTransform));
        var rt = (RectTransform)go.transform;

        rt.SetParent(rootCanvas.transform, false);

        // 关键：全屏拉伸
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; // left/bottom = 0
        rt.offsetMax = Vector2.zero; // right/top = 0

        _layers[layer] = rt;
    }

    private void EnsureModalMask()
    {
        if (modalMaskPrefab == null) return;
        _modalMaskInstance = Instantiate(modalMaskPrefab, _layers[UILayer.Popup]);
        _modalMaskInstance.name = "ModalMask";
        _modalMaskInstance.SetActive(false);
        _modalMaskInstance.transform.SetAsFirstSibling();
    }

    /// <summary>
    /// 异步打开窗口（Addressables key）。
    /// </summary>
    public async Task<T> ShowAsync<T>(string key, object args = null) where T : UIWindow
    {
        // 已有实例：直接显示
        if (_instances.TryGetValue(key, out var existing) && existing != null)
        {
            InternalShow(existing, args);
            return (T)existing;
        }

        // 先加载 prefab 句柄
        var handle = await _loader.LoadPrefabHandleAsync(key);
        var prefab = handle.Result;

        // 实例化
        var go = Instantiate(prefab);
        go.name = key;

        var w = go.GetComponent<T>();
        if (w == null)
        {
            Destroy(go);
            // prefab 句柄是否释放：这里不释放，因为 loader 做了 prefab 级缓存，给其它窗口复用
            throw new System.Exception($"Prefab '{key}' missing component {typeof(T).Name}");
        }

        // 放到层级
        go.transform.SetParent(_layers[w.Layer], false);

        _instances[key] = w;

        w.SetVisible(false);
        w.OnCreate();

        InternalShow(w, args);
        return w;
    }

    public void Hide(string key)
    {
        if (!_instances.TryGetValue(key, out var w) || w == null) return;
        if (!w.IsVisible) return;

        w.OnHide();
        w.SetVisible(false);

        if (w.IsModal) DecreaseModal();
        if (w.UseStack) RemoveFromStack(key);

        if (w.DestroyOnHide)
        {
            w.OnDestroyWindow();
            _instances.Remove(key);
            Destroy(w.gameObject);

            // 注意：DestroyOnHide 时，通常也希望释放 prefab 句柄（否则会一直占用内存）
            // 如果你希望“窗口销毁但 prefab 仍缓存”，把这一行删掉即可。
            _loader.Release(key);
        }
    }

    public void HideAll()
    {
        foreach (var key in new List<string>(_instances.Keys))
        {
            Hide(key);
        }
    }

    public void Back()
    {
        while (_stack.Count > 0)
        {
            var top = _stack.Pop();
            if (_instances.TryGetValue(top, out var w) && w != null && w.IsVisible)
            {
                Hide(top);
                return;
            }
        }
    }

    public bool IsShowing(string key)
    {
        return _instances.TryGetValue(key, out var w) && w != null && w.IsVisible;
    }

    private void InternalShow(UIWindow w, object args)
    {
        // 确保父节点正确（可能场景切换后 root 重建，你也可扩展重绑逻辑）
        w.transform.SetParent(_layers[w.Layer], false);

        if (w.IsModal) IncreaseModal();

        // sibling 排序
        if (_modalMaskInstance != null && _modalMaskInstance.activeSelf && w.Layer == UILayer.Popup)
        {
            _modalMaskInstance.transform.SetAsFirstSibling();
            w.transform.SetAsLastSibling();
        }
        else
        {
            w.transform.SetAsLastSibling();
        }

        if (w.UseStack)
        {
            RemoveFromStack(w.Key);
            _stack.Push(w.Key);
        }

        w.SetVisible(true);
        w.OnShow(args);
    }

    private void RemoveFromStack(string key)
    {
        if (_stack.Count == 0) return;

        var tmp = new Stack<string>();
        while (_stack.Count > 0)
        {
            var x = _stack.Pop();
            if (x != key) tmp.Push(x);
        }
        while (tmp.Count > 0) _stack.Push(tmp.Pop());
    }

    private void IncreaseModal()
    {
        _modalCount++;
        if (_modalMaskInstance != null) _modalMaskInstance.SetActive(true);
    }

    private void DecreaseModal()
    {
        _modalCount = Mathf.Max(0, _modalCount - 1);
        if (_modalMaskInstance != null) _modalMaskInstance.SetActive(_modalCount > 0);
    }

    private void OnDestroy()
    {
        // 如果你希望整个 UIManager 生命周期结束时释放所有资源：
        // _loader.ReleaseAll();
    }
}