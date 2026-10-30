using System;

namespace HarryTools.Signals {
    public interface IReadOnlySignal {
        string ID { get; }

        void AddListener(Action _listener);
        void RemoveListener(Action _listener);
    }
    
    public interface IReadOnlySignal<out T> {
        string ID { get; }
        T Value { get; }

        void AddListener(Action<T> _listener);
        void RemoveListener(Action<T> _listener);
    }
}