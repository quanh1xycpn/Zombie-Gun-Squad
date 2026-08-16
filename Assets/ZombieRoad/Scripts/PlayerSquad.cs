using System.Collections.Generic;
using UnityEngine;

namespace ZombieRoad
{
    public class PlayerSquad : MonoBehaviour
    {
        public List<Soldier> soldiers = new List<Soldier>();
        public float speed = 5f;
        public int tierIndex;
        float xTarget;
        // Spawn rải theo frame + pool tái sử dụng để ăn cổng x+ lớn không giật
        int pendingSpawn;
        // Quân "ảo" từ người 751 trở đi — không spawn hình, bù bằng hỏa lực đạn
        int extraVirtual;
        readonly Stack<Soldier> pool = new Stack<Soldier>();
        Coroutine prewarmCo;
        const int SpawnPerFramePooled = 30; // bật lại từ pool: rẻ
        const int SpawnPerFrameFresh = 10;  // Instantiate mới: đắt, làm ít thôi
        const int PrewarmTarget = 200;

        const float Spacing = 0.75f;

        // Count tính cả quân chờ spawn + quân ảo — số trên HUD và phép tính cổng nhảy ngay
        public int Count { get { return soldiers.Count + pendingSpawn + extraVirtual; } }

        // Đạn khỏe lên thay cho đám quân ảo phía sau (1 hình = tối đa 3 người => x3)
        public float FirepowerScale
        {
            get
            {
                int visual = soldiers.Count + pendingSpawn;
                if (visual <= 0) return 1f;
                return Mathf.Clamp((float)Count / visual, 1f, 3f);
            }
        }

        // Chiều sâu đội hình (mét) để camera biết mà zoom lùi
        public float FormationDepth
        {
            get
            {
                int rows = Mathf.CeilToInt(Mathf.Max(0, soldiers.Count - 1) / (float)ColsPerRow);
                return rows * 0.7f + 2f;
            }
        }

        // Camera lùi thêm theo độ dài đội — chỉ zoom 1 NỬA, không cần thấy hết đuôi đoàn quân
        public float ZoomExtra
        {
            get { return Mathf.Min(Mathf.Max(0f, FormationDepth - 6f) * 0.275f, 22f); }
        }

        // Zoom càng xa thì tầm đạn càng dài theo (tính từ đội trưởng)
        public float CurrentFireRange
        {
            get { return GameBalance.FireRange + ZoomExtra * 1.6f; }
        }
        public WeaponTier Tier { get { return GameBalance.Tiers[tierIndex]; } }
        public float LeaderZ { get { return transform.position.z; } }

        // Số cột tối đa của đội hình vừa với lòng đường
        int ColsPerRow
        {
            get { return Mathf.Max(1, Mathf.FloorToInt((GameBalance.RoadHalfWidth * 2f - 1.2f) / Spacing) + 1); }
        }

        public void Init(int count)
        {
            for (int i = soldiers.Count - 1; i >= 0; i--)
                ReturnToPool(soldiers[i]);
            soldiers.Clear();
            pendingSpawn = 0;
            extraVirtual = 0;
            upgradeCursor = int.MaxValue;
            tierIndex = 0;
            xTarget = 0f;
            transform.position = Vector3.zero;
            AddSoldiers(count);
            FlushPending(count); // đội khởi điểm nhỏ, spawn ngay cho đủ đội hình mở màn
            UpdateCountLabel();

            // Tranh thủ màn hình "Chạm để bắt đầu" đúc sẵn lính vào pool
            if (prewarmCo != null) StopCoroutine(prewarmCo);
            prewarmCo = StartCoroutine(Prewarm());
        }

        void ReturnToPool(Soldier s)
        {
            if (s == null) return;
            s.gameObject.SetActive(false);
            pool.Push(s);
        }

        System.Collections.IEnumerator Prewarm()
        {
            while (pool.Count < PrewarmTarget)
            {
                var gm = GameManager.I;
                // Chỉ đúc lúc đang đứng chờ, không giành CPU với gameplay
                if (gm == null || gm.State == GameState.Ready)
                {
                    for (int i = 0; i < 8 && pool.Count < PrewarmTarget; i++)
                    {
                        var s = Soldier.Create(this, new Vector3(0f, 0f, -3f));
                        s.gameObject.SetActive(false);
                        pool.Push(s);
                    }
                }
                yield return null;
            }
        }

        void FlushPending(int max)
        {
            int n = Mathf.Min(max, pendingSpawn);
            for (int i = 0; i < n; i++) SpawnOne();
            RefreshShooters();
        }

        void SpawnOne()
        {
            if (pendingSpawn <= 0) return;
            pendingSpawn--;
            Vector3 spawnPos = soldiers.Count == 0
                ? new Vector3(0f, 0f, 1.1f)
                : new Vector3(Random.Range(-1f, 1f), 0f, -0.5f - Random.value);
            Soldier s;
            if (pool.Count > 0)
            {
                s = pool.Pop();
                s.gameObject.SetActive(true);
                s.transform.localPosition = spawnPos;
                s.ResetForReuse(tierIndex);
            }
            else
            {
                s = Soldier.Create(this, spawnPos);
            }
            if (soldiers.Count == 0) s.SetLeader(true);
            soldiers.Add(s);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;

            Vector3 pos = transform.position;
            pos.z += speed * Time.deltaTime;
            pos.x = Mathf.MoveTowards(pos.x, xTarget, 9f * Time.deltaTime);
            // Đội trưởng được đi sát mép đường để chọn cổng chính xác
            pos.x = Mathf.Clamp(pos.x, -GameBalance.RoadHalfWidth + 0.5f, GameBalance.RoadHalfWidth - 0.5f);
            transform.position = pos;

            // Đám đông phía sau tự né mép đường: khi đầy chiều ngang thì bám giữa đường,
            // đội trưởng vẫn tự do trái/phải phía trước
            int cols = ColsPerRow;
            int bodyCount = Mathf.Max(1, soldiers.Count - 1);
            float rowHalf = 0.5f * (Mathf.Min(cols, bodyCount) - 1) * Spacing;
            float maxOff = Mathf.Max(0f, GameBalance.RoadHalfWidth - 0.7f - rowHalf);
            float centerWorldX = Mathf.Clamp(transform.position.x, -maxOff, maxOff);
            float centerLocalX = centerWorldX - transform.position.x;

            for (int i = 0; i < soldiers.Count; i++)
            {
                var s = soldiers[i];
                if (s == null) continue;
                Vector3 want = FormationPos(i, centerLocalX);
                // Mỗi lính lệch ngẫu nhiên một chút cho đội hình tự nhiên, đỡ "công nghiệp"
                want.x += s.formationJitter.x;
                want.z += s.formationJitter.y;
                s.transform.localPosition = Vector3.Lerp(s.transform.localPosition, want, 6f * Time.deltaTime);
            }

            ProcessUpgrade();
            UpdateCountLabel();

            // Quân thật chết bớt thì quân ảo phía sau bổ sung vào hàng ngũ hiện hình
            if (extraVirtual > 0)
            {
                int visualSpace = GameBalance.MaxVisualSoldiers - (soldiers.Count + pendingSpawn);
                if (visualSpace > 0)
                {
                    int move = Mathf.Min(extraVirtual, visualSpace);
                    pendingSpawn += move;
                    extraVirtual -= move;
                }
            }

            // Spawn rải quân đang chờ: từ pool thì nhanh tay, phải Instantiate mới thì nhỏ giọt
            if (pendingSpawn > 0)
            {
                int budget = pool.Count > 0 ? SpawnPerFramePooled : SpawnPerFrameFresh;
                int n = Mathf.Min(budget, pendingSpawn);
                for (int i = 0; i < n; i++) SpawnOne();
                RefreshShooters();
            }
        }

        public void Steer(float deltaX)
        {
            xTarget = Mathf.Clamp(xTarget + deltaX, -GameBalance.RoadHalfWidth + 0.5f, GameBalance.RoadHalfWidth - 0.5f);
        }

        // Đội trưởng đứng đầu; còn lại xếp hàng ngang, đầy hàng thì dàn tiếp về phía sau
        Vector3 FormationPos(int i, float centerLocalX)
        {
            if (i == 0) return new Vector3(0f, 0f, 1.1f);
            int idx = i - 1;
            int cols = ColsPerRow;
            int row = idx / cols;
            int col = idx % cols;
            int inRow = Mathf.Min(cols, (soldiers.Count - 1) - row * cols);
            float xOff = (col - (inRow - 1) * 0.5f) * Spacing;
            float zOff = -0.55f - row * 0.7f;
            return new Vector3(centerLocalX + xOff, 0f, zOff);
        }

        void RefreshShooters()
        {
            for (int i = 0; i < soldiers.Count; i++)
            {
                if (soldiers[i] == null) continue;
                soldiers[i].canShoot = i < GameBalance.ShooterCap;
                // Tất cả lính đều chạy animation (animator tự cull khi ngoài màn hình)
                soldiers[i].SetAnimated(true);
            }
        }

        public void AddSoldiers(int n)
        {
            n = Mathf.Clamp(n, 0, Mathf.Max(0, GameBalance.MaxSoldiers - Count));
            int visualSpace = GameBalance.MaxVisualSoldiers - (soldiers.Count + pendingSpawn);
            int toVisual = Mathf.Clamp(n, 0, Mathf.Max(0, visualSpace));
            pendingSpawn += toVisual;
            extraVirtual += n - toVisual;
        }

        public void RemoveSoldiers(int n)
        {
            // Trừ quân ảo phía sau trước, rồi tới quân chờ spawn, cuối cùng mới tới quân thật
            int fromVirtual = Mathf.Min(n, extraVirtual);
            extraVirtual -= fromVirtual;
            n -= fromVirtual;
            int fromPending = Mathf.Min(n, pendingSpawn);
            pendingSpawn -= fromPending;
            n -= fromPending;

            for (int i = 0; i < n; i++)
            {
                if (soldiers.Count == 0) break;
                var s = soldiers[soldiers.Count - 1];
                soldiers.RemoveAt(soldiers.Count - 1);
                if (s != null)
                {
                    if (i < 30) FX.Puff(s.transform.position + Vector3.up, new Color(0.9f, 0.2f, 0.2f), 1.2f);
                    ReturnToPool(s);
                }
            }
            RefreshShooters();
            if (Count == 0 && GameManager.I != null)
                GameManager.I.OnSquadEmpty();
        }

        public void ApplyGate(GateOp op, float val)
        {
            int before = soldiers.Count;
            if (op == GateOp.Add) AddSoldiers(Mathf.RoundToInt(val));
            else if (op == GateOp.Multiply) AddSoldiers(Mathf.RoundToInt(before * (val - 1f)));
            else if (op == GateOp.Minus)
            {
                // -N : trừ thẳng, luôn chừa lại ít nhất 1 người
                RemoveSoldiers(Mathf.Min(Mathf.RoundToInt(val), Mathf.Max(0, before - 1)));
            }
            else if (op == GateOp.Divide)
            {
                // ×−M : chia quân cho M, luôn chừa lại ít nhất 1 người
                int keep = Mathf.Max(1, Mathf.RoundToInt(before / Mathf.Max(2f, val)));
                RemoveSoldiers(before - keep);
            }
            else
            {
                // -X% : mất X phần trăm quân, luôn chừa lại ít nhất 1 người
                int remove = Mathf.RoundToInt(before * Mathf.Clamp01(val / 100f));
                remove = Mathf.Min(remove, before - 1);
                RemoveSoldiers(Mathf.Max(0, remove));
            }
        }

        public void KillOneNear(Vector3 pos)
        {
            // Còn quân ảo thì "hàng sau bước lên thế chỗ" — chết 1 quân ảo, đội hình giữ nguyên
            if (extraVirtual > 0)
            {
                extraVirtual--;
                FX.Puff(pos + Vector3.up, new Color(0.9f, 0.2f, 0.2f), 1.2f);
                return;
            }
            int bestI = -1;
            float bestSqr = float.MaxValue;
            // Đội trưởng chỉ chết khi là người cuối cùng
            int start = soldiers.Count > 1 ? 1 : 0;
            for (int i = start; i < soldiers.Count; i++)
            {
                var s = soldiers[i];
                if (s == null) continue;
                float sq = (s.transform.position - pos).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; bestI = i; }
            }
            if (bestI >= 0)
            {
                var s = soldiers[bestI];
                soldiers.RemoveAt(bestI);
                if (s != null)
                {
                    FX.Puff(s.transform.position + Vector3.up, new Color(0.9f, 0.2f, 0.2f), 1.2f);
                    ReturnToPool(s);
                }
                RefreshShooters();
                if (Count == 0 && GameManager.I != null)
                    GameManager.I.OnSquadEmpty();
            }
        }

        public Vector3 GetNearestSoldierPos(Vector3 from, out float dist)
        {
            Vector3 best = transform.position;
            float bestSqr = float.MaxValue;
            // Đội đông thì lấy mẫu thưa cho nhẹ CPU
            int step = soldiers.Count > 120 ? 4 : 1;
            for (int i = 0; i < soldiers.Count; i += step)
            {
                var s = soldiers[i];
                if (s == null) continue;
                float sq = (s.transform.position - from).sqrMagnitude;
                if (sq < bestSqr) { bestSqr = sq; best = s.transform.position; }
            }
            dist = Mathf.Sqrt(bestSqr);
            return best;
        }

        int upgradeCursor = int.MaxValue;
        TextMesh countLabel;

        // Số quân lơ lửng trên đầu đội — thấy ngay hiệu ứng mọi loại cổng (kể cả trừ vào quân ảo)
        void UpdateCountLabel()
        {
            if (countLabel == null)
            {
                countLabel = GameAssets.WorldText("", transform, new Vector3(0f, 3.1f, 1.1f), 2.2f, Color.white);
                countLabel.fontStyle = FontStyle.Bold;
            }
            countLabel.text = Count.ToString();
            var cam = Camera.main;
            if (cam != null)
                countLabel.transform.rotation = Quaternion.LookRotation(countLabel.transform.position - cam.transform.position);
        }

        public void UpgradeWeapon()
        {
            if (tierIndex >= GameBalance.Tiers.Length - 1) return;
            tierIndex++;
            upgradeCursor = 0; // đổi visual rải frame trong Update, không làm 1 phát cả nghìn lính
            if (UIManager.I != null)
                UIManager.I.Toast("Upgrade: " + GameBalance.Tiers[tierIndex].name + "!");
        }

        // Hỏa lực đổi NGAY khi nâng cấp (Tier đọc lúc bắn), còn súng trên tay
        // đổi dần dần 8 lính/frame cho êm mắt, không giật
        void ProcessUpgrade()
        {
            if (upgradeCursor >= soldiers.Count) return;
            int end = Mathf.Min(upgradeCursor + 8, soldiers.Count);
            for (int i = upgradeCursor; i < end; i++)
            {
                var s = soldiers[i];
                if (s == null) continue;
                s.SetTier(tierIndex);
                if (i < 25)
                    FX.Puff(s.transform.position + Vector3.up, new Color(1f, 0.85f, 0.2f), 1.5f);
            }
            upgradeCursor = end >= soldiers.Count ? int.MaxValue : end;
        }
    }
}
