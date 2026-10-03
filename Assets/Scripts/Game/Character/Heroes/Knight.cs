using System.Collections.Generic;
using UnityEngine;
using MyTowerDefense.Game;

public class Knight : HeroBase
{
    public GameObject ultimateEffect;
    [SerializeField]
    [Min(0.01f)]
    private float _ultimateDuration = 5f;

    [SerializeField]
    [Min(0.01f)]
    private float _ultimateInterval = 1f;

    [SerializeField]
    [Tooltip("关闭时，被冻结的 Cube 不会获得终极技能效果。")]
    private bool _ultimateCanApplyToFrozenCube = true;
    private bool _isActiveSkillHitPending;
    /// <summary>
    /// Knight 的主动技能和终极技能都不需要目标，直接在自身范围内释放。
    /// 只需要判断移动指令
    /// 未进入技能预备状态时，继续沿用对 Cube 的移动操作。
    /// </summary>
    public override bool CanOperate(ISelectable target)
    {
        if(target == null || target.SelectionRoot == null)
        {
            return false;
        }
        if(preState == HeroPreState.NoPre && target.SelectionRoot.CompareTag("Cube"))
        {
            return true;
        }
        return false;
    }
    public override void Operate(ISelectable target)
    {
        if (preState == HeroPreState.NoPre && target.SelectionRoot.CompareTag("Cube"))
        {
            MoveToPoint(target.SelectionRoot.transform.position.x, target.SelectionRoot.transform.position.z);
        }
    }   
    protected override bool FreeForP_Skill()
    {
        return IsAlive() && passiveSkillTimer == 0f && health <= 0.5 * _maxHealth && passiveSkillLevel > 0;
    }
    protected override void ApplyP_Skill()
    {
        health += passiveSkillFactor[passiveSkillLevel - 1] * _maxHealth / 100;
        passiveSkillTimer = _passiveSkillCooldown;
    }
    public override void ApplyA_Skill()
    {
        if (activeSkillTimer > 0f)
        {
            return;
        }

        if (anim == null)
        {
            Debug.LogWarning($"{name} 无法释放主动技能：缺少 Animator。", this);
            return;
        }

        _isActiveSkillHitPending = true;
        anim.SetTrigger(activeSkillAnimTrigger);
        activeSkillTimer = _activeSkillCooldown;
    }

    /// <summary>
    /// 由 ASkill 动画劈砍命中帧的 Animation Event 调用，结算当前格与前方一格的伤害。
    /// </summary>
    public void OnActiveSkillHitFrame()
    {
        if (!_isActiveSkillHitPending)
        {
            return;
        }

        _isActiveSkillHitPending = false;

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null || !gridManager.TryGetCellAtWorldPosition(transform.position, out Vector2Int currentCell))
        {
            return;
        }

        DamageEnemiesOnCube(gridManager, currentCell);
        DamageEnemiesOnCube(gridManager, currentCell + GetForwardCellOffset(transform.forward));
    }

    private void DamageEnemiesOnCube(CubeGridManager gridManager, Vector2Int cell)
    {
        if (!gridManager.TryGetCubeOccupancyAtCell(cell, out CubeOccupancy occupancy))
        {
            return;
        }
        List<Transform> _activeSkillTargets = new List<Transform>();
        occupancy.CopyOccupantsTo(_activeSkillTargets);
        foreach (Transform target in _activeSkillTargets)
        {
            if (target == null || !target.CompareTag("Enemy"))
            {
                continue;
            }

            IHealth health = target.GetComponent<IHealth>();
            if (health == null || !health.IsAlive())
            {
                continue;
            }

            health.TakeDamage(activeSkillFactor[activeSkillLevel-1] * _attack, AttackType.phsical, this);
        }
    }

    private static Vector2Int GetForwardCellOffset(Vector3 forward)
    {
        if (Mathf.Abs(forward.x) >= Mathf.Abs(forward.z))
        {
            return forward.x >= 0f ? Vector2Int.right : Vector2Int.left;
        }

        return forward.z >= 0f ? Vector2Int.up : Vector2Int.down;
    }

    public override void ApplyUltimate()
    {
        if (ultimateTimer > 0f)
        {
            return;
        }

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null || !gridManager.TryGetCellAtWorldPosition(transform.position, out Vector2Int currentCell))
        {
            return;
        }
        List<Transform> _ultimateAffectedCubes = new List<Transform>();
        gridManager.GetArea(currentCell + new Vector2Int(-1, -1), currentCell + new Vector2Int(1, 1), _ultimateAffectedCubes);
        gridManager.ApplyCubeEffect(
            _ultimateAffectedCubes,
            this,
            UltimateEffect,
            _ultimateDuration,
            _ultimateInterval,
            ultimateEffect,
            _ultimateCanApplyToFrozenCube);

        ultimateTimer = _ultimateCooldown;
    }

    /// <summary>
    /// Knight 终极技能的单格结算：伤害当前格敌人，并治疗英雄。
    /// </summary>
    private void UltimateEffect(CubeOccupancy occupancy, HeroBase caster)
    {
        List<Transform> _ultimateTargets = new List<Transform>();
        occupancy.CopyOccupantsTo(_ultimateTargets);
        foreach (Transform target in _ultimateTargets)
        {
            if (target == null)
            {
                continue;
            }

            if (target.CompareTag("Enemy"))
            {
                IHealth enemyHealth = target.GetComponent<IHealth>();
                if (enemyHealth != null && enemyHealth.IsAlive())
                {
                    enemyHealth.TakeDamage(ultimateFactor[ultimateLevel-1], AttackType.phsical, this);
                }

                continue;
            }

            if (target.CompareTag("Hero"))
            {
                caster.Heal(ultimateFactor[ultimateLevel-1]);
            }
        }
    }
}
