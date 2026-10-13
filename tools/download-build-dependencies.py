"""Download the official Android support module and matching NDK for Unity 2022.3."""
import concurrent.futures
import pathlib
import time
import urllib.request

root = pathlib.Path(__file__).resolve().parents[2] / '.build_artifacts' / 'unity-tools'
root.mkdir(parents=True, exist_ok=True)
downloads = {
    'UnitySetup-Android-Support-for-Editor-2022.3.62f2.exe':
        'https://download.unity3d.com/download_unity/7670c08855a9/TargetSupportInstaller/UnitySetup-Android-Support-for-Editor-2022.3.62f2.exe',
    'android-ndk-r23b-windows.zip': 'https://dl.google.com/android/repository/android-ndk-r23b-windows.zip',
}

def download(item):
    name, url = item
    target = root / name
    with urllib.request.urlopen(url, timeout=120) as response:
        total = int(response.headers['Content-Length'])
        if target.exists() and target.stat().st_size == total:
            print('Already downloaded:', name, flush=True)
            return
        partial = target.with_suffix('.partial')
        done = 0
        last = time.monotonic()
        with partial.open('wb') as output:
            while True:
                chunk = response.read(4 * 1024 * 1024)
                if not chunk:
                    break
                output.write(chunk)
                done += len(chunk)
                if time.monotonic() - last > 15:
                    print(f'{name}: {done / total:.0%}', flush=True)
                    last = time.monotonic()
        if done != total:
            raise RuntimeError('Incomplete download: ' + name)
        partial.replace(target)
        print('Downloaded:', name, flush=True)

with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    list(pool.map(download, downloads.items()))
