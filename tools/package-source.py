"""Validate asset references and make a portable source archive (no Library/build cache)."""
import hashlib
import json
import pathlib
import re
import uuid
import zipfile

project = pathlib.Path(__file__).resolve().parents[1]
assets = project / 'Assets'
for path in sorted(assets.rglob('*')):
    if path.name.endswith('.meta'):
        continue
    meta = path.with_name(path.name + '.meta')
    if meta.exists():
        continue
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'drone-training-lab/' + path.relative_to(project).as_posix()).hex
    content = f'fileFormatVersion: 2\nguid: {guid}\n'
    if path.is_dir():
        content += 'folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
    elif path.suffix == '.cs':
        content += 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n'
    else:
        # Let Unity select its importer and write format-specific defaults on first import.
        content += ''
    meta.write_text(content, encoding='utf-8')

guids = {}
for meta in assets.rglob('*.meta'):
    found = re.search(r'^guid: ([a-f0-9]{32})$', meta.read_text(encoding='utf-8-sig'), re.M)
    if not found:
        raise RuntimeError(f'Missing GUID: {meta}')
    if found[1] in guids:
        raise RuntimeError(f'Duplicate GUID: {meta}')
    guids[found[1]] = meta
scene = (assets / 'DroneTrainer/Scenes/Trainer.unity').read_text(encoding='utf-8-sig')
for guid in re.findall(r'guid: ([a-f0-9]{32})', scene):
    if guid not in guids:
        raise RuntimeError(f'Unresolved scene GUID: {guid}')
json.loads((project / 'Packages/manifest.json').read_text(encoding='utf-8-sig'))
font = assets / 'DroneTrainer/Resources/TrainerFont.otf'
if font.read_bytes()[:4] != b'OTTO':
    raise RuntimeError('Invalid OpenType font')
license_file = assets / 'DroneTrainer/Resources/FONT-LICENSE.txt'
assert 'SIL OPEN FONT LICENSE' in license_file.read_text(encoding='utf-8')

excluded = {'Library', 'Temp', 'Obj', 'Logs', 'UserSettings', 'Builds', 'TestResults', '.vs', '.git'}
archive = project.parent / 'unity-drone-simulator-source.zip'
with zipfile.ZipFile(archive, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as bundle:
    for file in sorted(project.rglob('*')):
        rel = file.relative_to(project)
        if file.is_file() and not any(part in excluded for part in rel.parts) and file.suffix not in ('.csproj', '.sln'):
            bundle.write(file, 'unity-drone-simulator/' + rel.as_posix())
checksum = hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix('.zip.sha256').write_text(f'{checksum}  {archive.name}\n', encoding='ascii')
print(f'PASS: {len(guids)} asset GUIDs, scene references, manifest, OpenType font and license')
print(f'Archive: {archive} ({archive.stat().st_size:,} bytes)')
print(f'SHA256: {checksum}')
