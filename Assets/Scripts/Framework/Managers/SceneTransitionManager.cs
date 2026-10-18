using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MyTowerDefense.Game;
using MyTowerDefense.Managers;

/// <summary>
/// Core 场景的内容场景切换器：Loading、资源就绪等待及失败重试。
/// 创建日期：2026-10-11；修改记录：支持场景 UI 初始化与退出清理。Core 本身始终不卸载。
/// 修改记录：2026-10-12，仅等待独立 GameDataManager 就绪，不承担数据初始化职责。
/// </summary>
public sealed class SceneTransitionManager : MonoBehaviour
{
    /// <summary>Core 中唯一的场景切换入口。</summary>
    public static SceneTransitionManager Instance { get; private set; }

    [Header("启动内容场景（留空则等待登录等流程主动调用）")]
    [SerializeField] private string initialScene;
    [Header("Core 内的独立数据管理器")]
    [SerializeField] private GameDataManager _dataManager;
    [Header("Core 内的全屏 Loading UI")]
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TMP_Text statusText;
    [Header("资源初始化等待上限，秒，使用真实时间")]
    [SerializeField, Min(1f)] private float initializationTimeout = 60f;

    private Scene _coreScene;
    private Scene _contentScene;
    private string _lastRequestedScene;

    /// <summary>正在执行一次切换；并发请求会被拒绝。</summary>
    public bool IsTransitioning { get; private set; }
    /// <summary>切换及失败遮罩期间禁止场景对象选择。</summary>
    public bool IsInputBlocked { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("SceneTransitionManager must only exist in Core.", this);
            Destroy(this);
            return;
        }
        Instance = this;
        _coreScene = gameObject.scene;
        if (progressSlider != null)
        {
            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.wholeNumbers = false;
        }
        SetLoading(false, 0f, string.Empty);
    }

    private async void Start()
    {
        if (!string.IsNullOrWhiteSpace(initialScene))
        {
            await SwitchSceneAsync(initialScene);
        }
    }

    /// <summary>适配 Unity Button.onClick；异步异常由切换流程记录。</summary>
    public void SwitchScene(string sceneName)
    {
        _ = SwitchSceneAsync(sceneName);
    }

    /// <summary>适配 Core 中的重试按钮；重试会重新加载失败的内容场景。</summary>
    public void RetryLastTransition()
    {
        if (!string.IsNullOrEmpty(_lastRequestedScene)) SwitchScene(_lastRequestedScene);
    }

    /// <summary>
    /// 卸载旧内容并增量加载新内容，等待战斗初始化后启动。
    /// sceneName 使用 Build Settings 中的场景名或路径；失败返回 false 并保留遮罩。
    /// </summary>
    public async Task<bool> SwitchSceneAsync(string sceneName)
    {
        if (Instance != this || IsTransitioning) return false;
        if (string.IsNullOrWhiteSpace(sceneName)) return false;

        // 解析准确的构建索引，拒绝重名场景及 Core，且不破坏旧场景。
        int targetIndex = ResolveBuildIndex(sceneName);
        if (targetIndex < 0 || targetIndex == _coreScene.buildIndex)
        {
            Debug.LogError($"Invalid content scene or Core requested: {sceneName}", this);
            return false;
        }
        if (!IsInputBlocked && _contentScene.IsValid() &&
            _contentScene.isLoaded && _contentScene.buildIndex == targetIndex &&
            _dataManager != null && _dataManager.IsReady)
        {
            return true;
        }
        if (loadingGroup == null || GUIManager.Instance == null)
        {
            Debug.LogError("Core requires a Loading CanvasGroup and GUIManager.", this);
            return false;
        }

        _lastRequestedScene = sceneName;
        IsTransitioning = true;
        IsInputBlocked = true;
        Time.timeScale = 1f;
        SetLoading(true, 0f, "正在准备切换…");
        SceneUIController sceneUI = null;
        try
        {
            // 只等待 Manager 的共享任务；配置读取、校验、发布都不放进切换器。
            // 此检查先于旧场景/UI 清理，数据失败时不会破坏当前内容场景。
            if (_dataManager == null || !_dataManager.isActiveAndEnabled ||
                _dataManager.gameObject.scene != _coreScene)
            {
                throw new InvalidOperationException("Core 必须绑定同场景内启用的 GameDataManager。");
            }

            bool dataReady = await _dataManager.EnsureInitializedAsync();
            EnsureAlive();
            if (!dataReady || _dataManager == null || !_dataManager.isActiveAndEnabled || !_dataManager.IsReady)
            {
                throw new InvalidOperationException("全局数据初始化失败，请检查 GameDataManager 的配置。");
            }

            EventBus.Publish(new ObjectSelectionCancelledEvent());
            if (_contentScene.IsValid() && _contentScene.isLoaded)
            {
                SceneUIController oldUI = GetSceneUIController(_contentScene);
                if (oldUI != null) oldUI.CloseSceneUI();
                RunSession oldSession = GetSession(_contentScene);
                if (oldSession != null)
                {
                    oldSession.StopBattle();
                    // 不取消 GUIManager.ShowAsync：先收尾再隐藏，防止迟到的旧 UI。
                    await WaitForInitialization(oldSession);
                    oldSession.StopBattle();
                }
                GUIManager.Instance.HideAll();
                SceneManager.SetActiveScene(_coreScene);
                await WaitForOperation(SceneManager.UnloadSceneAsync(_contentScene), 0f, 0.2f);
                _contentScene = default;
            }
            else
            {
                GUIManager.Instance.HideAll();
            }

            SetLoading(true, 0.2f, "正在加载场景…");
            await WaitForOperation(
                SceneManager.LoadSceneAsync(targetIndex, LoadSceneMode.Additive), 0.2f, 0.7f);
            _contentScene = SceneManager.GetSceneByBuildIndex(targetIndex);
            if (!_contentScene.IsValid() || !_contentScene.isLoaded ||
                !SceneManager.SetActiveScene(_contentScene))
            {
                throw new InvalidOperationException("Cannot activate loaded content scene.");
            }

            SetLoading(true, 0.7f, "正在加载资源并部署英雄…");
            sceneUI = GetSceneUIController(_contentScene);
            Task<bool> uiInitialization = sceneUI != null ? sceneUI.InitializeAsync() : null;
            RunSession session = GetSession(_contentScene);
            bool hasSession = session != null;
            if (session != null)
            {
                if (!await WaitForInitialization(session))
                {
                    throw new InvalidOperationException("Battle initialization failed. See earlier Console errors.");
                }
                if (session == null || !session.isActiveAndEnabled)
                {
                    throw new InvalidOperationException("RunSession was destroyed or disabled during initialization.");
                }
            }

            if (uiInitialization != null)
            {
                if (!await WaitForSceneUI(uiInitialization) || sceneUI == null || !sceneUI.isActiveAndEnabled)
                {
                    throw new InvalidOperationException("Scene UI initialization failed. See earlier Console errors.");
                }
            }
            if (hasSession)
            {
                if (session == null || !session.isActiveAndEnabled)
                {
                    throw new InvalidOperationException("RunSession became unavailable while waiting for scene UI.");
                }
                session.StartBattle();
            }

            SetLoading(false, 1f, "加载完成");
            IsInputBlocked = false;
            return true;
        }
        catch (Exception exception)
        {
            if (this != null)
            {
                if (sceneUI != null) sceneUI.CloseSceneUI();
                // 超时不等于取消底层加载；保留失败场景，下次重试先等待收尾。
                SetLoading(true, 0f, "加载失败：" + exception.Message + "\n请检查 Console 后重试。");
                Debug.LogException(exception, this);
            }
            return false;
        }
        finally
        {
            IsTransitioning = false;
        }
    }

    private async Task<bool> WaitForInitialization(RunSession session)
    {
        Task<bool> task = session.InitializationTask;
        float startedAt = Time.realtimeSinceStartup;
        while (!task.IsCompleted)
        {
            EnsureAlive();
            // 禁用并不取消正在 await 的 GUI 加载，仍须等实际初始化收尾。
            if (session == null) return false;
            if (Time.realtimeSinceStartup - startedAt >= Mathf.Max(1f, initializationTimeout))
            {
                throw new TimeoutException("RunSession initialization timed out.");
            }
            await Task.Yield();
        }
        return await task;
    }

    private async Task<bool> WaitForSceneUI(Task<bool> task)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!task.IsCompleted)
        {
            EnsureAlive();
            if (Time.realtimeSinceStartup - startedAt >= Mathf.Max(1f, initializationTimeout))
            {
                throw new TimeoutException("Scene UI initialization timed out.");
            }
            await Task.Yield();
        }
        return await task;
    }

    private async Task WaitForOperation(AsyncOperation operation, float start, float end)
    {
        if (operation == null) throw new InvalidOperationException("Scene operation could not start.");
        // Unity 场景操作无法取消；必须等待它结束，不能超时后允许另一次切换并发。
        while (!operation.isDone)
        {
            EnsureAlive();
            if (progressSlider != null)
            {
                progressSlider.value = Mathf.Lerp(start, end, Mathf.Clamp01(operation.progress / 0.9f));
            }
            await Task.Yield();
        }
        EnsureAlive();
    }

    private static RunSession GetSession(Scene scene)
    {
        RunSession result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RunSession session in root.GetComponentsInChildren<RunSession>(true))
            {
                if (result != null) throw new InvalidOperationException("Content scene contains multiple RunSessions.");
                result = session;
            }
        }
        return result;
    }

    private static SceneUIController GetSceneUIController(Scene scene)
    {
        SceneUIController result = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SceneUIController controller in root.GetComponentsInChildren<SceneUIController>(true))
            {
                if (result != null)
                {
                    throw new InvalidOperationException("Content scene contains multiple SceneUIControllers.");
                }
                result = controller;
            }
        }
        return result;
    }

    private static int ResolveBuildIndex(string sceneName)
    {
        int found = -1;
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            if (path != sceneName && System.IO.Path.GetFileNameWithoutExtension(path) != sceneName) continue;
            if (found >= 0) return -1;
            found = i;
        }
        return found;
    }

    private void SetLoading(bool visible, float progress, string message)
    {
        if (loadingGroup != null)
        {
            loadingGroup.gameObject.SetActive(true);
            loadingGroup.transform.SetAsLastSibling();
            loadingGroup.alpha = visible ? 1f : 0f;
            loadingGroup.blocksRaycasts = visible;
            loadingGroup.interactable = visible;
        }
        if (progressSlider != null) progressSlider.value = progress;
        if (statusText != null) statusText.text = message;
    }

    private void EnsureAlive()
    {
        if (this == null) throw new OperationCanceledException("Core was destroyed during scene switching.");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
