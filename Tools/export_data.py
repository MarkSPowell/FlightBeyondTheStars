"""Export Flight_Beyond_the_Stars_Systems_Values.xlsx to JSON files for Unity.

Usage: python export_data.py <workbook.xlsx> <output folder>
Reads cached values (open and save the workbook in Excel after editing so formulas are recalculated).
Each table starts at row 4 (header) with data from row 5 until the first blank first-column cell.
"""
import json, os, re, sys
from openpyxl import load_workbook


def key(h):
    # "Base price (cr/unit)" -> "basePrice"
    h = re.sub(r"\(.*?\)", "", str(h)).strip()
    parts = re.split(r"[^A-Za-z0-9]+", h)
    parts = [p for p in parts if p]
    if not parts:
        return None
    return parts[0].lower() + "".join(p[:1].upper() + p[1:] for p in parts[1:])


def table(ws, header_row=4, max_cols=None):
    headers = []
    for c in ws[header_row]:
        if c.value is None:
            break
        headers.append(key(c.value))
    if max_cols:
        headers = headers[:max_cols]
    rows = []
    r = header_row + 1
    while True:
        first = ws.cell(row=r, column=1).value
        if first is None or str(first).strip() == "":
            break
        item = {}
        for i, h in enumerate(headers):
            if not h:
                continue
            v = ws.cell(row=r, column=i + 1).value
            if v is None:
                continue  # Unity's JsonUtility can't read null numbers; missing keys use defaults
            item[h] = v
        rows.append(item)
        r += 1
    return rows


def main(path, out):
    wb = load_workbook(path, data_only=True)
    os.makedirs(out, exist_ok=True)

    def write(name, items):
        with open(os.path.join(out, name + ".json"), "w", encoding="utf-8") as f:
            json.dump({"items": items}, f, indent=2)
        print(f"{name}: {len(items)} rows")

    # Tuning: key/value pairs
    tun = [{"key": r["lever"], "value": float(r["value"])} for r in table(wb["Tuning"])]
    write("Tuning", tun)

    write("Commodities", table(wb["Commodities"]))

    # Race prices: flatten to race + category + multiplier
    ws = wb["RacePrices"]
    cats = [c.value for c in ws[4][1:10]]
    rp = []
    for row in table(ws):
        race = row[key("Race market")]
        for cat in cats:
            rp.append({"race": race, "category": cat, "multiplier": float(row[key(cat)])})
    write("RacePrices", rp)

    write("Hulls", table(wb["Hulls"]))
    write("ShipSystems", table(wb["ShipSystems"]))
    write("Weapons", table(wb["Weapons"]))
    write("Upgrades", table(wb["Upgrades"]))
    if "Events" in wb.sheetnames:
        write("Events", table(wb["Events"]))
    if "Encounters" in wb.sheetnames:
        write("Encounters", table(wb["Encounters"]))
    write("CrewRoles", table(wb["Crew"]))

    # Species: caps as a nested list in role order
    ws = wb["Species"]
    roles = [c.value for c in ws[4][2:10]]
    sp = []
    for row in table(ws):
        if row.get("species") in ("Best cap per role", "Best species per role"):
            continue
        sp.append({
            "name": row["species"],
            "majorOrMinor": row["majorOrMinor"],
            "caps": [int(row[key(r)] or 0) for r in roles],
            "howToGet": row["howToGet"],
            "trait": row["trait"],
        })
    write("Species", sp)

    write("Outposts", table(wb["Outposts"]))
    write("Colonies", table(wb["Colonies"]))
    write("DeadlineStages", table(wb["Deadline"]))


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
