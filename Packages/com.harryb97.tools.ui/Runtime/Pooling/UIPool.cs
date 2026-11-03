using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.UI
{
    public class UIPool<T> where T : Component
    {
        readonly T _gameobject;

        readonly RectTransform _parent;

        readonly List<T> _instances;

        private int _index = 0;

        public UIPool(T prefab, RectTransform parent)
        {
            _instances = new List<T>();
            _gameobject = prefab;
            _parent = parent;
        }

        public void ResetCounter()
        {
            _index = 0;
        }

        public T GetInstance()
        {
            while (_index < _instances.Count)
            {
                var existing = _instances[_index];

                if (!existing)
                {
                    _instances.RemoveAt(_index);
                    continue;
                }

                _index++;
                if (!existing.gameObject.activeSelf)
                    existing.gameObject.SetActive(true);
                return existing;
            }

            var instance = Object.Instantiate(_gameobject, _parent, false);
            _instances.Add(instance);
            _index++;
            return instance;
        }

        public void DiscardRest()
        {
            for (int i = _index; i < _instances.Count; i++)
            {
                if (_instances[i])
                    _instances[i].gameObject.SetActive(false);
            }
            ResetCounter();
        }

        public void Dispose()
        {
            ResetCounter();

            for (int i = 0; i < _instances.Count; i++)
            {
                if (_instances[i])
                    Object.Destroy(_instances[i].gameObject);
            }

            _instances.Clear();
            _instances.TrimExcess();
        }
    }
}
