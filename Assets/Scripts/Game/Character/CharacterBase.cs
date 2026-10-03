using UnityEngine;

public abstract class CharacterBase : MonoBehaviour, IHealth, IAttack
{
    protected Animator anim;
    public string key;
    [Header("血量")]
    public int health;
    public int maxHealth;
    public int physicalDefense;
    public int magicDefense;
    [Header("攻击")]
    public int attack;
    public float attackRange;
    [Header("基础移速")]
    public float defaultMoveSpeed;
    public float movingSpeed;

    [Header("状态")]
    public CharacterState state;

    protected virtual void Awake()
    {
        // Animator 在对象生命周期内不变，初始化时缓存以避免对象池反复取组件。
        anim = GetComponent<Animator>();
        if (anim == null)
        {
            Debug.LogWarning($"{name} is missing an Animator component.", this);
        }
    }

    protected virtual void OnEnable()
    {
        // 对象池每次取出时只走一次重置，避免 Awake 与 OnEnable 重复初始化。
        Reset();
        Appear();
    }
    protected abstract void OnDisable();
    protected abstract void Update();
    protected abstract void Reset();            //重置状态
    protected virtual void Appear() { return; }    //出场效果
    protected abstract void TakeAction();       //行动逻辑函数
    protected virtual void Die()              //死亡效果
    {
        state = CharacterState.Dying;
        if (anim != null)
            anim.SetTrigger("Die");
    }
    public virtual void TakeDamage(int damage, AttackType attackType, IAttack attacker)
    {
        if (state == CharacterState.Dead || state == CharacterState.Dying)
            return;
        int _damage = Mathf.Max(damage - (attackType == AttackType.phsical ?
            physicalDefense : attackType == AttackType.magic ? magicDefense : 0), 1);
        health = Mathf.Max(health - _damage, 0);
    }
    public virtual void Heal(int healAmount)
    {
        health = Mathf.Min(health + healAmount, maxHealth);
    }
    public bool IsAlive()
    {
        return health > 0;
    }
    public virtual void Attack(IHealth target, int damage, AttackType attackType, IAttack attacker)
    {
        if(target == null)
        {
            Debug.LogWarning("Attack target is null.");
            return;
        }
        target.TakeDamage(damage, attackType, attacker);
    }
    /// <summary>
    /// 查找攻击目标，返回是否找到目标，并通过输出参数返回目标Transform
    /// 默认实现为在攻击范围内查找敌方单位，子类可重写此方法实现不同的目标查找逻辑
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    protected virtual bool findTarget(out Transform target)
    {
        target = null;
        RaycastHit hit;
        bool heroInRange = Physics.Raycast(
            this.transform.position + this.transform.up * 1f,
            this.transform.forward,
            out hit,
            attackRange) && isHostile(hit.collider.gameObject);

        if (heroInRange)
        {
            target = hit.transform;
            return true;
        }
        return false;
    }
    protected abstract bool isHostile(GameObject ob);

    public Vector2 Coordinate() //获取当前坐标
    {
        return new Vector2(this.transform.position.x, this.transform.position.z);
    }
    public Vector2 CoordinateInCube()   //获取当前坐标对应的方格坐标
    {
        return new Vector2(this.transform.position.x/2, this.transform.position.z/2);
    }
}
