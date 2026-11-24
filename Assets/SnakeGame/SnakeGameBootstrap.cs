using System.Collections;
using SnakeGame.Controllers;
using SnakeGame.Core;
using SnakeGame.Input;
using SnakeGame.Model;
using SnakeGame.Rendering;
using SnakeGame.UI;
using UnityEngine;

namespace SnakeGame
{
    /// <summary>
    /// ЕДИНСТВЕННАЯ точка входа всей игры.
    ///
    /// ИНСТРУКЦИЯ (кратко, полная версия — в README.ru.md):
    ///  1. Создайте пустую сцену (File > New Scene).
    ///  2. Создайте пустой GameObject, назовите его "GameRoot".
    ///  3. Повесьте на него этот скрипт (SnakeGameBootstrap).
    ///  4. Нажмите Play. Всё остальное (камера, сетка, змея, UI) генерируется кодом.
    ///
    /// АРХИТЕКТУРА (порядок сборки зависимостей — «композиционный корень»):
    ///
    ///   GameConfig (ScriptableObject, данные)
    ///        │
    ///        ▼
    ///   SnakeModel + GameBoard  (чистая логика, без Unity-объектов)
    ///        │
    ///        ▼
    ///   LegacyInputProvider     (IInputProvider — ввод изолирован за интерфейсом)
    ///        │
    ///        ▼
    ///   GameController          (MonoBehaviour-дирижёр: таймер шагов, буфер ввода, счёт)
    ///        │            ▲
    ///        ▼            │ события (Moved/Grew/Died/Food*/StateChanged/ScoreChanged)
    ///   SnakeView / BoardView / UIManager   (только отображение, логигу не трогают)
    ///
    /// Никаких статических синглтонов-«богов»: композиционный корень здесь —
    /// Bootstrap. Он создаёт граф объектов и умирает вместе со сценой.
    /// Единственное допустимое исключение — кэш процедурных спрайтов (статика по сути констант).
    /// </summary>
    [DefaultExecutionOrder(-100)] // собираем мир раньше, чем любой Update начнёт тикать
    public class SnakeGameBootstrap : MonoBehaviour
    {
        [Tooltip("Необязательно: ваш .asset конфиг (Assets > Create > Snake Game > Game Config). " +
                 "Если пусто — создастся дефолтный конфиг прямо в памяти.")]
        [SerializeField] private GameConfig configOverride;

        private GameConfig _config;
        private GameController _controller;
        private SnakeView _snakeView;
        private SnakeModel _snakeModel;
        private GameBoard _board;

        private void Awake()
        {
            // --- Защита от двойной инициализации (Enter Play Mode Options / догруз сцены).
            if (_controller != null) return;

            Application.targetFrameRate = 60;

            // ---------- 1. Данные ----------
            // Клонируем конфиг, чтобы рантайм-эксперименты не мутировали asset в проекте.
            _config = (configOverride != null ? configOverride : ScriptableObject.CreateInstance<GameConfig>()).Clone();
            _config.name = "SnakeGameConfig_Runtime";

            // ---------- 2. Камера (создаём сами, если в сцене нет MainCamera) ----------
            EnsureCamera();

            // ---------- 3. Модель (чистые правила игры) ----------
            var spawn = new Vector2Int(_config.GridWidth / 2 + 1, _config.GridHeight / 2);
            _snakeModel = new SnakeModel(spawn, Direction.Right, _config.StartLength);
            _board = new GameBoard(_config.GridWidth, _config.GridHeight);

            // ---------- 4. Ввод (изолирован за интерфейсом) ----------
            IInputProvider input = new LegacyInputProvider(_config);

            // ---------- 5. Контроллер ----------
            var controllerGO = new GameObject("GameController");
            controllerGO.transform.SetParent(transform, false);
            _controller = controllerGO.AddComponent<GameController>();

            // NB: Unity не может вызвать конструктор MonoBehaviour с параметрами,
            // поэтому зависимости инжектируются явным методом из композиционного корня.
            // Тонкость порядка: AddComponent НЕ вызывает Awake на живом объекте — Awake/OnEnable
            // придут только в начале следующего кадра. Поэтому здесь мы делаем Inject ДО того,
            // как Unity вообще начнёт дёргать жизненный цикл контроллера. SubscribeModelEvents
            // идемпотентен и вызывается ещё раз из OnEnable-пути (см. GameController) — двойной защиты достаточно.
            _controller.Inject(_config, _snakeModel, _board, input);
            _controller.SubscribeModelEvents();

            // ---------- 6. View ----------
            var viewGO = new GameObject("WorldView");
            viewGO.transform.SetParent(transform, false);
            _snakeView = viewGO.AddComponent<SnakeView>();
            _snakeView.Init(_config, viewGO.transform);

            var boardView = viewGO.AddComponent<BoardView>();
            boardView.Init(_config, viewGO.transform);
            BoardView.FitCamera(Camera.main, _config);

            // ---------- 7. UI ----------
            var uiGO = new GameObject("UIManager");
            uiGO.transform.SetParent(transform, false);
            var ui = uiGO.AddComponent<UIManager>();

            // ---------- 8. Связывание событий (composition root — единственное место, где всё знают друг о друге) ----------
            WireEvents();

            ui.Init(_controller);
        }

        /// <summary>
        /// Инъекция зависимостей в GameController.
        /// Решение: Unity сам создаёт MonoBehaviour через Activator и не может позвать
        /// конструктор с аргументами — это известное ограничение движка. Поэтому у
        /// контроллера есть метод InjectDependencies, который вызывается ровно один раз
        /// из композиционного корня. Публичный API класса от этого не меняется.
        /// </summary>
        private void WireEvents()
        {
            // 1) Модель -> View: любое изменение позиции модели мгновенно перекладывается в трансформы.
            _snakeModel.Spawned += m => _snakeView.SyncWithModel(m);
            _snakeModel.Moved  += m => _snakeView.SyncWithModel(m);
            _snakeModel.Grew   += m => _snakeView.SyncWithModel(m);

            // 2) Поле -> View: еда.
            _board.FoodSpawned += cell => _snakeView.ShowFood(cell);
            _board.FoodEaten   += _    => _snakeView.HideFood();

            // 3) Контроллер -> View: сигнал «начался новый шаг» — перезапуск окна интерполяции.
            _controller.StepHappened += m => _snakeView.OnModelStepped(_controller.CurrentMoveInterval);

            // 4) Эффект поедания.
            _controller.EatFxRequested += cell =>
            {
                if (cell.HasValue) _snakeView.PlayEatFx(cell.Value);
            };

            // 5) Пауза: замораживаем интерполяцию, чтобы сегменты «застыли» на середине пути,
            //    а не доехали до клетки после выхода из паузы (Time.timeScale трогать нельзя — UI живёт на scaled time).
            _controller.StateChanged += state =>
            {
                _snakeView.SetInterpolation(state == GameState.Playing);
                if (state == GameState.GameOver)
                    StartCoroutine(ShowGameOverAfterDeath()); // короткая пауза, чтобы игрок увидел столкновение
            };

            // 6) Первичная синхронизация визуала стартовой змеи (она ещё «в меню», но уже видна — живой фон меню).
            _snakeView.SyncWithModel(_snakeModel);
            _board.RespawnFood(_snakeModel.Occupies);
        }

        /// <summary>Задержка перед экраном проигрыша: 0.35s — компромисс между драмой и отзывчивостью.</summary>
        private IEnumerator ShowGameOverAfterDeath()
        {
            yield return new WaitForSecondsRealtime(0.35f);
            // Панель GameOver включает UIManager по событию StateChanged;
            // событие уже ушло, поэтому просто форсируем обновление состояния повторной рассылкой.
            _controller.NotifyStateRefresh();
        }

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGO.tag = "MainCamera";
                camGO.transform.SetParent(null, false);
                cam = camGO.GetComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = _config.BackgroundColor;
        }
    }
}
