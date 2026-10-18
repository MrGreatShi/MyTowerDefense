using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// 内容场景的 UI 生命周期控制器：按 Addressables 地址加载，退出时关闭所属窗口。
    /// 模块：Game；创建日期：2026-10-11；修改记录：首次实现。
    /// 不持久化、不创建 GUIManager，每个内容场景只挂载一个。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneUIController : MonoBehaviour
    {
        [Header("进入场景时打开的 UI")]
        [Tooltip("填写精确的 Addressables 地址；不要重复配置由 RunSession 等系统打开的窗口。")]
        [SerializeField] private List<string> uiKeys = new List<string>();

        // 记录当前启用周期的归属，不依赖退出时可能已改动的 Inspector 列表。
        private readonly HashSet<string> _ownedKeys = new HashSet<string>(StringComparer.Ordinal);
        private CancellationTokenSource _loadCancellation;
        private Task<bool> _initializationTask;
        private int _generation;
        private bool _hasStarted;

        private void Start()
        {
            _hasStarted = true;
            // Core 正在切换时由切换器调用；单独测试场景时才自动启动。
            if (!IsManagedTransition()) _ = InitializeAsync();
        }

        private void OnEnable()
        {
            // Start 只执行一次；重新启用组件时仍需打开这一轮的窗口。
            if (_hasStarted && !IsManagedTransition()) _ = InitializeAsync();
        }

        private static bool IsManagedTransition()
        {
            var manager = SceneTransitionManager.Instance;
            return manager != null && manager.IsTransitioning;
        }

        /// <summary>
        /// 加载配置窗口；同一启用周期复用任务。空列表也视为初始化成功。
        /// 无 GUIManager、组件未启用或加载失败时返回 false。
        /// </summary>
        public Task<bool> InitializeAsync()
        {
            if (this == null || !isActiveAndEnabled) return Task.FromResult(false);
            if (_initializationTask != null) return _initializationTask;

            _loadCancellation = new CancellationTokenSource();
            int generation = ++_generation;
            var completion = new TaskCompletionSource<bool>();
            // 先发布任务，再执行加载：缓存命中时 OnShow 可能同步回调本组件。
            Task<bool> task = completion.Task;
            _initializationTask = task;
            _ = LoadSceneUIAsync(generation, _loadCancellation.Token, completion);
            return task;
        }

        /// <summary>
        /// 取消本轮显示请求，并逐个关闭本组件管理的窗口；重复调用安全。
        /// 不调用 HideAll，不清空共享资源缓存。之后可再次调用 InitializeAsync。
        /// </summary>
        public void CloseSceneUI()
        {
            ++_generation;
            CancellationTokenSource cancellation = _loadCancellation;
            _loadCancellation = null;
            _initializationTask = null;
            cancellation?.Cancel();
            cancellation?.Dispose();

            // 先清空再通知窗口，避免 OnHide 回调清理方法时重复遍历。
            var keys = new List<string>(_ownedKeys);
            _ownedKeys.Clear();
            foreach (string key in keys)
            {
                if (GUIManager.Instance == null) break;
                try
                {
                    GUIManager.Instance.Hide(key);
                }
                catch (Exception exception)
                {
                    // 某个窗口清理失败，其他窗口仍然要继续关闭。
                    Debug.LogException(exception, this == null ? null : this);
                }
            }
        }

        private async Task LoadSceneUIAsync(
            int generation, CancellationToken cancellationToken, TaskCompletionSource<bool> completion)
        {
            bool ready = false;
            try
            {
                if (GUIManager.Instance == null)
                {
                    throw new InvalidOperationException("SceneUIController requires an active GUIManager in Core.");
                }

                // 初始化期间不读取变化中的列表；Inspector 修改在下一轮初始化生效。
                var keys = uiKeys == null ? new List<string>() : new List<string>(uiKeys);
                foreach (string key in keys)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(key) || !_ownedKeys.Add(key)) continue;
                    if (GUIManager.Instance == null)
                    {
                        throw new InvalidOperationException("GUIManager was destroyed during UI initialization.");
                    }

                    // 先记录再显示，保证 OnCreate/OnShow 内触发退出时也能被清理。
                    await GUIManager.Instance.ShowAsync<UIWindow>(key, null, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (this == null || generation != _generation || !isActiveAndEnabled) return;
                }

                ready = this != null && generation == _generation && isActiveAndEnabled &&
                        !cancellationToken.IsCancellationRequested;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // 正常退出，不记录异常。旧任务不清理新启用周期的窗口。
            }
            catch (Exception exception)
            {
                if (this != null && generation == _generation)
                {
                    Debug.LogException(exception, this);
                    CloseSceneUI();
                }
            }
            finally
            {
                completion.TrySetResult(ready);
            }
        }

        private void OnDisable()
        {
            CloseSceneUI();
        }

        private void OnDestroy()
        {
            CloseSceneUI();
        }
    }
}
