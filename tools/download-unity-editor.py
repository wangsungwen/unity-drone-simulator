"""Download the pinned official Unity editor installer; does not run it."""
import pathlib
import time
import urllib.request

URL = 'https://download.unity3d.com/download_unity/7670c08855a9/Windows64EditorInstaller/UnitySetup64-2022.3.62f2.exe'
root = pathlib.Path(__file__).resolve().parents[2] / '.build_artifacts' / 'unity-tools'
root.mkdir(parents=True, exist_ok=True)
target = root / 'UnitySetup64-2022.3.62f2.exe'
with urllib.request.urlopen(URL, timeout=120) as response:
    total = int(response.headers['Content-Length'])
    if target.exists() and target.stat().st_size == total:
        print('Installer already downloaded:', target, flush=True)
    else:
        partial = target.with_suffix('.partial')
        count = 0
        last = time.monotonic()
        with partial.open('wb') as output:
            while True:
                chunk = response.read(4 * 1024 * 1024)
                if not chunk:
                    break
                output.write(chunk)
                count += len(chunk)
                if time.monotonic() - last >= 10:
                    print(f'{count / total:.1%} ({count // 1048576}/{total // 1048576} MiB)', flush=True)
                    last = time.monotonic()
        if count != total:
            raise RuntimeError('Incomplete download')
        partial.replace(target)
        print('Downloaded:', target, flush=True)
