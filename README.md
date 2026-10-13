# 無人機飛行訓練 / Unity Drone Simulator

Unity 3D 無人機模擬器，支援手機直接操作，以及手機透過同一個 Wi-Fi 當作電腦模擬器的雙搖桿遙控器。

![Unity 飛行場景](docs/screenshots/desktop-scene.png)

## 功能

- 手機雙指雙搖桿，支援 Mode 1／2／3、靈敏度調整。
- 起飛、降落、重置、定位／姿態練習及風速設定。
- 自由飛行、懸停、方形、8 字及定點降落練習。
- 追蹤、地面操作員與機頭視角。
- Wi-Fi 配對、即時遙測與 0.6 秒連線逾時控制歸零。
- Windows、Android、iOS 共用 Unity 原始碼與建置工具。

## 開始使用

1. 使用 Unity Hub 安裝 **Unity 2022.3.62f2** 與所需平台模組。
2. Clone 本儲存庫，在 Unity Hub 開啟儲存庫根目錄。
3. 開啟 `Assets/DroneTrainer/Scenes/Trainer.unity`，按 Play。
4. 選「手機／單機飛行」→「起飛」，開始操作。
5. 需要建置時，使用 **Drone Trainer → Build** 選單。

手機遙控電腦：電腦選「電腦接收手機」→「啟動接收」；手機選「手機遙控器」，輸入電腦 IPv4 與六位配對碼。兩端需在同一區域網路，電腦允許 UDP 47777。

本儲存庫包含原始碼、Unity 資產、設定、文件及測試；`Builds`、`Library`、本機工具與快取不納入 Git。請自行建置 APK／Windows 執行檔；iOS 安裝需 Mac／Xcode 編譯及簽署。

## 文件與驗證

- [完整繁體中文操作與建置說明](README_繁體中文.md)
- [架構與連線協定](docs/架構與協定.md)
- [驗證紀錄與實機驗收](docs/驗證紀錄.md)
- [Unity 整合測試結果](docs/play-smoke-results.txt)

已記錄 35 項核心測試及 15 項 Unity 整合檢查通過，Windows／Android 建置與 iOS 專案匯出成功。手機實機與跨裝置 Wi-Fi 尚待驗收。練習評分為自訂規則；本程式控制虛擬飛機，不包含真機飛控。

中文字型使用 Noto Sans CJK TC，授權見 [SIL OFL 1.1](Assets/DroneTrainer/Resources/FONT-LICENSE.txt)。
