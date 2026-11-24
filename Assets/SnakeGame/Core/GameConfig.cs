using UnityEngine;

namespace SnakeGame.Core
{
    /// <summary>
    /// Конфигурация игры. ScriptableObject — единая точка правды для всех настроек.
    /// Архитектурное решение: держим данные отдельно от логики, чтобы можно было
    /// менять баланс (размер сетки, скорость) без вмешательства в код.
    ///
    /// ВАЖНО: скрипт НЕ требует ручного создания asset-ов. Bootstrap сам создаст
    /// экземпляр через CreateInstance&lt;GameConfig&gt;() с дефолтными значениями.
    /// При желании можно создать .asset в меню: Assets > Create > Snake Game > Game Config
    /// и присвоить его полю bootstrap-а — код подхватит ваш конфиг вместо дефолтного.
    /// </summary>
    [CreateAssetMenu(fileName = "SnakeGameConfig", menuName = "Snake Game/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Сетка (логика всегда в Vector2Int)")]
        [Tooltip("Ширина игрового поля в клетках.")]
        public int GridWidth = 20;

        [Tooltip("Высота игрового поля в клетках.")]
        public int GridHeight = 16;

        [Tooltip("Мировой размер одной клетки (юниты). Все визуалы масштабируются отсюда.")]
        public float CellSize = 1f;

        [Header("Время (движение по фиксированному шагу, физика не используется)")]
        [Tooltip("Начальная длительность одного шага змеи в секундах.")]
        public float StartMoveInterval = 0.18f;

        [Tooltip("Минимально возможный интервал (предел ускорения).")]
        public float MinMoveInterval = 0.07f;

        [Tooltip("Насколько ускоряется змея за каждое съеденное яблоко.")]
        public float SpeedUpPerFood = 0.006f;

        [Tooltip("Длина змеи при старте (голова + тело).")]
        public int StartLength = 4;

        [Header("Цвета (процедурные спрайты, ассеты не нужны)")]
        public Color BackgroundColor = new Color(0.09f, 0.10f, 0.13f);
        public Color WallColor = new Color(0.25f, 0.28f, 0.35f);
        public Color HeadColor = new Color(0.30f, 0.90f, 0.45f);
        public Color BodyColor = new Color(0.15f, 0.60f, 0.30f);
        public Color FoodColor = new Color(0.95f, 0.30f, 0.25f);

        // Клон для безопасных изменений во время рантайма (чтобы не мутировать shared asset).
        public GameConfig Clone()
        {
            var copy = CreateInstance<GameConfig>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(this), copy);
            return copy;
        }
    }
}
