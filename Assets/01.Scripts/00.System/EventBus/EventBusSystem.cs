//using System;
using System.Collections.Generic;
using Util = DebugLogger;

/// <summary>
/// 프로젝트 내에서 이벤트 발행/구독 모델을 통해 시스템 간 결합도를 낮추는 이벤트 버스 시스템 
/// </summary>
public static class EventBusSystem
{
    // Event 타입별 매핑 딕셔너리
    private static readonly Dictionary<System.Type, System.Delegate> _eventHandlers = new Dictionary<System.Type, System.Delegate>();

    /// <summary>
    /// 특정 이벤트 타입에 대한 리스너 등록
    /// </summary>
    public static void Subscribe<T>(System.Action<T> listener) where T : struct, IEventType
    {
        System.Type eventType = typeof(T);

        if (_eventHandlers.TryGetValue(eventType, out System.Delegate existingDelegate))
        {
            _eventHandlers[eventType] = System.Delegate.Combine(existingDelegate, listener);
        }
        else
        {
            _eventHandlers.Add(eventType, listener);
        }
        Util.LogWithTag("EventBusSystem", $"[구독 등록] {eventType.Name} -> {listener.Method.Name}");
    }

    /// <summary>
    /// 등록된 리스너 해제
    /// </summary>
    public static void Unsubscribe<T>(System.Action<T> listener) where T : struct , IEventType
    {
        System.Type eventType = typeof (T);

        if(_eventHandlers.TryGetValue(eventType, out System.Delegate existingDelegate))
        {
            System.Delegate newDelegate = System.Delegate.Remove(existingDelegate, listener);

            if(newDelegate == null)
            {
                _eventHandlers.Remove(eventType);
            }
            else
            {
                _eventHandlers[eventType] = newDelegate;
            }
        }
        DebugLogger.LogWithTag("EventBusSystem", $"[구독 해제] {eventType.Name} -> {listener.Method.Name}");
    }

    public static void Publish<T>(in T eventData) where T : struct , IEventType
    {
        System.Type eventType = typeof(T);

        if(_eventHandlers.TryGetValue(eventType, out System.Delegate targetDelegate))
        {
            if(targetDelegate is System.Action<T> callback)
            {
                callback?.Invoke(eventData);
                DebugLogger.LogWithTag("EventBusSystem", $"[이벤트 발행] {eventType.Name}");
            }
        }
    }

    public static void ClearAllSubsribers()
    {
        _eventHandlers.Clear();
        DebugLogger.LogWithTag("EventBusSystem", $"[이벤트 전제 구독 정리]");
    }
}
