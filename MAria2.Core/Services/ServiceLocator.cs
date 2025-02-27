using System;
using System.Collections.Concurrent;

namespace MAria2.Core.Services
{
    /// <summary>
    /// A thread-safe, flexible dependency injection container
    /// Supports singleton and transient service registrations
    /// </summary>
    public class ServiceLocator
    {
        private readonly ConcurrentDictionary<Type, ServiceRegistration> _services = new();
        private static ServiceLocator _instance;
        private static readonly object _lock = new();

        // Singleton pattern with lazy initialization
        public static ServiceLocator Current
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ServiceLocator();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Register a service with specific lifetime
        /// </summary>
        public void Register<TService>(Func<TService> factory, ServiceLifetime lifetime = ServiceLifetime.Transient)
            where TService : class
        {
            var registration = new ServiceRegistration
            {
                Factory = () => factory(),
                Lifetime = lifetime,
                ServiceType = typeof(TService)
            };

            _services[typeof(TService)] = registration;
        }

        /// <summary>
        /// Register a singleton service instance
        /// </summary>
        public void RegisterSingleton<TService>(TService instance) 
            where TService : class
        {
            var registration = new ServiceRegistration
            {
                Factory = () => instance,
                Lifetime = ServiceLifetime.Singleton,
                ServiceType = typeof(TService),
                Instance = instance
            };

            _services[typeof(TService)] = registration;
        }

        /// <summary>
        /// Resolve a service of a specific type
        /// </summary>
        public TService Resolve<TService>() where TService : class
        {
            if (!_services.TryGetValue(typeof(TService), out var registration))
            {
                throw new InvalidOperationException($"Service of type {typeof(TService)} is not registered.");
            }

            return ResolveRegistration<TService>(registration);
        }

        /// <summary>
        /// Try to resolve a service, returning null if not found
        /// </summary>
        public TService TryResolve<TService>() where TService : class
        {
            return _services.TryGetValue(typeof(TService), out var registration)
                ? ResolveRegistration<TService>(registration)
                : null;
        }

        private TService ResolveRegistration<TService>(ServiceRegistration registration)
            where TService : class
        {
            switch (registration.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    return (TService)(registration.Instance ?? 
                        (registration.Instance = registration.Factory()));
                
                case ServiceLifetime.Transient:
                    return (TService)registration.Factory();
                
                default:
                    throw new NotSupportedException($"Unsupported lifetime: {registration.Lifetime}");
            }
        }

        /// <summary>
        /// Clear all registered services
        /// </summary>
        public void Reset()
        {
            _services.Clear();
        }

        private class ServiceRegistration
        {
            public Type ServiceType { get; set; }
            public Func<object> Factory { get; set; }
            public ServiceLifetime Lifetime { get; set; }
            public object Instance { get; set; }
        }
    }

    /// <summary>
    /// Defines the lifetime of a registered service
    /// </summary>
    public enum ServiceLifetime
    {
        /// <summary>
        /// A new instance is created each time the service is requested
        /// </summary>
        Transient,

        /// <summary>
        /// A single instance is created and reused for all requests
        /// </summary>
        Singleton
    }

    /// <summary>
    /// Extension methods for easier service registration and resolution
    /// </summary>
    public static class ServiceLocatorExtensions
    {
        /// <summary>
        /// Convenience method to register a service with a factory method
        /// </summary>
        public static ServiceLocator AddTransient<TService>(
            this ServiceLocator locator, 
            Func<TService> factory) 
            where TService : class
        {
            locator.Register(factory);
            return locator;
        }

        /// <summary>
        /// Convenience method to register a singleton service
        /// </summary>
        public static ServiceLocator AddSingleton<TService>(
            this ServiceLocator locator, 
            TService instance) 
            where TService : class
        {
            locator.RegisterSingleton(instance);
            return locator;
        }
    }
}
