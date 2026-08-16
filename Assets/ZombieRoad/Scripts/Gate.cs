using UnityEngine;

namespace ZombieRoad
{
    public class Gate : MonoBehaviour
    {
        public GateInfo info;
        public bool applied;

        static Material postMat;

        public static Gate Create(GateInfo info, Transform parent)
        {
            var go = new GameObject("Gate_" + info.z.ToString("F0"));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 0f, info.z);
            var g = go.AddComponent<Gate>();
            g.info = info;
            g.Build();
            return g;
        }

        void Build()
        {
            if (postMat == null) postMat = GameAssets.Mat(new Color(0.25f, 0.25f, 0.3f));
            float hw = GameBalance.RoadHalfWidth;
            BuildPost(new Vector3(-hw, 0f, 0f));
            BuildPost(new Vector3(0f, 0f, 0f));
            BuildPost(new Vector3(hw, 0f, 0f));
            BuildPanel(-hw * 0.5f, info.leftOp, info.leftVal);
            BuildPanel(hw * 0.5f, info.rightOp, info.rightVal);
        }

        void BuildPost(Vector3 pos)
        {
            GameAssets.Prim(PrimitiveType.Cylinder, "Post", transform, pos + Vector3.up * 1.4f, new Vector3(0.22f, 1.4f, 0.22f), postMat);
        }

        void BuildPanel(float x, GateOp op, float val)
        {
            Color c;
            if (op == GateOp.Multiply) c = new Color(0.15f, 0.25f, 0.95f);
            else if (op == GateOp.Add) c = new Color(0.95f, 0.12f, 0.15f);
            else if (op == GateOp.Minus) c = new Color(0.45f, 0.1f, 0.1f);
            else if (op == GateOp.Divide) c = new Color(0.3f, 0.12f, 0.38f); // ×− tím sẫm
            else c = new Color(0.22f, 0.22f, 0.22f); // -% xám đen

            var mat = GameAssets.Mat(c, true);
            float w = GameBalance.RoadHalfWidth - 0.5f;
            GameAssets.Prim(PrimitiveType.Cube, "Panel", transform, new Vector3(x, 1.3f, 0f), new Vector3(w, 1.6f, 0.12f), mat);

            string label = Label(op, val);
            var tm = GameAssets.WorldText(label, transform, new Vector3(x, 1.35f, -0.12f), 1.3f, Color.white);
            tm.transform.localRotation = Quaternion.identity;
        }

        public static string Label(GateOp op, float val)
        {
            if (op == GateOp.Multiply) return "×+" + Mathf.RoundToInt(val);
            if (op == GateOp.Add) return "+" + Mathf.RoundToInt(val);
            if (op == GateOp.Minus) return "−" + Mathf.RoundToInt(val);
            if (op == GateOp.Divide) return "×−" + Mathf.RoundToInt(val);
            return "−" + Mathf.RoundToInt(val) + "%";
        }
    }
}
