using UnityEngine;

public readonly struct EnemyInvadeEvent
{
    public readonly EnemyBase enemy;

    public EnemyInvadeEvent(EnemyBase enemy)
    {
        this.enemy = enemy;
    }
}
