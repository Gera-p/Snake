using System;
using SnakeGame.Core;
using SnakeGame.Input;
using SnakeGame.Model;
using UnityEngine;

namespace SnakeGame.Controllers
{
    /// <summary>
    /// ГЛАВНЫЙ контроллер — «дирижёр». Владеет Model и двигает её по часам.
    ///
    /// Архитектурные решения:
    /// 1) Дискретный таймер вместо FixedUpdate/Physics. Шаг змеи привязан к
    ///    накопленному времени (_timer += deltaTime), что даёт честную сетку
    ///    движения и независимость от частоты физики.
    /// 2) БУФЕР ВВОДА на 2 команды (Queue). Классический баг «змейки»: при быстрой
    ///    последовательности Left->Up в одном такте без буфера теряется второй ход,
    ///    а с наивным применением обоих — возможен самокус (обе команды применяются
    ///    к одному направлению). Буфер применяет ровно ОДНУ команду за шаг,
    ///    проверяя разворот уже относительно актуального направления модели.
    /// 3) Никакого синглтон-абьюза: зависимости приходят через конструктор (ручной DI).
    /// </summary>
    public class GameController : MonoBehaviour
    {
        private GameConfig _config;
        private SnakeModel _snake;
        private GameBoard _board;
        private IInputProvider _input;

        // Unity не может вызвать конструктор с параметрами у MonoBehaviour,
        // поэтому композиционный корень (Bootstrap) обязан один раз позвать Inject().
        private bool _wired;

        // View подключается событиями, а не прямыми ссылками на MonoBehaviour-и:
        // controller транслирует высокоуровневые сигналы, view решает, как их рисовать.
        public event Action<GameState> StateChanged;
        public event Action<int> ScoreChanged;
        public event Action<float> SpeedChanged; // для инфо-строки (0..1 прогресс ускорения)

        private GameState _state = GameState.MainMenu;
        public GameState State => _state;

        public int Score { get; private set; }
        public float CurrentMoveInterval { get; private set; }

        private float _stepTimer;
        private readonly System.Collections.Generic.Queue<Direction> _inputBuffer
            = new System.Collections.Generic.Queue<Direction>(2);

        // Переиспользуем делегаты-обёртки событий, чтобы подписка/отписка не аллоцировала каждый рестарт.
        private Action<SnakeModel> _onMovedHandler;
        private Action<SnakeModel> _onGrewHandler;
        private Action<SnakeModel> _onDiedHandler;

        public event Action<SnakeModel> StepHappened; // сигнал для SnakeView: начался новый интерпол-интервал

        /// <summary>
        /// Ручная инъекция зависимостей из композиционного корня (SnakeGameBootstrap).
        /// Почему не конструктор? Unity создаёт MonoBehaviour сам и НЕ умеет звать
        /// конструкторы с параметрами — это ограничение движка, а не архитектурная лень.
        /// Метод обязан быть вызван ровно один раз ДО первого Update (гарантируется Awake-порядком).
        /// </summary>
        public void Inject(GameConfig config, SnakeModel snake, GameBoard board, IInputProvider input)
        {
            if (_wired)
            {
                Debug.LogError("[GameController] Inject вызван повторно! Зависимости уже заданы.");
                return;
            }
            _config = config;
            _snake = snake;
            _board = board;
            _input = input;

            _onMovedHandler = HandleSnakeMoved;
            _onGrewHandler = HandleSnakeGrew;
            _onDiedHandler = HandleSnakeDied;

            CurrentMoveInterval = config.StartMoveInterval;
            _wired = true;
        }

        /// <summary>Подстраховка: к моменту прихода Awake из следующего кадра Bootstrap уже позвал Inject.</summary>
        private void Awake()
        {
            if (!_wired) enabled = false; // если инъекции почему-то нет — молча не тикаем, вместо NullReferenceException
        }

        /// <summary>
        /// Подписка на события модели. Идемпотентна: Bootstrap зовёт SubscribeModelEvents()
        /// сразу после Inject (в том же кадре), а Unity дозовёт OnEnable в начале следующего —
        /// флаг _subscribed гарантирует отсутствие двойной подписки (double-delegate bug).
        /// </summary>
        public void SubscribeModelEvents()
        {
            if (!_wired || _subscribed) return;
            _snake.Moved += _onMovedHandler;
            _snake.Grew += _onGrewHandler;
            _snake.Died += _onDiedHandler;
            _subscribed = true;
        }

        private bool _subscribed;

        private void OnEnable() => SubscribeModelEvents();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _snake.Moved -= _onMovedHandler;
            _snake.Grew -= _onGrewHandler;
            _snake.Died -= _onDiedHandler;
            _subscribed = false;
        }

        /// <summary>
        /// Движок игры. Архитектурное решение: физика Unity НЕ используется вообще —
        /// шаг змеи считается по накопленному deltaTime (дискретный таймер).
        /// Подстраховка на случай, если Bootstrap не позвал Inject (см. Awake): проверяем _wired.
        /// </summary>
        private void Update()
        {
            if (!_wired || _state != GameState.Playing) return;

            _input.Poll();
            ReadInputIntoBuffer();

            _stepTimer += Time.deltaTime;
            if (_stepTimer >= CurrentMoveInterval)
            {
                _stepTimer -= CurrentMoveInterval; // НЕ обнуляем: сохраняем остаток — нет «дрейфа» скорости
                PerformStep();
            }
        }

        private void ReadInputIntoBuffer()
        {
            if (_input.TryReadDirection(out var dir))
            {
                // Ограничиваем буфер двумя командами: лишние быстрые нажатия игнорируются,
                // иначе игрок «записывает» маршрут на 5 шагов вперёд — управление становится нечестным.
                if (_inputBuffer.Count < 2 && !IsRedundant(dir))
                {
                    _inputBuffer.Enqueue(dir);
                    _lastQueued = dir;
                }
            }

            if (_input.PausePressed) TogglePause();
        }

        /// <summary>Фильтр повторов: одно и то же направление подряд бессмысленно занимает слот буфера.</summary>
        private bool IsRedundant(Direction dir)
        {
            // Queue<T> не даёт доступ по индексу к последнему элементу без аллокации ToArray(),
            // поэтому храним «последнюю поставленную команду» отдельным полем — O(1), ноль GC.
            return _lastQueued.HasValue && dir == _lastQueued.Value;
        }

        private Direction? _lastQueued; // сбрасывается при dequeue и при очистке буфера

        /// <summary>Последний элемент очереди без аллокаций: при max capacity 2 это тривиально.</summary>
        private Direction PeekLast()
        {
            Direction result = default;
            foreach (var d in _inputBuffer) result = d; // очередь ≤ 2 элементов — цикл константный, GC нет
            return result;
        }

        private void PerformStep()
        {
            // Применяем ровно одну команду из буфера, с двойной проверкой разворота внутри модели.
            if (_inputBuffer.Count > 0)
            {
                var next = _inputBuffer.Dequeue();
                _lastQueued = _inputBuffer.Count > 0 ? PeekLast() : (Direction?)null;
                _snake.TryChangeDirection(next); // модель сама отклонит запрещённый разворот
            }

            var result = _snake.Step(_board, _board.FoodCell);

            switch (result)
            {
                case StepResult.Moved:
                    StepHappened?.Invoke(_snake);
                    break;

                case StepResult.AteFood:
                    HandleEating();
                    StepHappened?.Invoke(_snake);
                    break;

                case StepResult.HitWall:
                case StepResult.SelfBite:
                    // Смерть уже разослана событием Died модели — здесь только смена фазы.
                    // Задержка перед экраном GameOver ставится в Bootstrap (чтобы игрок увидел столкновение).
                    SetState(GameState.GameOver);
                    break;
            }
        }

        private void HandleEating()
        {
            Score += 10;
            ScoreChanged?.Invoke(Score);

            // Прогрессивное ускорение с потолком — игра обязана остаться играбельной.
            CurrentMoveInterval = Mathf.Max(
                _config.MinMoveInterval,
                CurrentMoveInterval - _config.SpeedUpPerFood);
            SpeedChanged?.Invoke(GetSpeedProgress());

            var eatenCell = _board.FoodCell; // помним ДО consume — для FX в нужной точке
            _board.ConsumeFood();
            _board.RespawnFood(_snake.Occupies);

            // Если свободных клеток не осталось — поле заполнено, это победа = тоже конец партии.
            if (!_board.FoodCell.HasValue)
            {
                SetState(GameState.GameOver);
                return;
            }

            EatFxRequested?.Invoke(eatenCell);
        }

        /// <summary>View подписывается: показать вспышку на съеденной клетке.</summary>
        public event Action<Vector2Int?> EatFxRequested;

        public float GetSpeedProgress()
        {
            float range = _config.StartMoveInterval - _config.MinMoveInterval;
            if (range <= 0f) return 1f;
            return Mathf.Clamp01(1f - (CurrentMoveInterval - _config.MinMoveInterval) / range);
        }

        // ---------- Обработчики событий модели ----------
        private void HandleSnakeMoved(SnakeModel m) { /* позиции уже обновлены; view синхронизируется через StepHappened */ }
        private void HandleSnakeGrew(SnakeModel m) { /* рост обработан в HandleEating */ }
        private void HandleSnakeDied(SnakeModel m) { /* фаза меняется в PerformStep */ }

        // ---------- API для UI ----------
        public void StartNewGame()
        {
            Score = 0;
            ScoreChanged?.Invoke(Score);
            CurrentMoveInterval = _config.StartMoveInterval;
            SpeedChanged?.Invoke(0f);
            _inputBuffer.Clear();
            _stepTimer = 0f;

            // Спавн: голова чуть правее центра, летит вправо, тело тянется влево.
            var spawn = new Vector2Int(_config.GridWidth / 2 + 1, _config.GridHeight / 2);
            _snake.Reset(spawn, Direction.Right, _config.StartLength);

            _board.RespawnFood(_snake.Occupies);
            SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (_state == GameState.Playing) SetState(GameState.Paused);
            else if (_state == GameState.Paused) SetState(GameState.Playing);
        }

        public void GoToMainMenu() => SetState(GameState.MainMenu);

        private void SetState(GameState newState)
        {
            if (_state == newState) return;
            _state = newState;
            _inputBuffer.Clear(); // при смене фазы накопленный ввод сгорает — никаких «мгновенных стартов»
            _lastQueued = null;   // синхронно чистим и «последнюю поставленную команду»
            StateChanged?.Invoke(newState);
        }

        /// <summary>
        /// Принудительная повторная рассылка текущего состояния.
        /// Нужна Bootstrap-у, чтобы показать панель GameOver с задержкой 0.35s
        /// (игрок должен увидеть момент столкновения до затемняющего экрана).
        /// </summary>
        public void NotifyStateRefresh() => StateChanged?.Invoke(_state);
    }
}
