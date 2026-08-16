using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    // Nạp model props từ Resources/Models/Props, chuẩn hóa trục dài nhất về +Z
    // và scale về đúng kích thước mong muốn. Prefab thiếu thì trả null để code fallback primitive.
    public static class ModelLib
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static GameObject LoadProp(string name)
        {
            GameObject p;
            if (!cache.TryGetValue(name, out p))
            {
                p = Resources.Load<GameObject>("Models/Props/" + name);
                cache[name] = p;
            }
            return p;
        }

        static Material wallMat;

        // Material tường: chỉ base color, TẮT metallic — texture Meshy đánh dấu kim loại sai
        // khiến tường phản chiếu màu trời thành mảng xanh chéo
        static Material WallMat
        {
            get
            {
                if (wallMat == null)
                {
                    wallMat = GameAssets.Mat(Color.white);
                    var tex = Resources.Load<Texture2D>("Models/Props/side_wall_base_color");
                    if (tex != null)
                    {
                        if (wallMat.HasProperty("_BaseMap")) wallMat.SetTexture("_BaseMap", tex);
                        if (wallMat.HasProperty("_MainTex")) wallMat.SetTexture("_MainTex", tex);
                    }
                    if (wallMat.HasProperty("_Metallic")) wallMat.SetFloat("_Metallic", 0f);
                    if (wallMat.HasProperty("_Smoothness")) wallMat.SetFloat("_Smoothness", 0.15f);
                }
                return wallMat;
            }
        }

        static Bounds GetBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        // Đoạn tường: dài nhất nằm dọc đường (Z), mỏng nhất là bề dày (X), còn lại là chiều cao (Y), đáy chạm đất
        // yaw180: xoay 180° để tường bên phải úp cùng một mặt vào đường như bên trái (không so le xanh/đen)
        public static GameObject SpawnWallSegment(string name, Transform parent, float targetLen, Vector3 groundPos, bool yaw180)
        {
            var prefab = LoadProp(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.transform.position = groundPos;
            // GIỮ rotation gốc của prefab (chứa phép đổi trục của FBX), chỉ reset scale
            go.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = Vector3.one;

            if (go.GetComponentsInChildren<Renderer>().Length == 0) return go;

            // Bước 1: đưa trục dài nhất về Z — CỘNG DỒN với rotation gốc của FBX
            // (Blender xuất FBX có sẵn phép đổi trục ở root, ghi đè là model lật nghiêng)
            Bounds b = GetBounds(go);
            Vector3 s = b.size;
            if (s.x >= s.y && s.x >= s.z) go.transform.rotation = Quaternion.Euler(0f, -90f, 0f) * go.transform.rotation;
            else if (s.y > s.x && s.y > s.z) go.transform.rotation = Quaternion.Euler(90f, 0f, 0f) * go.transform.rotation;

            // Bước 2: trong 2 trục còn lại, bề cao (Y) phải >= bề dày (X)
            b = GetBounds(go);
            if (b.size.x > b.size.y)
                go.transform.rotation = Quaternion.Euler(0f, 0f, 90f) * go.transform.rotation;

            // Bước 2b: model Meshy bị lộn ngược — lật lại 180° quanh trục dọc tường
            go.transform.rotation = Quaternion.Euler(0f, 0f, 180f) * go.transform.rotation;

            // Bước 2c: lật mặt cho tường bên phải để 2 bên đối xứng
            if (yaw180)
                go.transform.rotation = Quaternion.Euler(0f, 180f, 0f) * go.transform.rotation;

            // Bước 3: scale theo chiều dài đoạn
            b = GetBounds(go);
            if (b.size.z > 0.0001f)
                go.transform.localScale = Vector3.one * (targetLen / b.size.z);

            // Bước 4: đặt tâm về vị trí, đáy chạm mặt đất
            b = GetBounds(go);
            Vector3 shift = groundPos - b.center;
            shift.y = groundPos.y - b.min.y;
            go.transform.position += shift;

            // Bước 5: thay material chống phản chiếu xanh
            var rends2 = go.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < rends2.Length; i++)
                rends2[i].sharedMaterial = WallMat;
            return go;
        }

        public static GameObject SpawnNormalized(string name, Transform parent, float targetLen)
        {
            var prefab = LoadProp(name);
            if (prefab == null) return null;
            var go = Object.Instantiate(prefab, parent, false);
            go.transform.localPosition = Vector3.zero;
            // GIỮ rotation gốc của prefab (chứa phép đổi trục của FBX), chỉ reset scale
            go.transform.localRotation = prefab.transform.localRotation;
            go.transform.localScale = Vector3.one;

            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return go;

            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            Vector3 s = b.size;

            // Xoay trục dài nhất của model về +Z — CỘNG DỒN với rotation gốc của FBX (không ghi đè,
            // vì Blender xuất FBX có sẵn phép đổi trục Z-up -> Y-up nằm ở root)
            if (s.x >= s.y && s.x >= s.z) go.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * go.transform.localRotation;
            else if (s.y > s.x && s.y > s.z) go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * go.transform.localRotation;

            float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (maxDim > 0.0001f)
                go.transform.localScale = Vector3.one * (targetLen / maxDim);

            // Đưa tâm bounds về đúng gốc parent
            rends = go.GetComponentsInChildren<Renderer>();
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position += go.transform.parent != null
                ? go.transform.parent.position - b.center
                : -b.center;
            return go;
        }
    }
}
