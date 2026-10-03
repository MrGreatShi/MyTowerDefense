using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 基于 Unity 时间调度延迟回调的全局事件总线。
/// </summary>
/// <remarks>
/// 归属模块：Framework
/// 创建日期：2026-09-28
/// 修改记录：
///   2026-09-28  初版，实现缩放时间与非缩放时间的延迟事件调度。
/// </remarks>
public class TimeEventBus : MonoBehaviour
{
    private sealed class ScheduledEvent
    {
        public int Id;
        public float DueTime;
        public Action Callback;
    }

    private static TimeEventBus _instance;
    private static int _nextEventId;

    private readonly List<ScheduledEvent> _scaledEvents = new List<ScheduledEvent>();
    private readonly List<ScheduledEvent> _unscaledEvents = new List<ScheduledEvent>();
    private readonly List<ScheduledEvent> _dueEvents = new List<ScheduledEvent>();

    /// <summary>
    /// 在指定延迟后执行回调。
    /// </summary>
    /// <param name="delay">延迟秒数；负数按 0 秒处理。</param>
    /// <param name="callback">到期后执行的回调。</param>
    /// <param name="useUnscaledTime">为 true 时忽略 Time.timeScale。</param>
    /// <returns>可用于取消事件的 ID；回调为空时返回 -1。</returns>
    public static int Schedule(float delay, Action callback, bool useUnscaledTime = false)
    {
        float currentTime = useUnscaledTime ? Time.unscaledTime : Time.time;
        return ScheduleAt(currentTime + Mathf.Max(delay, 0f), callback, useUnscaledTime);
    }

    /// <summary>
    /// 在指定 Unity 时间点执行回调。
    /// </summary>
    /// <param name="dueTime">目标时间；使用哪个时间轴由 useUnscaledTime 决定。</param>
    /// <param name="callback">到期后执行的回调。</param>
    /// <param name="useUnscaledTime">为 true 时 dueTime 基于 Time.unscaledTime。</param>
    /// <returns>可用于取消事件的 ID；回调为空时返回 -1。</returns>
    public static int ScheduleAt(float dueTime, Action callback, bool useUnscaledTime = false)
    {
        if (callback == null)
        {
            Debug.LogError("TimeEventBus callback cannot be null.");
            return -1;
        }

        TimeEventBus instance = EnsureInstance();
        int eventId = GetNextEventId();
        ScheduledEvent scheduledEvent = new ScheduledEvent
        {
            Id = eventId,
            DueTime = dueTime,
            Callback = callback
        };

        instance.InsertScheduledEvent(
            useUnscaledTime ? instance._unscaledEvents : instance._scaledEvents,
            scheduledEvent);
        return eventId;
    }

    /// <summary>
    /// 取消尚未执行的延迟事件。
    /// </summary>
    /// <param name="eventId">Schedule 或 ScheduleAt 返回的事件 ID。</param>
    /// <returns>找到并取消事件时为 true。</returns>
    public static bool Cancel(int eventId)
    {
        if (_instance == null || eventId < 0)
        {
            return false;
        }

        return _instance.RemoveScheduledEvent(_instance._scaledEvents, eventId)
            || _instance.RemoveScheduledEvent(_instance._unscaledEvents, eventId);
    }

    /// <summary>
    /// 清除所有尚未触发的延迟事件。
    /// </summary>
    public static void Clear()
    {
        if (_instance == null)
        {
            return;
        }

        _instance._scaledEvents.Clear();
        _instance._unscaledEvents.Clear();
        _instance._dueEvents.Clear();
    }

    private static TimeEventBus EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        GameObject eventBusObject = new GameObject(nameof(TimeEventBus));
        _instance = eventBusObject.AddComponent<TimeEventBus>();
        DontDestroyOnLoad(eventBusObject);
        return _instance;
    }

    private static int GetNextEventId()
    {
        _nextEventId++;
        if (_nextEventId <= 0)
        {
            _nextEventId = 1;
        }

        return _nextEventId;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        ExecuteDueEvents(_scaledEvents, Time.time);
        ExecuteDueEvents(_unscaledEvents, Time.unscaledTime);
    }

    private void ExecuteDueEvents(List<ScheduledEvent> scheduledEvents, float currentTime)
    {
        _dueEvents.Clear();
        while (scheduledEvents.Count > 0 && scheduledEvents[0].DueTime <= currentTime)
        {
            _dueEvents.Add(scheduledEvents[0]);
            scheduledEvents.RemoveAt(0);
        }

        foreach (ScheduledEvent scheduledEvent in _dueEvents)
        {
            try
            {
                scheduledEvent.Callback.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void InsertScheduledEvent(List<ScheduledEvent> scheduledEvents, ScheduledEvent scheduledEvent)
    {
        int insertIndex = scheduledEvents.Count;
        while (insertIndex > 0 && scheduledEvents[insertIndex - 1].DueTime > scheduledEvent.DueTime)
        {
            insertIndex--;
        }

        scheduledEvents.Insert(insertIndex, scheduledEvent);
    }

    private bool RemoveScheduledEvent(List<ScheduledEvent> scheduledEvents, int eventId)
    {
        for (int index = 0; index < scheduledEvents.Count; index++)
        {
            if (scheduledEvents[index].Id != eventId)
            {
                continue;
            }

            scheduledEvents.RemoveAt(index);
            return true;
        }

        return false;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
}
