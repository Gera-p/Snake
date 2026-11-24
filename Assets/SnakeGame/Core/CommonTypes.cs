namespace SnakeGame.Core
{
    /// <summary>
    /// Направления движения змеи. Используем IntVector2-совместимые значения,
    /// чтобы вся логика оставалась целочисленной (сетка!), а рендер получал
    /// Vector3 уже на уровне View.
    /// </summary>
    public enum Direction
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>
    /// Расширения для Direction: перевод в Vector2Int и проверка «разворота на 180°».
    /// Разворот запрещён, иначе змея мгновенно кусает себя за шею — классический баг.
    /// </summary>
    public static class DirectionExtensions
    {
        public static UnityEngine.Vector2Int ToVec2Int(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:    return new UnityEngine.Vector2Int(0, 1);
                case Direction.Down:  return new UnityEngine.Vector2Int(0, -1);
                case Direction.Left:  return new UnityEngine.Vector2Int(-1, 0);
                case Direction.Right: return new UnityEngine.Vector2Int(1, 0);
                default:              return UnityEngine.Vector2Int.zero;
            }
        }

        /// <summary>Истина, если newDir — прямой разворот относительно current (180°).</summary>
        public static bool IsOpposite(this Direction newDir, Direction current)
        {
            return (newDir == Direction.Up   && current == Direction.Down) ||
                   (newDir == Direction.Down && current == Direction.Up)   ||
                   (newDir == Direction.Left && current == Direction.Right)||
                   (newDir == Direction.Right&& current == Direction.Left);
        }
    }

    /// <summary>
    /// Фазы конечного автомата игры. Controller следит за переходами,
    /// Model ничего о фазах не знает — это чистая ответственность управления.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        GameOver
    }
}
