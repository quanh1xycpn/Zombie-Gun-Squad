using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ZombieRoad
{
    public static class BuildScript
    {
        // Ưu tiên scene user tự chỉnh (edited.unity), thiếu GameRoot thì tự bổ sung
        static string PrepareScene()
        {
            string scenePath = System.IO.File.Exists("Assets/Scenes/edited.unity")
                ? "Assets/Scenes/edited.unity"
                : "Assets/Scenes/ZombieRoad.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
            if (Object.FindFirstObjectByType<GameManager>() == null)
            {
                var go = new GameObject("GameRoot");
                go.AddComponent<GameManager>();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                Debug.Log("ZR: da them GameRoot (GameManager) vao " + scenePath);
            }
            Debug.Log("ZR: build voi scene " + scenePath);
            return scenePath;
        }

        const string KeystorePass = "ZombieGun2026!";
        // DEMO App ID (Google test) — thay bằng App ID thật của bạn trước khi phát hành
        const string AdMobAppId = "ca-app-pub-3940256099942544~3347511713";

        // Chạy SAU khi import GoogleMobileAds.unitypackage:
        // bật define GOOGLE_MOBILE_ADS + điền App ID vào settings của SDK
        public static void SetupAds()
        {
            bool hasSdk = false;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetName().Name.StartsWith("GoogleMobileAds")) { hasSdk = true; break; }
            Debug.Log("ZR ADS: sdk=" + hasSdk);

            if (hasSdk)
            {
                string defs = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
                if (!defs.Contains("GOOGLE_MOBILE_ADS"))
                    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, defs + ";GOOGLE_MOBILE_ADS");

                var t = System.Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
                if (t != null)
                {
                    var load = t.GetMethod("LoadInstance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    var inst = load != null ? load.Invoke(null, null) : null;
                    if (inst != null)
                    {
                        var prop = t.GetProperty("GoogleMobileAdsAndroidAppId");
                        if (prop != null) prop.SetValue(inst, AdMobAppId, null);
                        var so = inst as Object;
                        if (so != null) { EditorUtility.SetDirty(so); AssetDatabase.SaveAssets(); }
                        Debug.Log("ZR ADS: da dien App ID Android");
                    }
                    else Debug.LogWarning("ZR ADS: khong LoadInstance duoc settings");
                }
                else Debug.LogWarning("ZR ADS: khong tim thay GoogleMobileAdsSettings");
            }
            EditorApplication.Exit(0);
        }

        static void ApplyCommonSettings()
        {
            PlayerSettings.productName = "Zombie Gun Squad";
            PlayerSettings.companyName = "ZombieGun";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.zombiegun.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.bundleVersion = "1.0";
            // Ký bằng keystore riêng (bắt buộc để lên Google Play)
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = "zombiegun.keystore";
            PlayerSettings.Android.keystorePass = KeystorePass;
            PlayerSettings.Android.keyaliasName = "zombiegun";
            PlayerSettings.Android.keyaliasPass = KeystorePass;
        }

        public static void PerformAndroidBuild()
        {
            string scenePath = PrepareScene();
            PrepareAssets();
            ApplyCommonSettings();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/ZombieGunSquad.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("ZR BUILD RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors + " time=" + report.summary.totalTime);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // Build .aab đã ký để upload Google Play Console
        public static void PerformPlayBuild()
        {
            string scenePath = PrepareScene();
            PrepareAssets();
            ApplyCommonSettings();
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.bundleVersionCode = PlayerSettings.Android.bundleVersionCode + 1;
            EditorUserBuildSettings.buildAppBundle = true;
            // Xuất kèm file native debug symbols để crash report trên Play Console đọc được
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = "Builds/ZombieGunSquad.aab",
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log("ZR AAB RESULT: " + report.summary.result + " errors=" + report.summary.totalErrors + " versionCode=" + PlayerSettings.Android.bundleVersionCode);
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // Chuẩn bị asset trước build: material template chống strip shader,
        // icon UI dạng Sprite, app icon từ logo
        static void PrepareAssets()
        {
            ConfigureSoldierAnimation();
            ConfigureCharacters();

            CreateMatIfMissing("Assets/ZombieRoad/Resources/BaseLit.mat", "Universal Render Pipeline/Lit");
            CreateMatIfMissing("Assets/ZombieRoad/Resources/BaseUnlit.mat", "Universal Render Pipeline/Unlit");
            CreateFxAdditiveIfMissing("Assets/ZombieRoad/Resources/FxAdditive.mat");

            MarkAsSprite("Assets/ZombieRoad/Resources/UI/icon_rocket.png");
            MarkAsSprite("Assets/ZombieRoad/Resources/UI/icon_freeze.png");

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ZombieRoad/Icons/app_icon.png");
            if (icon != null)
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new Texture2D[] { icon }, IconKind.Any);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // FBX có thể bị thay bản mới bất kỳ lúc nào -> mỗi lần build:
        // ép lại Humanoid + loop + chạy-tại-chỗ, rồi DỰNG LẠI AnimatorController từ clip hiện tại
        static void ConfigureSoldierAnimation()
        {
            // Model hiển thị (soldier_base) cũng phải Humanoid để nhận clip retarget
            var baseImp = AssetImporter.GetAtPath("Assets/ZombieRoad/Resources/Models/soldier_base.fbx") as ModelImporter;
            if (baseImp != null && baseImp.animationType != ModelImporterAnimationType.Human)
            {
                baseImp.animationType = ModelImporterAnimationType.Human;
                baseImp.SaveAndReimport();
            }

            const string fbx = "Assets/ZombieRoad/Resources/Models/soldier_run.fbx";
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (imp == null) { Debug.LogWarning("soldier_run.fbx not found"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            var clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = "run";
                clips[i].loopTime = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();

            // FBX có thể chứa cả clip object-level (humanMotion=false, không chạy được trên
            // Humanoid) lẫn clip xương chuẩn — phải chọn đúng clip humanMotion=true
            AnimationClip run = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var c = a as AnimationClip;
                if (c == null || c.name.Contains("__preview__")) continue;
                if (c.humanMotion) { run = c; break; }
                if (run == null) run = c;
            }
            if (run == null) { Debug.LogWarning("no run clip in soldier_run.fbx"); return; }
            Debug.Log("ZR: chon clip len=" + run.length + " humanMotion=" + run.humanMotion);

            const string ctrlPath = "Assets/ZombieRoad/Resources/SoldierRunController.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, run);
            Debug.Log("ZR: rebuilt SoldierRunController with clip '" + run.name + "' len=" + run.length);
        }

        public static void DiagnoseSoldier()
        {
            ConfigureSoldierAnimation();
            string[] paths = { "Assets/ZombieRoad/Resources/Models/soldier_base.fbx", "Assets/ZombieRoad/Resources/Models/soldier_run.fbx" };
            foreach (var p in paths)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var anim = go != null ? go.GetComponent<Animator>() : null;
                var av = anim != null ? anim.avatar : null;
                Debug.Log("ZRDIAG: " + p + " | prefab=" + (go != null) + " | animator=" + (anim != null)
                    + " | avatar=" + (av != null ? av.name + " valid=" + av.isValid + " human=" + av.isHuman : "NULL"));
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp != null) Debug.Log("ZRDIAG: " + p + " | animType=" + imp.animationType);
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
                {
                    var c = a as AnimationClip;
                    if (c != null && !c.name.Contains("__preview__"))
                        Debug.Log("ZRDIAG: clip '" + c.name + "' len=" + c.length + " loop=" + c.isLooping + " humanMotion=" + c.humanMotion);
                }
            }
            var ctrl = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/ZombieRoad/Resources/SoldierRunController.controller");
            if (ctrl != null)
            {
                foreach (var st in ctrl.layers[0].stateMachine.states)
                    Debug.Log("ZRDIAG: controller state '" + st.state.name + "' motion=" + (st.state.motion != null ? st.state.motion.name : "NULL"));
            }
            else Debug.Log("ZRDIAG: controller NULL");
            EditorApplication.Exit(0);
        }

        // 4 zombie + robot: Humanoid, clip đi (loop) + clip chết (không loop) trong 1 controller,
        // material tắt metallic + LUÔN cập nhật texture, texture nén 1024
        static readonly string[] CharNames = { "zombie_normal", "zombie_runner", "zombie_tank", "zombie_boss", "robot" };

        [MenuItem("ZombieRoad/3. Cấu hình model nhân vật (zombie + robot)")]
        public static void ConfigureCharacters()
        {
            foreach (var name in CharNames)
            {
                string fbx = "Assets/ZombieRoad/Resources/Models/Chars/" + name + ".fbx";
                if (AssetImporter.GetAtPath(fbx) as ModelImporter == null) { Debug.LogWarning("ZR: thieu " + fbx); continue; }
                ImportHumanoid(fbx, true);

                AnimationClip move = PickClip(fbx, new[] { "walk", "run", "limp" });
                // Clip chết: tìm trong fbx chính (file merged) hoặc file *_death.fbx riêng
                AnimationClip death = PickClip(fbx, new[] { "dead", "die", "dying", "lying" }, true);
                string deathFbx = "Assets/ZombieRoad/Resources/Models/Chars/" + name + "_death.fbx";
                if (death == null && AssetImporter.GetAtPath(deathFbx) as ModelImporter != null)
                {
                    ImportHumanoid(deathFbx, false);
                    death = PickClip(deathFbx, new[] { "dead", "die", "dying", "lying", "" });
                }

                string ctrlPath = "Assets/ZombieRoad/Resources/" + name + "_ctrl.controller";
                AssetDatabase.DeleteAsset(ctrlPath);
                if (move != null)
                {
                    var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPathWithClip(ctrlPath, move);
                    ctrl.layers[0].stateMachine.defaultState.name = "move";
                    if (death != null)
                    {
                        var st = ctrl.layers[0].stateMachine.AddState("death");
                        st.motion = death;
                    }
                    Debug.Log("ZR: " + name + " move='" + move.name + "' death=" + (death != null ? "'" + death.name + "'" : "KHONG CO"));
                }
                else Debug.LogWarning("ZR: " + name + " KHONG co clip humanMotion!");

                string texPath = "Assets/ZombieRoad/Resources/Models/Chars/" + name + "_tex.png";
                var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (ti != null && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }

                // UPSERT material: tạo nếu thiếu, LUÔN gán lại texture (fix zombie trắng bệch)
                string matPath = "Assets/ZombieRoad/Resources/" + name + "_mat.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (m == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit");
                    if (sh == null) continue;
                    m = new Material(sh);
                    AssetDatabase.CreateAsset(m, matPath);
                }
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (tex == null) Debug.LogWarning("ZR: " + name + " KHONG tim thay texture " + texPath);
                m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                m.SetFloat("_Metallic", 0f);
                m.SetFloat("_Smoothness", 0.2f);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ZR: cau hinh model nhan vat xong.");
        }

        static void ImportHumanoid(string fbx, bool loop)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.animationType = ModelImporterAnimationType.Human;
            var clips = imp.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                clips[i].lockRootPositionXZ = true;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
            }
            imp.clipAnimations = clips;
            imp.SaveAndReimport();
        }

        // Chọn clip humanMotion theo từ khóa tên; strict=true thì BẮT BUỘC khớp từ khóa
        static AnimationClip PickClip(string fbx, string[] keywords, bool strict = false)
        {
            AnimationClip first = null, matched = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(fbx))
            {
                var c = a as AnimationClip;
                if (c == null || c.name.Contains("__preview__") || !c.humanMotion) continue;
                if (first == null) first = c;
                string n = c.name.ToLowerInvariant();
                foreach (var k in keywords)
                    if (k.Length > 0 && n.Contains(k)) { matched = c; break; }
                if (matched != null) break;
            }
            return matched != null ? matched : (strict ? null : first);
        }

        // Render tường ở cả 2 chiều lật để chọn đúng mặt/đúng chiều
        public static void DiagWallRender()
        {
            var lightGo = new GameObject("dlight");
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(60f, 20f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.64f);

            for (int variant = 0; variant < 2; variant++)
            {
                // Nhìn vào 2 MẶT BÊN của tường (mặt +X và -X) để chọn mặt đẹp úp vào đường
                var wall = ModelLib.SpawnWallSegment("side_wall", null, 8f, Vector3.zero, false);
                var camGo = new GameObject("dcam");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
                camGo.transform.position = new Vector3(variant == 0 ? 7f : -7f, 2.5f, 0f);
                camGo.transform.LookAt(new Vector3(0f, 1f, 0f));
                var rt = new RenderTexture(900, 500, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(900, 500, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 900, 500), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes("diag_wall_" + (variant == 0 ? "A" : "B") + ".png", tex.EncodeToPNG());
                RenderTexture.active = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(wall);
            }
            Object.DestroyImmediate(lightGo);
            Debug.Log("ZRDIAG: wall renders saved");
            EditorApplication.Exit(0);
        }

        public static void DiagWall()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRoad/Resources/Models/Props/side_wall.fbx");
            if (prefab == null) { Debug.Log("ZRDIAG: prefab null"); EditorApplication.Exit(0); return; }
            var go = Object.Instantiate(prefab);
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                Debug.Log("ZRDIAG: world size=" + b.size.ToString("F3") + " center=" + b.center.ToString("F3"));
            }
            foreach (var t in go.GetComponentsInChildren<Transform>())
                Debug.Log("ZRDIAG: node '" + t.name + "' localRot=" + t.localRotation.eulerAngles.ToString("F1") + " localScale=" + t.localScale.ToString("F3"));
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                var mats = mr.sharedMaterials;
                var mf = mr.GetComponent<MeshFilter>();
                Debug.Log("ZRDIAG: renderer '" + mr.name + "' mats=" + mats.Length + " submeshes=" + (mf != null && mf.sharedMesh != null ? mf.sharedMesh.subMeshCount : -1));
                for (int i = 0; i < mats.Length; i++)
                    Debug.Log("ZRDIAG:   mat[" + i + "]=" + (mats[i] != null ? mats[i].name + " color=" + (mats[i].HasProperty("_BaseColor") ? mats[i].GetColor("_BaseColor").ToString() : "?") + " tex=" + (mats[i].mainTexture != null ? mats[i].mainTexture.name : "null") : "NULL"));
            }
            Object.DestroyImmediate(go);
            EditorApplication.Exit(0);
        }

        static void CreateMatIfMissing(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find(shaderName);
            if (sh == null) { Debug.LogWarning("Shader not found: " + shaderName); return; }
            AssetDatabase.CreateAsset(new Material(sh), path);
        }

        // Material phát sáng cộng màu (additive) cho trail đạn + chớp lửa đầu nòng
        static void CreateFxAdditiveIfMissing(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) return;
            var m = new Material(sh);
            m.SetFloat("_Surface", 1f); // Transparent
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One); // additive
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(m, path);
        }

        static void MarkAsSprite(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.SaveAndReimport();
            }
        }
    }
}
