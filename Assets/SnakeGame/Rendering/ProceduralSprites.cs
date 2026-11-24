using UnityEngine;

namespace SnakeGame.Rendering
{
    /// <summary>
    /// Процедурная генерация спрайтов. Никаких ассетов: рисуем пиксельную
    /// текстуру кодом и заворачиваем в Sprite. Все визуальные элементы игры
    /// (фон, стены, сегменты, еда) используют эти спрайты + SpriteRenderer.color.
    ///
    /// Почему белый квадрат, а не цветной? Цвет задаётся через material color
    /// у SpriteRenderer — один спрайт покрывает все цвета конфигурации.
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite _square;      // сплошной квадрат (главный примитив)
        private static Sprite _circle;      // «яблоко» — скруглённый квадрат с антиалиасингом
        private static Sprite _checkerLine; // тонкая линия для разметки сетки/стен

        public static Sprite Square
        {
            get
            {
                if (_square == null)
                {
                    var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    tex.SetPixel(0, 0, Color.white);
                    tex.Apply();
                    _square = Sprite.Create(tex, new Rect(0, 0, 1, 1),
                        new Vector2(0.5f, 0.5f), 1f); // PPU=1 -> спрайт ровно 1x1 юнит
                    _square.name = "ProceduralSquare";
                }
                return _square;
            }
        }

        /// <summary>Круглый спрайт 16x16 с простым суперсэмплингом — еда выглядит «яблоком», а не плиткой.</summary>
        public static Sprite Circle
        {
            get
            {
                if (_circle == null)
                {
                    const int size = 16;
                    var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                    float center = (size - 1) * 0.5f;
                    float radius = size * 0.5f - 0.5f;

                    for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        // Суперсэмплинг 2x2: считаем долю покрытых субпикселей — получаем мягкий край.
                        int covered = 0;
                        for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                        {
                            float px = x + sx * 0.5f - center;
                            float py = y + sy * 0.5f - center;
                            if (px * px + py * py <= radius * radius) covered++;
                        }
                        tex.SetPixel(x, y, new Color(1, 1, 1, covered / 4f));
                    }
                    tex.Apply();
                    _circle = Sprite.Create(tex, new Rect(0, 0, size, size),
                        new Vector2(0.5f, 0.5f), size); // PPU=16 -> снова 1x1 юнит
                    _circle.name = "ProceduralCircle";
                }
                return _circle;
            }
        }

        /// <summary>Пунктирная граница поля (для «стенок» без объёмных блоков).</summary>
        public static Sprite CheckerLine
        {
            get
            {
                if (_checkerLine == null)
                {
                    const int w = 8, h = 8;
                    var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool on = ((x / 2) + (y / 2)) % 2 == 0; // шахматный узор 2x2
                        tex.SetPixel(x, y, on ? Color.white : new Color(1, 1, 1, 0.35f));
                    }
                    tex.Apply();
                    _checkerLine = Sprite.Create(tex, new Rect(0, 0, w, h),
                        new Vector2(0.5f, 0.5f), w);
                    _checkerLine.name = "ProceduralChecker";
                }
                return _checkerLine;
            }
        }

        /// <summary>
        /// Создаёт GameObject со SpriteRenderer поверх встроенного белого квадрата.
        /// Единственный способ собрать «префаб» без префабов — фабрика-функция для пула.
        /// </summary>
        public static GameObject CreateBlock(Sprite sprite, Color color, float sortingOrder, Transform parent)
        {
            var go = new GameObject("Block");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = (int)sortingOrder;

            // Убираем фильтрацию, чтобы 1-пиксельные спрайты были чёткими на любом разрешении.
            if (sprite.texture != null) sprite.texture.filterMode = FilterMode.Point;
            return go;
        }
    }
}
