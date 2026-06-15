import sys, io, sqlite3, json
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")

db = sqlite3.connect(r"d:\TRIO2026\tools\TestDataSeeder\test_data.db")
db.row_factory = sqlite3.Row

print("=" * 70)
print("test_data.db 資料完整度檢查")
print("=" * 70)

# --- TestRecord 欄位填充率 ---
print("\n=== TestRecord (38 筆) — 各欄位填充率 ===")
cols = [desc[0] for desc in db.execute("PRAGMA table_info(TestRecord)").fetchall()]
# cols from pragma returns tuple (cid, name, type, notnull, dflt, pk)
cols = [row[1] for row in db.execute("PRAGMA table_info(TestRecord)").fetchall()]

for col in cols:
    total = db.execute("SELECT COUNT(*) FROM TestRecord").fetchone()[0]
    filled = db.execute(
        "SELECT COUNT(*) FROM TestRecord WHERE {} IS NOT NULL AND {} != ''".format(col, col)
    ).fetchone()[0]
    pct = (filled / total * 100) if total > 0 else 0
    marker = "✅" if pct > 50 else ("⚠️" if pct > 0 else "❌")
    print("  {} {:30s} {:3d}/{} ({:5.1f}%)".format(marker, col, filled, total, pct))

# --- SampleResult 欄位填充率 ---
print("\n=== SampleResult (231 筆) — 各欄位填充率 ===")
cols2 = [row[1] for row in db.execute("PRAGMA table_info(SampleResult)").fetchall()]
for col in cols2:
    total = db.execute("SELECT COUNT(*) FROM SampleResult").fetchone()[0]
    filled = db.execute(
        "SELECT COUNT(*) FROM SampleResult WHERE {} IS NOT NULL AND {} != ''".format(col, col)
    ).fetchone()[0]
    pct = (filled / total * 100) if total > 0 else 0
    marker = "✅" if pct > 50 else ("⚠️" if pct > 0 else "❌")
    print("  {} {:30s} {:3d}/{} ({:5.1f}%)".format(marker, col, filled, total, pct))

# --- RawMeasurement ---
print("\n=== RawMeasurement (38 筆) — 各欄位填充率 ===")
cols3 = [row[1] for row in db.execute("PRAGMA table_info(RawMeasurement)").fetchall()]
for col in cols3:
    total = db.execute("SELECT COUNT(*) FROM RawMeasurement").fetchone()[0]
    filled = db.execute(
        "SELECT COUNT(*) FROM RawMeasurement WHERE {} IS NOT NULL AND {} != ''".format(col, col)
    ).fetchone()[0]
    pct = (filled / total * 100) if total > 0 else 0
    marker = "✅" if pct > 50 else ("⚠️" if pct > 0 else "❌")
    print("  {} {:30s} {:3d}/{} ({:5.1f}%)".format(marker, col, filled, total, pct))

# --- Data Page 需要顯示的欄位 vs 實際有值 ---
print("\n" + "=" * 70)
print("Data Page 需要的欄位 vs 實際狀態")
print("=" * 70)

needed = [
    ("TestRecord", "OperatorUserId",          "清單卡片: 操作員 (Admin可見)"),
    ("TestRecord", "OperatorUsername",         "清單卡片: 操作員名稱"),
    ("TestRecord", "ReportType",              "清單卡片: 報告類型色碼"),
    ("TestRecord", "ExperimentDate",          "清單卡片: 實驗日期"),
    ("TestRecord", "SampleCount",             "清單卡片: 樣本數"),
    ("TestRecord", "Status",                  "清單卡片: 狀態"),
    ("TestRecord", "ExtractionProgram",       "清單卡片: 萃取程式"),
    ("TestRecord", "EndTime",                 "清單卡片: 完成時間"),
    ("TestRecord", "StartTime",               "詳情頁: 開始時間"),
    ("TestRecord", "ExtractionKitLotNo",      "詳情頁: Kit 批號"),
    ("TestRecord", "ElutionVolume",           "詳情頁: 洗脫體積"),
    ("TestRecord", "PcrPlateId",              "詳情頁: PCR Plate ID"),
    ("TestRecord", "PcrTotalNucleicAcidInput","詳情頁: PCR 核酸輸入"),
    ("TestRecord", "IntelliPlexKit1Name",     "詳情頁: Kit1 名稱"),
    ("TestRecord", "IntelliPlexKit1LotNo",    "詳情頁: Kit1 批號"),
    ("TestRecord", "FunctionModulesSelected", "詳情頁: 功能模組 (Custom)"),
    ("TestRecord", "CustomPcrSetupJson",      "詳情頁: PCR 設定 (Custom)"),
    ("TestRecord", "RunId",                   "詳情頁: RunId"),
    ("SampleResult", "SamplePosition",        "詳情頁表格: 孔位"),
    ("SampleResult", "SampleType",            "詳情頁表格: 類型"),
    ("SampleResult", "Concentration",         "詳情頁表格: 濃度"),
    ("SampleResult", "ConcentrationDisplay",  "詳情頁表格: 濃度顯示"),
    ("SampleResult", "UtilizedElutedVolume",  "詳情頁表格: 使用體積"),
    ("SampleResult", "PcrWellKit1",           "詳情頁表格: PCR Well"),
    ("SampleResult", "SampleId",              "詳情頁表格: Sample ID"),
    ("SampleResult", "ElutionTubeId",         "詳情頁表格: Tube ID"),
    ("RawMeasurement", "S1AdValue",           "詳情頁: S1 A/D"),
    ("RawMeasurement", "S2AdValue",           "詳情頁: S2 A/D"),
]

for table, col, usage in needed:
    total = db.execute("SELECT COUNT(*) FROM {}".format(table)).fetchone()[0]
    filled = db.execute(
        "SELECT COUNT(*) FROM {} WHERE {} IS NOT NULL AND {} != ''".format(table, col, col)
    ).fetchone()[0]
    pct = (filled / total * 100) if total > 0 else 0
    status = "✅ 有值" if pct > 50 else ("⚠️ 部分" if pct > 0 else "❌ 全空")
    print("  {} {:5.0f}% {:30s} {}".format(status, pct, col, usage))

db.close()
