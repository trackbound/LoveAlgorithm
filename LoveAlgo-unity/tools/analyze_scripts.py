#!/usr/bin/env python3
"""
Script analysis tool
- Checks CSV files in Assets/Resources/Story for character/emote references and verifies against manifest
- Scans C# for deprecated patterns and TODO/FIXME comments
- Produces a report at tools/script_analysis_report.txt
"""
import os, re, json, csv
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
MANIFEST = ROOT / 'Assets' / 'Data' / 'characters_emotes.json'
CSV_DIR = ROOT / 'Assets' / 'Resources' / 'Story'
CS_DIR = ROOT / 'Assets' / 'Scripts'
REPORT = ROOT / 'tools' / 'script_analysis_report.txt'

# Patterns
char_ref_pattern = re.compile(r"C:Enter:([A-Za-z0-9_]+)(?::([A-Za-z0-9_]+))?")
option_effect_char = re.compile(r"Love:([A-Za-z0-9_]+):")
if_love_pattern = re.compile(r"If:Love:([A-Za-z0-9_]+)")
# Deprecated patterns to flag
deprecated_patterns = [
    (re.compile(r"StartCoroutine\s*\(|yield\s+return"), 'Coroutine usage (avoid, use UniTask)'),
    (re.compile(r"\.OnComplete\s*\(|OnComplete\s*\(|OnFinish\s*\(|callback\s*=>"), 'Callback-based tween/event usage (prefer await/UniTask)'),
    (re.compile(r"Debug\.Log\s*\(|Console\.WriteLine\s*\("), 'Logging - consider structured logging or remove in production')
]
# TODO / FIXME
todo_pattern = re.compile(r"\b(TODO|FIXME)\b")


def load_manifest():
    if not MANIFEST.exists():
        return {}
    with open(MANIFEST, 'r', encoding='utf-8') as f:
        return json.load(f)


def scan_csvs(manifest):
    missing_emotes = []
    unknown_chars = set()
    csv_files = list(CSV_DIR.glob('**/*.csv'))
    for csvfile in csv_files:
        try:
            with open(csvfile, newline='', encoding='utf-8') as f:
                reader = csv.reader(f)
                for row in reader:
                    if len(row) < 5: continue
                    Type = row[1].strip()
                    Value = row[3].strip()
                    if Type == 'Char':
                        m = char_ref_pattern.search(Value)
                        if m:
                            char = m.group(1)
                            emote = m.group(2) or 'Default'
                            # check char
                            char_entry = next((c for c in manifest.get('characters', []) if c.get('id') == char), None)
                            if not char_entry:
                                unknown_chars.add((char, str(csvfile)))
                            else:
                                emotes = [e.get('key') for e in char_entry.get('emotes', [])]
                                if emote not in emotes:
                                    missing_emotes.append((str(csvfile), char, emote))
                    # Options / If patterns
                    for m in option_effect_char.findall(Value):
                        if not any(c.get('id') == m for c in manifest.get('characters', [])):
                            unknown_chars.add((m, str(csvfile)))
                    for m in if_love_pattern.findall(Value):
                        if not any(c.get('id') == m for c in manifest.get('characters', [])):
                            unknown_chars.add((m, str(csvfile)))
        except Exception as e:
            print('CSV parse error', csvfile, e)
    return missing_emotes, unknown_chars


def scan_csharp():
    issues = []
    todos = []
    for csfile in CS_DIR.rglob('*.cs'):
        text = csfile.read_text(encoding='utf-8')
        for pat, label in deprecated_patterns:
            if pat.search(text):
                issues.append((str(csfile), label))
        for m in todo_pattern.findall(text):
            todos.append((str(csfile), m))
    return issues, todos


def main():
    manifest = load_manifest()
    with open(REPORT, 'w', encoding='utf-8') as rep:
        rep.write('Script Analysis Report\n')
        rep.write('Generated: ' + __import__('datetime').datetime.now().isoformat() + '\n\n')

        rep.write('Manifest summary:\n')
        rep.write(f"Characters in manifest: {len(manifest.get('characters', []))}\n\n")

        rep.write('Scanning CSVs for char/emote refs...\n')
        missing_emotes, unknown_chars = scan_csvs(manifest)
        if missing_emotes:
            rep.write('Missing emotes referenced in CSVs:\n')
            for f,c,e in missing_emotes:
                rep.write(f" - {f} references {c}/{e} (missing)\n")
        else:
            rep.write(' - No missing emote references found.\n')
        rep.write('\n')

        if unknown_chars:
            rep.write('Unknown character IDs used in CSVs:\n')
            for ch, src in sorted(unknown_chars):
                rep.write(f" - {ch} used in {src}\n")
        else:
            rep.write(' - No unknown character IDs used.\n')
        rep.write('\n')

        rep.write('Scanning C# for deprecated patterns...\n')
        issues, todos = scan_csharp()
        if issues:
            rep.write('Deprecated patterns found:\n')
            for file, label in sorted(issues):
                rep.write(f" - {file}: {label}\n")
        else:
            rep.write(' - No deprecated patterns found.\n')
        rep.write('\n')

        if todos:
            rep.write('TODO / FIXME occurrences:\n')
            for file, note in todos:
                rep.write(f" - {file}: {note}\n")
        else:
            rep.write(' - No TODO/FIXME found.\n')
        rep.write('\n')

    print('Analysis complete. Report written to', REPORT)

if __name__ == '__main__':
    main()
