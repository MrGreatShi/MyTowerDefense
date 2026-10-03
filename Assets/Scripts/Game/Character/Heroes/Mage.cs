using System.Collections.Generic;
using UnityEngine;
using MyTowerDefense.Game;

public class Mage : HeroBase
{
    public GameObject attackEffect;
    public GameObject aSkillEffect;
    public GameObject ultimateEffectCount;

    private int storeAttackCount = 0; // 被动技能存储的额外攻击次数

    protected override void Reset()
    {
        base.Reset();
        storeAttackCount = 0;
    }

    [SerializeField]
    [Min(0.01f)]
    [Tooltip("主动技能光波生成后保留的时间，单位：秒。")]
    private float _activeSkillEffectLifetime = 0.3f;

    [SerializeField]
    [Min(0f)]
    [Tooltip("主动技能光波与 Mage 碰撞盒右侧的水平间隔，单位：世界单位。")]
    private float _activeSkillForwardOffset = 0.6f;

    private const float ActiveSkillEffectHeight = 0.5f;
    private bool _isActiveSkillReleasePending;
    protected override void TryAttack()
    {
        for(int i = 0; i <= storeAttackCount; i++)
        {
            float xOffset = Random.Range(-0.05f, 0.05f);
            float zOffset = Random.Range(-0.05f, 0.05f);
            GameObject prefab = Instantiate(attackEffect, transform.position + Vector3.up * 0.5f + new Vector3(xOffset, 0, zOffset), Quaternion.identity);
            prefab.GetComponent<MageAttack>().mage = this;
        }
        storeAttackCount = 0;
        passiveSkillTimer = _passiveSkillCooldown;
    }
    public override bool CanOperate(ISelectable target)
    {
        if (target == null || target.SelectionRoot == null)
        {
            return false;
        }
        if (preState == HeroPreState.NoPre && target.SelectionRoot.CompareTag("Cube"))
        {
            return true;
        }
        if(preState == HeroPreState.Ultimate && target.SelectionRoot.CompareTag("Cube"))
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
        else if (preState == HeroPreState.Ultimate && target.SelectionRoot.CompareTag("Cube"))
        {
            ApplyUltimate(target);
        }
    }
    protected override bool FreeForP_Skill()
    {
        return IsAlive() && passiveSkillTimer == 0f && passiveSkillLevel > 0;
    }
    protected override void ApplyP_Skill()
    {
        storeAttackCount = Mathf.Min(storeAttackCount+1, passiveSkillFactor[passiveSkillLevel - 1]);
    }
    public override void ApplyA_Skill()
    {
        if (activeSkillLevel <= 0 || activeSkillTimer > 0f)
        {
            return;
        }

        if (anim == null)
        {
            Debug.LogWarning($"{name} 无法释放主动技能：缺少 Animator。", this);
            return;
        }

        // 当前游戏相机固定，世界 +X 即为屏幕右方；施法前统一校正 Mage 朝向。
        transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        _isActiveSkillReleasePending = true;
        anim.SetTrigger(activeSkillAnimTrigger);
        activeSkillTimer = _activeSkillCooldown;

    }

    /// <summary>
    /// 由 ASkill 动画中光波离手的 Animation Event 调用，创建向屏幕右方的视觉光波并结算右侧直线格子的敌人伤害。
    /// </summary>
    public void OnActiveSkillReleaseFrame()
    {
        if (!_isActiveSkillReleasePending)
        {
            return;
        }

        _isActiveSkillReleasePending = false;

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (gridManager == null ||
            !gridManager.TryGetCellAtWorldPosition(transform.position, out Vector2Int currentCell) ||
            activeSkillFactor == null ||
            activeSkillLevel > activeSkillFactor.Length)
        {
            return;
        }

        Vector2Int firstCell = currentCell + Vector2Int.right;
        Transform lastCube = GetLastCubeOnRightLine(gridManager, firstCell);
        if (lastCube == null)
        {
            return;
        }

        if (aSkillEffect != null)
        {
            float effectStartX = transform.position.x + _activeSkillForwardOffset;
            if (TryGetComponent(out Collider heroCollider))
            {
                effectStartX = heroCollider.bounds.max.x + _activeSkillForwardOffset;
            }

            Transform currentCube = gridManager.GetCube(currentCell.x, currentCell.y);
            float groundSurfaceY = currentCube != null && currentCube.TryGetComponent(out Collider cubeCollider)
                ? cubeCollider.bounds.max.y
                : transform.position.y;
            Vector3 effectStartPosition = new Vector3(
                effectStartX,
                groundSurfaceY + ActiveSkillEffectHeight,
                transform.position.z);
            float effectEndX = lastCube.position.x + GetCubeWidth(lastCube) * 0.5f;
            float effectLength = Mathf.Max(0.01f, effectEndX - effectStartPosition.x);
            GameObject effectInstance = Instantiate(
                aSkillEffect,
                effectStartPosition,
                Quaternion.FromToRotation(Vector3.up, Vector3.right));

            if (TryGetRendererXRange(effectInstance, out float rearX, out float frontX))
            {
                float currentLength = frontX - rearX;
                if (currentLength > 0.0001f)
                {
                    Vector3 effectScale = effectInstance.transform.localScale;
                    effectScale.y *= effectLength / currentLength;
                    effectInstance.transform.localScale = effectScale;
                }

                if (TryGetRendererXRange(effectInstance, out rearX, out _))
                {
                    effectInstance.transform.position += Vector3.right * (effectStartPosition.x - rearX);
                }
            }
            Destroy(effectInstance, _activeSkillEffectLifetime);
        }

        DamageEnemiesOnRightLine(gridManager, firstCell);
    }

    /// <summary>
    /// 沿屏幕右方逐格检查，占用记录中的所有存活敌人都会受到同一次光波伤害。
    /// </summary>
    private void DamageEnemiesOnRightLine(CubeGridManager gridManager, Vector2Int firstCell)
    {
        int damage = activeSkillFactor[activeSkillLevel - 1] * _attack;
        Vector2Int currentCell = firstCell;
        List<Transform> _activeSkillTargets = new List<Transform>();

        while (gridManager.TryGetCubeOccupancyAtCell(currentCell, out CubeOccupancy occupancy))
        {
            occupancy.CopyOccupantsTo(_activeSkillTargets);
            foreach (Transform target in _activeSkillTargets)
            {
                if (target == null || !target.CompareTag("Enemy"))
                {
                    continue;
                }

                IHealth targetHealth = target.GetComponent<IHealth>();
                if (targetHealth != null && targetHealth.IsAlive())
                {
                    targetHealth.TakeDamage(activeSkillFactor[activeSkillLevel - 1] * _attack, AttackType.magic, this);
                }
            }

            currentCell += Vector2Int.right;
        }
    }

    /// <summary>
    /// 获取右侧连续有效 Cube 中最远的一个，用于确定光波视觉范围的终点。
    /// </summary>
    private static Transform GetLastCubeOnRightLine(CubeGridManager gridManager, Vector2Int firstCell)
    {
        Transform lastCube = null;
        Vector2Int currentCell = firstCell;

        while (gridManager.TryGetCubeOccupancyAtCell(currentCell, out CubeOccupancy _))
        {
            lastCube = gridManager.GetCube(currentCell.x, currentCell.y);
            currentCell += Vector2Int.right;
        }

        return lastCube;
    }

    /// <summary>
    /// 取得 Cube 的 X 轴尺寸，以使右方光波覆盖最远 Cube 的完整右边缘。
    /// </summary>
    private static float GetCubeWidth(Transform cube)
    {
        if (cube != null && cube.TryGetComponent(out Collider cubeCollider))
        {
            return cubeCollider.bounds.size.x;
        }

        // 当前网格默认间距为 2；缺少碰撞盒时用该值防止视觉光波缩短为零。
        return 2f;
    }

    /// <summary>
    /// 取得光柱所有 Renderer 的实际 X 轴边界，避免预制体 Pivot 偏移影响右方光波的位置。
    /// </summary>
    private static bool TryGetRendererXRange(GameObject effectInstance, out float rearX, out float frontX)
    {
        rearX = float.MaxValue;
        frontX = float.MinValue;

        foreach (Renderer renderer in effectInstance.GetComponentsInChildren<Renderer>())
        {
            Bounds bounds = renderer.bounds;
            rearX = Mathf.Min(rearX, bounds.min.x);
            frontX = Mathf.Max(frontX, bounds.max.x);
        }

        return rearX <= frontX;
    }
    /// <summary>
    /// 记录玩家指定的中心 Cube，播放大招动画并从施法开始计算冷却。
    /// 实际特效创建与范围伤害由动画事件和特效组件负责，避免动画被打断时提前结算。
    /// </summary>
    public override void ApplyUltimate(ISelectable target)
    {
        if (ultimateLevel <= 0 || ultimateTimer > 0f || target == null ||
            target.SelectionRoot == null || !target.SelectionRoot.CompareTag("Cube"))
        {
            return;
        }

        if (anim == null)
        {
            Debug.LogWarning($"{name} 无法释放终极技能：缺少 Animator。", this);
            return;
        }

        if (ultimateEffectCount == null)
        {
            Debug.LogWarning($"{name} 无法释放终极技能：未配置大招特效预制体。", this);
            return;
        }

        CubeGridManager gridManager = CubeGridManager.Instance;
        Transform targetCube = target.SelectionRoot.transform;
        if (gridManager == null || !gridManager.TryGetCellAtWorldPosition(targetCube.position, out _))
        {
            return;
        }

        ultimateTimer = _ultimateCooldown;
        anim.SetTrigger(ultimateSkillAnimTrigger);

        GameObject effectInstance = Instantiate(
            ultimateEffectCount,
            targetCube.position,
            ultimateEffectCount.transform.rotation);

        MageUltimateEffect effectController = effectInstance.GetComponent<MageUltimateEffect>();
        if (effectController == null)
        {
            Debug.LogWarning(
                $"{ultimateEffectCount.name} 缺少 {nameof(MageUltimateEffect)} 组件，无法处理 Mage 大招效果。",
                effectInstance);
            Destroy(effectInstance);
            return;
        }

        effectController.Initialize(this, targetCube);
    }
}
