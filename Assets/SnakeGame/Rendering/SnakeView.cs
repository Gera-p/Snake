using System.Collections.Generic;
using SnakeGame.Core;
using SnakeGame.Model;
using UnityEngine;

namespace SnakeGame.Rendering
{
    /// <summary>
    /// View змеи и мира. Подписывается на события модели (Moved/Grew/Spawned/Food*)
    /// и только перекладывает клетки Vector2Int в позиции Vector3.
    ///
    /// КЛЮЧЕВОЕ РЕШЕНИЕ — интерполяция: модель живёт «дискретными прыжками»,
    /// а игроку нужна плавная картинка. Поэтому каждый сегмент хранит
    /// «прошлую» и «текущую» клетку, а Update() плавно смешивает их по
    /// времени, прошедшему с последнего шага (_stepTime / _moveInterval).
    /// Никакой физики — чистый лерп по данным модели.
    /// </summary>
    public class SnakeView : MonoBehaviour
    {
        [Header("Настройки вида (заполняются Bootstrap-ом)")]
        private GameConfig _config;
        private GameObjectPool _segmentPool;
        private Transform _segmentsRoot;

        // Активные инстансы сегментов (0 — голова). Параллельный список прошлых клеток для лерпа.
        private readonly List<GameObject> _activeSegments = new List<GameObject>(64);
        private readonly List<Vector2Int> _prevCells = new List<Vector2Int>(64);
        private readonly List<Vector2Int> _currCells = new List<Vector2Int>(64);

        private float _stepTime = 1f;     // время с последнего шага (в начале — «шаг завершён»)
        private float _moveInterval = 0.2f;
        private bool _interpEnabled = true;

        private GameObject _foodInstance;
        private GameObject _foodPulse; // лёгкая «вспышка» на месте съеденной еды

        // Сортировка слоёв: фон(-10) < стены(-5) < тело(0) < голова(1) < еда(2) < эффекты(3)
        private const int ORDER_BODY = 0;
        private const int ORDER_HEAD = 1;
        private const int ORDER_FOOD = 2;
        private const int ORDER_FX = 3;

        /// <summary>Инициализация извне (Bootstrap не дергает Inspector вообще).</summary>
        public void Init(GameConfig config, Transform parent)
        {
            _config = config;
            _moveInterval = config.StartMoveInterval;

            _segmentsRoot = new GameObject("Segments").transform;
            _segmentsRoot.SetParent(parent, false);

            // Фабрика сегмента: белый квадрат + SpriteRenderer. Префаб не нужен.
            _segmentPool = new GameObjectPool(
                "SnakeSegment",
                transform,
                () => ProceduralSprites.CreateBlock(
                    ProceduralSprites.Square, _config.BodyColor, ORDER_BODY, _segmentsRoot),
                prewarm: Mathf.Max(config.StartLength * 4, 32));
        }

        /// <summary>Контроллер сообщает, что модель сделала шаг — запускаем окно интерполяции.</summary>
        public void OnModelStepped(float moveInterval)
        {
            _moveInterval = moveInterval;
            _stepTime = 0f;
        }

        public void SetInterpolation(bool enabled) => _interpEnabled = enabled;

        /// <summary>Полная синхронизация визуала с моделью (вызывается из событий Spawned/Moved/Grew).</summary>
        public void SyncWithModel(SnakeModel model)
        {
            var segs = model.Segments;

            // 1) Синхронизируем количество инстансов через пул.
            while (_activeSegments.Count > segs.Count)
            {
                int last = _activeSegments.Count - 1;
                _segmentPool.Despawn(_activeSegments[last]);
                _activeSegments.RemoveAt(last);
                _prevCells.RemoveAt(last);
                _currCells.RemoveAt(last);
            }
            while (_activeSegments.Count < segs.Count)
            {
                var go = _segmentPool.Spawn(CellToWorld(segs[_activeSegments.Count]), Quaternion.identity);
                _activeSegments.Add(go);
                _prevCells.Add(segs[_activeSegments.Count - 1]);
                _currCells.Add(segs[_activeSegments.Count - 1]);
            }

            // 2) Сдвигаем «прошлая -> текущая»: новая текущая становится прошлой для следующего шага.
            for (int i = 0; i < segs.Count; i++)
            {
                _prevCells[i] = _currCells[i];
                _currCells[i] = segs[i];
            }

            // 3) Цвета: голова всегда ярче тела — меняем color вместо разных объектов.
            for (int i = 0; i < _activeSegments.Count; i++)
            {
                var sr = _activeSegments[i].GetComponent<SpriteRenderer>();
                sr.color = (i == 0) ? _config.HeadColor : _config.BodyColor;
                sr.sortingOrder = (i == 0) ? ORDER_HEAD : ORDER_BODY;
            }
        }

        /// <summary>Спавн/перемещение еды. Переиспользуем один объект — аллокаций нет.</summary>
        public void ShowFood(Vector2Int cell)
        {
            if (_foodInstance == null)
            {
                _foodInstance = ProceduralSprites.CreateBlock(
                    ProceduralSprites.Circle, _config.FoodColor, ORDER_FOOD, transform);
            }
            _foodInstance.SetActive(true);
            _foodInstance.transform.position = CellToWorld(cell);
            _foodInstance.transform.localScale = Vector3.one * _config.CellSize * 0.8f;
        }

        public void HideFood()
        {
            if (_foodInstance != null) _foodInstance.SetActive(false);
        }

        /// <summary>Визуальный отклик на поедание: быстрая пульсация-кольцо. Без частиц и ассетов.</summary>
        public void PlayEatFx(Vector2Int cell)
        {
            if (_foodPulse == null)
            {
                _foodPulse = ProceduralSprites.CreateBlock(
                    ProceduralSprites.Circle, Color.white, ORDER_FX, transform);
            }
            _foodPulse.SetActive(true);
            _foodPulse.transform.position = CellToWorld(cell);
            _fxTimer = 0.25f; // длительность эффекта в секундах
        }

        private float _fxTimer;

        private void Update()
        {
            // Плавность движения сегментов. t in [0..1] — доля пройденного шага.
            float t = _interpEnabled && _moveInterval > 0.0001f
                ? Mathf.Clamp01(_stepTime / _moveInterval)
                : 1f;

            for (int i = 0; i < _activeSegments.Count; i++)
            {
                Vector3 from = CellToWorld(_prevCells[i]);
                Vector3 to = CellToWorld(_currCells[i]);
                _activeSegments[i].transform.position = Vector3.Lerp(from, to, EaseLinear(t));

                // Лёгкий «дышащий» масштаб головы — мелочь, но оживляет картинку.
                if (i == 0)
                {
                    float s = _config.CellSize * Mathf.Lerp(0.92f, 1f, Mathf.PingPong(Time.time * 4f, 1f));
                    _activeSegments[i].transform.localScale = Vector3.one * s;
                }
            }

            _stepTime += Time.deltaTime;

            // Таймер эффекта поедания.
            if (_fxTimer > 0f)
            {
                _fxTimer -= Time.deltaTime;
                if (_foodPulse != null)
                {
                    float k = 1f - Mathf.Clamp01(_fxTimer / 0.25f);
                    _foodPulse.transform.localScale = Vector3.one * _config.CellSize * (0.8f + k * 1.2f);
                    var psr = _foodPulse.GetComponent<SpriteRenderer>();
                    var c = psr.color; c.a = 1f - k; psr.color = c;
                    if (_fxTimer <= 0f) _foodPulse.SetActive(false);
                }
            }
        }

        private static float EaseLinear(float t) => t; // линейно: сеточная игра должна читаться честно

        /// <summary>
        /// Перевод клетки сетки в мировую позицию. Поле центрировано в (0,0),
        /// чтобы камера смотрела в центр независимо от размера поля.
        /// </summary>
        public Vector3 CellToWorld(Vector2Int cell)
        {
            float ox = -(_config.GridWidth - 1) * 0.5f * _config.CellSize;
            float oy = -(_config.GridHeight - 1) * 0.5f * _config.CellSize;
            return new Vector3(ox + cell.x * _config.CellSize,
                               oy + cell.y * _config.CellSize, 0f);
        }

        private void OnDestroy()
        {
            _segmentPool?.Dispose();
        }
    }
}
