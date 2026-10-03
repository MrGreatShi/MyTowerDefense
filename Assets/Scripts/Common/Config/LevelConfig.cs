using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level Config", fileName = "LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [Header("基础信息")]
    public int levelId = 1;
    public string levelName;
    public string levelDescription;
    public LevelType levelType;
    public int heartCount = 3;     // 关卡生命值（玩家失败条件）

    public int originalGold = 100;   // 初始金币数量

    [Header("场景")]
    public string sceneName; // 场景名称（必须与实际场景名匹配）

    [Header("下一关")]
    public int nextLevelId = 0; // 0=没有下一关

    [Header("敌人设置")]
    public string BOSSName = null;      // BOSS名称（必须与实际敌人预制体名匹配）, 非BOSS关卡可为空
    public List<string> enemies;
    public List<EnemyWave> waves = new List<EnemyWave>();
}
[Serializable]
public struct EnemyWave
{
    public float waveStartTime; // 波次开始时间（相对于关卡开始的时间）
    public string enemyName;    // 敌人名称（必须与实际敌人预制体名匹配）
    public int count;       // 生成数量
}