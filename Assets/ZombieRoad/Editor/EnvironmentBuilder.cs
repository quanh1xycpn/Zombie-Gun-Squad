using UnityEditor;
using UnityEngine;

namespace ZombieRoad
{
    // Menu "ZombieRoad" trên thanh menu Unity:
    //  - Dựng môi trường (đường + cỏ + tường) thành GameObject thật trong scene để tự chỉnh sửa
    //  - Game runtime thấy object "Environment" sẽ KHÔNG sinh môi trường nữa, chỉ sinh gameplay
    public static class EnvironmentBuilder
    {
        const float Length = 480f;   // phủ mọi màn (dài nhất 420m) + dư
        const float StartZ = -20f;

        [MenuItem("ZombieRoad/1. Dựng môi trường vào Scene")]
        public static void BuildEnvironment()
        {
            var old = GameObject.Find("Environment");
            if (old != null)
            {
                if (!EditorUtility.DisplayDialog("Environment đã tồn tại",
                    "Scene đã có 'Environment'. Xóa và dựng lại từ đầu?", "Dựng lại", "Thôi"))
                    return;
                Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject("Environment");
            Undo.RegisterCreatedObjectUndo(root, "Build Environment");
            float hw = GameBalance.RoadHalfWidth;
            float midZ = StartZ + Length * 0.5f;

            // Mặt đường
            var road = MakeBox(root.transform, "Road", new Vector3(0f, -0.25f, midZ),
                new Vector3(hw * 2f + 1f, 0.5f, Length), new Color(0.72f, 0.72f, 0.74f));

            // Thảm cỏ 2 bên (ốp sát mép đường, không hở khe lộ nền trời)
            float roadHalf = hw + 0.5f;
            MakeBox(root.transform, "GroundL", new Vector3(-roadHalf - 10f, -0.3f, midZ),
                new Vector3(20f, 0.4f, Length), new Color(0.45f, 0.6f, 0.4f));
            MakeBox(root.transform, "GroundR", new Vector3(roadHalf + 10f, -0.3f, midZ),
                new Vector3(20f, 0.4f, Length), new Color(0.45f, 0.6f, 0.4f));

            // Tường lát nối tiếp 2 bên, 2 mặt giống nhau úp vào trong
            var wallsRoot = new GameObject("Walls");
            wallsRoot.transform.SetParent(root.transform, false);
            if (ModelLib.LoadProp("side_wall") != null)
            {
                const float seg = 8f;
                for (float wz = StartZ; wz < StartZ + Length; wz += seg)
                {
                    ModelLib.SpawnWallSegment("side_wall", wallsRoot.transform, seg,
                        new Vector3(-hw - 1.1f, 0f, wz + seg * 0.5f), false);
                    ModelLib.SpawnWallSegment("side_wall", wallsRoot.transform, seg,
                        new Vector3(hw + 1.1f, 0f, wz + seg * 0.5f), true);
                }
            }
            else
            {
                Debug.LogWarning("Không thấy model side_wall trong Resources/Models/Props — bỏ qua tường.");
            }

            Selection.activeGameObject = root;
            EditorSceneUtilityMarkDirty();
            Debug.Log("ZombieRoad: đã dựng Environment (" + Length + "m). Chỉnh sửa thoải mái rồi Save scene. " +
                      "Runtime sẽ dùng môi trường này thay vì tự sinh.");
        }

        [MenuItem("ZombieRoad/2. Xóa môi trường khỏi Scene")]
        public static void ClearEnvironment()
        {
            var old = GameObject.Find("Environment");
            if (old == null)
            {
                EditorUtility.DisplayDialog("Không có gì để xóa", "Scene không có object 'Environment'.", "OK");
                return;
            }
            Undo.DestroyObjectImmediate(old);
            EditorSceneUtilityMarkDirty();
            Debug.Log("ZombieRoad: đã xóa Environment — runtime sẽ quay lại tự sinh môi trường.");
        }

        static GameObject MakeBox(Transform parent, string name, Vector3 pos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var mat = GameAssets.Mat(color);
            mat.name = name + "Mat";
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void EditorSceneUtilityMarkDirty()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
