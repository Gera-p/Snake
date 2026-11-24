using System;
using System.Collections.Generic;
using SnakeGame.Core;
using UnityEngine;

namespace SnakeGame.Model
{
    /// <summary>
    /// Модель змеи — ЧИСТАЯ логика. Никаких MonoBehaviour-ов, никаких Transform.
    /// Знает только клетки сетки (Vector2Int). Это позволяет:
    ///  - тестировать правила игры без сцены Unity;
    ///  - сериализовать/воспроизводить состояние (реплеи, save/load);
    ///  - менять View, не трогая правила.
    ///
    /// Представление: LinkedList-free реализация на List<Vector2Int>, где
    /// индекс 0 — голова, последний элемент — хвост. Хвост при шаге удаляется (O(n),
    /// но n мало и аллокаций нет), голова вставляется через Insert(0,...) — тоже O(n).
    /// Для классических размеров змеи (<100 сегментов) это быстрее и проще, чем deque.
    /// </summary>
    public class SnakeModel
    {
        private readonly List<Vector2Int> _segments = new List<Vector2Int>(64); // ёмкость заранее — без GC в рантайме

        public Direction CurrentDirection { get; private set; }
        public IReadOnlyList<Vector2Int> Segments => _segments;
        public Vector2Int Head => _segments[0];
        public Vector2Int Tail => _segments[_segments.Count - 1];
        public int Length => _segments.Count;

        /// <summary>События (delegate-based) — View подписывается и НЕ поллит по кадрам.</summary>
        public event Action<SnakeModel> Moved;          // змея сделала шаг (позиции обновились)
        public event Action<SnakeModel> Grew;           // змея отросла после еды
        public event Action<SnakeModel> Died;           // столкновение со стеной или собой
        public event Action<SnakeModel> Spawned;        // полное пересоздание (новая игра)

        public SnakeModel(Vector2Int spawnPos, Direction dir, int length)
        {
            Reset(spawnPos, dir, length);
        }

        /// <summary>Полный сброс состояния для новой партии (переиспользует тот же объект и список).</summary>
        public void Reset(Vector2Int spawnPos, Direction dir, int length)
        {
            _segments.Clear();
            CurrentDirection = dir;

            // Змея лежит горизонтально влево от головы: голова справа, хвост слева.
            for (int i = 0; i < length; i++)
            {
                _segments.Add(new Vector2Int(spawnPos.x - i, spawnPos.y));
            }

            Spawned?.Invoke(this);
        }

        /// <summary>
        /// Смена направления из ввода. Возвращает false, если ход запрещён (разворот на 180°).
        /// Направление буферизуется не здесь — это задача контроллера (см. InputToModelBuffer).
        /// Здесь — последний рубеж защиты правил: даже если контроллер ошибётся, разворота не будет.
        /// </summary>
        public bool TryChangeDirection(Direction newDir)
        {
            if (newDir.IsOpposite(CurrentDirection)) return false;
            CurrentDirection = newDir;
            return true;
        }

        /// <summary>
        /// Один шаг змеи. Возвращает результат, чтобы контроллер решал, что делать с едой.
        /// Важно: проверка самопересечения учитывает, что ХВОСТ СДВИГАЕТСЯ, если змея НЕ ест —
        /// поэтому клетка текущего хвоста легальна для головы (классическая тонкость «Змейки»).
        /// </summary>
        public StepResult Step(GameBoard board, Vector2Int? foodCell)
        {
            Vector2Int next = Head + CurrentDirection.ToVec2Int();

            // 1) Стена — смерть немедленно, позиции не меняем (View покажет финальную позу).
            if (!board.IsInside(next))
            {
                Died?.Invoke(this);
                return StepResult.HitWall;
            }

            bool eats = foodCell.HasValue && foodCell.Value == next;

            // 2) Самокус: если не едим, хвост освобождает свою клетку — исключаем её из проверки.
            for (int i = 0; i < _segments.Count - (eats ? 0 : 1); i++)
            {
                if (_segments[i] == next)
                {
                    Died?.Invoke(this);
                    return StepResult.SelfBite;
                }
            }

            // 3) Применяем ход.
            _segments.Insert(0, next);
            if (!eats)
            {
                _segments.RemoveAt(_segments.Count - 1); // обычный шаг: длина та же
                Moved?.Invoke(this);
                return StepResult.Moved;
            }
            else
            {
                Grew?.Invoke(this);                      // поедание: длина +1, хвост не удаляем
                return StepResult.AteFood;
            }
        }

        /// <summary>Занята ли клетка змеёй (нужно, чтобы еда не спавнилась «в теле»).</summary>
        public bool Occupies(Vector2Int cell)
        {
            for (int i = 0; i < _segments.Count; i++)
                if (_segments[i] == cell) return true;
            return false;
        }
    }

    /// <summary>Результат одного дискретного шага — словарь протокола Model -> Controller.</summary>
    public enum StepResult
    {
        Moved,       // обычный ход
        AteFood,     // ход + поедание (рост)
        HitWall,     // смерть о стену
        SelfBite     // смерть от укуса себя
    }
}
