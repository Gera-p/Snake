using SnakeGame.Core;
using SnakeGame.Model;
using UnityEngine;

namespace SnakeGame.Rendering
{
    /// <summary>
    /// Статичная отрисовка мира: фон, рамка поля, опциональная сетка.
    /// Создаётся ОДИН раз при инициализации (не в Update) — статичные объекты
    /// не генерируют перерисовку трансформов, значит нет лишней нагрузки.
    /// </summary>
    public class BoardView : MonoBehaviour
    {
        private GameConfig _config;

        public void Init(GameConfig config, Transform parent)
        {
            _config = config;
            BuildStaticVisuals(parent);
        }

        private void BuildStaticVisuals(Transform parent)
        {
            float w = _config.GridWidth * _config.CellSize;
            float h = _config.GridHeight * _config.CellSize;

            // --- Фон: один растянутый квадрат позади всего.
            var bg = ProceduralSprites.CreateBlock(ProceduralSprites.Square,
                _config.BackgroundColor, -10, transform);
            bg.name = "Background";
            bg.transform.position = new Vector3(0, 0, 5f); // чуть дальше камеры по Z — гарантированно позади
            bg.transform.localScale = new Vector3(w + _config.CellSize * 6f,
                                                  h + _config.CellSize * 6f, 1f);

            // --- Рамка поля: 4 блока стены толщиной в полклетки. Дешевле и честнее, чем N блоков по периметру.
            float t = _config.CellSize * 0.5f;
            CreateWallBar("WallTop",    new Vector3(0,  h / 2f + t / 2f, 0), new Vector3(w + t * 2f, t, 1f), parent);
            CreateWallBar("WallBottom", new Vector3(0, -h / 2f - t / 2f, 0), new Vector3(w + t * 2f, t, 1f), parent);
            CreateWallBar("WallLeft",   new Vector3(-w / 2f - t / 2f, 0, 0), new Vector3(t, h, 1f), parent);
            CreateWallBar("WallRight",  new Vector3( w / 2f + t / 2f, 0, 0), new Vector3(t, h, 1f), parent);

            // --- Тонкая шахматная сетка внутри поля для читаемости клеток.
            // Один спрайт с tile-повтором эмулируется масштабированной CheckerLine-полосой:
            // для простоты рисуем клетки «шахматкой» только если поле небольшое (иначе дорого).
            if (_config.GridWidth * _config.GridHeight <= 20 * 20)
            {
                var gridRoot = new GameObject("Grid").transform;
                gridRoot.SetParent(parent, false);
                for (int y = 0; y < _config.GridHeight; y++)
                for (int x = 0; x < _config.GridWidth; x++)
                {
                    if (((x + y) & 1) != 1) continue; // только «тёмные» клетки — вдвое меньше объектов
                    var cell = ProceduralSprites.CreateBlock(ProceduralSprites.Square,
                        new Color(1, 1, 1, 0.03f), -8, gridRoot);
                    cell.name = "GridCell";
                    // Позиция считается тем же способом, что и у змеи — единый источник координат.
                    float ox = -(_config.GridWidth - 1) * 0.5f * _config.CellSize;
                    float oy = -(_config.GridHeight - 1) * 0.5f * _config.CellSize;
                    cell.transform.position = new Vector3(ox + x * _config.CellSize,
                                                           oy + y * _config.CellSize, 0f);
                    cell.transform.localScale = Vector3.one * _config.CellSize;
                }
            }
        }

        private void CreateWallBar(string name, Vector3 pos, Vector3 scale, Transform parent)
        {
            var go = ProceduralSprites.CreateBlock(ProceduralSprites.CheckerLine,
                _config.WallColor, -5, parent);
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = scale;
        }

        /// <summary>
        /// Подгоняет ортографическую камеру под размер поля + UI-зазоры.
        /// Вызывается Bootstrap-ом после создания BoardView.
        /// </summary>
        public static void FitCamera(Camera cam, GameConfig config)
        {
            if (cam == null) return;
            float worldW = config.GridWidth * config.CellSize;
            float worldH = config.GridHeight * config.CellSize;

            float aspect = cam.aspect;
            float margin = 1.5f * config.CellSize; // запас сверху/снизу под HUD
            float neededV = (worldH + margin * 2f) * 0.5f;
            float neededH = (worldW + margin * 2f) * 0.5f / aspect;

            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(neededV, neededH);
            cam.transform.position = new Vector3(0, 0, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = config.BackgroundColor;
        }
    }
}
