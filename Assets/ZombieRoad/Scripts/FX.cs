using System.Collections;
using UnityEngine;

namespace ZombieRoad
{
    public class FX : MonoBehaviour
    {
        static FX _i;
        static FX I
        {
            get
            {
                if (_i == null)
                {
                    var go = new GameObject("FX");
                    DontDestroyOnLoad(go);
                    _i = go.AddComponent<FX>();
                }
                return _i;
            }
        }

        public static void Puff(Vector3 pos, Color color, float size)
        {
            I.StartCoroutine(I.PuffRoutine(pos, color, size, 0.3f));
        }

        public static void Explosion(Vector3 pos, float radius)
        {
            I.StartCoroutine(I.PuffRoutine(pos, new Color(1f, 0.55f, 0.1f), radius * 2f, 0.4f));
        }

        public static void FloatText(Vector3 pos, string text, Color color)
        {
            I.StartCoroutine(I.FloatTextRoutine(pos, text, color));
        }

        IEnumerator PuffRoutine(Vector3 pos, Color color, float size, float dur)
        {
            var mat = GameAssets.Mat(color, true);
            var go = GameAssets.Prim(PrimitiveType.Sphere, "Puff", null, Vector3.zero, Vector3.one * 0.1f, mat);
            go.transform.position = pos;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, size, Mathf.Sqrt(k));
                yield return null;
            }
            Destroy(go);
            Destroy(mat);
        }

        IEnumerator FloatTextRoutine(Vector3 pos, string text, Color color)
        {
            var go = new GameObject("FloatText");
            go.transform.position = pos;
            var tm = GameAssets.WorldText(text, go.transform, Vector3.zero, 1.6f, color);
            var cam = Camera.main;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime;
                go.transform.position = pos + Vector3.up * (2f + t * 2.5f);
                if (cam != null)
                    go.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
                yield return null;
            }
            Destroy(go);
        }
    }
}
