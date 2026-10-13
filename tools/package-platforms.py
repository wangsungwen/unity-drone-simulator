"""Package v0.2.1 Windows and both mobile app variants, validating launch modes."""
from pathlib import Path
import hashlib
import json
import plistlib
import struct
import zipfile

project = Path(__file__).resolve().parents[1]
builds = project / 'Builds'
version = '0.2.1'

def launch_signature(mode):
    data = mode.encode()
    return b'TrainerLaunch\x00\x00\x00' + struct.pack('<I', len(data)) + data

for folder, apk, mode in [('Android','DroneTrainingLab.apk','local'),('AndroidRemote','DroneRemoteController.apk','remote')]:
    with zipfile.ZipFile(builds / folder / apk) as z:
        assert any(launch_signature(mode) in z.read(n) for n in z.namelist() if n.startswith('assets/bin/Data/') and not n.endswith('/')), f'{folder} launch mode'

for folder, mode in [('iOS','local'),('iOSRemote','remote')]:
    root = builds / folder
    with (root/'Info.plist').open('rb') as f:
        info = plistlib.load(f)
    assert info['CFBundleShortVersionString'] == version
    assert info['NSLocalNetworkUsageDescription']
    pbx = (root/'Unity-iPhone.xcodeproj/project.pbxproj').read_text(encoding='utf-8')
    assert 'tw.traininglab.dronesimulator' + ('.remote' if mode=='remote' else '') in pbx
    assert any(launch_signature(mode) in p.read_bytes() for p in (root/'Data').rglob('*') if p.is_file()), f'{folder} launch mode'

specs = {
    'Windows': [('Windows', None)],
    'Android': [('Android', 'DroneTrainingLab.apk'), ('AndroidRemote', 'DroneRemoteController.apk')],
    'iOS-Xcode': [('iOS', None), ('iOSRemote', None)],
}
notes = {
    'Windows': '解壓縮 Windows 資料夾後執行 DroneTrainingLab.exe，請保留全部 DLL 與 _Data。要用手機遙控時選「電腦畫面接收端」，再啟動接收。',
    'Android': '兩個 APK 可同時安裝：\nAndroid/DroneTrainingLab.apk：無人機飛行模擬器，搖桿直接控制手機畫面，免網路。\nAndroidRemote/DroneRemoteController.apk：無人機電腦遙控器，搖桿控制電腦畫面。\n遙控時兩端需同一區域網路，輸入電腦 IPv4 與六位配對碼，電腦允許 UDP 47777。\n最低 Android 8 / ARM64；測試簽署，實機驗收尚待完成。',
    'iOS-Xcode': '兩個 Xcode 專案：iOS 為手機飛行模擬器，iOSRemote 為電腦遙控器，使用不同 Bundle ID。\n在 Mac 解壓，開啟各資料夾內 Unity-iPhone.xcodeproj，設定 Team 後編譯及簽署。\n這不是 IPA，不能直接安裝；最低 iOS 13。尚未在 Mac 編譯或真實 iPhone 驗收。',
}
manifest = []
for platform, entries in specs.items():
    output = builds / f'DroneTrainingLab-v{version}-{platform}.zip'
    with zipfile.ZipFile(output, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for folder, single in entries:
            root = builds / folder
            files = [root/single] if single else root.rglob('*')
            for p in files:
                if p.is_file() and not any('DoNotShip' in part for part in p.parts):
                    z.write(p, folder+'/'+p.relative_to(root).as_posix())
        z.writestr('安裝說明.txt', f'Drone Training Lab v{version}\n'+notes[platform]+'\n')
    with zipfile.ZipFile(output) as z:
        assert z.testzip() is None
    with output.open('rb') as f:
        checksum = hashlib.file_digest(f,'sha256').hexdigest()
    manifest.append({'file':output.name, 'bytes':output.stat().st_size, 'sha256':checksum})
    print('PASS:', output.name, output.stat().st_size, 'bytes', flush=True)
(builds/f'DroneTrainingLab-v{version}-checksums.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
