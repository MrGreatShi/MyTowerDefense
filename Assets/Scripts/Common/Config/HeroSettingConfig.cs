using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Hero Setting Config", fileName = "HeroSettingConfig")]
public class HeroSettingConfig : ScriptableObject
{
    public int Key;     // 英雄唯一标识符（用于存档和配置关联）
    public string heroName;         // 英雄中文显示名
    public int health;          // 英雄生命值
    public int attack;          // 英雄攻击力
    public int attackRange;     // 英雄攻击范围
    public int physicalDefense; // 英雄物理防御
    public int magicDefense;    // 英雄法术防御
    public int speed;           // 英雄移动速度
    public string passiveSkillDescription;              // 英雄被动技能描述
    public string activeSkillDescription;               // 英雄主动技能描述
    public string ultimateDescription;                  // 英雄终极技能描述
    public string[] talent_0Description = new string[2];        //英雄天赋 0 描述
    public string[] talent_1Description = new string[2];        //英雄天赋 1 描述
    public string[] talent_2Description = new string[2];        //英雄天赋 2 描述
    public string[] talent_3Description = new string[2];        //英雄天赋 3 描述

}
