using UnityEngine;
using UnityEngine.InputSystem;

namespace FBTS
{
    /// <summary>Placeholder art made from code: circles, boxes and lines. Swap for real sprites later.</summary>
    public static class Draw
    {
        static Sprite circle, square;

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float r = n / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                        byte a = (byte)(Mathf.Clamp01(r - d) * 255);
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                return circle;
            }
        }

        public static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4);
                return square;
            }
        }

        public static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, Vector2 pos, Vector2 scale, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        public static SpriteRenderer Dot(string name, Transform parent, Vector2 pos, float diameter, Color color, int order)
            => MakeSprite(name, parent, Circle, pos, new Vector2(diameter, diameter), color, order);

        public static SpriteRenderer Line(string name, Transform parent, Vector2 a, Vector2 b, float width, Color color, int order)
        {
            var sr = MakeSprite(name, parent, Square, (a + b) / 2f, new Vector2(Vector2.Distance(a, b), width), color, order);
            float ang = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
            sr.transform.localRotation = Quaternion.Euler(0, 0, ang);
            return sr;
        }

        /// <summary>A simple arrow-shaped ship: a body plus a nose dot.</summary>
        public static Transform Ship(string name, Transform parent, Color color, float size)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            MakeSprite("Hull", root, Square, Vector2.zero, new Vector2(size, size * 0.5f), color, 20);
            Dot("Nose", root, new Vector2(size * 0.55f, 0f), size * 0.4f, color, 20);
            Dot("Engine", root, new Vector2(-size * 0.55f, 0f), size * 0.3f, new Color(1f, 0.6f, 0.2f), 19);
            return root;
        }
    }

    /// <summary>Keyboard + gamepad input through the Input System package (the project's only input backend).</summary>
    public static class Controls
    {
        public static Vector2 Move()
        {
            Vector2 v = Vector2.zero;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1;
            }
            var g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();
            return Vector2.ClampMagnitude(v, 1f);
        }

        public static bool Action()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame))
                || (g != null && g.buttonSouth.wasPressedThisFrame);
        }

        public static bool Skip()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            var m = Mouse.current;
            return (k != null && (k.spaceKey.wasPressedThisFrame || k.eKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                || (g != null && (g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame))
                || (m != null && m.leftButton.wasPressedThisFrame);
        }

        /// <summary>Secondary action: L key or gamepad Y (launch from a planet).</summary>
        public static bool Secondary()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && k.lKey.wasPressedThisFrame) || (g != null && g.buttonNorth.wasPressedThisFrame);
        }

        public static bool Map()
        {
            var k = Keyboard.current;
            var g = Gamepad.current;
            return (k != null && k.mKey.wasPressedThisFrame) || (g != null && g.selectButton.wasPressedThisFrame);
        }

        public static bool Clicked(out Vector2 screenPos)
        {
            var m = Mouse.current;
            screenPos = m != null ? m.position.ReadValue() : Vector2.zero;
            return m != null && m.leftButton.wasPressedThisFrame;
        }

        public static float Scroll()
        {
            var m = Mouse.current;
            return m != null ? m.scroll.ReadValue().y : 0f;
        }
    }
}
