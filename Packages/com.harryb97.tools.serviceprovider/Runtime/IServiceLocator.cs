using System;

namespace HarryTools.ServiceLocator {
    public interface IServiceLocator {
        void Register<T>(T _service);
        void Register(Type _type, object _service);

        bool Unregister<T>(T _service);
        bool Unregister(Type _type, object _service);

        T Get<T>();
        object Get(Type _type);

        bool TryGetByImplementation(Type _implementation, out object _service);

        bool IsRegistered<T>();
        bool IsRegistered(Type _type);

        void DisposeAll();
    }
}