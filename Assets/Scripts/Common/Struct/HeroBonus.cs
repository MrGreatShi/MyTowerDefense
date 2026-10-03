using System.Runtime.Serialization;

public struct HeroBonus
{
    public bool applied;           //加成启用开关
    public int healthBonus;        //血量加成
    public float healthPerBonus;   //血量百分比加成
    public int attackBonus;        //攻击加成
    public float attackPerBonus;       //攻击百分比加成
    public int physicalDefenseBonus;       //物理防御加成
    public float physicalDefensePerBonus;      //物理防御百分比加成
    public int magicDefenseBonus;          //魔法防御加成
    public float magicDefensePerBonus;     //魔法防御百分比加成
    public float cooldownPerBonus;        //冷却时间加成
    public float slowingEffect;        //减速效果

    public HeroBonus(bool applied = true)
    {
        this.applied = applied;
        healthBonus = 0;
        healthPerBonus = 0f;
        attackBonus = 0;
        attackPerBonus = 0f;
        physicalDefenseBonus = 0;
        physicalDefensePerBonus = 0f;
        magicDefenseBonus = 0;
        magicDefensePerBonus = 0f;
        cooldownPerBonus = 0f;
        slowingEffect = 0f;
    }
}
