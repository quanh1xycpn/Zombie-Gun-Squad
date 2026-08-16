using UnityEngine;

namespace ZombieRoad
{
    public class CameraRig : MonoBehaviour
    {
        public Transform target;
        // Chỉnh cho màn hình dọc: cao hơn, gần hơn, FOV rộng hơn
        static readonly Vector3 Offset = new Vector3(0f, 14f, -10f);

        static CameraRig instance;
        float shakeLeft;
        float shakeAmp;

        public static void Shake(float amplitude, float duration)
        {
            if (instance == null) return;
            instance.shakeAmp = amplitude;
            instance.shakeLeft = duration;
        }

        public static CameraRig Setup(Transform target)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            var rig = cam.GetComponent<CameraRig>();
            if (rig == null) rig = cam.gameObject.AddComponent<CameraRig>();
            instance = rig;
            rig.target = target;
            cam.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
            cam.fieldOfView = 62f;
            cam.backgroundColor = new Color(0.65f, 0.75f, 0.85f);
            SetupSky(cam);
            rig.Snap();
            return rig;
        }

        // Đội càng dài camera càng lùi và nâng cao để thấy hết đoàn quân
        Vector3 DynamicOffset()
        {
            float extra = 0f;
            var gm = GameManager.I;
            if (gm != null && gm.Squad != null)
                extra = gm.Squad.ZoomExtra;
            return Offset + new Vector3(0f, extra * 0.9f, -extra * 0.7f);
        }

        // Nền trời: quad lớn gắn vào camera ở khoảng cách xa
        static void SetupSky(Camera cam)
        {
            if (cam.transform.Find("SkyQuad") != null) return;
            var skyTex = Resources.Load<Texture2D>("UI/sky_gradient");
            if (skyTex == null) return;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "SkyQuad";
            Object.Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam.transform, false);
            const float dist = 380f;
            quad.transform.localPosition = new Vector3(0f, 0f, dist);
            quad.transform.localRotation = Quaternion.identity;
            float h = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.3f;
            quad.transform.localScale = new Vector3(h * Mathf.Max(cam.aspect, 0.6f), h, 1f);
            var mat = GameAssets.Mat(Color.white, true);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", skyTex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", skyTex);
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, dist + 60f);
        }

        public void Snap()
        {
            if (target == null) return;
            Vector3 t = target.position;
            t.x *= 0.4f;
            transform.position = t + DynamicOffset();
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 t = target.position;
            t.x *= 0.4f;
            Vector3 want = t + DynamicOffset();
            Vector3 pos = Vector3.Lerp(transform.position, want, 3f * Time.deltaTime);
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.deltaTime;
                pos += Random.insideUnitSphere * shakeAmp * Mathf.Clamp01(shakeLeft * 3f);
            }
            transform.position = pos;
        }
    }
}
