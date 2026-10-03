using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// 以事件数据类型为标识的全局事件总线。
/// </summary>
/// <remarks>
/// 归属模块：Framework
/// 创建日期：2026-09-28
/// 修改记录：
///   2026-09-28  初版，实现类型安全的订阅、取消订阅与发布。
/// </remarks>
public class EventBus : MonoBehaviour
{
    private static readonly Dictionary<Type, Delegate> _listeners = new Dictionary<Type, Delegate>();

    /// <summary>
    /// 订阅指定类型的事件。
    /// </summary>
    /// <typeparam name="TEvent">事件数据类型。</typeparam>
    /// <param name="listener">收到事件时执行的回调。</param>
    public static void Subscribe<TEvent>(Action<TEvent> listener)
    {
        if (listener == null)
        {
            Debug.LogError("EventBus listener cannot be null.");
            return;
        }

        Type eventType = typeof(TEvent);
        if (_listeners.TryGetValue(eventType, out Delegate existingListeners))
        {
            _listeners[eventType] = Delegate.Combine(existingListeners, listener);
            return;
        }

        _listeners.Add(eventType, listener);
    }

    /// <summary>
    /// 取消订阅指定类型的事件。
    /// </summary>
    /// <typeparam name="TEvent">事件数据类型。</typeparam>
    /// <param name="listener">此前订阅的同一回调实例。</param>
    public static void Unsubscribe<TEvent>(Action<TEvent> listener)
    {
        if (listener == null)
        {
            return;
        }

        Type eventType = typeof(TEvent);
        if (!_listeners.TryGetValue(eventType, out Delegate existingListeners))
        {
            return;
        }

        Delegate remainingListeners = Delegate.Remove(existingListeners, listener);
        if (remainingListeners == null)
        {
            _listeners.Remove(eventType);
            return;
        }

        _listeners[eventType] = remainingListeners;
    }

    /// <summary>
    /// 向所有订阅者发布事件。
    /// </summary>
    /// <typeparam name="TEvent">事件数据类型。</typeparam>
    /// <param name="eventData">要传递的事件数据。</param>
    public static void Publish<TEvent>(TEvent eventData)
    {
        if (!_listeners.TryGetValue(typeof(TEvent), out Delegate listeners))
        {
            return;
        }

        // 使用调用列表快照，允许回调在执行期间安全地取消订阅自身。
        foreach (Delegate registeredListener in listeners.GetInvocationList())
        {
            try
            {
                ((Action<TEvent>)registeredListener).Invoke(eventData);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    /// <summary>
    /// 清除所有事件订阅，通常用于完整游戏会话结束时。
    /// </summary>
    public static void Clear()
    {
        _listeners.Clear();
    }
}
