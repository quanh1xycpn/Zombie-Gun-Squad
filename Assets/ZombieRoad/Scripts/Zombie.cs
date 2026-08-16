using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    public enum ZombieType { Normal, Runner, Tank, Boss }

    public class Zombie : MonoBehaviour
    {
        public static readonly List<Zombie> All = new List<Zombie>();

        public float hp, maxHp, speed, scale;
        public ZombieType type = ZombieType.Normal;
        // Quái spawn từ đích: luôn lao về phía đội, không chờ vào tầm aggro
        public bool alwaysChase;
        public float frozenUntil;
        float attackCd;
        Material bodyMat;
        Color baseColor;
        bool tinted;
        bool dead;

        Transform barRoot;
        Transform barFill;
        Material fillMat, barBgMat;

        public bool Alive { get { return !dead; } }
        public bool Frozen { get { return Time.time < frozenUntil; } }

        public static Zombie Spawn(Vector3 pos, float hp, float speed, float scale, Transform parent, ZombieType type)
        {
            var root = new GameObject("Zombie_" + type);
            root.transform.SetParent(parent, true);
            root.transform.position = pos;
            root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var z = root.AddComponent<Zombie>();
            z.hp = z.maxHp = hp;
            z.speed = speed;
            z.scale = scale;
            z.type = type;
            z.Build();
            return z;
        }

        // Boss/Tank mỗi đòn giết nhiều lính hơn
        int KillPower
        {
            get
            {
                if (type == ZombieType.Boss) return 3;
                if (type == ZombieType.Tank) return 2;
                return 1;
            }
        }

        // Màu quái đổi theo mốc màn: cứ 10 màn lên 1 tông dữ hơn
        static readonly Color[] LevelColors = new Color[]
        {
            new Color(0.55f, 0.66f, 0.52f), // 1-10: xanh xác chết
            new Color(0.72f, 0.68f, 0.35f), // 11-20: vàng úa
            new Color(0.85f, 0.50f, 0.20f), // 21-30: cam
            new Color(0.65f, 0.28f, 0.18f), // 31-40: đỏ nâu
            new Color(0.78f, 0.13f, 0.13f), // 41-50: đỏ máu
            new Color(0.55f, 0.18f, 0.62f), // 51-60: tím độc
            new Color(0.22f, 0.28f, 0.72f), // 61-70: xanh quỷ
            new Color(0.30f, 0.30f, 0.36f), // 71-80: xám thép
            new Color(0.22f, 0.08f, 0.08f), // 81-90: đen đỏ
            new Color(0.12f, 0.05f, 0.18f), // 91-100: đen tím
        };

        // Model thật 4 loại zombie (Meshy) — nạp 1 lần
        static bool charChecked;
        static readonly GameObject[] charPrefabs = new GameObject[4];
        static readonly RuntimeAnimatorController[] charCtrls = new RuntimeAnimatorController[4];
        static readonly Material[] charMats = new Material[4];
        static readonly string[] charNames = { "zombie_normal", "zombie_runner", "zombie_tank", "zombie_boss" };
        static readonly float[] charHeights = { 2.0f, 1.75f, 2.2f, 2.6f };
        Animator animator;
        float animSpeed = 1f;

        static void EnsureCharModels()
        {
            if (charChecked) return;
            charChecked = true;
            for (int i = 0; i < 4; i++)
            {
                charPrefabs[i] = Resources.Load<GameObject>("Models/Chars/" + charNames[i]);
                charCtrls[i] = Resources.Load<RuntimeAnimatorController>(charNames[i] + "_ctrl");
                charMats[i] = Resources.Load<Material>(charNames[i] + "_mat");
            }
        }

        void Build()
        {
            int lv = (GameManager.I != null && GameManager.I.Data != null) ? GameManager.I.Data.level : 1;
            Color baseCol = LevelColors[Mathf.Clamp((lv - 1) / 10, 0, LevelColors.Length - 1)];
            // Con càng to trong màn thì màu càng sẫm
            float t = Mathf.InverseLerp(1f, 2.4f, scale);
            baseColor = Color.Lerp(baseCol, baseCol * 0.55f, t);

            // Boss màu càng về cuối càng đậm đặc
            if (type == ZombieType.Boss)
                baseColor *= Mathf.Lerp(0.8f, 0.45f, (lv - 1f) / 99f);
            else if (type == ZombieType.Runner)
                baseColor = Color.Lerp(baseColor, Color.white, 0.25f);
            else if (type == ZombieType.Tank)
                baseColor *= 0.8f;

            EnsureCharModels();
            int mapIdx = type == ZombieType.Boss ? 3 : (type == ZombieType.Tank ? 2 : (type == ZombieType.Runner ? 1 : 0));
            if (charPrefabs[mapIdx] != null)
            {
                BuildModelVisual(mapIdx);
                transform.localScale = Vector3.one * scale;
                BuildHpBar();
                if (type == ZombieType.Boss)
                {
                    barRoot.localScale = Vector3.one * 1.7f;
                    barRoot.localPosition = new Vector3(0f, 3.4f, 0f);
                    barRoot.gameObject.SetActive(true);
                }
                return;
            }

            bodyMat = GameAssets.Mat(baseColor);

            if (type == ZombieType.Runner)
            {
                // Gầy, nhanh
                GameAssets.Prim(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, 1f, 0f), new Vector3(0.55f, 1.05f, 0.55f), bodyMat);
                GameAssets.Prim(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, 2.1f, 0f), Vector3.one * 0.45f, bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmL", transform, new Vector3(-0.26f, 1.5f, 0.5f), new Vector3(0.12f, 0.12f, 0.75f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmR", transform, new Vector3(0.26f, 1.5f, 0.5f), new Vector3(0.12f, 0.12f, 0.75f), bodyMat);
            }
            else if (type == ZombieType.Tank)
            {
                // Bè ngang, vai u thịt bắp
                GameAssets.Prim(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, 1f, 0f), new Vector3(1.15f, 0.95f, 1.15f), bodyMat);
                GameAssets.Prim(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, 2f, 0f), Vector3.one * 0.6f, bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ShoulderL", transform, new Vector3(-0.65f, 1.7f, 0f), new Vector3(0.4f, 0.4f, 0.5f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ShoulderR", transform, new Vector3(0.65f, 1.7f, 0f), new Vector3(0.4f, 0.4f, 0.5f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmL", transform, new Vector3(-0.5f, 1.4f, 0.5f), new Vector3(0.25f, 0.25f, 0.8f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmR", transform, new Vector3(0.5f, 1.4f, 0.5f), new Vector3(0.25f, 0.25f, 0.8f), bodyMat);
            }
            else if (type == ZombieType.Boss)
            {
                // Khổng lồ, có sừng và mắt đỏ
                GameAssets.Prim(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, 1.05f, 0f), new Vector3(1.2f, 1.1f, 1.2f), bodyMat);
                GameAssets.Prim(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, 2.25f, 0f), Vector3.one * 0.7f, bodyMat);
                var hornMat = GameAssets.Mat(new Color(0.12f, 0.1f, 0.1f));
                GameAssets.Prim(PrimitiveType.Cube, "HornL", transform, new Vector3(-0.3f, 2.75f, 0f), new Vector3(0.14f, 0.5f, 0.14f), hornMat);
                GameAssets.Prim(PrimitiveType.Cube, "HornR", transform, new Vector3(0.3f, 2.75f, 0f), new Vector3(0.14f, 0.5f, 0.14f), hornMat);
                var eyeMat = GameAssets.Mat(new Color(1f, 0.1f, 0.1f), true);
                GameAssets.Prim(PrimitiveType.Sphere, "EyeL", transform, new Vector3(-0.16f, 2.3f, 0.3f), Vector3.one * 0.14f, eyeMat);
                GameAssets.Prim(PrimitiveType.Sphere, "EyeR", transform, new Vector3(0.16f, 2.3f, 0.3f), Vector3.one * 0.14f, eyeMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmL", transform, new Vector3(-0.55f, 1.5f, 0.55f), new Vector3(0.3f, 0.3f, 0.95f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmR", transform, new Vector3(0.55f, 1.5f, 0.55f), new Vector3(0.3f, 0.3f, 0.95f), bodyMat);
            }
            else
            {
                GameAssets.Prim(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1f, 0.8f), bodyMat);
                GameAssets.Prim(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, 2.05f, 0f), Vector3.one * 0.55f, bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmL", transform, new Vector3(-0.32f, 1.45f, 0.45f), new Vector3(0.18f, 0.18f, 0.7f), bodyMat);
                GameAssets.Prim(PrimitiveType.Cube, "ArmR", transform, new Vector3(0.32f, 1.45f, 0.45f), new Vector3(0.18f, 0.18f, 0.7f), bodyMat);
            }

            transform.localScale = Vector3.one * scale;
            BuildHpBar();

            // Boss luôn hiện thanh máu to ngay từ đầu
            if (type == ZombieType.Boss)
            {
                barRoot.localScale = Vector3.one * 1.7f;
                barRoot.localPosition = new Vector3(0f, 3.4f, 0f);
                barRoot.gameObject.SetActive(true);
            }
        }

        void BuildModelVisual(int mapIdx)
        {
            var go = Instantiate(charPrefabs[mapIdx], transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                float h = smr.bounds.size.y;
                if (h > 0.01f)
                    go.transform.localScale = Vector3.one * (charHeights[mapIdx] / h);
            }

            // Material instance để tint theo màn + tint đóng băng (nhân màu lên texture)
            Material template = charMats[mapIdx];
            bodyMat = template != null ? new Material(template) : GameAssets.Mat(baseColor);
            float maxC = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
            Color hue = maxC > 0.01f ? new Color(baseColor.r / maxC, baseColor.g / maxC, baseColor.b / maxC) : Color.white;
            // Giữ chi tiết texture: pha hue của màn vào nền trắng
            baseColor = Color.Lerp(Color.white, hue, 0.6f);
            SetColor(baseColor);
            var rends = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (int i = 0; i < rends.Length; i++)
                rends[i].sharedMaterial = bodyMat;

            animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            if (charCtrls[mapIdx] != null)
            {
                animator.runtimeAnimatorController = charCtrls[mapIdx];
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animSpeed = 0.85f + Random.value * 0.3f;
                animator.speed = animSpeed;
                animator.Play(0, 0, Random.value);
            }
        }

        void BuildHpBar()
        {
            barRoot = new GameObject("HPBar").transform;
            barRoot.SetParent(transform, false);
            barRoot.localPosition = new Vector3(0f, 2.75f, 0f);
            barBgMat = GameAssets.Mat(new Color(0.1f, 0.1f, 0.1f), true);
            GameAssets.Prim(PrimitiveType.Quad, "BG", barRoot, Vector3.zero, new Vector3(1.1f, 0.16f, 1f), barBgMat);
            fillMat = GameAssets.Mat(new Color(0.2f, 0.9f, 0.2f), true);
            var fill = GameAssets.Prim(PrimitiveType.Quad, "Fill", barRoot, new Vector3(0f, 0f, -0.01f), new Vector3(1.04f, 0.11f, 1f), fillMat);
            barFill = fill.transform;
            barRoot.gameObject.SetActive(false);
        }

        void UpdateHpBar()
        {
            if (barRoot == null) return;
            if (!barRoot.gameObject.activeSelf) barRoot.gameObject.SetActive(true);
            float frac = Mathf.Clamp01(hp / maxHp);
            barFill.localScale = new Vector3(1.04f * frac, 0.11f, 1f);
            barFill.localPosition = new Vector3(-0.52f * (1f - frac), 0f, -0.01f);
            Color c = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.9f, 0.2f), frac);
            if (fillMat.HasProperty("_BaseColor")) fillMat.SetColor("_BaseColor", c);
            if (fillMat.HasProperty("_Color")) fillMat.SetColor("_Color", c);
        }

        void LateUpdate()
        {
            if (barRoot == null || !barRoot.gameObject.activeSelf) return;
            var cam = Camera.main;
            if (cam != null)
                barRoot.rotation = Quaternion.LookRotation(barRoot.position - cam.transform.position);
        }

        void OnDestroy()
        {
            if (bodyMat != null) Destroy(bodyMat);
            if (fillMat != null) Destroy(fillMat);
            if (barBgMat != null) Destroy(barBgMat);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (dead) return;
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;

            if (Frozen)
            {
                if (!tinted)
                {
                    SetColor(new Color(0.4f, 0.85f, 1f));
                    tinted = true;
                    if (animator != null) animator.speed = 0f; // đứng hình khi bị đóng băng
                }
                return;
            }
            if (tinted)
            {
                SetColor(baseColor);
                tinted = false;
                if (animator != null) animator.speed = animSpeed;
            }

            var squad = gm.Squad;
            if (squad == null || squad.Count == 0) return;

            float dist;
            Vector3 target = squad.GetNearestSoldierPos(transform.position, out dist);
            float aggro = 30f;
            if (!alwaysChase && transform.position.z - squad.LeaderZ > aggro && dist > aggro) return;

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            float d = dir.magnitude;
            if (d > 0.7f * scale)
            {
                dir /= d;
                transform.position += dir * speed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir);
            }
            else
            {
                attackCd -= Time.deltaTime;
                if (attackCd <= 0f)
                {
                    attackCd = type == ZombieType.Boss ? 1.5f : 1.2f;
                    int kills = KillPower;
                    for (int k = 0; k < kills; k++)
                        squad.KillOneNear(transform.position);
                    // Boss/Tank lì đòn hơn khi cận chiến
                    float selfDmg = type == ZombieType.Boss ? maxHp * 0.12f
                        : (type == ZombieType.Tank ? maxHp * 0.2f : Mathf.Max(35f, maxHp * 0.34f));
                    Damage(selfDmg);
                }
            }
        }

        void SetColor(Color c)
        {
            if (bodyMat == null) return;
            if (bodyMat.HasProperty("_BaseColor")) bodyMat.SetColor("_BaseColor", c);
            if (bodyMat.HasProperty("_Color")) bodyMat.SetColor("_Color", c);
        }

        public void Damage(float amount)
        {
            if (dead) return;
            hp -= amount;
            UpdateHpBar();
            if (hp <= 0f) Die();
        }

        void Die()
        {
            dead = true;
            if (barRoot != null) barRoot.gameObject.SetActive(false);
            // Có animation chết thật thì ngã xuống rồi lún dần; không thì co rúm như cũ
            bool hasDeath = animator != null && animator.runtimeAnimatorController != null
                && animator.HasState(0, Animator.StringToHash("death"));
            if (hasDeath)
            {
                animator.speed = 1f;
                animator.Play("death", 0, 0f);
                StartCoroutine(DeathAnimModel());
            }
            else
            {
                StartCoroutine(DeathAnimShrink());
            }
        }

        IEnumerator DeathAnimModel()
        {
            yield return new WaitForSeconds(1.3f);
            // Lún xuống đất rồi biến mất
            float t = 0f;
            Vector3 start = transform.position;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                transform.position = start + Vector3.down * (t * 2f);
                yield return null;
            }
            Destroy(gameObject);
        }

        IEnumerator DeathAnimShrink()
        {
            float t = 0f;
            Vector3 start = transform.localScale;
            Quaternion rot = transform.rotation;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float k = 1f - t / 0.35f;
                transform.localScale = start * Mathf.Max(0.01f, k);
                transform.rotation = rot * Quaternion.Euler(t * 200f, 0f, 0f);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
