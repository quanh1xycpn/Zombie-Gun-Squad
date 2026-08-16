using System.Collections;
using UnityEngine;

namespace ZombieRoad
{
    public class SkillManager : MonoBehaviour
    {
        public float rocketCd = 12f;
        public float freezeCd = 18f;
        float rocketReadyAt;
        float freezeReadyAt;

        public float RocketFrac { get { return Mathf.Clamp01((rocketReadyAt - Time.time) / rocketCd); } }
        public float FreezeFrac { get { return Mathf.Clamp01((freezeReadyAt - Time.time) / freezeCd); } }

        // Vào màn mới / chơi lại: skill sẵn sàng ngay, không kéo hồi chiêu từ màn trước
        public void ResetCooldowns()
        {
            rocketReadyAt = 0f;
            freezeReadyAt = 0f;
            StopAllCoroutines();
        }

        public void FireRocket()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (Time.time < rocketReadyAt) return;
            rocketReadyAt = Time.time + rocketCd;
            StartCoroutine(RocketRoutine());
        }

        IEnumerator RocketRoutine()
        {
            var gm = GameManager.I;
            Vector3 start = gm.Squad.transform.position + new Vector3(0f, 2f, 0f);
            Vector3 target = gm.Squad.transform.position + new Vector3(0f, 0f, 16f);

            Material mat = null;
            GameObject rocket = new GameObject("Rocket");
            var rm = ModelLib.SpawnNormalized("rocket_missile", rocket.transform, 1.5f);
            if (rm == null)
            {
                mat = GameAssets.Mat(new Color(1f, 0.4f, 0.1f), true);
                GameAssets.Prim(PrimitiveType.Capsule, "RocketVis", rocket.transform, Vector3.zero, new Vector3(0.7f, 1.3f, 0.7f), mat);
            }

            float t = 0f;
            const float dur = 0.7f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                Vector3 p = Vector3.Lerp(start, target, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 9f;
                rocket.transform.position = p;
                rocket.transform.rotation = Quaternion.Euler(k * 180f, 0f, 0f);
                // Vệt khói phía sau tên lửa
                if (Random.value < 0.5f)
                    FX.Puff(p, new Color(1f, 0.7f, 0.3f), 0.8f);
                yield return null;
            }
            Destroy(rocket);
            if (mat != null) Destroy(mat);

            float radius = 6f;
            float dmg = 500f + gm.Data.level * 25f;
            FX.Explosion(target + Vector3.up, radius);
            FX.FloatText(target, "BOOM!", new Color(1f, 0.55f, 0.1f));
            CameraRig.Shake(0.6f, 0.45f);
            if (UIManager.I != null) UIManager.I.Flash(new Color(1f, 0.5f, 0.1f, 0.28f));
            var all = Zombie.All;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var z = all[i];
                if (z == null || !z.Alive) continue;
                Vector3 d = z.transform.position - target;
                d.y = 0f;
                if (d.sqrMagnitude < radius * radius)
                    z.Damage(dmg);
            }
        }

        public void Freeze()
        {
            var gm = GameManager.I;
            if (gm == null || gm.State != GameState.Playing) return;
            if (Time.time < freezeReadyAt) return;
            freezeReadyAt = Time.time + freezeCd;

            var all = Zombie.All;
            for (int i = 0; i < all.Count; i++)
            {
                var z = all[i];
                if (z == null || !z.Alive) continue;
                z.frozenUntil = Time.time + 4f;
            }
            FX.FloatText(gm.Squad.transform.position + Vector3.forward * 8f, "FREEZE!", new Color(0.5f, 0.9f, 1f));
            CameraRig.Shake(0.25f, 0.25f);
            if (UIManager.I != null) UIManager.I.Flash(new Color(0.4f, 0.85f, 1f, 0.3f));
        }
    }
}
