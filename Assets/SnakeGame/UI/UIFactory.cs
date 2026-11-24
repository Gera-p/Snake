using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SnakeGame.UI
{
    /// <summary>
    /// Фабрика элементов UI. Вся «рутина» создания Canvas/Text/Button/Image
    /// собрана в одном месте, чтобы остальные классы оперировали понятиями
    /// («экран», «кнопка», «лейбл»), а не низ-level UnityUI API.
    ///
    /// Почему не Prefab-и? Требование самодостаточности: сцена пустая,
    /// ассетов нет — значит весь интерфейс строится кодом при старте.
    /// </summary>
    public static class UIFactory
    {
        // Единый кэш шрифта Legacy (Arial.ttf встроен в Unity и доступен из Resources).
        private static Font _cachedFont;

        public static Font GetDefaultFont()
        {
            if (_cachedFont == null)
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // В очень старых версиях Unity имя другое — fallback на всякий случай:
                if (_cachedFont == null) _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return _cachedFont;
        }

        /// <summary>
        /// Создаёт полный UI-стек: EventSystem + Canvas (Screen Space Overlay).
        /// CanvasScaler 1920x1080 с Match 0.5 — интерфейс адекватно масштабируется
        /// на любом разрешении без ручной подгонки.
        /// </summary>
        public static Canvas CreateCanvas(Transform parent)
        {
            var canvasGO = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(parent, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100; // поверх спрайтов игры

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem обязателен для кликов по кнопкам. Создаём один раз.
            if (Object.FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(parent, false);
            }

            return canvas;
        }

        /// <summary>Полнэкранный контейнер («экран») со затемняющей подложкой.</summary>
        public static RectTransform CreateFullScreenPanel(string name, Transform parent, Color background)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            StretchFull(rect);

            var img = go.GetComponent<Image>();
            img.color = background;
            img.raycastTarget = true; // экран блокирует клики «сквозь» него в мир
            return rect;
        }

        /// <summary>Текстовый лейбл по центру родителя (с опциональным смещением).</summary>
        public static Text CreateLabel(string name, Transform parent, string text,
                                       int fontSize, Color color, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1600, 200);
            rect.anchoredPosition = anchoredPos;

            var t = go.GetComponent<Text>();
            t.font = GetDefaultFont();
            t.text = text;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false; // текст не должен перехватывать клики
            return t;
        }

        /// <summary>Кнопка с подписью. onClick наружу — подписывается контроллер экрана.</summary>
        public static Button CreateButton(string name, Transform parent, string label,
                                          Vector2 anchoredPos, Vector2 size,
                                          Color normalColor, UnityAction onClick)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;

            var img = go.GetComponent<Image>();
            img.color = normalColor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            // Цветовые состояния через ColorBlock — стандартный механизм Unity UI.
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            btn.colors = colors;

            // Подпись кнопки — дочерний Text, растянутый на всю кнопку.
            var labelText = CreateLabel(name + "_Label", go.transform, label, 42, Color.white, Vector2.zero);
            StretchFull(labelText.rectTransform);

            return btn;
        }

        /// <summary>Растягивает RectTransform на весь родитель.</summary>
        public static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>HUD-элемент с привязкой к углу экрана (для счёта/паузы).</summary>
        public static RectTransform CreateCornerAnchored(string name, Transform parent,
                                                         TextAnchor corner, Vector2 offset, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            Vector2 anchor = corner switch
            {
                TextAnchor.UpperLeft => new Vector2(0, 1),
                TextAnchor.UpperRight => new Vector2(1, 1),
                TextAnchor.LowerLeft => new Vector2(0, 0),
                _ => new Vector2(1, 0)
            };
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
            return rect;
        }
    }
}
