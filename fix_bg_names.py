"""Fix BG/CG/SD references in story CSVs to use actual resource filenames."""
import csv
import os

# Exact mapping: CSV reference -> actual resource filename
BG_RENAME = {
    # Folder paths -> filename only
    'MyRoom/BG_MyRoom_Interior_Day': 'BG_MyRoom_Interior_Day',
    'MyRoom/BG_MyRoom_Interior_Night': 'BG_MyRoom_Interior_Night',
    'MyRoom/BG_MyRoom_Interior_Night_LightOn': 'BG_MyRoom_Interior_Night_LightOn',
    'MyRoom/BG_MyRoom_Bed_Day': 'BG_MyRoom_Bed_Day',
    'MyRoom/BG_MyRoom_Bed_Night': 'BG_MyRoom_Bed_Night',
    'MyRoom/BG_MyRoom_Desk': 'BG_MyRoom_Desk',
    'CampusStreet/BG_Campus_Street1_Day': 'BG_Campus_Street1_Day',
    'CampusStreet/BG_Campus_Street2_Day': 'BG_Campus_Street2_Day',
    'Engineering/BG_Engineering_Classroom': 'BG_Engineering_Classroom',
    'Engineering/BG_Engineering_Corridor': 'BG_Engineering_Corridor',
    'Engineering/BG_Engineering_Corridor_Day': 'BG_Engineering_Corridor',
    'Engineering/BG_Engineering_Front_Day': 'BG_Engineering_Front_Day',
    'Engineering/BG_Engineering_Front_Night': 'BG_Engineering_Front_Night',
    'Engineering/BG_Engineering_StudentLounge': 'BG_Engineering_StudentLounge',
    'StudentCenter/BG_StudentCenter_Front_Day': 'BG_StudentCenter_Front_Day',
    'StudentCenter/BG_StudentCenter_Front_Night': 'BG_StudentCenter_Front_Night',
    'StudentCenter/BG_StudentCenter_Office': 'BG_StudentCenter_Office',
    'StudentCenter/BG_StudentCenter_Office_Day': 'BG_StudentCenter_Office',
    'StudentCenter/BG_StudentCenter_Board': 'BG_StudentCenter_Board',
    'StudentCenter/BG_StudentCenter_Hallway': 'BG_StudentCenter_Hallway',
    'ClubRoom/BG_ClubRoom_Interior_Day': 'BG_ClubRoom_Interior_Day',
    'ClubRoom/BG_ClubRoom_Interior_Day_Cherry': 'BG_ClubRoom_Interior_Day_Cherry',

    # Night variant -> Day (no Night file exists for Campus_Street1)
    'CampusStreet/BG_Campus_Street1_Night': 'BG_Campus_Street1_Day',

    # Legacy names -> actual filenames
    'BG_MyRoom_Day': 'BG_MyRoom_Interior_Day',
    'BG_MyRoom_Night_LightOn': 'BG_MyRoom_Interior_Night_LightOn',
    'BG_Black': 'BG_BlackCut',
    'Black': 'BG_BlackCut',

    # Resources not yet created - strip folder, keep name for future
    'MT/BG_MT_Day': 'BG_MT_Day',
    'MT/BG_MT_Night': 'BG_MT_Night',
    'Library/BG_Library_Day': 'BG_Library_Day',
    # BG_ConvenienceStore_Inside already has no folder prefix, keep as-is
}


def fix_csv_file(fpath):
    with open(fpath, 'r', encoding='utf-8-sig') as f:
        original = f.read()

    lines = []
    current = []
    in_quotes = False
    for ch in original:
        if ch == '"':
            in_quotes = not in_quotes
            current.append(ch)
        elif ch == '\n' and not in_quotes:
            lines.append(''.join(current))
            current = []
        elif ch == '\r' and not in_quotes:
            continue
        else:
            current.append(ch)
    if current:
        lines.append(''.join(current))

    modified = False
    new_lines = []

    for line in lines:
        stripped = line.strip()
        if not stripped or stripped.startswith('#') or stripped.startswith('LineID,'):
            new_lines.append(line)
            continue

        try:
            reader = csv.reader([line])
            cols = list(next(reader))
        except Exception:
            new_lines.append(line)
            continue

        if len(cols) < 4:
            new_lines.append(line)
            continue

        line_type = cols[1].strip() if len(cols) > 1 else ''
        value = cols[3].strip() if len(cols) > 3 else ''
        changed = False

        if line_type == 'BG' and value:
            parts = value.split(':')
            bg_ref = parts[0]
            if bg_ref in BG_RENAME:
                parts[0] = BG_RENAME[bg_ref]
                cols[3] = ':'.join(parts)
                changed = True

        if changed:
            modified = True
            out_parts = []
            for col in cols:
                if ',' in col or '"' in col or '\n' in col:
                    out_parts.append('"' + col.replace('"', '""') + '"')
                else:
                    out_parts.append(col)
            new_lines.append(','.join(out_parts))
        else:
            new_lines.append(line)

    if modified:
        with open(fpath, 'w', encoding='utf-8-sig', newline='\n') as f:
            f.write('\n'.join(new_lines))

    return modified


story_dir = os.path.join(os.path.dirname(__file__),
    'LoveAlgo-unity', 'Assets', 'Resources', 'Story')
files = sorted(f for f in os.listdir(story_dir) if f.endswith('.csv'))

for fname in files:
    fpath = os.path.join(story_dir, fname)
    if fix_csv_file(fpath):
        print(f'Fixed: {fname}')
    else:
        print(f'OK:    {fname}')

print('\nDone.')
