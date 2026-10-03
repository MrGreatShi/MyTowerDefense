using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine;

public class RunSession : MonoBehaviour
{
    public static RunSession Instance { get; private set; }
    [Header("关卡信息")]
    [SerializeField]
    LevelConfig level;
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
    private ISelectable _selectedObject;
    private ISelectionActionSource _selectedActionSource;
    public int GoldCount => goldCount;
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        addressablesLoader = new AddressablesLoader<GameObject>();
    }

    private async void Start()
    {
        await InitializeGameAsync();
    }

    void OnEnable()
    {
        EventBus.Subscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<EnemyInvadeEvent>(OnEnemyInvaded);
        EventBus.Subscribe<ObjectSelectedEvent>(OnObjectSelected);
        EventBus.Subscribe<ObjectSelectionCancelledEvent>(OnObjectSelectionCancelled);
    }

    void OnDisable()
    {
        EventBus.Unsubscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<EnemyInvadeEvent>(OnEnemyInvaded);
        EventBus.Unsubscribe<ObjectSelectedEvent>(OnObjectSelected);
        EventBus.Unsubscribe<ObjectSelectionCancelledEvent>(OnObjectSelectionCancelled);
    }
    void OnDestroy()
    {
        ClearSelection();
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

    private async Task InitializeGameAsync()
    {
        if (level == null)
        {
            Debug.LogError("RunSession requires a LevelConfig.", this);
            enabled = false;
            return;
        }

        // 上一局可能暂停过时间；新关卡初始化时必须恢复正常时间轴。
        Time.timeScale = 1f;
        state = LevelState.Prepare;

        if (!await LoadObjectPoolsAsync())
        {
            enabled = false;
            return;
        }

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
            return;
        }

        await GUIManager.Instance.ShowAsync<LevelCommonWindow>(
            "UI/LevelCommonWindow",
            new LevelCommonWindowArgs(goldCount, heartCount, existingEnemyCount));

        state = LevelState.Running;
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

    /// <summary>
    /// 根据当前操作源是否为空判断点击对象是第一目标还是第二目标。
    /// </summary>
    private async void OnObjectSelected(ObjectSelectedEvent selectionEvent)
    {
        if (selectionEvent == null || selectionEvent.Selectable == null)
        {
            return;
        }

        if(_selectedObject == null)     //当前无选择对象
        {
            await SelectObject(selectionEvent.Selectable);
        }
        else if(_selectedActionSource == null)     //当前选择对象无法额外操作，对其进行替换
        {
            ClearSelection();
            await SelectObject(selectionEvent.Selectable);
        }
        else                       //当前选择对象可额外操作，尝试执行第二目标操作
        {
            if (_selectedActionSource.CanOperate(selectionEvent.Selectable))             //当前选择对象可额外操作,尝试操作
            {
                _selectedActionSource.Operate(selectionEvent.Selectable);
                ClearSelection();
            }
            else                                         //当前选择对象不可额外操作，替换选择对象
            {
                ClearSelection();
                await SelectObject(selectionEvent.Selectable);
            }
        }
    }
    private void OnObjectSelectionCancelled(ObjectSelectionCancelledEvent cancelEvent)
    {
        ClearSelection();
    }
    private async Task SelectObject(ISelectable selectable)     //选择对象,并处理逻辑
    {
        if (selectable == null)
        {
            return;
        }
        _selectedObject = selectable;
        _selectedObject.AddVisualState(SelectionVisualState.Selected);
        selectable.SelectionRoot.TryGetComponent<ISelectionActionSource>(out _selectedActionSource);
        if(_selectedObject.SelectionRoot.CompareTag("Hero"))
        {
            // 处理英雄选择的逻辑
            await GUIManager.Instance.ShowAsync<LevelHeroWindow>("UI/LevelHeroWindow", _selectedObject.SelectionRoot.GetComponent<HeroBase>());
        }
        else if(_selectedObject.SelectionRoot.CompareTag("Enemy"))
        {
            // 处理敌人选择的逻辑
        }
        else
        {
            // 处理其他类型选择的逻辑
        }
    }
    private void ClearSelection()         //清除选择对象,并处理逻辑
    {
        ISelectable selectedObject = _selectedObject;
        _selectedObject = null;
        _selectedActionSource = null;

        if (selectedObject == null)
        {
            return;
        }

        if (selectedObject is UnityEngine.Object unityObject && unityObject == null)
        {
            return;
        }

        Transform selectionRoot = selectedObject.SelectionRoot;
        if (selectionRoot == null)
        {
            return;
        }

        if (selectionRoot.CompareTag("Hero"))
        {
            if (GUIManager.Instance != null)
            {
                GUIManager.Instance.Hide("UI/LevelHeroWindow");
            }
        }
        else if (selectionRoot.CompareTag("Enemy"))
        {
            // 处理敌人取消选择的逻辑
        }
        else
        {

        }
        selectedObject.RemoveVisualState(SelectionVisualState.Selected);
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
