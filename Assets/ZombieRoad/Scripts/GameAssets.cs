using UnityEngine;

namespace ZombieRoad
{
    public static class GameAssets
    {
        static Shader _lit, _unlit;

        public static Shader Lit
        {
            get
            {
                if (_lit == null)
                {
                    _lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (_lit == null) _lit = Shader.Find("Standard");
                }
                return _lit;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null)
                {
                    _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                    if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                }
                return _unlit;
            }
        }

        static Material litTpl, unlitTpl;
        static Material fxAdditive;

        // Material additive dùng chung cho trail đạn / chớp lửa (asset trong Resources)
        public static Material FxAdditive
        {
            get
            {
                if (fxAdditive == null)
                {
                    fxAdditive = Resources.Load<Material>("FxAdditive");
                    if (fxAdditive == null) fxAdditive = Mat(new Color(1f, 0.9f, 0.4f), true);
                }
                return fxAdditive;
            }
        }

        // Trên device, Shader.Find có thể trả null nếu shader bị strip khỏi build.
        // Material template trong Resources (BaseLit/BaseUnlit) vừa giữ shader khỏi bị strip
        // vừa là fallback an toàn.
        public static Material Mat(Color c, bool unlit = false)
        {
            Material m = null;
            Shader sh = unlit ? UnlitShader : Lit;
            if (sh != null)
            {
                m = new Material(sh);
            }
            else
            {
                if (unlit)
                {
                    if (unlitTpl == null) unlitTpl = Resources.Load<Material>("BaseUnlit");
                    if (unlitTpl != null) m = new Material(unlitTpl);
                }
                else
                {
                    if (litTpl == null) litTpl = Resources.Load<Material>("BaseLit");
                    if (litTpl != null) m = new Material(litTpl);
                }
            }
            if (m == null)
            {
                var fb = Shader.Find("Sprites/Default");
                m = fb != null ? new Material(fb) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }

        public static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        public static Font UIFont()
        {
            Font f = null;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
            }
            return f;
        }

        public static TextMesh WorldText(string text, Transform parent, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            var font = UIFont();
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.text = text;
            tm.characterSize = 0.1f;
            tm.fontSize = 96;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.transform.localScale = Vector3.one * size;
            return tm;
        }
    }
}
