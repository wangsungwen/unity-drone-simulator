# 無人機輕量遙控器 1.0.0

Android 原生 Java App，最低 Android 5.0 / API 21，不包含 Unity 引擎、3D 場景或視訊傳送。使用手機雙搖桿控制電腦的 Unity 無人機模擬器。沒有原生 CPU 函式庫，支援 Android 的 32／64 位元裝置。

## 安裝與連線

1. 將 `DroneLegacyRemote.apk` 複製到手機並允許該來源安裝。App 名稱為「無人機輕量遙控器」，可與既有兩種 Unity App 同時安裝。
2. 電腦開啟 Drone Training Lab v0.2.1 或後續相容協定版本，選「電腦畫面接收端」並啟動接收，記下 IPv4 與六位配對碼。
3. 手機與電腦連接同一區域網路。若使用訪客 Wi-Fi，裝置隔離可能阻止連線；電腦防火牆需允許 UDP 47777。
4. 手機輸入電腦 IPv4 與六位碼，按「連線」。畫面出現「已連線」與高度／速度／航向後即可操作。
5. 預設 Mode 2：左桿上下升降、左右轉向；右桿上下前後、左右側移。放開即回中。可切換 Mode 1／3；靈敏度可選 40%、65%、100%。
6. 按「起飛」開始，「降落」結束。「重置」需長按；「程序確認」用於電腦端程序複習項目。練習項目、視角及暫停由電腦設定。
7. 按「中斷」、切換其他 App 或鎖定螢幕會結束連線。回來後請在電腦重新啟動接收並配對，不會自動恢復飛行控制。

遙測逾時 0.6 秒會中斷連線；電腦既有協定會將接收控制歸零。斷線不等於自動降落。此 App 僅控制電腦中的虛擬無人機。

## 建置

主目錄 `D:\ResearchCodes\Mavic2Autonomy`；原始碼位於 `unity-drone-simulator/AndroidLegacyRemote`。

執行 `./AndroidLegacyRemote/build.ps1 -Sdk <Android SDK路徑> -Jdk <JDK11路徑>`。需要 SDK platform android-21 與 android-34、build-tools 34.0.0、JDK 11 及使用者 `.android/debug.keystore`。編譯、DEX、打包及簽署均使用 Android SDK 官方工具，不需 Unity 或 Gradle。Java 原始碼直接以 API 21 的 android.jar 編譯，避免誤用新版 API；打包 target API 為 34。

輸出 `Builds/AndroidLegacyRemote/DroneLegacyRemote.apk` 及 `Builds/DroneLegacyRemote-v1.0.0-Android5.zip`。使用開發測試簽署，尚非商店發行版本。實機多指操作、Android 5.0 顯示及跨裝置 Wi-Fi 仍需驗收。

協定沿用 `Assets/DroneTrainer/Scripts/RemoteLink.cs`：hello／welcome nonce 配對、session、遞增 sequence、action／ackAction 去重、30 Hz 控制及 0.6 秒遙測逾時。所有 UDP 操作在獨立工作執行緒執行，IPv4 直接解析，不依賴 DNS。

## 本次驗證

- API 21 函式庫編譯、DEX 產生及 APK 打包成功。
- 最低 SDK 21、target SDK 34；APK 不包含 native-code CPU 限制。
- APK 簽署 v1／v2／v3 驗證通過，v1 供 Android 5.0 使用。
- `./AndroidLegacyRemote/test.ps1`：27 項桌面 UDP 協定斷言通過，使用實際 RemoteClient 程式搭配模擬電腦端。
- 測試涵蓋配對、錯誤 nonce／session、過期序號、三種搖桿模式、指令重送與 ack、0.6 秒逾時、重新連線控制歸零及主動斷線。
- 未完成 Android 實機／模擬器畫面、多指觸控或與實際 Unity 電腦端的跨裝置連線驗收。桌面測試使用的 JSON jar 不打包進 APK。
