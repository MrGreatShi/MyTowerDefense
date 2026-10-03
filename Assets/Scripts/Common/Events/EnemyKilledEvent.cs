using UnityEngine;

public readonly struct EnemyKilledEvent
{
    public readonly EnemyBase Enemy { get; }

    public EnemyKilledEvent(EnemyBase enemy)
    {
        Enemy = enemy;
    }
}