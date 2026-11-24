using System;
using SnakeGame.Controllers;
using SnakeGame.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SnakeGame.UI
{
    /// <summary>
    /// UI-медиатор: строит все экраны и переключает их по событию StateChanged
    /// от контроллера. Экраны — обычные GameObject-и, активация SetActive.
    ///
    /// Архитектурное решение: UI НИКОГДА не лезет в Model. Он дергает только
    /// публичный API контроллера (StartNewGame/TogglePause/GoToMainMenu) и
    /// слушает его события. Это односторонний поток данных (unidirectional flow),
    /// который делает интерфейс тестируемым и защищённым от рассинхрона.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        private GameController _controller;

        private Canvas _canvas;
        private RectTransform _mainMenuPanel;
        private RectTransform _hudPanel;
        private RectTransform _pausePanel;
        private RectTransform _gameOverPanel;

        private Text _scoreText;
        private Text _speedText;
        private Text _finalScoreText;

        // Палитра интерфейса (тёмная тема под цвет поля).
        private static readonly Color DimBackdrop = new Color(0.02f, 0.03f, 0.05f, 0.92f);
        private static readonly Color AccentButton = new Color(0.16f, 0.55f, 0.30f);
        private static readonly Color DangerButton = new Color(0.75f, 0.25f, 0.22f);
        private static readonly Color NeutralButton = new Color(0.30f, 0.33f, 0.40f);

        /// <summary>Точка входа для Bootstrap: передать контроллер — остальное само.</summary>
        public void Init(GameController controller)
        {
            _controller = controller;

            _canvas = UIFactory.CreateCanvas(transform);
            BuildMainMenu();
            BuildHud();
            BuildPauseMenu();
            BuildGameOver();

            // Подписка на высокоуровневые события — единственный канал «игра -> интерфейс».
            _controller.StateChanged += HandleStateChanged;
            _controller.ScoreChanged += HandleScoreChanged;
            _controller.SpeedChanged += HandleSpeedChanged;

            // Начальная фаза.
            HandleStateChanged(_controller.State);
        }

        private void OnDestroy()
        {
            // Отписка обязательна: делегаты держат ссылки, а UI может пережить сцену при догрузке.
            if (_controller != null)
            {
                _controller.StateChanged -= HandleStateChanged;
                _controller.ScoreChanged -= HandleScoreChanged;
                _controller.SpeedChanged -= HandleSpeedChanged;
            }
        }

        // ---------------- Построение экранов ----------------

        private void BuildMainMenu()
        {
            _mainMenuPanel = UIFactory.CreateFullScreenPanel("MainMenu", _canvas.transform, DimBackdrop);

            UIFactory.CreateLabel("Title", _mainMenuPanel, "ЗМЕЙКА", 140,
                new Color(0.30f, 0.90f, 0.45f), new Vector2(0, 260));

            UIFactory.CreateLabel("Subtitle", _mainMenuPanel,
                "WASD / стрелки — движение  |  ESC — пауза", 40,
                new Color(0.8f, 0.8f, 0.85f), new Vector2(0, 120));

            UIFactory.CreateButton("PlayBtn", _mainMenuPanel, "ИГРАТЬ",
                new Vector2(0, -60), new Vector2(520, 110), AccentButton,
                () => _controller.StartNewGame());

            UIFactory.CreateButton("QuitBtn", _mainMenuPanel, "ВЫХОД",
                new Vector2(0, -220), new Vector2(520, 90), NeutralButton,
                () =>
                {
                    // Application.Quit в редакторе не работает — корректный production-way:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
        }

        private void BuildHud()
        {
            _hudPanel = UIFactory.CreateFullScreenPanel("HUD", _canvas.transform, Color.clear);
            _hudPanel.GetComponent<Image>().raycastTarget = false; // HUD не должен глушить клики по игре

            // Счёт — левый верхний угол, привязан anchor-ами, живёт при любом разрешении.
            var scoreRoot = UIFactory.CreateCornerAnchored("ScoreBox", _hudPanel,
                TextAnchor.UpperLeft, new Vector2(40, -20), new Vector2(500, 100));
            _scoreText = UIFactory.CreateLabel("ScoreValue", scoreRoot, "Счёт: 0", 56,
                Color.white, Vector2.zero);
            _scoreText.alignment = TextAnchor.UpperLeft;
            UIFactory.StretchFull(_scoreText.rectTransform); // растягиваем на весь ScoreBox — текст прижмётся к углу

            // Индикатор скорости — правый верхний угол.
            var speedRoot = UIFactory.CreateCornerAnchored("SpeedBox", _hudPanel,
                TextAnchor.UpperRight, new Vector2(-40, -20), new Vector2(500, 100));
            _speedText = UIFactory.CreateLabel("SpeedValue", speedRoot, "Скорость: 1x", 40,
                new Color(1f, 0.85f, 0.4f), Vector2.zero);
            _speedText.alignment = TextAnchor.UpperRight;
            UIFactory.StretchFull(_speedText.rectTransform); // аналогично — прижатие к правому верхнему углу
        }

        private void BuildPauseMenu()
        {
            _pausePanel = UIFactory.CreateFullScreenPanel("PauseMenu", _canvas.transform, DimBackdrop);

            UIFactory.CreateLabel("PausedTitle", _pausePanel, "ПАУЗА", 110,
                Color.white, new Vector2(0, 200));

            UIFactory.CreateButton("ResumeBtn", _pausePanel, "ПРОДОЛЖИТЬ",
                new Vector2(0, 0), new Vector2(520, 100), AccentButton,
                () => _controller.TogglePause());

            UIFactory.CreateButton("MenuFromPauseBtn", _pausePanel, "В ГЛАВНОЕ МЕНЮ",
                new Vector2(0, -160), new Vector2(520, 90), NeutralButton,
                () => _controller.GoToMainMenu());
        }

        private void BuildGameOver()
        {
            _gameOverPanel = UIFactory.CreateFullScreenPanel("GameOver", _canvas.transform, DimBackdrop);

            UIFactory.CreateLabel("GameOverTitle", _gameOverPanel, "ИГРА ОКОНЧЕНА", 110,
                new Color(0.95f, 0.35f, 0.30f), new Vector2(0, 240));

            _finalScoreText = UIFactory.CreateLabel("FinalScore", _gameOverPanel,
                "Счёт: 0", 64, Color.white, new Vector2(0, 80));

            UIFactory.CreateButton("RestartBtn", _gameOverPanel, "РЕСТАРТ",
                new Vector2(0, -80), new Vector2(520, 110), AccentButton,
                () => _controller.StartNewGame());

            UIFactory.CreateButton("MenuBtn", _gameOverPanel, "В ГЛАВНОЕ МЕНЮ",
                new Vector2(0, -240), new Vector2(520, 90), DangerButton,
                () => _controller.GoToMainMenu());
        }

        // ---------------- Обработчики событий контроллера ----------------

        private void HandleStateChanged(GameState state)
        {
            _mainMenuPanel.gameObject.SetActive(state == GameState.MainMenu);
            _hudPanel.gameObject.SetActive(state == GameState.Playing || state == GameState.Paused);
            _pausePanel.gameObject.SetActive(state == GameState.Paused);
            _gameOverPanel.gameObject.SetActive(state == GameState.GameOver);

            if (state == GameState.GameOver)
                _finalScoreText.text = $"Счёт: {_controller.Score}";
        }

        private void HandleScoreChanged(int score)
        {
            _scoreText.text = $"Счёт: {score}";
        }

        private void HandleSpeedChanged(float progress01)
        {
            // Переводим прогресс ускорения в понятный игроку множитель (1.0x .. ~2.5x).
            float mult = Mathf.Lerp(1f, 2.5f, progress01);
            _speedText.text = $"Скорость: {mult:F1}x";
        }
    }
}
