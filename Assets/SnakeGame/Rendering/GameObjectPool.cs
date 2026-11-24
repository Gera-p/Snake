using System.Collections.Generic;
using UnityEngine;

namespace SnakeGame.Rendering
{
    /// <summary>
    /// Пул GameObject-ов сегментов змеи и «вспышек» еды.
    /// Архитектурное решение: при росте/движении змеи мы НЕ Instantiate/Destroy-им
    /// объекты каждый кадр — берём из стека и возвращаем обратно. Это устраняет
    /// GC-аллокации и скачки кадров (Instantiate в Unity всегда дорог).
    ///
    /// Пул владеет своими корнями (Transform), чтобы иерархия сцены оставалась чистой.
    /// </summary>
    public class GameObjectPool
    {
        private readonly Stack<GameObject> _free = new Stack<GameObject>();
        private readonly Transform _root;
        private readonly string _prefabName;
        private readonly System.Func<GameObject> _factory;

        /// <param name="name">Имя корневого узла в иерархии.</param>
        /// <param name="parent">Родитель (обычно узел View).</param>
        /// <param name="factory">Фабрика создания нового элемента, когда пул пуст.</param>
        /// <param name="prewarm">Сколько объектов создать заранее (размер типичной змеи).</param>
        public GameObjectPool(string name, Transform parent, System.Func<GameObject> factory, int prewarm = 32)
        {
            _prefabName = name;
            _factory = factory;
            _root = new GameObject(name + "_POOL").transform;
            _root.SetParent(parent, false);

            for (int i = 0; i < prewarm; i++)
            {
                var go = _factory();
                go.transform.SetParent(_root, false);
                go.SetActive(false);
                _free.Push(go);
            }
        }

        public GameObject Spawn(Vector3 worldPos, Quaternion rot)
        {
            GameObject go;
            if (_free.Count > 0)
            {
                go = _free.Pop();
            }
            else
            {
                // Пул исчерпан — расширяем его на ходу (это единственная аллокация, и она редка).
                go = _factory();
                go.transform.SetParent(_root, false);
            }

            go.name = _prefabName;
            go.transform.position = worldPos;
            go.transform.rotation = rot;
            go.SetActive(true);
            return go;
        }

        public void Despawn(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(_root, false); // держим всё под корнем пула — никаких случайных объектов в сцене
            _free.Push(go);
        }

        /// <summary>Полный возврат всех активных объектов (при рестарте партии).</summary>
        public void DespawnAll(IEnumerable<GameObject> actives)
        {
            foreach (var go in actives) Despawn(go);
        }

        public void Dispose()
        {
            foreach (var go in _free)
                if (go != null) Object.Destroy(go);
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
