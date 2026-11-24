using SnakeGame.Core;

namespace SnakeGame.Input
{
    /// <summary>
    /// Абстракция ввода. Контроллер зависит ТОЛЬКО от интерфейса, поэтому
    /// legacy Input (Input.GetKeyDown) можно заменить на new Input System,
    /// touch-джойстик или AI-бота — правкой одного класса, без трогания игры.
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>Опрошен ли ввод в этом кадре и какое направление запрошено.</summary>
        bool TryReadDirection(out Direction direction);

        /// <summary>Нажата ли клавиша паузы (Edge-triggered).</summary>
        bool PausePressed { get; }

        /// <summary>Вызывается один раз за кадр для сброса edge-состояний.</summary>
        void Poll();
    }
}
