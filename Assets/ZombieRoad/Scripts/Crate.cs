using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    public class Crate : MonoBehaviour
    {
        public static readonly List<Crate> All = new List<Crate>();

        public float hp;
        TextMesh hpText;
        TextMesh labelText;
        bool destroyed;

        public bool Destroyed { get { return destroyed; } }

        static Material crateMat, lidMat;

        public static Crate Create(CrateInfo info, Transform parent)
        {
            var go = new GameObject("Crate_" + info.z.ToString("F0"));
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(info.x, 0f, info.z);
            var c = go.AddComponent<Crate>();
            c.hp = info.hp;
            c.Build();
            return c;
        }

        void Build()
        {
            if (crateMat == null) crateMat = GameAssets.Mat(new Color(1f, 0.72f, 0.1f));
            if (lidMat == null) lidMat = GameAssets.Mat(new Color(0.18f, 0.18f, 0.22f));
            GameAssets.Prim(PrimitiveType.Cylinder, "Barrel", transform, new Vector3(0f, 0.7f, 0f), new Vector3(2f, 0.7f, 2f), crateMat);
            GameAssets.Prim(PrimitiveType.Cube, "Ring", transform, new Vector3(0f, 0.7f, 0f), new Vector3(2.1f, 0.18f, 2.1f), lidMat);
            GameAssets.Prim(PrimitiveType.Cube, "Gun", transform, new Vector3(0f, 1.65f, 0f), new Vector3(0.4f, 0.4f, 1.6f), lidMat);
            GameAssets.Prim(PrimitiveType.Cube, "GunTip", transform, new Vector3(0f, 1.65f, 0.9f), new Vector3(0.5f, 0.5f, 0.25f), crateMat);
            hpText = GameAssets.WorldText(Mathf.CeilToInt(hp).ToString(), transform, new Vector3(0f, 2.9f, 0f), 1.5f, Color.white);
            // Vật phẩm được roll sẵn từ lúc tạo màn — ghi rõ phần thưởng lên thùng
            labelText = GameAssets.WorldText("GUN +1", transform, new Vector3(0f, 4f, 0f), 1f, new Color(1f, 0.85f, 0.2f));
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (hpText != null)
                hpText.transform.rotation = Quaternion.LookRotation(hpText.transform.position - cam.transform.position);
            if (labelText != null)
                labelText.transform.rotation = Quaternion.LookRotation(labelText.transform.position - cam.transform.position);
        }

        public void Damage(float amount)
        {
            if (destroyed) return;
            hp -= amount;
            if (hpText != null) hpText.text = Mathf.Max(0, Mathf.CeilToInt(hp)).ToString();
            if (hp <= 0f)
            {
                destroyed = true;
                FX.Explosion(transform.position + Vector3.up, 2.5f);
                FX.FloatText(transform.position, "UPGRADE!", new Color(1f, 0.85f, 0.2f));
                if (GameManager.I != null && GameManager.I.Squad != null)
                    GameManager.I.Squad.UpgradeWeapon();
                Destroy(gameObject);
            }
        }
    }
}
