using System;
using System.Threading;
using System.Threading.Tasks;
using MyTowerDefense.Data;
using UnityEngine;

namespace MyTowerDefense.Managers
{
    /// <summary>
    /// 职责：在 Core 中独立管理游戏目录配置初始化及发布者生命周期。
    /// 模块：Managers；创建日期：2026-10-12；修改记录：首次实现。
    /// 修改记录：2026-10-12，按 Address 获取共享 ScriptableObject 配置并统一管理句柄生命周期。
    /// 不处理场景切换、UI、玩家存档或资源预加载；Core 不卸载，无需再持久化对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDataManager : MonoBehaviour
    {
        /// <summary>本轮唯一有效的数据管理器；业务查询应优先使用 GameData。</summary>
        public static GameDataManager Instance { get; private set; }

        [Header("全局目录配置")]
        [Tooltip("在 Core 绑定配置资产；初始化成功后本轮不再读取 Inspector 变化。")]
        [SerializeField] private GameCatalogConfig _catalogConfig;

        private Task<bool> _initializationTask;
        private bool _isRejected;

        // 同一 Address 的不同泛型查询共享一个基类缓存，不重复持有加载句柄。
        private readonly AddressablesLoader<ScriptableObject> _scriptableAssetLoader =
            new AddressablesLoader<ScriptableObject>();

        // 配置缓存所属运行周期；清理时递增，阻止旧请求向新周期交付结果。
        private int _assetGeneration;

        /// <summary>本实例仍为有效发布者，且全部目录已初始化。</summary>
        public bool IsReady => this != null && !_isRejected &&
            Instance == this && GameData.IsOwnedBy(this);

        private void Awake()
        {
            _ = EnsureInitializedAsync();
        }

        /// <summary>
        /// 启动或复用本轮初始化任务；所有调用必须在 Unity 主线程。
        /// 当前配置为直接引用，初始化同步完成；统一 Task 入口不依赖 Awake 顺序。
        /// 配置错误返回 false 并记录原因；本轮失败结果也会复用，修正后需重新 Play。
        /// </summary>
        public Task<bool> EnsureInitializedAsync()
        {
            if (this == null || !isActiveAndEnabled || _isRejected)
            {
                return Task.FromResult(false);
            }

            if (Instance != null && Instance != this)
            {
                _isRejected = true;
                Debug.LogError("Core 中只允许一个 GameDataManager；重复组件已拒绝。", this);
                Destroy(this);
                return Task.FromResult(false);
            }

            // 支持场景切换器先于本组件 Awake 调用；无需全局脚本执行顺序设置。
            Instance = this;
            if (_initializationTask != null)
            {
                return _initializationTask;
            }

            var completion = new TaskCompletionSource<bool>();
            // 先登记任务，避免初始化期间的重入创建第二轮任务。
            _initializationTask = completion.Task;
            try
            {
                GameData.Initialize(_catalogConfig, this);
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                completion.TrySetResult(false);
            }

            return _initializationTask;
        }

        /// <summary>
        /// 按完整 Address 异步取得具体类型的配置资产；同地址请求共享缓存和底层加载。
        /// 必须在 Unity 主线程调用。返回的是共享实例，应只读使用，不能自行 Release 或 Destroy。
        /// </summary>
        /// <typeparam name="T">ScriptableObject 配置类型，例如 HeroSettingConfig 或 LevelConfig。</typeparam>
        /// <param name="address">单个配置资产的唯一 Address，不是英雄 Prefab 的地址或一组资产的 Label。</param>
        /// <param name="cancellationToken">
        /// 只取消本次结果交付，不中断或释放共享加载；任务可能要等待底层加载结束后才完成取消。
        /// </param>
        /// <returns>缓存持有的共享配置实例；需要可修改副本时由调用方自行创建并管理副本。</returns>
        /// <exception cref="ArgumentException">Address 为空。</exception>
        /// <exception cref="ObjectDisposedException">Manager 已销毁或被拒绝。</exception>
        /// <exception cref="InvalidOperationException">Manager 未启用、初始化失败或资源结果无效。</exception>
        /// <exception cref="OperationCanceledException">调用取消、Manager 失效或缓存所属周期已改变。</exception>
        /// <exception cref="InvalidCastException">资产的实际配置类型不符合 T。</exception>
        public async Task<T> LoadScriptableAssetAsync<T>(
            string address,
            CancellationToken cancellationToken = default)
            where T : ScriptableObject
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("配置资产 Address 不能为空。", nameof(address));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (this == null || _isRejected)
            {
                throw new ObjectDisposedException(nameof(GameDataManager));
            }

            if (!isActiveAndEnabled)
            {
                throw new InvalidOperationException("加载配置前必须启用 Core 中的 GameDataManager。");
            }

            int generation = _assetGeneration;
            if (!await EnsureInitializedAsync())
            {
                throw new InvalidOperationException("GameDataManager 初始化失败，无法获取配置资产。");
            }

            ValidateAssetRequest(generation, cancellationToken);
            var handle = await _scriptableAssetLoader.LoadPrefabHandleAsync(address);

            // 先检查生命周期，再读取句柄结果，避免清理后的无效句柄被访问。
            ValidateAssetRequest(generation, cancellationToken);
            if (!handle.IsValid())
            {
                throw new InvalidOperationException($"配置资产句柄无效，Address='{address}'。");
            }

            ScriptableObject asset = handle.Result;
            if (asset == null)
            {
                throw new InvalidOperationException($"配置资产结果为空或已销毁，Address='{address}'。");
            }

            if (asset is T result)
            {
                return result;
            }

            // 类型错误不释放缓存，其他调用者仍可能按正确类型使用同一资产。
            throw new InvalidCastException(
                $"Address='{address}' 的实际类型为 {asset.GetType().Name}，不能作为 {typeof(T).Name} 返回。");
        }

        private void ValidateAssetRequest(int generation, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (this == null || _isRejected || !isActiveAndEnabled || !IsReady ||
                generation != _assetGeneration)
            {
                throw new OperationCanceledException("配置加载所属 Manager 或运行周期已失效。");
            }
        }

        private void ClearScriptableAssets()
        {
            // 先标记旧请求失效，再释放句柄；释放回调重入时不能交付旧结果。
            unchecked
            {
                _assetGeneration++;
            }

            _scriptableAssetLoader.ReleaseAll();
        }

        private void OnDestroy()
        {
            _isRejected = true;
            // 每个实例只清理自己的加载器；重复组件不能释放有效实例的资源。
            ClearScriptableAssets();
            if (Instance != this)
            {
                return;
            }

            GameData.Release(this);
            Instance = null;
            _initializationTask = null;
        }
        /// <summary>
        /// 在编辑器中 Play 模式切换或运行时重载时，清理本轮静态状态；避免残留数据影响下一轮。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            GameDataManager manager = Instance;
            if (manager != null)
            {
                // 缓存释放期间拒绝重入请求，不能让请求混入尚未结束的重置过程。
                manager._isRejected = true;
                manager.ClearScriptableAssets();
                manager._initializationTask = null;
                manager._isRejected = false;
            }

            Instance = null;
        }
    }
}
