using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakeGame.Model
{
    /// <summary>
    /// Игровое поле: границы сетки + размещение еды.
    /// Выделено из SnakeModel, потому что «стены» и «еда» — свойства мира, а не змеи.
    /// </summary>
    public class GameBoard
    {
        public int Width { get; }
        public int Height { get; }

        /// <summary>Клетка, где сейчас лежит еда (null — еда не активна, идёт переспавн).</summary>
        public Vector2Int? FoodCell { get; private set; }

        public event Action<Vector2Int> FoodEaten;   // параметры: где съели
        public event Action<Vector2Int> FoodSpawned; // параметры: куда поставили новое

        // Переиспользуемый список свободных клеток — чтобы RespawnFood не аллоцировал каждый раз.
        private readonly List<Vector2Int> _freeCellsBuffer = new List<Vector2Int>(512);

        public GameBoard(int width, int height)
        {
            // Защита от некорректной конфигурации: поле обязано быть хотя бы 3x3,
            // иначе змея стартовой длины физически не разместится.
            Width = Mathf.Max(3, width);
            Height = Mathf.Max(3, height);
        }

        public bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        /// <summary>
        /// Спавн еды ТОЛЬКО на валидных клетках: collect-all-free + равномерный выбор.
        /// Отказ от «random until free» — тот может зависнуть/съесть CPU, когда поле почти занято.
        /// </summary>
        public void RespawnFood(Func<Vector2Int, bool> isOccupied)
        {
            _freeCellsBuffer.Clear();
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var c = new Vector2Int(x, y);
                if (!isOccupied(c)) _freeCellsBuffer.Add(c);
            }

            if (_freeCellsBuffer.Count == 0)
            {
                // Поле забито полностью — победа игрока. Еду не ставим, controller обработает null.
                FoodCell = null;
                return;
            }

            int index = UnityEngine.Random.Range(0, _freeCellsBuffer.Count);
            FoodCell = _freeCellsBuffer[index];
            FoodSpawned?.Invoke(FoodCell.Value);
        }

        /// <summary>Вызывается контроллером, когда модель сообщила, что еда съедена.</summary>
        public void ConsumeFood()
        {
            if (!FoodCell.HasValue) return;
            var eaten = FoodCell.Value;
            FoodCell = null;
            FoodEaten?.Invoke(eaten);
        }
    }
}
