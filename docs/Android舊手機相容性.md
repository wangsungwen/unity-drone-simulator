# Android 舊手機相容性

後續主工作目錄：`D:\ResearchCodes\Mavic2Autonomy`。
Unity 專案：`D:\ResearchCodes\Mavic2Autonomy\unity-drone-simulator`。

## v0.2.2 調整

兩種 APK 均將最低版本從 Android 8（API 26）降至 Android 5.1（API 22），CPU 從僅 ARM64 改為 ARMv7（NEON）與 ARM64。圖形 API 依序嘗試 OpenGL ES 3.0、2.0，不要求 Vulkan。保留 target SDK 34；它不代表最低安裝版本。

Unity 2022.3 官方最低 Android 系統為 5.1，不能將設定改成 API 21 就宣稱支援 Android 5.0。
來源：https://docs.unity3d.com/2022.3/Documentation/Manual/system-requirements.html

## 安裝與驗收

1. 在手機「設定 → 關於手機」確認完整版本；5.0 與 5.1 不同。
2. 將 Android ZIP 解壓後，把所需 APK 複製至手機，允許該來源安裝。
3. `Android/DroneTrainingLab.apk` 是手機直接操作 3D 場景的版本。
4. `AndroidRemote/DroneRemoteController.apk` 是手機搖桿控制電腦的版本。舊手機可優先測試此模式，其不顯示 3D 飛行場景。
5. 遙控時電腦啟動「電腦畫面接收端」，手機與電腦連接同一區域網路，輸入電腦 IPv4 與六位配對碼；電腦須允許 UDP 47777。
6. 檢查起飛、兩指同時操縱、遙測更新、降落，以及斷線後控制歸零；單機版本另檢查材質、畫面流暢度與持續操作穩定性。

兩個 APK 可同時安裝；本次仍使用測試簽署。符合最低系統不保證所有舊手機都有足夠效能；尚未完成 Android 5.1 實機驗收。

## 若手機是 Android 5.0

先確認原廠是否提供升級至 5.1 以上。若無法升級且需求是「手機遙控電腦」，已另製 [Android 原生輕量遙控器 1.0.0](../AndroidLegacyRemote/README.md)，沿用本專案 UDP 配對及控制協定。安裝 `Builds/AndroidLegacyRemote/DroneLegacyRemote.apk`，最低 Android 5.0；仍待實機驗收。若還需要手機執行完整 3D 模擬，需另行評估舊 Unity 分支與所有功能的相容性，不能沿用目前 APK 宣稱支援。

## 建置

使用 `tools/build.ps1` 的 Android 與 AndroidRemote 目標，接著執行 `python tools/package-platforms.py --platform Android`。若 aapt 未加入 PATH，追加 `--aapt "Android SDK/build-tools/34.0.0/aapt.exe 的完整路徑"`。版本號由 `TrainerBuild.cs` 與打包工具一起維護。
