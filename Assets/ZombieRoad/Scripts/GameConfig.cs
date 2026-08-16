using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    // Add = +N | Minus = -N | Multiply = ×+M (nhân) | Subtract = -X% (mất % quân) | Divide = ×−M (chia)
    public enum GateOp { Add, Multiply, Subtract, Minus, Divide }

    public class GateInfo
    {
        public float z;
        public GateOp leftOp, rightOp;
        public float leftVal, rightVal;
    }

    public class PackInfo
    {
        public float z;
        public int count;
        public float hpMul;
        public float scaleMul;
    }

    public class CrateInfo
    {
        public float z;
        public float x;
        public float hp;
    }

    public class LevelData
    {
        public int level;
        public float length;
        public float zombieHp;
        public float zombieSpeed;
        public float zombieScale;
        public int startSoldiers;
        public int bossCount;
        public List<GateInfo> gates = new List<GateInfo>();
        public List<PackInfo> packs = new List<PackInfo>();
        public List<CrateInfo> crates = new List<CrateInfo>();
    }

    public struct WeaponTier
    {
        public string name;
        public float damage;
        public float fireRate;
        public int bullets;
        public bool robot;
        public WeaponTier(string n, float d, float r, int b, bool rb)
        {
            name = n; damage = d; fireRate = r; bullets = b; robot = rb;
        }
    }

    public static class GameBalance
    {
        public static readonly WeaponTier[] Tiers = new WeaponTier[]
        {
            new WeaponTier("Pistol", 12f, 2.2f, 1, false),
            new WeaponTier("SMG", 12f, 4.5f, 1, false),
            new WeaponTier("Rifle", 22f, 4.5f, 1, false),
            new WeaponTier("Dual SMG", 22f, 4.5f, 2, false),
            new WeaponTier("Minigun", 26f, 8f, 1, false),
            new WeaponTier("ROBOT", 48f, 8f, 2, true),
        };

        // Quân số logic tối đa 1500 nhưng chỉ hiện hình tối đa 500 —
        // 1 lính hiện hình đại diện tối đa 3 người, bù bằng hỏa lực đạn (tối đa x3)
        public const int MaxSoldiers = 1500;
        public const int MaxVisualSoldiers = 500;
        public const int ShooterCap = 120;
        public const int MaxZombiesPerLevel = 400;
        public const float RoadHalfWidth = 4f;
        // Tầm bắn = khoảng nhìn thấy phía trước trên màn hình dọc
        public const float FireRange = 26f;

        public static LevelData Generate(int level)
        {
            // Không truyền seed: roll mới hoàn toàn (dùng cho chơi lại)
            return Generate(level, System.Environment.TickCount ^ (level * 7919));
        }

        public static LevelData Generate(int level, int seed)
        {
            var rng = new System.Random(seed);
            var d = new LevelData();
            d.level = level;
            d.length = Mathf.Min(130f + level * 5f, 420f);
            // Máu quái +8% MỖI MÀN (nhân dồn): màn 1 = 25, màn 50 ~ 1.085, màn 100 ~ 51.000
            d.zombieHp = 25f * Mathf.Pow(1.08f, level - 1);
            d.zombieSpeed = 2.2f + Mathf.Min(1.8f, level * 0.03f);
            d.zombieScale = 1f + Mathf.Min(1.1f, level * 0.014f);
            // Luôn mở màn với đúng 1 lính — cổng khởi động (+/x+) ngay 12m đầu là nguồn quân đầu tiên
            d.startSoldiers = 1;
            // Boss mỗi màn đều có, tăng dần: màn 1 có 1 con, màn 100 có 13 con
            d.bossCount = 1 + level / 8;

            // Số quái +10% MỖI MÀN (nhân dồn): màn 1 = 40, màn 10 ~ 94, màn 24+ chạm trần 400
            int zombieBudget = Mathf.Min(Mathf.RoundToInt(40f * Mathf.Pow(1.1f, level - 1)), MaxZombiesPerLevel);

            // Cổng khởi động ngay đầu màn: cả 2 bên RANDOM HOÀN TOÀN đủ 5 dấu (+, -, x+, x-, -%)
            var starter = new GateInfo();
            starter.z = 12f;
            MakeGateSide(rng, level, true, out starter.leftOp, out starter.leftVal);
            MakeGateSide(rng, level, true, out starter.rightOp, out starter.rightVal);
            d.gates.Add(starter);

            // Cổng: SỐ LƯỢNG và VỊ TRÍ đều random, trần giảm dần theo màn
            // (màn 1 tối đa 9 cổng, màn 40 ~5, màn 100 chỉ 2)
            int maxGates = Mathf.Max(2, 9 - level / 12);
            int gateCount = 1 + rng.Next(maxGates);
            var gateZs = new List<float>();
            int guard = 0;
            while (gateZs.Count < gateCount && guard++ < 200)
            {
                float gz = 20f + (float)rng.NextDouble() * (d.length - 50f);
                bool ok = true;
                for (int i = 0; i < gateZs.Count; i++)
                    if (Mathf.Abs(gateZs[i] - gz) < 16f) { ok = false; break; }
                if (ok) gateZs.Add(gz);
            }
            gateZs.Sort();
            for (int i = 0; i < gateZs.Count; i++)
            {
                var g = new GateInfo();
                g.z = gateZs[i];
                // 2 bên roll độc lập — cả 2 cùng trừ thì chọn bên thiệt ít hơn
                MakeGateSide(rng, level, true, out g.leftOp, out g.leftVal);
                MakeGateSide(rng, level, true, out g.rightOp, out g.rightVal);
                d.gates.Add(g);
            }

            float z = 20f;
            float segment = 24f;
            var packZs = new List<float>();
            while (z < d.length - 20f)
            {
                if (rng.NextDouble() < 0.30)
                {
                    // Tránh đặt thùng đè lên cổng
                    bool nearGate = false;
                    for (int i = 0; i < gateZs.Count; i++)
                        if (Mathf.Abs(gateZs[i] - z) < 6f) { nearGate = true; break; }
                    if (!nearGate)
                    {
                        var c = new CrateInfo();
                        c.z = z;
                        c.x = (float)(rng.NextDouble() * 5.0 - 2.5);
                        c.hp = 140f + level * 25f;
                        d.crates.Add(c);
                    }
                }
                packZs.Add(z + segment * 0.5f);
                z += segment;
            }

            int packCount = packZs.Count;
            for (int i = 0; i < packCount; i++)
            {
                float t = (i + 1f) / packCount;
                int count = Mathf.Max(2, Mathf.RoundToInt(zombieBudget * (0.4f + 0.9f * t) / packCount));
                var p = new PackInfo();
                p.z = packZs[i];
                p.count = count;
                p.hpMul = 1f + t * 0.6f;
                p.scaleMul = 1f + t * 0.3f;
                d.packs.Add(p);
            }

            // Luôn có ít nhất 2 thùng nâng cấp vũ khí mỗi màn
            float[] forcedPos = { 0.35f, 0.68f };
            for (int fi = 0; d.crates.Count < 2 && fi < forcedPos.Length; fi++)
            {
                var forced = new CrateInfo();
                forced.z = d.length * forcedPos[fi];
                forced.x = 0f;
                forced.hp = 140f + level * 25f;
                d.crates.Add(forced);
            }

            // Bầy cuối màn to hơn
            var final = new PackInfo();
            final.z = d.length - 12f;
            final.count = Mathf.Min(5 + level / 2, 40);
            final.hpMul = 1.3f;
            final.scaleMul = 1.4f;
            d.packs.Add(final);
            return d;
        }

        // Tỉ lệ trải tuyến tính theo màn (t = 0 ở màn 1, t = 1 ở màn 100):
        // Tỉ lệ trải tuyến tính (t = 0 màn 1, t = 1 màn 100), tổng luôn đúng 100:
        //   ×+ : 32.5% -> 2%   |   + : 32.5% -> 4%
        //   3 cổng hại (-%, ×−, −) chia đều phần còn lại: mỗi loại ~11.7% -> ~31.3%
        static void MakeGateSide(System.Random rng, int level, bool allowSubtract, out GateOp op, out float val)
        {
            float t = Mathf.Clamp01((level - 1f) / 99f);
            // Phần trăm NGUYÊN, phần dư dồn vào cổng − cuối cùng => tổng luôn đúng 100
            int pMulUp = Mathf.RoundToInt(Mathf.Lerp(32.5f, 2f, t));
            int pAdd = Mathf.RoundToInt(Mathf.Lerp(32.5f, 4f, t));
            int pBad = 100 - pMulUp - pAdd;
            int pPct = pBad / 3;      // -%
            int pDiv = pBad / 3;      // ×− chia
            int pMinus = pBad - pPct - pDiv; // − (nhận phần dư)
            int roll = rng.Next(100); // 0..99, mỗi đơn vị = 1%
            if (roll < pMulUp) op = GateOp.Multiply;
            else if (roll < pMulUp + pAdd) op = GateOp.Add;
            else if (roll < pMulUp + pAdd + pPct) op = GateOp.Subtract;
            else if (roll < pMulUp + pAdd + pPct + pDiv) op = GateOp.Divide;
            else op = GateOp.Minus;
            val = RollValue(rng, level, op);
        }

        static float RollValue(System.Random rng, int level, GateOp op)
        {
            if (op == GateOp.Add || op == GateOp.Minus)
            {
                // Cộng/trừ nhẹ dần: màn 1 random 1..110, màn 100 random 1..10
                int maxN = Mathf.Max(10, Mathf.RoundToInt(110f - (level - 1f) * (100f / 99f)));
                return 1 + rng.Next(maxN);
            }
            if (op == GateOp.Subtract)
            {
                // -X%: mất X% quân, X random 1..màn (màn 1: luôn 1%, màn 100: 1..100%)
                int maxPct = Mathf.Clamp(level, 1, 100);
                return 1 + rng.Next(maxPct);
            }
            // ×+ và ×− (chia) dùng chung dải hệ số 2..15, cứ 10 màn giảm 1, màn 100 tối đa 5
            int maxMul = Mathf.Max(2, 15 - level / 10);
            return 2 + rng.Next(maxMul - 1);
        }
    }
}
