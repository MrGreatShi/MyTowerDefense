using System.Collections;
using System.Collections.Generic;
using MyTowerDefense.Game;
using UnityEngine;

/// <summary>
/// Mage 终极技能的预制体行为。
/// 从指定 Cube 中心由一格范围扩展为三乘三范围，并在扩展结束后对范围内敌人结算一次魔法伤害。
/// 创建日期：2026-10-02。
/// 修改记录：初始版本。
/// </summary>
public sealed class MageUltimateEffect : MonoBehaviour
{
    [SerializeField]
    [Min(0.01f)]
    [Tooltip("特效从一格范围扩大到三乘三范围所需的时间，单位：秒。")]
    private float _expandDuration = 2f;

    [SerializeField]
    [Tooltip("特效相对中心 Cube 顶面的垂直偏移，单位：世界单位。")]
    private float _surfaceHeight = 0.05f;

    private readonly List<Transform> _affectedCubes = new List<Transform>();
    private readonly List<Transform> _occupants = new List<Transform>();

    private HeroBase _caster;
    private Coroutine _expandRoutine;

    /// <summary>
    /// 以指定 Cube 为中心初始化一次大招流程。
    /// </summary>
    /// <param name="caster">本次技能的施放者。</param>
    /// <param name="targetCube">玩家指定的中心 Cube。</param>
    public void Initialize(HeroBase caster, Transform targetCube)
    {
        if (_expandRoutine != null)
        {
            return;
        }

        CubeGridManager gridManager = CubeGridManager.Instance;
        if (caster == null || targetCube == null || gridManager == null ||
            !gridManager.TryGetCellAtWorldPosition(targetCube.position, out Vector2Int centerCell))
        {
            Destroy(gameObject);
            return;
        }

        gridManager.GetArea(
            centerCell + new Vector2Int(-1, -1),
            centerCell + new Vector2Int(1, 1),
            _affectedCubes);
        if (_affectedCubes.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        _caster = caster;

        // Cube 的 Pivot 不一定在其顶面，使用碰撞盒上边界防止特效被方块遮挡。
        float cubeSurfaceY = targetCube.position.y;
        if (targetCube.TryGetComponent(out Collider cubeCollider))
        {
            cubeSurfaceY = cubeCollider.bounds.max.y;
        }

        transform.position = new Vector3(
            targetCube.position.x,
            cubeSurfaceY + _surfaceHeight,
            targetCube.position.z);
        _expandRoutine = StartCoroutine(ExpandAndExplode());
    }

    private IEnumerator ExpandAndExplode()
    {
        Vector3 oneCellScale = transform.localScale;
        Vector3 threeByThreeScale = new Vector3(
            oneCellScale.x * 3f,
            oneCellScale.y,
            oneCellScale.z * 3f);
        float elapsed = 0f;

        while (elapsed < _expandDuration)
        {
            if (_caster == null)
            {
                Destroy(gameObject);
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / _expandDuration);
            transform.localScale = Vector3.Lerp(oneCellScale, threeByThreeScale, progress);
            yield return null;
        }

        DamageEnemiesInAffectedCubes();
        Destroy(gameObject);
    }

    /// <summary>
    /// 对扩展区域内所有存活敌人结算一次终极技能魔法伤害。
    /// </summary>
    private void DamageEnemiesInAffectedCubes()
    {
        if (_caster == null || _caster.ultimateLevel <= 0 ||
            _caster.ultimateFactor == null || _caster.ultimateLevel > _caster.ultimateFactor.Length)
        {
            return;
        }

        int damage = _caster.ultimateFactor[_caster.ultimateLevel - 1] * _caster._attack;
        foreach (Transform cube in _affectedCubes)
        {
            if (cube == null || !cube.TryGetComponent(out CubeOccupancy occupancy))
            {
                continue;
            }

            occupancy.CopyOccupantsTo(_occupants);
            foreach (Transform occupant in _occupants)
            {
                if (occupant == null || !occupant.CompareTag("Enemy"))
                {
                    continue;
                }

                IHealth enemyHealth = occupant.GetComponent<IHealth>();
                if (enemyHealth != null && enemyHealth.IsAlive())
                {
                    enemyHealth.TakeDamage(damage, AttackType.magic, _caster);
                }
            }
        }
    }
}
