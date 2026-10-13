"""Package verified build folders and the source project for delivery."""
import concurrent.futures
import hashlib
import json
import pathlib
import subprocess
import sys
import zipfile

project = pathlib.Path(__file__).resolve().parents[1]
destination = project.parent
subprocess.run([sys.executable, str(project / 'tools/package-source.py')], check=True)

def archive_platform(platform):
    folder = project / 'Builds' / platform
    if not folder.is_dir():
        raise RuntimeError('Missing build: ' + platform)
    target = destination / ('unity-drone-simulator-' + platform.lower() + '.zip')
    with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for file in sorted(folder.rglob('*')):
            if file.is_file():
                archive.write(file, 'DroneTrainingLab-' + platform + '/' + file.relative_to(folder).as_posix())
    with zipfile.ZipFile(target) as archive:
        if archive.testzip() is not None:
            raise RuntimeError('ZIP integrity failed: ' + str(target))
    print('Packaged:', target.name, flush=True)
    return target

with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
    archives = list(pool.map(archive_platform, ['Windows', 'iOS']))
archives += [destination / 'unity-drone-simulator-source.zip', project / 'Builds/Android/DroneTrainingLab.apk']
manifest = []
for file in archives:
    sha = hashlib.sha256()
    with file.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            sha.update(block)
    manifest.append({'file': file.relative_to(destination).as_posix(), 'bytes': file.stat().st_size, 'sha256': sha.hexdigest()})
(destination / 'unity-drone-simulator-release.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
print(json.dumps(manifest, indent=2), flush=True)
