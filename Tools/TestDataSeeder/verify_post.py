import sys, io, sqlite3
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8")
db = sqlite3.connect(r"d:\TRIO2026\tools\TestDataSeeder\test_data.db")
db.row_factory = sqlite3.Row

print("=== 操作員分配 ===")
for r in db.execute("SELECT OperatorUsername, COUNT(*) as cnt FROM TestRecord GROUP BY OperatorUsername"):
    print("  {}: {} 筆".format(r["OperatorUsername"], r["cnt"]))

print()
print("=== 狀態分布 ===")
for r in db.execute("SELECT Status, COUNT(*) as cnt FROM TestRecord GROUP BY Status"):
    print("  {}: {} 筆".format(r["Status"], r["cnt"]))

print()
print("=== Error/Aborted 詳情 ===")
for r in db.execute("SELECT RunId, Status, ErrorCode, ErrorMessage, OperatorUsername FROM TestRecord WHERE Status != 'Completed'"):
    print("  {} | {} | {} | {} | by {}".format(
        r["RunId"], r["Status"], r["ErrorCode"], r["ErrorMessage"], r["OperatorUsername"]))

db.close()
