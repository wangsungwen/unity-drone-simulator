---
title: Drone Training Lab
emoji: 🚁
colorFrom: blue
colorTo: green
sdk: static
app_file: index.html
pinned: false
---

# 無人機線上飛行訓練 Web 0.1.1

可直接部署到 Hugging Face **Static HTML Space** 的獨立網頁版。3D 畫面與双搖桿在同一個瀏覽器執行，不需 Unity、Python 後端、GPU Space 或登入。Three.js 已隨程式附上，不需要 CDN。

## 部署

1. 在 Hugging Face 建立新的 Space，選擇 **Static** SDK。
2. 將本資料夾的內容直接上傳到 Space 根目錄：`README.md`、`index.html`、`style.css`、`app.js`、`model.js`、`vendor/`。不要再包一層 HuggingFaceSpace 資料夾。
3. 保留 README 最上方的 YAML 設定；入口是 `index.html`。提交後等 Space 更新，再開啟 App。
4. 使用手機時橫向操作；也可透過 Space 的直接 App 網址開啟，減少嵌入頁的可用高度限制。

亦可把整個本資料夾作為獨立 Git 儲存庫推送到自己的 Space。已部署至 https://huggingface.co/spaces/wangsongwen/drone-training-lab ，直接 App 網址為 https://wangsongwen-drone-training-lab.static.hf.space/index.html 。

官方 Static Spaces 說明：https://huggingface.co/docs/hub/en/spaces-sdks-static

## 功能與範圍

- 瀏覽器 3D 飛行畫面、双指雙搖桿、鍵盤、Mode 1／2／3、靈敏度及側風。
- 自動起飛至 1.5 m、降落、暫停、重置；全景俯視、眼高操作員、跟隨視角。
- 自由飛行、四面停懸、12 × 5 m 雙向矩形、8 字水平圓、側面前後飛行、20 m 五邊航線、定點降落。
- 級別選單調整 8 字、側飛及五邊尺寸，矩形固定 12 × 5 m。
- 切到背景時自動暫停，需手動繼續；一般失焦只清除控制，不暫停；搖桿放開／取消觸控時歸零。

這是獨立 JavaScript 簡化模擬，不是 Unity WebGL 匯出。路線參考本儲存庫 Unity 版本；回饋判定不是官方評分，也不與 Unity 全功能等同：未包含完整起落架判定、順時針轉向累積驗證、緊急返航及程序卡。模型高度上限 40 m，範圍 X ±110 m、前後 -40～120 m，速度及風為訓練設定。

不支援跨裝置遙控、UDP 配對、Windows 畫面串流或真機飛控。每位訪客的模擬狀態只在自己的瀏覽器；重新整理會重置。若要手機網頁遙控另一個電腦網頁，需另加配對伺服器與 WebSocket／WebRTC 通訊。

需要支援 WebGL 與 JavaScript modules 的現代瀏覽器；不保證 Android 5 舊瀏覽器可用，舊手機請使用原生 APK。

## 本機執行與測試

在此資料夾執行 `python -m http.server 7860`，開啟 `http://localhost:7860`，不要直接以 file:// 開啟。

模型測試：`node --test tests/model.test.mjs`。Three.js 0.160.1 的 MIT 授權位於 `vendor/THREE-LICENSE.txt`。

## Web 0.1.1 修正

- 修正 Hugging Face iframe 焦點切換導致每次操縱搖桿都暫停：blur 僅清除控制，visibilitychange 進入背景及 pagehide 才暫停；手動暫停保持不變。
- 新增前進低頭、後退抬頭、左右側傾，最大 16°，以平滑過渡回正。姿態作用於機身本地座標，不影響航向與攝影機位置；降落時回正。
- 視覺傾斜與 Unity 輸入反應一致，但網頁仍為簡化飛行模型，非完整空氣動力模擬。
