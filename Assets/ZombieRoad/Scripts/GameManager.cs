using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    public enum GameState { Ready, Playing, Won, Lost }

    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public GameState State = GameState.Ready;
        public LevelData Data;
        public PlayerSquad Squad;
        public SkillManager Skills;

        int level;
        int nextGate;

        // Sát thương súng tăng theo màn để bù zombie ngày càng trâu
        public float DamageScale { get { return Data == null ? 1f : 1f + (Data.level - 1) * 0.08f; } }
        GameObject levelRoot;
        float streamTimer;
        readonly List<Gate> gates = new List<Gate>();

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            Skills = gameObject.GetComponent<SkillManager>();
            if (Skills == null) Skills = gameObject.AddComponent<SkillManager>();
        }

        void Start()
        {
            Ads.Init(); // OPEN ad + banner khi vào game
            level = Mathf.Clamp(PlayerPrefs.GetInt("ZR_Level", 1), 1, 100);
            if (UIManager.I == null) UIManager.Create();

            var squadGo = new GameObject("PlayerSquad");
            Squad = squadGo.AddComponent<PlayerSquad>();

            EnsureLight();
            BuildLevel();
        }

        void EnsureLight()
        {
            var l = Object.FindFirstObjectByType<Light>();
            if (l == null)
            {
                var go = new GameObject("Sun");
                l = go.AddComponent<Light>();
                l.type = LightType.Directional;
            }
            // Sáng sủa cho màn hình điện thoại: đèn mạnh hơn + ambient cao,
            // vẫn giữ gần thẳng đứng để tường 2 bên ăn sáng đều
            l.intensity = 1.35f;
            l.transform.rotation = Quaternion.Euler(70f, 10f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.78f, 0.78f, 0.82f);
        }

        public void BuildLevel()
        {
            ClearLevel();
            // Mỗi lần vào màn (kể cả chơi lại) đều roll random mới — thua thì ván sau khác hẳn
            int seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            Data = GameBalance.Generate(level, seed);
            levelRoot = new GameObject("LevelRoot");

            BuildTrack();

            gates.Clear();
            nextGate = 0;
            for (int i = 0; i < Data.gates.Count; i++)
                gates.Add(Gate.Create(Data.gates[i], levelRoot.transform));

            for (int i = 0; i < Data.crates.Count; i++)
                Crate.Create(Data.crates[i], levelRoot.transform);

            var rng = new System.Random(seed ^ 0x2F5D1E);
            // Càng về sau càng nhiều loại zombie: Runner từ màn 8, Tank từ màn 18
            float pRunner = level >= 8 ? Mathf.Min(0.28f, 0.04f + level * 0.0025f) : 0f;
            float pTank = level >= 18 ? Mathf.Min(0.22f, 0.02f + level * 0.002f) : 0f;
            for (int i = 0; i < Data.packs.Count; i++)
            {
                var p = Data.packs[i];
                for (int j = 0; j < p.count; j++)
                {
                    float x = (float)(rng.NextDouble() * 2.0 - 1.0) * (GameBalance.RoadHalfWidth - 0.8f);
                    float dz = (float)(rng.NextDouble() * 6.0 - 3.0);
                    float scaleJitter = 0.9f + (float)rng.NextDouble() * 0.3f;

                    double roll = rng.NextDouble();
                    ZombieType type = ZombieType.Normal;
                    float hpMul = 1f, speedMul = 1f, scaleMul = 1f;
                    if (roll < pRunner) { type = ZombieType.Runner; hpMul = 0.5f; speedMul = 1.9f; scaleMul = 0.75f; }
                    else if (roll < pRunner + pTank) { type = ZombieType.Tank; hpMul = 3f; speedMul = 0.7f; scaleMul = 1.35f; }

                    Zombie.Spawn(
                        new Vector3(x, 0f, p.z + dz),
                        Data.zombieHp * p.hpMul * hpMul,
                        Data.zombieSpeed * speedMul,
                        Data.zombieScale * p.scaleMul * scaleJitter * scaleMul,
                        levelRoot.transform,
                        type);
                }
            }

            // Boss rải đều dọc màn, số lượng tăng theo màn
            for (int i = 0; i < Data.bossCount; i++)
            {
                float t = (i + 1f) / (Data.bossCount + 1f);
                float bz = Mathf.Lerp(45f, Data.length - 8f, t);
                float bx = (float)(rng.NextDouble() * 2.0 - 1.0) * (GameBalance.RoadHalfWidth - 1.3f);
                Zombie.Spawn(
                    new Vector3(bx, 0f, bz),
                    Data.zombieHp * (6f + Data.level * 0.05f),
                    Data.zombieSpeed * 0.8f,
                    Data.zombieScale * 2.2f,
                    levelRoot.transform,
                    ZombieType.Boss);
            }

            Squad.Init(Data.startSoldiers);
            Skills.ResetCooldowns();
            CameraRig.Setup(Squad.transform);

            State = GameState.Ready;
            if (UIManager.I != null)
            {
                UIManager.I.ShowStart(level);
                UIManager.I.SetHUD(level, Squad.Count, 0f);
            }
        }

        void BuildTrack()
        {
            float len = Data.length + 60f;
            float hw = GameBalance.RoadHalfWidth;

            // Scene có "Environment" (user tự dựng/chỉnh bằng menu ZombieRoad trong editor)
            // -> không tự sinh đường/cỏ/tường nữa, chỉ dựng vạch đích
            if (GameObject.Find("Environment") != null)
            {
                BuildFinish(hw);
                return;
            }

            var roadMat = GameAssets.Mat(new Color(0.72f, 0.72f, 0.74f));
            var sideMat = GameAssets.Mat(new Color(0.35f, 0.35f, 0.4f));
            var grassMat = GameAssets.Mat(new Color(0.45f, 0.6f, 0.4f));

            var road = GameAssets.Prim(PrimitiveType.Cube, "Road", levelRoot.transform,
                new Vector3(0f, -0.25f, len * 0.5f - 20f), new Vector3(hw * 2f + 1f, 0.5f, len), roadMat);

            // Tường bật lại: các đoạn nối tiếp nhau, 2 bên lật gương để cùng úp MỘT MẶT vào lòng đường
            const bool WallsEnabled = true;
            if (WallsEnabled && ModelLib.LoadProp("side_wall") != null)
            {
                const float seg = 8f;
                for (float wz = -20f; wz < len - 20f; wz += seg)
                {
                    ModelLib.SpawnWallSegment("side_wall", levelRoot.transform, seg, new Vector3(-hw - 1.1f, 0f, wz + seg * 0.5f), false);
                    ModelLib.SpawnWallSegment("side_wall", levelRoot.transform, seg, new Vector3(hw + 1.1f, 0f, wz + seg * 0.5f), true);
                }
            }

            // Thảm cỏ ốp SÁT mép đường (trước đây hở 1.5m -> lộ nền trời xanh thành dải xanh dọc đường)
            float roadHalf = hw + 0.5f;
            GameAssets.Prim(PrimitiveType.Cube, "GroundL", levelRoot.transform,
                new Vector3(-roadHalf - 10f, -0.3f, len * 0.5f - 20f), new Vector3(20f, 0.4f, len), grassMat);
            GameAssets.Prim(PrimitiveType.Cube, "GroundR", levelRoot.transform,
                new Vector3(roadHalf + 10f, -0.3f, len * 0.5f - 20f), new Vector3(20f, 0.4f, len), grassMat);

            BuildFinish(hw);
        }

        // Vạch đích + chữ ĐÍCH — luôn sinh runtime vì vị trí phụ thuộc độ dài từng màn
        void BuildFinish(float hw)
        {
            var finishMat = GameAssets.Mat(new Color(0.2f, 0.9f, 0.3f), true);
            GameAssets.Prim(PrimitiveType.Cube, "Finish", levelRoot.transform,
                new Vector3(0f, 0.05f, Data.length), new Vector3(hw * 2f, 0.1f, 2.5f), finishMat);
            var tm = GameAssets.WorldText("FINISH", levelRoot.transform, new Vector3(0f, 3f, Data.length + 2f), 2.5f, new Color(0.2f, 0.9f, 0.3f));
            tm.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
        }

        void ClearLevel()
        {
            BulletPool.RecycleAll();
            for (int i = Zombie.All.Count - 1; i >= 0; i--)
                if (Zombie.All[i] != null) Destroy(Zombie.All[i].gameObject);
            Zombie.All.Clear();
            for (int i = Crate.All.Count - 1; i >= 0; i--)
                if (Crate.All[i] != null) Destroy(Crate.All[i].gameObject);
            Crate.All.Clear();
            if (levelRoot != null) Destroy(levelRoot);
        }

        public void StartRun()
        {
            if (State != GameState.Ready) return;
            State = GameState.Playing;
            if (UIManager.I != null) UIManager.I.HidePanels();
        }

        // Quái tràn LIÊN TỤC từ đích chạy về phía đội — càng về sau spawn càng dày
        void UpdateZombieStream()
        {
            streamTimer -= Time.deltaTime;
            if (streamTimer > 0f) return;
            streamTimer = Mathf.Max(0.6f, 3f - Data.level * 0.024f);
            if (Zombie.All.Count >= 450) return; // trần bảo vệ FPS

            float pRunner = Data.level >= 8 ? Mathf.Min(0.28f, 0.04f + Data.level * 0.0025f) : 0f;
            float pTank = Data.level >= 18 ? Mathf.Min(0.22f, 0.02f + Data.level * 0.002f) : 0f;
            int n = 1 + Data.level / 35;
            for (int i = 0; i < n; i++)
            {
                float x = Random.Range(-(GameBalance.RoadHalfWidth - 0.8f), GameBalance.RoadHalfWidth - 0.8f);
                float dz = Random.Range(0f, 3f);
                float roll = Random.value;
                ZombieType type = ZombieType.Normal;
                float hpMul = 1f, speedMul = 1f, scaleMul = 1f;
                if (roll < pRunner) { type = ZombieType.Runner; hpMul = 0.5f; speedMul = 1.9f; scaleMul = 0.75f; }
                else if (roll < pRunner + pTank) { type = ZombieType.Tank; hpMul = 3f; speedMul = 0.7f; scaleMul = 1.35f; }

                var zb = Zombie.Spawn(
                    new Vector3(x, 0f, Data.length - 2f - dz),
                    Data.zombieHp * hpMul,
                    Data.zombieSpeed * speedMul * 1.15f,
                    Data.zombieScale * scaleMul * Random.Range(0.9f, 1.2f),
                    levelRoot.transform,
                    type);
                zb.alwaysChase = true;
            }
        }

        void Update()
        {
            if (State != GameState.Playing) return;

            UpdateZombieStream();

            float z = Squad.LeaderZ;

            while (nextGate < gates.Count && z + 0.2f >= gates[nextGate].info.z)
            {
                var g = gates[nextGate];
                nextGate++;
                if (g == null || g.applied) continue;
                g.applied = true;
                bool left = Squad.transform.position.x < 0f;
                GateOp op = left ? g.info.leftOp : g.info.rightOp;
                float val = left ? g.info.leftVal : g.info.rightVal;
                Squad.ApplyGate(op, val);
                Color c = op == GateOp.Multiply ? new Color(0.4f, 0.6f, 1f)
                    : (op == GateOp.Add ? new Color(1f, 0.4f, 0.4f) : Color.gray);
                FX.FloatText(Squad.transform.position + Vector3.forward * 3f, Gate.Label(op, val), c);
            }

            if (z >= Data.length && State == GameState.Playing)
                WinLevel();

            if (UIManager.I != null)
                UIManager.I.SetHUD(level, Squad.Count, Mathf.Clamp01(z / Data.length));
        }

        void WinLevel()
        {
            State = GameState.Won;
            bool isLast = level >= 100;
            if (UIManager.I != null) UIManager.I.ShowWin(level, isLast);
        }

        public void OnSquadEmpty()
        {
            if (State != GameState.Playing) return;
            State = GameState.Lost;
            if (UIManager.I != null) UIManager.I.ShowLose(level);
        }

        public void NextLevel()
        {
            if (level >= 100) level = 1;
            else level++;
            PlayerPrefs.SetInt("ZR_Level", level);
            PlayerPrefs.Save();
            BuildLevel();
        }

        public void Retry()
        {
            BuildLevel();
        }
    }
}
