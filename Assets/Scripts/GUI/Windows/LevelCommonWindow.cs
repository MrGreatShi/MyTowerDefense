using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public struct LevelCommonWindowArgs
{
    public int goldCount;
    public int heartCount;
    public int enemyCount;
    public LevelCommonWindowArgs(int goldCount, int heartCount, int enemyCount)
    {
        this.goldCount = goldCount;
        this.heartCount = heartCount;
        this.enemyCount = enemyCount;
    }
}
public class LevelCommonWindow : UIWindow
{
    public override string Key => "UI/LevelCommonWindow";
    public TextMeshProUGUI goldCount;
    public TextMeshProUGUI heartCount;
    public TextMeshProUGUI enemyCount;
    public override void OnCreate() { }
    public override void OnShow(object args) 
    {
        if (args is LevelCommonWindowArgs windowArgs)
        {
            goldCount.text = windowArgs.goldCount.ToString();
            heartCount.text = windowArgs.heartCount.ToString();
            enemyCount.text = windowArgs.enemyCount.ToString();
        }
        EventBus.Subscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Subscribe<EnemyInvadeEvent>(onEnemyInvaded);
        EventBus.Subscribe<LevelGoldCostEvent>(OnLevelGoldCost);
    }
    public override void OnHide() 
    {
        EventBus.Unsubscribe<EnemySpawnEvent>(OnEnemySpawned);
        EventBus.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        EventBus.Unsubscribe<EnemyInvadeEvent>(onEnemyInvaded);
        EventBus.Unsubscribe<LevelGoldCostEvent>(OnLevelGoldCost);
    }
    public override void OnDestroyWindow() { }
    private void OnEnemySpawned(EnemySpawnEvent spawnEvent)
    {
        enemyCount.text = (int.Parse(enemyCount.text) + 1).ToString();
    }

    private void OnEnemyKilled(EnemyKilledEvent killedEvent)
    {
        enemyCount.text = (int.Parse(enemyCount.text) - 1).ToString();
        goldCount.text = (int.Parse(goldCount.text) + killedEvent.Enemy.bounty).ToString();
        if (int.Parse(enemyCount.text) < 0)
            enemyCount.text = "0";
    }
    private void onEnemyInvaded(EnemyInvadeEvent invadeEvent)
    {
        enemyCount.text = (int.Parse(enemyCount.text) - 1).ToString();
        heartCount.text = (int.Parse(heartCount.text) - invadeEvent.enemy.damageNum).ToString();
        if (int.Parse(enemyCount.text) < 0)
            enemyCount.text = "0";
    }
    private void OnLevelGoldCost(LevelGoldCostEvent costEvent)
    {
        goldCount.text = costEvent.currentGold.ToString();
    }
}
