using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    public class Bullet : MonoBehaviour
    {
        Vector3 dir;
        float damage;
        float life;
        public TrailRenderer trail;
        const float Speed = 32f;

        public void Launch(Vector3 pos, Vector3 d, float dmg, float range)
        {
            transform.position = pos;
            dir = d.normalized;
            damage = dmg;
            // Mỗi viên có tầm bay riêng (hàng sau bay xa hơn để chết cùng vạch với hàng trước)
            life = Mathf.Max(0.1f, range) / Speed;
            gameObject.SetActive(true);
            if (trail != null) trail.Clear(); // không kéo vệt từ vị trí cũ khi tái sử dụng
        }

        void Update()
        {
            transform.position += dir * Speed * Time.deltaTime;
            life -= Time.deltaTime;
            if (life <= 0f) { BulletPool.Recycle(this); return; }

            Vector3 p = transform.position;

            var zombies = Zombie.All;
            for (int i = 0; i < zombies.Count; i++)
            {
                var z = zombies[i];
                if (z == null || !z.Alive) continue;
                Vector3 zp = z.transform.position + Vector3.up * z.scale;
                float r = 0.95f * z.scale;
                // Loại nhanh theo trục z trước khi tính khoảng cách đầy đủ
                float dz = zp.z - p.z;
                if (dz > r || dz < -r) continue;
                if ((zp - p).sqrMagnitude < r * r)
                {
                    z.Damage(damage);
                    BulletPool.Recycle(this);
                    return;
                }
            }

            var crates = Crate.All;
            for (int i = 0; i < crates.Count; i++)
            {
                var c = crates[i];
                if (c == null || c.Destroyed) continue;
                Vector3 cp = c.transform.position;
                if (Mathf.Abs(cp.x - p.x) < 1.1f && Mathf.Abs(cp.z - p.z) < 1.1f && p.y < 2.5f)
                {
                    c.Damage(damage);
                    BulletPool.Recycle(this);
                    return;
                }
            }
        }
    }

    public static class BulletPool
    {
        static readonly Stack<Bullet> pool = new Stack<Bullet>();
        static readonly List<Bullet> active = new List<Bullet>();
        static Transform root;
        static Material bulletMat;

        static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("BulletPool");
                    Object.DontDestroyOnLoad(go);
                    root = go.transform;
                }
                return root;
            }
        }

        public static void Fire(Vector3 pos, Vector3 dir, float dmg, float range)
        {
            Bullet b = null;
            while (pool.Count > 0 && b == null) b = pool.Pop();
            if (b == null)
            {
                var go = new GameObject("Bullet");
                go.transform.SetParent(Root, false);
                // Model đạn tracer nếu có, không thì cầu vàng
                var m = ModelLib.SpawnNormalized("bullet_tracer", go.transform, 0.5f);
                if (m == null)
                {
                    if (bulletMat == null) bulletMat = GameAssets.Mat(new Color(1f, 0.9f, 0.2f), true);
                    GameAssets.Prim(PrimitiveType.Sphere, "Vis", go.transform, Vector3.zero, Vector3.one * 0.28f, bulletMat);
                }
                b = go.AddComponent<Bullet>();

                // Vệt sáng glow mờ dần sau đuôi đạn
                var tr = go.AddComponent<TrailRenderer>();
                tr.time = 0.13f;
                tr.startWidth = 0.2f;
                tr.endWidth = 0.02f;
                tr.minVertexDistance = 0.25f;
                tr.material = GameAssets.FxAdditive;
                var grad = new Gradient();
                grad.SetKeys(
                    new[] { new GradientColorKey(new Color(1f, 0.95f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.1f), 1f) },
                    new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
                tr.colorGradient = grad;
                b.trail = tr;
            }
            b.Launch(pos, dir, dmg, range);
            active.Add(b);
        }

        public static void Recycle(Bullet b)
        {
            if (b == null) return;
            b.gameObject.SetActive(false);
            active.Remove(b);
            pool.Push(b);
        }

        public static void RecycleAll()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var b = active[i];
                if (b != null) { b.gameObject.SetActive(false); pool.Push(b); }
            }
            active.Clear();
        }
    }
}
