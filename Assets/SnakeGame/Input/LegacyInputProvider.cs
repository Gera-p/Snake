using SnakeGame.Core;
using UnityEngine;

namespace SnakeGame.Input
{
    /// <summary>
    /// Реализация IInputProvider на LEGACY Input Manager (WASD + стрелки).
    /// Клавиши берутся из GameConfig, чтобы не хардкодить маппинг.
    /// </summary>
    public class LegacyInputProvider : IInputProvider
    {
        private readonly GameConfig _config;

        // Edge-detection: GetKeyDown уже по краю, но держим флаг, чтобы несколько
        // систем за кадр прочитали одно и то же нажатие, а затем оно «погасло».
        private Direction? _pendingDirection;
        private bool _pausePressed;

        public bool PausePressed => _pausePressed;

        public LegacyInputProvider(GameConfig config)
        {
            _config = config;
        }

        /// <summary>Кадровый опрос. Вызывается контроллером ДО чтения направления.</summary>
        public void Poll()
        {
            _pendingDirection = null;
            _pausePressed = false;

            if (IsDown(_config.UpKey) || IsDown(KeyCode.W) || IsDown(KeyCode.UpArrow))
                _pendingDirection = Direction.Up;
            else if (IsDown(_config.DownKey) || IsDown(KeyCode.S) || IsDown(KeyCode.DownArrow))
                _pendingDirection = Direction.Down;
            else if (IsDown(_config.LeftKey) || IsDown(KeyCode.A) || IsDown(KeyCode.LeftArrow))
                _pendingDirection = Direction.Left;
            else if (IsDown(_config.RightKey) || IsDown(KeyCode.D) || IsDown(KeyCode.RightArrow))
                _pendingDirection = Direction.Right;

            _pausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        }

        public bool TryReadDirection(out Direction direction)
        {
            if (_pendingDirection.HasValue)
            {
                direction = _pendingDirection.Value;
                return true;
            }
            direction = default;
            return false;
        }

        private static bool IsDown(KeyCode key) => Input.GetKeyDown(key);
    }
}
