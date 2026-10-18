using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;
using MyTowerDefense.Game;

public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }
    [Header("关卡信息")]
    [SerializeField]
    LevelConfig level;
    public List<string> heroes;
    // heroes 保留原序列化配置；服务独立管理英雄实例和资源生命周期。
    private HeroDeploymentService _heroDeployment;
    private bool _isDestroyed;
    [Header("战斗启动")]
    // 单独打开战斗场景时可自动开始；Core 切换期间统一由切换器启动。
    [SerializeField] private bool startAutomatically = true;
    private readonly TaskCompletionSource<bool> _initialization = new TaskCompletionSource<bool>();

    /// <summary>资源、关卡 UI 和英雄部署全部完成时返回 true；失败返回 false。</summary>
    public Task<bool> InitializationTask => _initialization.Task;
    float elapsedTime = 0f;     // 关卡运行时间（秒）
    int existingEnemyCount = 0;   // 当前场景中存在的敌人数量
    int heartCount;          // 关卡生命值（玩家失败条件）
    int goldCount;          // 当前金币数量   

    [SerializeField]
    LevelState state = LevelState.Prepare; // 关卡状态
    readonly Dictionary<string, SimpleObjectPool> pools = new Dictionary<string, SimpleObjectPool>();
    Queue<EnemyWave> enemyWaves;
    EnemyWave nextWave;
    bool hasNextWave;
    AddressablesLoader<GameObject> addressablesLoader;
    public int GoldCount => goldCount;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        addressablesLoader = new AddressablesLoader<GameObject>();
        _heroDeployment = new HeroDeploymentService(transform);
    }

    private async void Start()
    {
        try
        {
            bool ready = await InitializeGameAsync();
            _initialization.TrySetResult(ready);
            if (ready && startAutomatically &&
                (SceneTransitionManager.Instance == null ||
                 !SceneTransitionManager.Instance.IsInputBlocked))
            {
                StartBattle();
            }
        }
        catch (Exception exception)
        {
            if (this == null || _isDestroyed) return;
            _heroDeployment?.ClearSpawnedHeroes();
            Debug.LogException(exception, this);
            enabled = false;
        }
        finally
        {
            // 包括异常、禁用和提前返回，不能让切换器永久等待。
            _initialization.TrySetResult(false);
        }
    }

    /// <summary>初始化成功后启动波次；重复调用不重新计时。</summary>
    public void StartBattle()
    {
        if (_isDestroyed || !isActiveAndEnabled || state != LevelState.Prepare ||
            !_initialization.Task.IsCompleted || !_initialization.Task.Result)
        {
            return;
        }
        state = LevelState.Running;
    }

    /// <summary>切换前停止波次调度，不取消尚未结束的资源初始化。</summary>
    public void StopBattle()
    {
        state = LevelState.Prepare;
    }

    void OnEnable()
    {
        EventBus.Subscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<EnemyInvadeEvent>(OnEnemyInvaded);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<EnemyInvadeEvent>(OnEnemyInvaded);
    }
    void OnDestroy()
    {
        _isDestroyed = true;
        _initialization.TrySetResult(false);
        if (Instance == this) Instance = null;
        _heroDeployment?.Dispose();
        foreach (SimpleObjectPool pool in pools.Values)
            pool.Clear();

        pools.Clear();
        addressablesLoader?.ReleaseAll();
    }
    void Update()
    {
        if (state != LevelState.Running) return;
        if (heartCount <= 0)
        {
            Lose();
            return;
        }
        if (!hasNextWave && enemyWaves.Count == 0 && existingEnemyCount == 0)
        {
            Success();
            return;
        }
        elapsedTime += Time.deltaTime;
        if (hasNextWave && elapsedTime >= nextWave.waveStartTime)
        {
            Spawn(nextWave);        // 触发下一波次
            hasNextWave = TryLoadNextWave();
        }
    }

    private async Task<bool> InitializeGameAsync()
    {
        if (level == null)
        {
            Debug.LogError("RunSession requires a LevelConfig.", this);
            enabled = false;
            return false;
        }

        // 上一局可能暂停过时间；新关卡初始化时必须恢复正常时间轴。
        Time.timeScale = 1f;
        state = LevelState.Prepare;

        if (!await LoadObjectPoolsAsync())
        {
            if (this != null && !_isDestroyed) enabled = false;
            return false;
        }
        if (this == null || _isDestroyed || !isActiveAndEnabled) return false;

        enemyWaves = new Queue<EnemyWave>(level.waves ?? new List<EnemyWave>());
        hasNextWave = TryLoadNextWave();
        elapsedTime = 0f;
        existingEnemyCount = 0;
        heartCount = level.heartCount;
        goldCount = level.originalGold;

        if (GUIManager.Instance == null)
        {
            Debug.LogError("RunSession requires an active GUIManager.", this);
            enabled = false;
            return false;
        }

        await GUIManager.Instance.ShowAsync<LevelCommonWindow>(
            "UI/LevelCommonWindow",
            new LevelCommonWindowArgs(goldCount, heartCount, existingEnemyCount));

        if (this == null || _isDestroyed || !isActiveAndEnabled) return false;
        // 完整部署成功后才启动波次计时，关卡会话不再处理具体地块查找。
        bool deployed = await _heroDeployment.DeployAsync(heroes, CubeGridManager.Instance);
        if (this == null || _isDestroyed || !isActiveAndEnabled)
        {
            _heroDeployment?.Dispose();
            return false;
        }
        if (!deployed)
        {
            enabled = false;
            return false;
        }

        return true;
    }

    private async Task<bool> LoadObjectPoolsAsync()
    {
        if (level.enemies == null)
        {
            return true;
        }

        foreach (string enemyName in level.enemies)
        {
            if (string.IsNullOrEmpty(enemyName) || pools.ContainsKey(enemyName))
                continue;
            try
            {
                var handle = await addressablesLoader.LoadPrefabHandleAsync(enemyName);
                if (this == null || _isDestroyed || !isActiveAndEnabled)
                {
                    addressablesLoader.Release(enemyName);
                    return false;
                }
                var prefab = handle.Result;
                if (prefab == null)
                {
                    Debug.LogError($"RunSession: prefab load result is null: {enemyName}", this);
                    return false;
                }

                pools.Add(enemyName, new SimpleObjectPool(prefab, this.transform));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return false;
            }
        }

        return true;
    }

    private bool TryLoadNextWave()
    {
        if (enemyWaves == null || enemyWaves.Count == 0)
        {
            return false;
        }

        nextWave = enemyWaves.Dequeue();
        return true;
    }
    void Success()
    {
        Debug.Log("Level completed successfully!");
        Time.timeScale = 0f; // 暂停游戏
        state = LevelState.Success;
    }
    void Lose()
    {
        Debug.Log("Level failed!");
        Time.timeScale = 0f; // 暂停游戏
        state = LevelState.Failed;
    }

    void Spawn(EnemyWave wave)
    {
        SimpleObjectPool pool = null;
        if (!pools.TryGetValue(wave.enemyName, out pool))
        {
            Debug.LogError("No object pool found for enemy: " + wave.enemyName, this);
            return;
        }
        for (int i = 0; i < wave.count; i++)
        {
            GameObject enemy = pool.Get(transform.position, transform.rotation);
            if (enemy != null)
                EventBus.Publish(new EnemySpawnEvent(enemy, wave.enemyName));
        }
    }

    private void OnEnemySpawned(EnemySpawnEvent spawnEvent)
    {
        // 仅在对象池确认返回有效敌人后计数，避免加载或取对象失败时出现虚假存活数。
        existingEnemyCount++;
    }

    private void OnEnemyKilled(EnemyKilledEvent killedEvent)
    {
        existingEnemyCount--;
        goldCount += killedEvent.Enemy.bounty;
        if (existingEnemyCount < 0)
            existingEnemyCount = 0;
    }
    private void OnEnemyInvaded(EnemyInvadeEvent invadeEvent)
    {
        existingEnemyCount--;
        heartCount -= invadeEvent.enemy.damageNum;
        if (existingEnemyCount < 0)
            existingEnemyCount = 0;
    }

    public void costGold(int amount)
    {
        goldCount -= amount;
        EventBus.Publish(new LevelGoldCostEvent(goldCount + amount, goldCount));
#if UNITY_EDITOR
        Debug.Log($"Gold spent: {amount}. Remaining gold: {goldCount}");
#endif
    }
}
