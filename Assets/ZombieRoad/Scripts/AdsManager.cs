using UnityEngine;

namespace ZombieRoad
{
    // Lớp vỏ an toàn: SDK chưa cài thì mọi lời gọi đều no-op, game vẫn chạy bình thường
    public static class Ads
    {
        public static void Init()
        {
#if GOOGLE_MOBILE_ADS
            AdsManager.Ensure();
#endif
        }

        // REWARDED khi bấm "chơi màn tiếp" / "chơi lại" — xem xong (hoặc chưa sẵn sàng) thì đi tiếp
        public static void ShowRewardedThen(System.Action then)
        {
#if GOOGLE_MOBILE_ADS
            AdsManager.ShowRewardedThen(then);
#else
            if (then != null) then();
#endif
        }
    }

#if GOOGLE_MOBILE_ADS
    public class AdsManager : MonoBehaviour
    {
        // DEMO ad units (Google test IDs) — thay bằng ID thật của bạn trước khi phát hành
        const string BannerId = "ca-app-pub-3940256099942544/6300978111";
        const string OpenId = "ca-app-pub-3940256099942544/9257395921";
        const string RewardedId = "ca-app-pub-3940256099942544/5224354917";

        static AdsManager instance;
        GoogleMobileAds.Api.BannerView banner;
        GoogleMobileAds.Api.RewardedAd rewarded;
        GoogleMobileAds.Api.AppOpenAd openAd;
        System.Action pendingAfterRewarded;
        volatile bool firePending;
        bool sdkReady;
        bool openShown;

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("AdsManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<AdsManager>();
        }

        void Start()
        {
            GoogleMobileAds.Api.MobileAds.RaiseAdEventsOnUnityMainThread = true;
            GoogleMobileAds.Api.MobileAds.Initialize(status =>
            {
                sdkReady = true;
                LoadBanner();
                LoadOpenAd();
                LoadRewarded();
            });
        }

        // ===== Thủ thuật load nền: luôn có sẵn quảng cáo trong "kho" =====
        // Load hỏng -> tự thử lại sau 15s. Hiện xong -> load nền ngay cái tiếp theo.

        // BANNER trên cùng màn hình
        void LoadBanner()
        {
            if (banner != null) banner.Destroy();
            banner = new GoogleMobileAds.Api.BannerView(
                BannerId, GoogleMobileAds.Api.AdSize.Banner, GoogleMobileAds.Api.AdPosition.Top);
            banner.OnBannerAdLoadFailed += err => Invoke("LoadBanner", 30f);
            banner.LoadAd(new GoogleMobileAds.Api.AdRequest());
        }

        // OPEN ad hiện 1 lần khi mở game
        void LoadOpenAd()
        {
            GoogleMobileAds.Api.AppOpenAd.Load(OpenId, new GoogleMobileAds.Api.AdRequest(),
                (ad, err) =>
                {
                    if (err != null || ad == null)
                    {
                        if (!openShown) Invoke("LoadOpenAd", 15f); // thử lại tới khi hiện được 1 lần
                        return;
                    }
                    openAd = ad;
                    if (!openShown)
                    {
                        openShown = true;
                        openAd.Show();
                    }
                });
        }

        bool loadingRewarded;

        void LoadRewarded()
        {
            if (loadingRewarded) return; // tránh load chồng
            loadingRewarded = true;
            GoogleMobileAds.Api.RewardedAd.Load(RewardedId, new GoogleMobileAds.Api.AdRequest(),
                (ad, err) =>
                {
                    loadingRewarded = false;
                    if (err != null || ad == null)
                    {
                        Invoke("LoadRewarded", 15f); // load nền thất bại -> thử lại
                        return;
                    }
                    rewarded = ad;
                    rewarded.OnAdFullScreenContentClosed += () =>
                    {
                        firePending = true;
                        rewarded = null;
                        LoadRewarded(); // xem xong -> load nền cái tiếp theo ngay
                    };
                    rewarded.OnAdFullScreenContentFailed += _ =>
                    {
                        firePending = true;
                        rewarded = null;
                        LoadRewarded();
                    };
                });
        }

        void Update()
        {
            if (firePending)
            {
                firePending = false;
                var a = pendingAfterRewarded;
                pendingAfterRewarded = null;
                if (a != null) a();
            }
        }

        public static void ShowRewardedThen(System.Action then)
        {
            if (instance == null || !instance.sdkReady || instance.rewarded == null || !instance.rewarded.CanShowAd())
            {
                // Kho trống (hoặc quảng cáo hết hạn) -> không chặn người chơi,
                // đồng thời load nền để lần bấm sau có sẵn
                if (instance != null && instance.sdkReady)
                {
                    instance.rewarded = null;
                    instance.LoadRewarded();
                }
                if (then != null) then();
                return;
            }
            instance.pendingAfterRewarded = then;
            instance.rewarded.Show(reward => { });
        }
    }
#endif
}
