/* 담당자 - 송태훈
싱글톤의 결합도를 낮추고 서비스 인스턴스를 중앙에서 바인딩·조회하는 정적 서비스 로케이터
Global/Local 수명 주기를 구분 등록하여, 씬 전환 시 씬 종속(Local) 서비스만 안전하게 일괄 해제
*/

using System.Collections.Generic;
using System;
using Util = DebugLogger;

public enum SceneId
{
   
}
public enum ServiceLifetime
{
    Global, // 싱글톤
    Local   // 로컬
}

public static class ServiceLocator
{
    private class ServiceEntry
    {
        public object Instance { get; set; }
        public ServiceLifetime Lifetime { get; set; }
    }

    private static readonly Dictionary<Type, ServiceEntry> _services = new();

    /// <summary>
    /// 지정한 타입(T)의 인스턴스를 수명 주기(Global / Local)와 함께 딕셔너리에 등록
    /// 이미 등록된 동일 타입이 존재할 경우 경고 로그를 남기고 인스턴스를 갱신
    /// </summary>
    /// <param name="service"> 등록할 서비스 </param>
    /// <param name="lifetime"> 전역 or 로컬 </param>
    public static void Register<T>(T service, ServiceLifetime lifetime = ServiceLifetime.Global) where T : class
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
        {
            Util.LogWarningWithTag("ServiceLocator", $"이미 등록된 서비스");
        }

        _services[type] = new ServiceEntry()
        {
            Instance = service,
            Lifetime = lifetime
        };
    }

    /// <summary>
    /// 등록된 특정 서비스 타입(T)을 딕셔너리에서 수동으로 제거
    /// </summary>
    public static void Unregister<T>() where T : class
    {
        _services.Remove(typeof(T));
    }

    /// <summary>
    /// 등록된 서비스 인스턴스를 조회하여 반환하며, 등록되지 않은 서비스인 경우 에러 로그를 남기고 null을 반환
    /// </summary>
    public static T Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var entry)
            )
        {
            return entry.Instance as T;
        }

        Util.LogErrorWithTag("ServiceLocator", $"등록되지 않은 서비스 요청: {typeof(T).Name}");
        return null;
    }

    /// <summary>
    /// 등록된 서비스 조회를 시도하여 성공 시 true와 인스턴스를 반환하고, 실패 시 에러 로그와 함께 false를 반환
    /// </summary>
    public static bool TryGet<T>(out T service) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var entry))
        {
            service = entry.Instance as T;
            return true;
        }

        Util.LogErrorWithTag("ServiceLocator", $"{typeof(T).Name}을 찾을 수 없음");
        service = null;
        return false;
    }

    /// <summary>
    /// 씬 전환 시 호출되어 전역(Global) 서비스는 유지하고 Local 수명 주기를 가진 서비스들만 일괄 해제
    /// </summary>
    public static void ClearSceneLocalServices()
    {
        List<Type> toRemove = new();
        foreach (var pair in _services)
        {
            if (pair.Value.Lifetime == ServiceLifetime.Local)
            {
                toRemove.Add(pair.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            _services.Remove(toRemove[i]);
        }
    }
}