// ============================================================================
// 逃离魔塔 - 服务定位器 (ServiceLocator)
// 轻量级依赖注入替代方案。严禁 Singleton 满天飞和 GameObject.Find。
// 所有核心 Manager 在 GameBootstrapper.Awake() 中统一注册。
//
// 来源：DesignDocs/13_Architecture_and_Operations_SLA.md（解耦架构要求）
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace EscapeTheTower.Core
{
    /// <summary>
    /// 全局服务定位器 —— 集中管理所有核心服务的注册与获取
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>
        /// 注册一个服务实例（通常在 GameBootstrapper.Awake 中调用）
        /// </summary>
        /// <typeparam name="T">服务接口或基类类型</typeparam>
        /// <param name="service">服务实例</param>
        public static void Register<T>(T service) where T : class
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] 服务 {type.Name} 已被注册，将覆盖旧实例。");
                _services[type] = service;
            }
            else
            {
                _services.Add(type, service);
                Debug.Log($"[ServiceLocator] 服务 {type.Name} 注册成功。");
            }
        }

        /// <summary>
        /// 获取已注册的服务实例
        /// </summary>
        /// <typeparam name="T">服务接口或基类类型</typeparam>
        /// <returns>服务实例</returns>
        /// <exception cref="InvalidOperationException">服务未注册时抛出</exception>
        public static T Get<T>() where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }

            throw new InvalidOperationException(
                $"[ServiceLocator] 服务 {type.Name} 未注册！请确认已在 GameBootstrapper 中完成注册。");
        }

        /// <summary>
        /// 尝试获取服务，获取失败不抛异常
        /// </summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var obj))
            {
                service = (T)obj;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>
        /// 注销指定服务
        /// </summary>
        public static void Unregister<T>() where T : class
        {
            var type = typeof(T);
            if (_services.Remove(type))
            {
                Debug.Log($"[ServiceLocator] 服务 {type.Name} 已注销。");
            }
        }

        /// <summary>
        /// 清空所有已注册的服务（场景切换时调用）
        /// </summary>
        public static void ClearAll()
        {
            _services.Clear();
            Debug.Log("[ServiceLocator] 所有服务已清空。");
        }
    }
}
