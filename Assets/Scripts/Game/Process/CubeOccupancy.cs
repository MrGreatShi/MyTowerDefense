using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// Game 模块的方块占用记录器，保存当前位于所属 Cube 上的英雄、建筑等对象。
    /// 创建日期：2026-09-30。
    /// 修改记录：2026-10-08，停留单位独占一个槽位，敌人使用列表保存，保持技能查询兼容。
    /// </summary>
    public sealed class CubeOccupancy : MonoBehaviour
    {
        /// <summary>当前 Cube 是否被冻结，冻结时不允许选择。</summary>
        public bool IsFrozen = false;
        private Transform _stationaryOccupant;
        private readonly List<Transform> _enemies = new List<Transform>();

        /// <summary>独占地块的英雄、建筑或障碍物；敌人不占用此槽位。</summary>
        public Transform StationaryOccupant => _stationaryOccupant;
        private readonly List<int> _timedEffectEventIds = new List<int>();
        private readonly List<GameObject> _timedEffectInstances = new List<GameObject>();

        /// <summary>仅停留单位阻止其他停留单位占据该格；敌人数量不影响此属性。</summary>
        public bool IsOccupied => _stationaryOccupant != null;

        /// <summary>全部有效单位数量；敌人数量不会影响 IsOccupied。</summary>
        public int Count
        {
            get
            {
                RemoveDestroyedOccupants();
                return _enemies.Count + (IsOccupied ? 1 : 0);
            }
        }

        /// <summary>检查停留单位能否占据该格；同一对象重复检查仍可通过。</summary>
        public bool CanOccupy(Transform occupant)
        {
            return occupant != null && (!IsOccupied || _stationaryOccupant == occupant);
        }

        /// <summary>复制所有有效敌人，避免向外暴露可修改的内部列表。</summary>
        public void CopyEnemiesTo(List<Transform> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }
            RemoveDestroyedOccupants();
            results.Clear();
            results.AddRange(_enemies);
        }

        /// <summary>
        /// 将对象登记为占用当前 Cube。
        /// </summary>
        /// <param name="occupant">要登记的英雄、建筑或其他场景对象 Transform。</param>
        /// <returns>对象首次登记时返回 true；已登记或参数为空时返回 false。</returns>
        public bool Register(Transform occupant)
        {
            if (occupant == null)
            {
                return false;
            }

            RemoveDestroyedOccupants();
            if (occupant.CompareTag("Enemy"))
            {
                if (_enemies.Contains(occupant))
                {
                    return false;
                }
                _enemies.Add(occupant);
                return true;
            }

            // 非敌人作为停留单位处理，无需为每种建筑额外定义 Tag。
            if (!CanOccupy(occupant) || _stationaryOccupant == occupant)
            {
                return false;
            }
            _stationaryOccupant = occupant;
            return true;
        }

        /// <summary>
        /// 移除对象在当前 Cube 的占用登记。
        /// </summary>
        /// <param name="occupant">要移除的对象 Transform。</param>
        /// <returns>成功移除时返回 true。</returns>
        public bool Unregister(Transform occupant)
        {
            if (occupant == null)
            {
                return false;
            }

            // 按引用移除，不依赖对象当前 Tag，避免运行时换 Tag 导致登记残留。
            if (_stationaryOccupant == occupant)
            {
                _stationaryOccupant = null;
                return true;
            }
            return _enemies.Remove(occupant);
        }

        /// <summary>
        /// 判断对象是否登记在当前 Cube 上。
        /// </summary>
        /// <param name="occupant">待判断的对象 Transform。</param>
        /// <returns>已登记且对象仍有效时返回 true。</returns>
        public bool Contains(Transform occupant)
        {
            if (occupant == null)
            {
                return false;
            }

            RemoveDestroyedOccupants();
            return _stationaryOccupant == occupant || _enemies.Contains(occupant);
        }

        /// <summary>
        /// 将当前所有有效占用对象复制到调用方提供的列表中，避免查询时创建临时集合。
        /// </summary>
        /// <param name="results">用于接收结果的列表；调用前会被清空。</param>
        public void CopyOccupantsTo(List<Transform> results)
        {
            if (results == null)
            {
                throw new System.ArgumentNullException(nameof(results));
            }

            RemoveDestroyedOccupants();
            results.Clear();

            if (_stationaryOccupant != null)
            {
                results.Add(_stationaryOccupant);
            }
            results.AddRange(_enemies);
        }

        /// <summary>
        /// 在当前 Cube 登记一个持续效果；首次立即触发，后续触发和结束均由 TimeEventBus 调度。
        /// </summary>
        /// <param name="caster">本次效果的施放者。</param>
        /// <param name="effectFunc">每次触发时执行的效果函数。</param>
        /// <param name="duration">效果总持续时间，单位：秒。</param>
        /// <param name="interval">效果触发间隔，单位：秒。</param>
        /// <param name="effectInstance">附着在当前 Cube 的可选特效实例。</param>
        public void AddTimedEffect(
            HeroBase caster,
            Action<CubeOccupancy, HeroBase> effectFunc,
            float duration,
            float interval,
            GameObject effectInstance)
        {
            if (caster == null || effectFunc == null || duration <= 0f || interval <= 0f)
            {
                if (effectInstance != null)
                {
                    Destroy(effectInstance);
                }

                return;
            }

            int triggerCount = Mathf.Max(1, Mathf.CeilToInt(duration / interval));
            int triggeredCount = 0;
            List<int> effectEventIds = new List<int>();
            Action finishEffect = null;
            Action triggerEffect = null;

            finishEffect = () =>
            {
                foreach (int eventId in effectEventIds)
                {
                    TimeEventBus.Cancel(eventId);
                    _timedEffectEventIds.Remove(eventId);
                }

                if (effectInstance != null)
                {
                    Destroy(effectInstance);
                    _timedEffectInstances.Remove(effectInstance);
                }
            };

            triggerEffect = () =>
            {
                if (this == null || !isActiveAndEnabled || caster == null)
                {
                    finishEffect();
                    return;
                }

                effectFunc.Invoke(this, caster);
                triggeredCount++;

                if (triggeredCount >= triggerCount)
                {
                    return;
                }

                int nextTriggerEventId = TimeEventBus.Schedule(interval, triggerEffect);
                effectEventIds.Add(nextTriggerEventId);
                _timedEffectEventIds.Add(nextTriggerEventId);
            };

            if (effectInstance != null)
            {
                _timedEffectInstances.Add(effectInstance);
            }

            triggerEffect.Invoke();

            int finishEventId = TimeEventBus.Schedule(duration, finishEffect);
            effectEventIds.Add(finishEventId);
            _timedEffectEventIds.Add(finishEventId);
        }

        private void OnDisable()
        {
            foreach (int eventId in _timedEffectEventIds)
            {
                TimeEventBus.Cancel(eventId);
            }

            _timedEffectEventIds.Clear();

            foreach (GameObject effectInstance in _timedEffectInstances)
            {
                if (effectInstance != null)
                {
                    Destroy(effectInstance);
                }
            }

            _timedEffectInstances.Clear();
        }

        private void RemoveDestroyedOccupants()
        {
            if (_stationaryOccupant == null)
            {
                _stationaryOccupant = null;
            }
            for (int index = _enemies.Count - 1; index >= 0; index--)
            {
                if (_enemies[index] == null)
                {
                    _enemies.RemoveAt(index);
                }
            }
        }
    }
}
