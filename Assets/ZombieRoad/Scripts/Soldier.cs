using UnityEngine;

namespace ZombieRoad
{
    public class Soldier : MonoBehaviour
    {
        public PlayerSquad squad;
        public bool canShoot = true;
        public Vector2 formationJitter;
        float fireTimer;
        int builtTier = -1;
        bool isLeader;
        Animator animator;
        Transform handBone;
        Transform gunRoot;
        Transform muzzleFlash;
        float flashTimer;
        static Material flashMat;

        static Material soldierMat, robotMat, gunMat, leaderMat;
        static Material[] accentMats;

        // Model thật (Meshy) — load 1 lần từ Resources
        static GameObject modelPrefab;
        static Material modelMat;
        static RuntimeAnimatorController runCtrl;
        static GameObject robotPrefab;
        static Material robotModelMat;
        static RuntimeAnimatorController robotCtrl;
        static bool modelChecked;

        static void EnsureModel()
        {
            if (modelChecked) return;
            modelChecked = true;
            // Model hiển thị = soldier_base (bản user chỉnh), clip chạy retarget từ soldier_run qua Humanoid
            modelPrefab = Resources.Load<GameObject>("Models/soldier_base");
            if (modelPrefab == null) modelPrefab = Resources.Load<GameObject>("Models/soldier_run");
            modelMat = Resources.Load<Material>("SoldierMat");
            runCtrl = Resources.Load<RuntimeAnimatorController>("SoldierRunController");
            robotPrefab = Resources.Load<GameObject>("Models/Chars/robot");
            robotModelMat = Resources.Load<Material>("robot_mat");
            robotCtrl = Resources.Load<RuntimeAnimatorController>("robot_ctrl");
        }

        public static Soldier Create(PlayerSquad squad, Vector3 localPos)
        {
            var go = new GameObject("Soldier");
            go.transform.SetParent(squad.transform, false);
            go.transform.localPosition = localPos;
            var s = go.AddComponent<Soldier>();
            s.squad = squad;
            s.fireTimer = Random.value * 0.3f;
            s.formationJitter = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.32f, 0.32f));
            s.SetTier(squad.tierIndex);
            return s;
        }

        // Tái sử dụng lính từ pool: chỉ rebuild visual nếu tier/leader đổi
        public void ResetForReuse(int tierIdx)
        {
            formationJitter = new Vector2(Random.Range(-0.28f, 0.28f), Random.Range(-0.32f, 0.32f));
            fireTimer = Random.value * 0.3f;
            if (isLeader) SetLeader(false);
            SetTier(tierIdx);
            if (animator != null)
            {
                animator.speed = 0.95f + Random.value * 0.1f;
                animator.Play(0, 0, Random.value);
            }
        }

        public void SetLeader(bool leader)
        {
            if (isLeader == leader) return;
            isLeader = leader;
            if (leader) formationJitter = Vector2.zero;
            builtTier = -1;
            SetTier(squad != null ? squad.tierIndex : 0);
        }

        // Đội đông thì chỉ tốp đầu chạy animation cho nhẹ CPU
        public void SetAnimated(bool on)
        {
            if (animator != null) animator.enabled = on;
        }

        static void EnsureMats()
        {
            if (soldierMat == null) soldierMat = GameAssets.Mat(new Color(0.2f, 0.5f, 1f));
            if (robotMat == null) robotMat = GameAssets.Mat(new Color(0.78f, 0.8f, 0.88f));
            if (gunMat == null) gunMat = GameAssets.Mat(new Color(0.15f, 0.15f, 0.18f));
            if (leaderMat == null) leaderMat = GameAssets.Mat(new Color(1f, 0.85f, 0.1f), true);
            if (accentMats == null)
            {
                accentMats = new Material[6];
                accentMats[0] = GameAssets.Mat(new Color(0.35f, 0.35f, 0.4f), true);
                accentMats[1] = GameAssets.Mat(new Color(1f, 0.85f, 0.2f), true);
                accentMats[2] = GameAssets.Mat(new Color(0.2f, 0.95f, 0.4f), true);
                accentMats[3] = GameAssets.Mat(new Color(1f, 0.5f, 0.15f), true);
                accentMats[4] = GameAssets.Mat(new Color(0.95f, 0.2f, 0.2f), true);
                accentMats[5] = GameAssets.Mat(new Color(0.5f, 0.9f, 1f), true);
            }
        }

        public void SetTier(int tierIdx)
        {
            if (builtTier == tierIdx) return;
            EnsureMats();
            EnsureModel();

            WeaponTier tier = GameBalance.Tiers[tierIdx];
            Material accent = accentMats[Mathf.Clamp(tierIdx, 0, accentMats.Length - 1)];

            // Nâng cấp thường (không dính robot): CHỈ thay súng, giữ nguyên thân + animation — không giật
            bool bodyKeepable = builtTier >= 0 && !tier.robot
                && !GameBalance.Tiers[builtTier].robot
                && gunRoot != null;
            if (bodyKeepable)
            {
                builtTier = tierIdx;
                for (int i = gunRoot.childCount - 1; i >= 0; i--)
                    Destroy(gunRoot.GetChild(i).gameObject);
                muzzleFlash = null;
                BuildWeaponModel(tierIdx, accent);
                return;
            }

            builtTier = tierIdx;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            animator = null;
            gunRoot = null;
            muzzleFlash = null;

            float k = tier.robot ? 1.5f : 1f + tierIdx * 0.05f;
            if (isLeader) k *= 1.15f;

            if (!tier.robot && modelPrefab != null)
                BuildModelVisual(tierIdx, accent, k);
            else if (tier.robot && robotPrefab != null)
                BuildRobotVisual(k);
            else
                BuildPrimitiveVisual(tier, accent, k, tierIdx);

            if (isLeader)
            {
                GameAssets.Prim(PrimitiveType.Cylinder, "LeaderPole", transform, new Vector3(0f, 2.5f * k, 0f), new Vector3(0.06f, 0.3f, 0.06f), leaderMat);
                GameAssets.Prim(PrimitiveType.Sphere, "LeaderMark", transform, new Vector3(0f, 2.95f * k, 0f), Vector3.one * 0.34f, leaderMat);
            }
        }

        void BuildModelVisual(int tierIdx, Material accent, float k)
        {
            var go = Instantiate(modelPrefab, transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Chuẩn hóa chiều cao về ~1.9m x k (đo từ bounds thật của mesh)
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                float h = smr.bounds.size.y;
                if (h > 0.01f)
                    go.transform.localScale = Vector3.one * (1.9f * k / h);
                if (modelMat != null)
                {
                    var rends = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                    for (int i = 0; i < rends.Length; i++)
                        rends[i].sharedMaterial = modelMat;
                }
            }

            animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            if (runCtrl != null)
            {
                animator.runtimeAnimatorController = runCtrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.speed = 0.95f + Random.value * 0.1f;
                animator.Play(0, 0, Random.value);
            }

            // Súng bám vị trí tay phải nhưng luôn chĩa nòng về phía trước (LateUpdate)
            handBone = FindHandBone(go.transform);
            if (handBone != null)
            {
                var gunGo = new GameObject("GunRoot");
                gunGo.transform.SetParent(transform, false);
                gunRoot = gunGo.transform;
                BuildWeaponModel(tierIdx, accent);
            }
        }

        static readonly string[] TierWeaponModels = { "weapon_pistol", "weapon_smg", "weapon_rifle", "weapon_smg_dual", "weapon_minigun", "" };
        static readonly float[] TierWeaponLens = { 0.4f, 0.55f, 0.75f, 0.5f, 0.85f, 0f };

        void BuildWeaponModel(int tierIdx, Material accent)
        {
            string modelName = tierIdx < TierWeaponModels.Length ? TierWeaponModels[tierIdx] : "";
            float len = tierIdx < TierWeaponLens.Length ? TierWeaponLens[tierIdx] : 0.6f;
            bool dual = GameBalance.Tiers[tierIdx].bullets > 1 && tierIdx == 3;

            GameObject m = string.IsNullOrEmpty(modelName) ? null : ModelLib.SpawnNormalized(modelName, gunRoot, len);
            if (m != null)
            {
                m.transform.localPosition += new Vector3(dual ? -0.1f : 0f, 0f, len * 0.35f);
                if (dual)
                {
                    var m2 = ModelLib.SpawnNormalized(modelName, gunRoot, len);
                    if (m2 != null) m2.transform.localPosition += new Vector3(0.1f, 0f, len * 0.35f);
                }
            }
            else
            {
                // Fallback primitive khi thiếu model
                float fat = (0.09f + tierIdx * 0.015f);
                float plen = (0.55f + tierIdx * 0.09f);
                GameAssets.Prim(PrimitiveType.Cube, "Gun", gunRoot, new Vector3(0f, 0f, plen * 0.35f), new Vector3(fat, fat * 1.3f, plen), gunMat);
                GameAssets.Prim(PrimitiveType.Cube, "Tip", gunRoot, new Vector3(0f, 0f, plen * 0.92f), new Vector3(fat * 1.4f, fat * 1.4f, 0.09f), accent);
            }

            BuildMuzzleFlash(len * 0.95f + 0.1f);
        }

        // Chớp lửa đầu nòng: 2 quad chéo nhau, bật/tắt theo nhịp bắn (không cấp phát mỗi phát)
        void BuildMuzzleFlash(float zOffset)
        {
            if (gunRoot == null) return;
            if (flashMat == null)
            {
                flashMat = new Material(GameAssets.FxAdditive);
                if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", new Color(1f, 0.75f, 0.25f, 0.9f));
            }
            var flashGo = new GameObject("MuzzleFlash");
            flashGo.transform.SetParent(gunRoot, false);
            flashGo.transform.localPosition = new Vector3(0f, 0f, zOffset);
            GameAssets.Prim(PrimitiveType.Quad, "F1", flashGo.transform, Vector3.zero, Vector3.one * 0.4f, flashMat);
            var f2 = GameAssets.Prim(PrimitiveType.Quad, "F2", flashGo.transform, Vector3.zero, Vector3.one * 0.32f, flashMat);
            f2.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            flashGo.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f); // ngửa về phía camera
            flashGo.SetActive(false);
            muzzleFlash = flashGo.transform;
        }

        void LateUpdate()
        {
            if (gunRoot != null && handBone != null)
            {
                gunRoot.position = handBone.position;
                gunRoot.rotation = Quaternion.identity;
            }
        }

        // Tier ROBOT: model Cyber Sentinel, pháo liền thân — chỉ cần chớp lửa
        void BuildRobotVisual(float k)
        {
            var go = Instantiate(robotPrefab, transform);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
            {
                float h = smr.bounds.size.y;
                if (h > 0.01f)
                    go.transform.localScale = Vector3.one * (1.9f * k / h);
                if (robotModelMat != null)
                {
                    var rends = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                    for (int i = 0; i < rends.Length; i++)
                        rends[i].sharedMaterial = robotModelMat;
                }
            }

            animator = go.GetComponent<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            if (robotCtrl != null)
            {
                animator.runtimeAnimatorController = robotCtrl;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                animator.speed = 1.1f + Random.value * 0.1f;
                animator.Play(0, 0, Random.value);
            }

            // Chớp lửa ở vị trí pháo tay
            handBone = null;
            var gunGo = new GameObject("GunRoot");
            gunGo.transform.SetParent(transform, false);
            gunGo.transform.localPosition = new Vector3(0.25f * k, 1.45f * k, 0.45f * k);
            gunRoot = gunGo.transform;
            BuildMuzzleFlash(0.35f);
        }

        static Transform FindHandBone(Transform root)
        {
            Transform best = null;
            var all = root.GetComponentsInChildren<Transform>();
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (!n.Contains("hand")) continue;
                if (n.Contains("right") || n.Contains("_r") || n.Contains("r_") || n.EndsWith(".r") || n.EndsWith("r"))
                    return all[i];
                if (best == null) best = all[i];
            }
            return best;
        }

        void BuildPrimitiveVisual(WeaponTier tier, Material accent, float k, int tierIdx)
        {
            Material bodyM = tier.robot ? robotMat : soldierMat;
            GameAssets.Prim(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, 0.95f * k, 0f), new Vector3(0.7f * k, 0.9f * k, 0.7f * k), bodyM);
            GameAssets.Prim(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, 1.9f * k, 0f), Vector3.one * 0.5f * k, bodyM);
            if (tier.robot)
                GameAssets.Prim(PrimitiveType.Cube, "Visor", transform, new Vector3(0f, 1.9f * k, 0.2f * k), new Vector3(0.4f * k, 0.12f * k, 0.15f), accent);

            if (tier.bullets > 1)
            {
                BuildGun(new Vector3(-0.28f * k, 1.35f * k, 0.5f * k), k, tierIdx, accent);
                BuildGun(new Vector3(0.28f * k, 1.35f * k, 0.5f * k), k, tierIdx, accent);
            }
            else
            {
                BuildGun(new Vector3(0.22f * k, 1.35f * k, 0.5f * k), k, tierIdx, accent);
            }
        }

        void BuildGun(Vector3 pos, float k, int tierIdx, Material accent)
        {
            float len = (0.85f + tierIdx * 0.14f) * k;
            float fat = (0.16f + tierIdx * 0.03f) * k;
            GameAssets.Prim(PrimitiveType.Cube, "Gun", transform, pos, new Vector3(fat, fat * 1.3f, len), gunMat);
            GameAssets.Prim(PrimitiveType.Cube, "Tip", transform, pos + new Vector3(0f, 0f, len * 0.55f), new Vector3(fat * 1.5f, fat * 1.5f, 0.16f * k), accent);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;

            // Luôn hướng và bắn thẳng về phía trước
            transform.rotation = Quaternion.identity;

            // Tắt chớp lửa sau một nháy
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                if (flashTimer <= 0f && muzzleFlash != null) muzzleFlash.gameObject.SetActive(false);
            }

            if (!canShoot) return;

            WeaponTier tier = squad.Tier;
            fireTimer -= Time.deltaTime;
            if (fireTimer > 0f) return;

            fireTimer = 1f / tier.fireRate;
            Vector3 origin = transform.position + Vector3.up * (tier.robot ? 1.9f : 1.35f);
            // Hàng sau bắn xa hơn hàng trước để mọi viên đạn cùng chết ở 1 vạch phía trước đội trưởng
            float range = squad.LeaderZ + squad.CurrentFireRange - transform.position.z;

            for (int i = 0; i < tier.bullets; i++)
            {
                Vector3 dir = Vector3.forward;
                if (tier.bullets > 1)
                    dir = Quaternion.Euler(0f, (i - (tier.bullets - 1) * 0.5f) * 6f, 0f) * dir;
                BulletPool.Fire(origin, dir, tier.damage * gm.DamageScale * squad.FirepowerScale, range);
            }

            // Nháy chớp lửa đầu nòng
            if (muzzleFlash != null)
            {
                muzzleFlash.gameObject.SetActive(true);
                muzzleFlash.localScale = Vector3.one * Random.Range(0.8f, 1.25f);
                muzzleFlash.localRotation = Quaternion.Euler(-40f, 0f, Random.Range(0f, 360f));
                flashTimer = 0.055f;
            }
        }
    }
}
