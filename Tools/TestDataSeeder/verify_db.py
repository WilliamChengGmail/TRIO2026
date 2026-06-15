import sys, io, sqlite3, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

db = sqlite3.connect(r"d:\TRIO2026\tools\TestDataSeeder\test_data.db")
db.row_factory = sqlite3.Row

print("=== TestRecord 統計 ===")
for row in db.execute("SELECT ReportType, COUNT(*) as cnt FROM TestRecord GROUP BY ReportType"):
    print("  {}: {} 筆".format(row["ReportType"], row["cnt"]))

print()
print("=== SampleResult 統計 ===")
for row in db.execute("SELECT SampleType, COUNT(*) as cnt FROM SampleResult GROUP BY SampleType"):
    print("  {}: {} 筆".format(row["SampleType"], row["cnt"]))

print()
print("=== 驗證: 20260317_135504 (IntelliPlex) ===")
tr = db.execute('SELECT * FROM TestRecord WHERE RunId="20260317_135504"').fetchone()
for k in ["ReportType", "ExperimentDate", "ExtractionProgram", "ExtractionKitLotNo",
           "PcrTotalNucleicAcidInput", "IntelliPlexKit1Name", "SampleCount"]:
    print("  {}: {}".format(k, tr[k]))

rm = db.execute("SELECT * FROM RawMeasurement WHERE TestRecordId=?", (tr["Id"],)).fetchone()
print("  S1AdValue: {}".format(rm["S1AdValue"]))
print("  S2AdValue: {}".format(rm["S2AdValue"]))

print()
print("  Samples:")
for s in db.execute("SELECT * FROM SampleResult WHERE TestRecordId=? ORDER BY Id", (tr["Id"],)):
    print("    Pos={} Type={} Conc={} Vol={} Kit1={} Kit2={}".format(
        s["SamplePosition"], s["SampleType"], s["Concentration"],
        s["UtilizedElutedVolume"], s["PcrWellKit1"], s["PcrWellKit2"]))

print()
print("=== 驗證: 20260116_141721 (Custom, 3 Rxn) ===")
tr2 = db.execute('SELECT * FROM TestRecord WHERE RunId="20260116_141721"').fetchone()
print("  FunctionModulesSelected:", tr2["FunctionModulesSelected"])
pcr = json.loads(tr2["CustomPcrSetupJson"])
for rxn, cfg in pcr.items():
    if cfg:
        print("  {}: NA={}ng, SmpVol={}uL, MMVol={}uL".format(
            rxn, cfg["NucleicAcid"], cfg["SampleVol"], cfg["MasterMixVol"]))
    else:
        print("  {}: null".format(rxn))

print()
print("  First 3 samples:")
for s in db.execute("SELECT * FROM SampleResult WHERE TestRecordId=? ORDER BY Id LIMIT 3", (tr2["Id"],)):
    print("    Pos={} ConcDisp={} Rxn1={} Rxn2={} Rxn3={}".format(
        s["SamplePosition"], s["ConcentrationDisplay"],
        s["PcrWellKit1"], s["PcrWellKit2"], s["PcrWellRxn3"]))

db.close()
