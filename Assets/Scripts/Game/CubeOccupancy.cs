using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyTowerDefense.Game
{
    /// <summary>
    /// Game 模块的方块占用记录器，保存当前位于所属 Cube 上的英雄、建筑等对象。
    /// 创建日期：2026-09-30。
    /// 修改记录：初始版本，提供显式登记、移除和查询接口。
    /// </summary>
    public sealed class CubeOccupancy : MonoBehaviour
    {
        /// <summary>当前 Cube 是否被冻结，冻结时不允许选择。</summary>
        public bool IsFrozen = false;
        private readonly HashSet<Transform> _occupants = new HashSet<Transform>();
        private readonly List<Transform> _destroyedOccupants = new List<Transform>();
        private readonly List<int> _timedEffectEventIds = new List<int>();
        private readonly List<GameObject> _timedEffectInstances = new List<GameObject>();

        /// <summary>Cube 上仍有效的对象数量。</summary>
        public int Count
        {
            get
            {
                RemoveDestroyedOccupants();
                return _occupants.Count;
            }
        }

        /// <summary>Cube 是否至少被一个有效对象占用。</summary>
        public bool IsOccupied => Count > 0;

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
            return _occupants.Add(occupant);
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

            return _occupants.Remove(occupant);
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
            return _occupants.Contains(occupant);
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

            foreach (Transform occupant in _occupants)
            {
                results.Add(occupant);
            }
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
            _destroyedOccupants.Clear();

            foreach (Transform occupant in _occupants)
            {
                if (occupant == null)
                {
                    _destroyedOccupants.Add(occupant);
                }
            }

            foreach (Transform destroyedOccupant in _destroyedOccupants)
            {
                _occupants.Remove(destroyedOccupant);
            }
        }
    }
}
