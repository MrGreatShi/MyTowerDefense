using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using MyTowerDefense.Data;
using UnityEngine;

namespace MyTowerDefense.Managers
{
    /// <summary>
    /// 职责：在 Core 中统一管理只读游戏目录、SO 资源查询和缓存生命周期。
    /// 模块：Managers；创建日期：2026-10-12；修改记录：首次实现。
    /// 修改记录：2026-10-12，按 Address 获取共享 ScriptableObject 配置并统一管理句柄生命周期。
    /// 修改记录：2026-10-18，提供英雄配置的统一地址规则与基础属性校验。
    /// 修改记录：2026-10-19，统一持有目录快照、查询与初始化状态。
    /// 不处理场景切换、UI、玩家存档或资源预加载；Core 不卸载，无需再持久化对象。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameDataManager : MonoBehaviour
    {
        /// <summary>Core 中唯一有效的数据管理器；目录和配置统一通过此实例访问。</summary>
        public static GameDataManager Instance { get; private set; }

        [Header("全局目录配置")]
        [Tooltip("在 Core 绑定配置资产；初始化成功后本轮不再读取 Inspector 变化。")]
        [SerializeField] private GameCatalogConfig _catalogConfig;

        private Task<bool> _initializationTask;
        private bool _isRejected;
        private bool _isInitialized;

        // 快照独立于 SO 源列表；HashSet 只服务查询，不影响 Inspector 中的展示顺序。
        private ReadOnlyCollection<string> _heroes;
        private ReadOnlyCollection<string> _equipments;
        private HashSet<string> _heroKeys;
        private HashSet<string> _equipmentKeys;

        // 同一 Address 的不同泛型查询共享一个基类缓存，不重复持有加载句柄。
        private readonly AddressablesLoader<ScriptableObject> _scriptableAssetLoader =
            new AddressablesLoader<ScriptableObject>();

        // 配置缓存所属运行周期；清理时递增，阻止旧请求向新周期交付结果。
        private int _assetGeneration;

        /// <summary>本实例启用、仍为有效单例，且全部目录均已校验完成。</summary>
        public bool IsReady => this != null && isActiveAndEnabled && !_isRejected &&
            Instance == this && _isInitialized;

        /// <summary>按配置顺序排列的英雄 Key 快照；未就绪时抛出明确异常。</summary>
        public IReadOnlyList<string> Heroes
        {
            get
            {
                EnsureReady();
                return _heroes;
            }
        }

        /// <summary>按配置顺序排列的装备 Key 快照，不暴露 SO 内部可修改的 List。</summary>
        public IReadOnlyList<string> Equipments
        {
            get
            {
                EnsureReady();
                return _equipments;
            }
        }

        /// <summary>按区分大小写的完整 Key 查询英雄；空 Key 返回 false，未就绪则抛出异常。</summary>
        public bool ContainsHero(string key)
        {
            EnsureReady();
            return !string.IsNullOrWhiteSpace(key) && _heroKeys.Contains(key);
        }

        /// <summary>按区分大小写的完整 Key 查询装备；空 Key 返回 false，未就绪则抛出异常。</summary>
        public bool ContainsEquipment(string key)
        {
            EnsureReady();
            return !string.IsNullOrWhiteSpace(key) && _equipmentKeys.Contains(key);
        }

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
                if (_catalogConfig == null)
                {
                    throw new InvalidOperationException("GameDataManager 未绑定有效的 GameCatalogConfig。");
                }

                // 两个目录都构建成功后再发布，任何一项无效都不能留下半初始化状态。
                ReadOnlyCollection<string> heroes = CreateSnapshot(_catalogConfig.HeroKeys, "英雄", out var heroKeys);
                ReadOnlyCollection<string> equipments = CreateSnapshot(_catalogConfig.EquipmentKeys, "装备", out var equipmentKeys);
                _heroes = heroes;
                _equipments = equipments;
                _heroKeys = heroKeys;
                _equipmentKeys = equipmentKeys;
                _isInitialized = true;
                completion.TrySetResult(true);
            }
            catch (Exception exception)
            {
                ClearCatalog();
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

        /// <summary>
        /// 按英雄资源标识取得共享基础配置；详情和部署必须使用同一个查询入口。
        /// 返回资产只读使用，战斗状态不得写回配置。必须在 Unity 主线程调用。
        /// </summary>
        /// <param name="heroKey">英雄 Prefab 的资源标识，例如 Knight；不是中文显示名或数值 Key。</param>
        /// <param name="cancellationToken">仅取消本次结果交付，不释放共享缓存。</param>
        /// <returns>Address 为 heroKey + "_Setting" 的有效基础配置。</returns>
        public async Task<HeroSettingConfig> GetHeroConfigAsync(
            string heroKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(heroKey))
            {
                throw new ArgumentException("英雄资源标识不能为空。", nameof(heroKey));
            }

            string address = heroKey + "_Setting";
            int generation = _assetGeneration;
            HeroSettingConfig config = await LoadScriptableAssetAsync<HeroSettingConfig>(
                address, cancellationToken);

            // 本包装方法本身也有 await；继续执行时需再次核对所属周期及调用者取消。
            ValidateAssetRequest(generation, cancellationToken);
            if (config == null)
            {
                throw new InvalidOperationException($"英雄配置 '{address}' 已被销毁。");
            }

            // 在创建任何英雄前即可发现非法数值；详情也不会显示另一套兜底数值。
            if (config.health <= 0 || config.attack < 0 || config.attackRange < 0 ||
                config.physicalDefense < 0 || config.magicDefense < 0 || config.speed < 0)
            {
                throw new InvalidOperationException(
                    $"英雄配置 '{address}' 无效：生命必须大于 0，攻击、范围、双防和移速不能为负数。");
            }

            return config;
        }

        private static ReadOnlyCollection<string> CreateSnapshot(
            IReadOnlyList<string> source, string category, out HashSet<string> keys)
        {
            if (source == null)
            {
                throw new InvalidOperationException($"{category}目录为 null，请检查配置资产。");
            }

            keys = new HashSet<string>(StringComparer.Ordinal);
            var items = new List<string>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                string key = source[i];
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidOperationException($"{category}目录 Element {i} 的 Key 为空。");
                }

                // 不自动 Trim，避免悄悄改写 Addressables 资源标识。
                if (!string.Equals(key, key.Trim(), StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{category} Key '{key}' 含首尾空格，请修正配置。");
                }

                if (!keys.Add(key))
                {
                    throw new InvalidOperationException($"{category}目录存在重复 Key：'{key}'。");
                }

                items.Add(key);
            }

            return items.AsReadOnly();
        }

        private void EnsureReady()
        {
            if (!IsReady)
            {
                throw new InvalidOperationException(
                    "GameDataManager 尚未就绪。请从 Core 启动，并等待 EnsureInitializedAsync 成功后读取目录。");
            }
        }

        private void ClearCatalog()
        {
            _isInitialized = false;
            _heroes = null;
            _equipments = null;
            _heroKeys = null;
            _equipmentKeys = null;
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
            ClearCatalog();
            _initializationTask = null;
            // 每个实例只清理自己的加载器；重复组件不能释放有效实例的资源。
            ClearScriptableAssets();
            if (!ReferenceEquals(Instance, this))
            {
                return;
            }

            Instance = null;
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
                manager.ClearCatalog();
                manager.ClearScriptableAssets();
                manager._initializationTask = null;
                manager._isRejected = false;
            }

            Instance = null;
        }
    }
}
