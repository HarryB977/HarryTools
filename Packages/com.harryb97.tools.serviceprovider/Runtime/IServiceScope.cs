using System;

namespace HarryTools.ServiceLocator {
    public interface IServiceScope {
        bool GetService<T>(out T _service);
        bool GetService(Type _type, out object _service);

        void DisposeServices();
    }
}