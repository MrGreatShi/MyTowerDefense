using UnityEngine;
using MyTowerDefense.Game;

public abstract class EnemyBase : CharacterBase
{
    private const float HitFlashDuration = 0.3f;
    private static readonly Color HitFlashColor = Color.red;

    protected readonly string attackAnimBool = "Attack";
    protected readonly string dieAnimTrigger = "Die";
    protected PooledObject pooledObject;
    [SerializeField]
    [Min(0f)]
    [Tooltip("敌人目标射线相对脚底的高度，单位：世界单位；用于避开地面 Cube 碰撞体。")]
    private float _targetDetectionHeight = 1f;

    private readonly RaycastHit[] _targetHitBuffer = new RaycastHit[16];
    private SelectionHighlighter _hitHighlighter;
    private Transform _currentCube;
    private CubeOccupancy _currentCubeOccupancy;
    [Header("扣除生命值")]
    public int damageNum;
    [Header("赏金")]
    public int bounty;
    EnemyBonus bonus = new EnemyBonus();

    public int _maxHealth => maxHealth + (bonus.applied ? bonus.healthBonus + Mathf.RoundToInt(maxHealth * bonus.healthPerBonus) : 0);
    public int _attack => attack + (bonus.applied ? bonus.attackBonus + Mathf.RoundToInt(attack * bonus.attackPerBonus) : 0);
    public int _physicalDefense => physicalDefense + (bonus.applied ? bonus.physicalDefenseBonus + Mathf.RoundToInt(physicalDefense * bonus.physicalDefensePerBonus) : 0);
    public int _magicDefense => magicDefense + (bonus.applied ? bonus.magicDefenseBonus + Mathf.RoundToInt(magicDefense * bonus.magicDefensePerBonus) : 0);

    protected override void OnEnable()
    {
        // 预加载对象首次 Instantiate 时可能尚未挂载 PooledObject，后续激活时再补充缓存。
        if (pooledObject == null)
        {
            pooledObject = GetComponent<PooledObject>();
        }

        base.OnEnable();
        movingSpeed = defaultMoveSpeed;
        state = CharacterState.Moving;
        RefreshCubeOccupancy();
    }

    protected override void Awake()
    {
        base.Awake();
        _hitHighlighter = GetComponent<SelectionHighlighter>();
    }

    protected override void Update()
    {
        if(this.transform.position.x < -1.5f)   //敌人抵达终点
        {
            EventBus.Publish<EnemyInvadeEvent>(new EnemyInvadeEvent(this));
            DespawnToPool();
            return;
        }

        if (health <= 0)        //敌人死亡，进入死亡状态
        {
            if(state != CharacterState.Dying && state != CharacterState.Dead)
            {
                EventBus.Publish<EnemyKilledEvent>(new EnemyKilledEvent(this));
                Die();
            }
            return;
        }
        TakeAction();           //敌人AI行动逻辑
    }
    protected override void OnDisable()
    {
        ClearCubeOccupancy();
        movingSpeed = defaultMoveSpeed;
        if (anim != null)
            anim.SetBool(attackAnimBool, false);
    }
    protected override void Reset()
    {
        this.transform.position = GetSpawnPosition();
        this.transform.rotation = Quaternion.Euler(0, -90, 0);
        health = maxHealth;
        if (anim != null)
        {
            anim.SetBool(attackAnimBool, false);
        }
    }
    protected override void TakeAction()        //敌人AI行动逻辑
    {
        Transform target;
        if (findTarget(out target))
        {
            state = CharacterState.Attacking;
            movingSpeed = 0f;

            if (anim != null)
            {
                anim.SetBool(attackAnimBool, true);
            }

            return;
        }

        state = CharacterState.Moving;
        movingSpeed = defaultMoveSpeed;

        if (anim != null)
        {
            anim.SetBool(attackAnimBool, false);
        }

        this.transform.position -= new Vector3(movingSpeed * Time.deltaTime, 0f, 0f);
        RefreshCubeOccupancy();
    }

    /// <summary>
    /// 将敌人登记到当前位置的 Cube；只有跨越方块时才会修改占用集合。
    /// </summary>
    protected void RefreshCubeOccupancy()
    {
        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null ||
            !gridManager.TryGetCubeOccupancyAtWorldPosition(
                transform.position,
                out Transform nextCube,
                out CubeOccupancy nextCubeOccupancy))
        {
            ClearCubeOccupancy();
            return;
        }

        if (nextCube == _currentCube)
        {
            return;
        }

        ClearCubeOccupancy();
        if (!nextCubeOccupancy.Register(transform))
        {
            return;
        }

        _currentCube = nextCube;
        _currentCubeOccupancy = nextCubeOccupancy;
    }

    private void ClearCubeOccupancy()
    {
        if (_currentCubeOccupancy != null)
        {
            _currentCubeOccupancy.Unregister(transform);
        }

        _currentCube = null;
        _currentCubeOccupancy = null;
    }

    protected override bool findTarget(out Transform target)
    {
        target = null;
        Vector3 origin = transform.position + Vector3.up * _targetDetectionHeight;
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            transform.forward,
            _targetHitBuffer,
            attackRange,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Collide);

        float closestDistance = float.MaxValue;
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit hit = _targetHitBuffer[index];
            if (hit.collider == null || !isHostile(hit.collider.gameObject) || hit.distance >= closestDistance)
            {
                continue;
            }

            closestDistance = hit.distance;
            target = hit.transform;
        }

        return target != null;
    }
    /// <summary>
    /// 由敌人死亡动画最后一帧的 Animation Event 调用，将敌人归还对象池。
    /// </summary>
    public void OnDeathAnimationFinished()
    {
        if (state != CharacterState.Dying)
        {
            return;
        }

        state = CharacterState.Dead;
        DespawnToPool();
    }

    private void DespawnToPool()
    {
        if (pooledObject != null)
        {
            pooledObject.Despawn();
            return;
        }

        // 场景中手动放置且未使用对象池的敌人仍可安全销毁表现。
        gameObject.SetActive(false);
    }
    public override void TakeDamage(int damage, AttackType attackType, IAttack attacker)
    {
        if (state == CharacterState.Dead || state == CharacterState.Dying)
            return;
        int _damage = Mathf.Max(damage - (attackType == AttackType.phsical ?
            _physicalDefense : attackType == AttackType.magic ? _magicDefense : 0), 1);
        health = Mathf.Max(health - _damage, 0);

        if (health <= 0)
        {
            EventBus.Publish<EnemyKilledEvent>(new EnemyKilledEvent(this));
            Die();
            return;
        }

        if (_hitHighlighter != null)
        {
            _hitHighlighter.Flash(HitFlashColor, HitFlashDuration);
        }
    }
    public override void Heal(int healAmount)
    {
        health = Mathf.Min(health + healAmount, _maxHealth);
    }
    protected override bool isHostile(GameObject ob)
    {
        if (ob == null || !ob.CompareTag("Hero"))
        {
            return false;
        }

        IHealth health = ob.GetComponent<IHealth>();
        return health != null && health.IsAlive();
    }

    /// <summary>
    /// 获取敌人生成位置，默认在地图右侧随机生成，子类可重写此方法实现不同的生成逻辑
    /// </summary>
    /// <returns></returns>
    protected virtual Vector3 GetSpawnPosition()
    {
        int randomZ = Random.Range(0, 6);
        return new Vector3(19, 0, 2 * (float)randomZ);
    }
    /// <summary>
    /// 尝试攻击目标，如果找到目标则进行攻击
    /// 默认无子弹特效对面前攻击范围内的敌人进行攻击，子类可重写此方法实现不同的攻击逻辑
    /// </summary>
    protected virtual void TryAttack()
    {
        Transform transform;
        bool hasTarget = findTarget(out transform);
        if (hasTarget)
        {
            Attack(transform.GetComponent<IHealth>(), _attack, AttackType.phsical, this);
        }
    }
}
