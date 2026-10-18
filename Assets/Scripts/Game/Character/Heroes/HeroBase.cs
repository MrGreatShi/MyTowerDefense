using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using MyTowerDefense.Game;

public abstract class HeroBase : CharacterBase, ISelectionActionSource
{
    protected readonly string attackAnimBool = "Attack";
    protected readonly string dieAnimTrigger = "Die";
    protected readonly string activeSkillAnimTrigger = "ASkill";
    protected readonly string ultimateSkillAnimTrigger = "Ultimate";
    protected readonly string passiveSkillAnimTrigger = "PSkill";
    protected readonly string reviveAnimTrigger = "Revive";
    protected readonly string moveAnimBool = "Move";
    private readonly List<CubeOccupancy> _attackDetectionCubes = new List<CubeOccupancy>();
    private readonly List<Transform> _targetOccupants = new List<Transform>();
    [Header("被动技能相关设置")]
    public float[] passiveSkillCooldown = new float[3];
    public int[] passiveSkillFactor = new int[3];
    public int[] passiveSkillUpgradeCost = new int[3];
    [Range(0, 3)] public int passiveSkillLevel;
    protected float passiveSkillTimer = 0f;     //被动技能计时器
    [Header("主动技能相关设置")]
    public float[] activeSkillCooldown = new float[3];
    public int[] activeSkillFactor = new int[3];
    public int[] activeSkillUpgradeCost = new int[3];
    [Range(0, 3)] public int activeSkillLevel;
    protected float activeSkillTimer = 0f;  //主动技能计时器
    [Header("终极技能相关设置")]
    public float[] ultimateCooldown = new float[3];
    public int[] ultimateFactor = new int[3];
    public int[] ultimateUpgradeCost = new int[3];
    [Range(0, 3)] public int ultimateLevel;
    protected float ultimateTimer = 0f; //终极技能计时器   
    [Header("复活相关设置")]
    [SerializeField]
    protected float reviveTime  = 5f;
    protected float reviveTimer = 0f;
    public HeroPreState preState = HeroPreState.NoPre;
    public bool NeedTargetForA_Skill;
    public bool NeedTargetForUltimate;
    HeroBonus bonus = new HeroBonus();
    protected Vector3 destination;
    private Transform _currentCube;
    private CubeOccupancy _currentCubeOccupancy;
    private Vector3 _lastDetectionPosition;
    private Vector3 _lastDetectionForward;

    public float _passiveSkillCooldown => passiveSkillLevel == 0 ? 0f : 
            passiveSkillCooldown[passiveSkillLevel-1] - 
            (bonus.applied ? passiveSkillCooldown[passiveSkillLevel-1] * bonus.cooldownPerBonus : 0f);
    public float _activeSkillCooldown => activeSkillLevel == 0 ? 0f : 
            activeSkillCooldown[activeSkillLevel-1] - 
            (bonus.applied ? activeSkillCooldown[activeSkillLevel-1] * bonus.cooldownPerBonus : 0f);
    public float _ultimateCooldown => ultimateLevel == 0 ? 0f : 
            ultimateCooldown[ultimateLevel-1] - 
            (bonus.applied ? ultimateCooldown[ultimateLevel-1] * bonus.cooldownPerBonus : 0f);
    public float passiveSkillCooldownPersent => passiveSkillTimer / _passiveSkillCooldown;
    public float activeSkillCooldownPersent => activeSkillTimer / _activeSkillCooldown;
    public float ultimateCooldownPersent => ultimateTimer / _ultimateCooldown;

    public int _maxHealth => maxHealth + (bonus.applied ? bonus.healthBonus + Mathf.RoundToInt(maxHealth * bonus.healthPerBonus) : 0);
    public int _attack => attack + (bonus.applied ? bonus.attackBonus + Mathf.RoundToInt(attack * bonus.attackPerBonus) : 0);
    public int _physicalDefense => physicalDefense + (bonus.applied ? bonus.physicalDefenseBonus + Mathf.RoundToInt(physicalDefense * bonus.physicalDefensePerBonus) : 0);
    public int _magicDefense => magicDefense + (bonus.applied ? bonus.magicDefenseBonus + Mathf.RoundToInt(magicDefense * bonus.magicDefensePerBonus) : 0);

    protected override void OnEnable()
    {
        base.OnEnable();
        RefreshCubeOccupancy();
        UpdateSphere();
        passiveSkillLevel = 0;
        activeSkillLevel = 0;
        ultimateLevel = 0;
    }

    protected override void Update()
    {
        if (state == CharacterState.Dead)
        {
            reviveTimer += Time.deltaTime;
            if (reviveTimer >= reviveTime)
            {
                reviveTimer = 0f;
                Appear();
                Reset();
            }
            return;
        }
        if(health <= 0)
        {
            // 死亡动画只触发一次，状态切换由动画末帧的事件完成。
            if (state != CharacterState.Dying)
            {
                Die();
            }
            return;
        }
        CalculateTimer();
        TakeAction();
    }
    protected override void OnDisable()
    {
        ClearCubeOccupancy();
        if (anim != null)
            anim.SetBool(attackAnimBool, false);
    }
    protected override void Reset()
    {
        health = _maxHealth;
        preState = HeroPreState.NoPre;
        passiveSkillTimer = 0f;
        activeSkillTimer = 0f;
        ultimateTimer = 0f;
        reviveTimer = 0f;
    }
    protected override void Appear()
    {
        this.gameObject.SetActive(true);
        anim.SetTrigger(reviveAnimTrigger);
        state = CharacterState.Appearing;
    }
    protected void CalculateTimer()
    {
        passiveSkillTimer = Mathf.Max(0f, passiveSkillTimer - Time.deltaTime);
        activeSkillTimer = Mathf.Max(0f, activeSkillTimer - Time.deltaTime);
        ultimateTimer = Mathf.Max(0f, ultimateTimer - Time.deltaTime);
    }
    protected override void TakeAction()
    {
        if(state == CharacterState.Appearing || 
           state == CharacterState.Casting  ||
           state == CharacterState.Dying ||
           state == CharacterState.Dead)
        {
            return;
        }
        if (state == CharacterState.Moving)
        {
            if((this.transform.position - destination).sqrMagnitude <= 0.0001f)
            {
                // 精确落在目标格中心，消除到达容差造成的残余偏移。
                this.transform.position = destination;
                state = CharacterState.Idle;
                movingSpeed = 0f;
                anim.SetBool(moveAnimBool, false);
                this.transform.LookAt(this.transform.position + Vector3.right);
                RefreshCubeOccupancy();
                UpdateSphere();
            }
            else
            {
                // 目标格已在接收指令时占据，途中不登记路径格，也不因路径格占据而停在边界。
                this.transform.position = Vector3.MoveTowards(this.transform.position, destination, movingSpeed * Time.deltaTime);
                UpdateSphere();
                return;
            }
        }
        if(FreeForP_Skill())
        {
            ApplyP_Skill();
        }
        Transform transform;
        bool hasTarget = findTarget(out transform);
        if (state == CharacterState.Idle)
        {
            if (hasTarget) 
            { 
                state = CharacterState.Attacking;
                anim.SetBool(attackAnimBool, true);
            } 
        }
        if(state == CharacterState.Attacking)
        {
            if (!hasTarget) 
            { 
                state = CharacterState.Idle;
                anim.SetBool(attackAnimBool, false);
            }
        }
    }

    /// <summary>
    /// 将英雄登记到当前位置的 Cube；只有跨越方块时才会更新占用集合。
    /// </summary>
    protected void RefreshCubeOccupancy()
    {
        // 移动中占用信息表示目标格，而非表现模型当前经过的格子。
        if (state == CharacterState.Moving)
        {
            return;
        }

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

        // 新格拒绝登记时保留旧格记录；登记成功后才释放旧格。
        if (!nextCubeOccupancy.Register(transform) && !nextCubeOccupancy.Contains(transform))
        {
            return;
        }

        ClearCubeOccupancy();

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
    /// <summary>
    /// 由英雄出场动画最后一帧的 Animation Event 调用。
    /// </summary>
    public void OnAppearAnimationFinished()
    {
        state = CharacterState.Idle;
    }
    /// <summary>
    /// 由英雄死亡动画最后一帧的 Animation Event 调用，随后开始复活倒计时。
    /// </summary>
    public void OnDeathAnimationFinished()
    {
        if (state != CharacterState.Dying)
        {
            return;
        }

        state = CharacterState.Dead;
        reviveTimer = 0f;
    }
    /// <summary>判断英雄当前是否可以对目标执行操作。</summary>
    public abstract bool CanOperate(ISelectable target);

    /// <summary>
    /// 由选择系统在目标校验成功后调用，执行移动、主动技能或目标型第二技能。
    /// </summary>
    /// <param name="target">要操作的目标对象。</param>
    /// <returns>是否需要额外操作。</returns>
    public abstract void Operate(ISelectable target);

    public void MoveToPoint(float x, float y)
    {
        if (!IsFree()) return;

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null ||
            !gridManager.TryGetCubeOccupancyAtWorldPosition(new Vector3(x, 0f, y), out Transform targetCube, out CubeOccupancy occupancy) ||
            occupancy.IsFrozen || !occupancy.CanOccupy(transform))
        {
            return;
        }

        RaycastHit[] hits = Physics.RaycastAll(this.transform.position, this.transform.forward, 30f);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && (hit.collider.CompareTag("Enemy") || hit.collider.CompareTag("Obstacle")))
            {
                // 射线范围内存在敌人，本次移动行动失败
                return;
            }
        }
        // 全部指令校验结束后，先占据目标格，再释放原格；其他英雄立即能看到该占用。
        if (!occupancy.Contains(transform) && !occupancy.Register(transform))
        {
            return;
        }
        if (_currentCubeOccupancy != occupancy)
        {
            ClearCubeOccupancy();
        }
        _currentCube = targetCube;
        _currentCubeOccupancy = occupancy;
        destination = new Vector3(targetCube.position.x, transform.position.y, targetCube.position.z);
        state = CharacterState.Moving;
        if (anim != null)
        {
            anim.SetBool(moveAnimBool, true);
        }
        movingSpeed = defaultMoveSpeed;
        if ((destination - transform.position).sqrMagnitude > 0.0001f)
        {
            this.transform.LookAt(destination);
        }
    }
    public bool IsFree()
    {
        return state == CharacterState.Idle || state == CharacterState.Attacking;
    }
    public override void TakeDamage(int damage, AttackType attackType, IAttack attacker)
    {
        if (state == CharacterState.Dead || state == CharacterState.Dying)
            return;
        int _damage = Mathf.Max(damage - (attackType == AttackType.phsical ?
            _physicalDefense : attackType == AttackType.magic ? _magicDefense : 0), 1);
        health = Mathf.Max(health - _damage, 0);
    }
    public override void Heal(int healAmount)
    {
        health = Mathf.Min(health + healAmount, _maxHealth);
    }

    protected override bool isHostile(GameObject ob)
    {
        if (ob == null || !ob.CompareTag("Enemy"))
        {
            return false;
        }

        IHealth health = ob.GetComponent<IHealth>();
        return health != null && health.IsAlive();
    }

    /// <summary>
    /// 刷新英雄面朝攻击范围内的方块缓存。
    /// 外部位移、击退或传送直接修改坐标后，也应调用本方法。
    /// </summary>
    public void UpdateSphere()
    {
        _attackDetectionCubes.Clear();

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null || !gridManager.TryGetCellAtWorldPosition(transform.position, out Vector2Int currentCell))
        {
            return;
        }

        Vector2Int cellOffset = GetForwardCellOffset(transform.forward);
        Vector2Int nextCell = currentCell;
        float attackRangeSqr = attackRange * attackRange;

        while (gridManager.TryGetCubeOccupancyAtCell(nextCell, out CubeOccupancy occupancy))
        {
            Transform cube = gridManager.GetCube(nextCell.x, nextCell.y);
            Vector3 horizontalOffset = cube.position - transform.position;
            horizontalOffset.y = 0f;
            if (horizontalOffset.sqrMagnitude > attackRangeSqr)
            {
                break;
            }

            _attackDetectionCubes.Add(occupancy);
            nextCell += cellOffset;
        }

        _lastDetectionPosition = transform.position;
        _lastDetectionForward = transform.forward;
    }

    /// <summary>
    /// 在英雄正前方的攻击范围内查找最近的存活敌人，跳过地块和其他非敌人碰撞体。
    /// </summary>
    protected override bool findTarget(out Transform target)
    {
        target = null;

        if ((transform.position - _lastDetectionPosition).sqrMagnitude > 0.0001f ||
            Vector3.Dot(transform.forward, _lastDetectionForward) < 0.999f)
        {
            UpdateSphere();
        }

        float closestDistanceSqr = float.MaxValue;
        foreach (CubeOccupancy occupancy in _attackDetectionCubes)
        {
            occupancy.CopyOccupantsTo(_targetOccupants);
            foreach (Transform occupant in _targetOccupants)
            {
                if (occupant == null || !isHostile(occupant.gameObject))
                {
                    continue;
                }

                Vector3 horizontalOffset = occupant.position - transform.position;
                horizontalOffset.y = 0f;
                float distanceSqr = horizontalOffset.sqrMagnitude;
                if (distanceSqr > attackRange * attackRange || distanceSqr >= closestDistanceSqr)
                {
                    continue;
                }

                closestDistanceSqr = distanceSqr;
                target = occupant;
            }
        }

        return target != null;
    }

    private static Vector2Int GetForwardCellOffset(Vector3 forward)
    {
        if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
        {
            return forward.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }

        return forward.z >= 0f ? Vector2Int.up : Vector2Int.down;
    }
    protected virtual bool FreeForP_Skill()
    {
        return IsFree() && passiveSkillTimer == 0f;
    }
    protected virtual void ApplyP_Skill()
    {
        passiveSkillTimer = 0f;
    }
    /// <summary>
    /// 尝试对目标进行普通攻击，如果找到目标则进行攻击
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
    public virtual void ApplyA_Skill() { return; }
    public virtual void ApplyA_Skill(ISelectable target) { return ; }
    public virtual void ApplyUltimate() { return; }
    public virtual void ApplyUltimate(ISelectable target) { return ; }
}
