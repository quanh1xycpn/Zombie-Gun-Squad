using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZombieRoad
{
    public class SteerArea : MonoBehaviour, IDragHandler
    {
        public void OnDrag(PointerEventData eventData)
        {
            var gm = GameManager.I;
            if (gm != null && gm.Squad != null)
                gm.Squad.Steer(eventData.delta.x * 0.02f);
        }
    }

    public class UIManager : MonoBehaviour
    {
        public static UIManager I;

        Font font;
        Canvas canvas;
        Text levelText, countText, toastText;
        GameObject startPanel, winPanel, losePanel;
        Text startLabel, winLabel, loseLabel;
        Image rocketOverlay, freezeOverlay;
        GameObject rocketBtn, freezeBtn;
        Image flashImage;
        Coroutine toastCo;
        Coroutine flashCo;

        public static UIManager Create()
        {
            var go = new GameObject("UIManager");
            var ui = go.AddComponent<UIManager>();
            I = ui;
            ui.font = GameAssets.UIFont();
            ui.Build();
            return ui;
        }

        void Build()
        {
            var cgo = new GameObject("Canvas");
            cgo.transform.SetParent(transform, false);
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            // Vùng kéo để lái đội hình (dưới cùng, hứng raycast)
            var steer = MakeImage(canvas.transform, "SteerArea", new Color(0f, 0f, 0f, 0f));
            Stretch(steer.rectTransform);
            steer.gameObject.AddComponent<SteerArea>();

            // Dời xuống dưới vùng banner quảng cáo trên cùng
            levelText = MakeText(canvas.transform, "LevelText", "Màn 1", 56, TextAnchor.MiddleLeft);
            Anchor(levelText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -200f), new Vector2(500f, 80f));

            // (đã bỏ hiển thị "Lính: N" theo yêu cầu)

            toastText = MakeText(canvas.transform, "Toast", "", 64, TextAnchor.MiddleCenter);
            toastText.color = new Color(1f, 0.85f, 0.2f);
            Anchor(toastText.rectTransform, new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(900f, 100f));

            rocketOverlay = MakeSkillButton(new Vector2(0f, 0f), new Vector2(160f, 160f), "ROCKET",
                new Color(0.85f, 0.35f, 0.08f, 0.95f), "icon_rocket",
                delegate { if (GameManager.I != null) GameManager.I.Skills.FireRocket(); });
            rocketBtn = rocketOverlay.transform.parent.gameObject;
            freezeOverlay = MakeSkillButton(new Vector2(1f, 0f), new Vector2(-160f, 160f), "FREEZE",
                new Color(0.12f, 0.45f, 0.85f, 0.95f), "icon_freeze",
                delegate { if (GameManager.I != null) GameManager.I.Skills.Freeze(); });
            freezeBtn = freezeOverlay.transform.parent.gameObject;

            flashImage = MakeImage(canvas.transform, "Flash", new Color(0f, 0f, 0f, 0f));
            Stretch(flashImage.rectTransform);
            flashImage.raycastTarget = false;

            startPanel = MakePanel("StartPanel", "Level 1\n\nTAP TO START", out startLabel,
                delegate { if (GameManager.I != null) GameManager.I.StartRun(); });
            winPanel = MakePanel("WinPanel", "LEVEL CLEAR!", out winLabel,
                delegate
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        winLabel.text = "NO INTERNET!\n\nConnect to Wi-Fi\nto continue";
                        return;
                    }
                    Ads.ShowRewardedThen(delegate { if (GameManager.I != null) GameManager.I.NextLevel(); });
                });
            losePanel = MakePanel("LosePanel", "DEFEAT!", out loseLabel,
                delegate
                {
                    if (Application.internetReachability == NetworkReachability.NotReachable)
                    {
                        loseLabel.text = "NO INTERNET!\n\nConnect to Wi-Fi\nto continue";
                        return;
                    }
                    Ads.ShowRewardedThen(delegate { if (GameManager.I != null) GameManager.I.Retry(); });
                });
        }

        Image MakeImage(Transform parent, string name, Color c)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = c;
            return img;
        }

        Text MakeText(Transform parent, string name, string content, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.text = content;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.alignment = anchor;
            t.color = Color.white;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.7f);
            outline.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        Image MakeSkillButton(Vector2 anchor, Vector2 pos, string label, Color bgColor, string spriteName, UnityEngine.Events.UnityAction onClick)
        {
            var img = MakeImage(canvas.transform, "Skill_" + label, bgColor);
            Anchor(img.rectTransform, anchor, pos, new Vector2(240f, 240f));
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            // Icon thật nếu có, không thì fallback chữ
            var spr = Resources.Load<Sprite>("UI/" + spriteName);
            if (spr == null)
            {
                // Texture chưa được import dạng Sprite thì tự dựng sprite runtime
                var tex = Resources.Load<Texture2D>("UI/" + spriteName);
                if (tex != null)
                    spr = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
            if (spr != null)
            {
                img.sprite = spr;
                img.color = Color.white;
            }
            else
            {
                var txt = MakeText(img.transform, "Label", label, 40, TextAnchor.MiddleCenter);
                Stretch(txt.rectTransform);
            }

            var overlay = MakeImage(img.transform, "Cooldown", new Color(0f, 0f, 0f, 0.55f));
            Stretch(overlay.rectTransform);
            overlay.raycastTarget = false;
            if (spr != null) overlay.sprite = spr; // phủ đen theo đúng hình icon bo góc
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Vertical;
            overlay.fillOrigin = (int)Image.OriginVertical.Bottom;
            overlay.fillAmount = 0f;
            overlay.enabled = false; // chỉ bật khi đang hồi chiêu
            return overlay;
        }

        GameObject MakePanel(string name, string label, out Text labelText, UnityEngine.Events.UnityAction onClick)
        {
            var img = MakeImage(canvas.transform, name, new Color(0f, 0f, 0f, 0.55f));
            Stretch(img.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            labelText = MakeText(img.transform, "Label", label, 80, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform);
            img.gameObject.SetActive(false);
            return img.gameObject;
        }

        // Panel phủ toàn màn hình đè lên nút skill -> ẩn nút khi panel hiện, tránh bấm hụt
        void SetSkillButtonsVisible(bool on)
        {
            if (rocketBtn != null) rocketBtn.SetActive(on);
            if (freezeBtn != null) freezeBtn.SetActive(on);
        }

        public void ShowStart(int level)
        {
            startPanel.SetActive(true);
            winPanel.SetActive(false);
            losePanel.SetActive(false);
            SetSkillButtonsVisible(false);
            startLabel.text = "Level " + level + "\n\nTAP TO START\n\n(drag to steer your squad)";
        }

        public void HidePanels()
        {
            startPanel.SetActive(false);
            winPanel.SetActive(false);
            losePanel.SetActive(false);
            SetSkillButtonsVisible(true);
        }

        public void ShowWin(int level, bool isLast)
        {
            winPanel.SetActive(true);
            SetSkillButtonsVisible(false);
            winLabel.text = isLast
                ? "YOU BEAT ALL 100 LEVELS!\n\nTAP TO PLAY AGAIN"
                : "LEVEL " + level + " CLEAR!\n\nTAP TO PLAY LEVEL " + (level + 1);
        }

        public void ShowLose(int level)
        {
            losePanel.SetActive(true);
            SetSkillButtonsVisible(false);
            loseLabel.text = "DEFEAT!\n\nTAP TO RETRY LEVEL " + level;
        }

        public void SetHUD(int level, int count, float progress)
        {
            levelText.text = "Level " + level + "  (" + Mathf.RoundToInt(progress * 100f) + "%)";
        }

        public void Toast(string msg)
        {
            if (toastCo != null) StopCoroutine(toastCo);
            toastCo = StartCoroutine(ToastRoutine(msg));
        }

        IEnumerator ToastRoutine(string msg)
        {
            toastText.text = msg;
            yield return new WaitForSeconds(1.6f);
            toastText.text = "";
        }

        // Chớp màu toàn màn hình khi dùng skill
        public void Flash(Color c)
        {
            if (flashCo != null) StopCoroutine(flashCo);
            flashCo = StartCoroutine(FlashRoutine(c));
        }

        IEnumerator FlashRoutine(Color c)
        {
            float t = 0f;
            const float dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                Color cc = c;
                cc.a = c.a * (1f - t / dur);
                flashImage.color = cc;
                yield return null;
            }
            flashImage.color = new Color(0f, 0f, 0f, 0f);
        }

        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            float rf = gm.Skills.RocketFrac;
            float ff = gm.Skills.FreezeFrac;
            if (rocketOverlay != null)
            {
                rocketOverlay.fillAmount = rf;
                rocketOverlay.enabled = rf > 0.01f; // sẵn sàng = icon sáng sạch, không lớp phủ
            }
            if (freezeOverlay != null)
            {
                freezeOverlay.fillAmount = ff;
                freezeOverlay.enabled = ff > 0.01f;
            }
        }
    }
}
