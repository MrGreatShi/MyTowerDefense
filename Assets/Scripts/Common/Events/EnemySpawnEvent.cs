using UnityEngine;

/// <summary>
/// 敌人从对象池成功取出并激活时发布的事件。
/// </summary>
/// <remarks>
/// 归属模块：Common.Events
/// 创建日期：2026-09-28
/// 修改记录：
///   2026-09-28  初版，用于同步关卡中的存活敌人数量。
/// </remarks>
public readonly struct EnemySpawnEvent
{
    /// <summary>
    /// 已成功生成并激活的敌人对象。
    /// </summary>
    public GameObject Enemy { get; }

    /// <summary>
    /// 创建该敌人的关卡配置 Key。
    /// </summary>
    public string EnemyKey { get; }

    /// <summary>
    /// 初始化敌人生成事件。
    /// </summary>
    /// <param name="enemy">已成功生成的敌人对象。</param>
    /// <param name="enemyKey">敌人对应的关卡配置 Key。</param>
    public EnemySpawnEvent(GameObject enemy, string enemyKey)
    {
        Enemy = enemy;
        EnemyKey = enemyKey;
    }
}
