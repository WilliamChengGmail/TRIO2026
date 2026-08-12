import os, sys, shutil, subprocess, re

sys.stdout.reconfigure(encoding='utf-8')

deploy_dir = r"F:\TRIO2026_Deploy"
project_root = r"D:\TRIO2026"

print("==========================================")
print("  TRIO2026 準備獨立部署環境 (含 Tools)    ")
print(f"  目標資料夾: {deploy_dir}")
print("==========================================")

if os.path.exists(deploy_dir):
    print("[1/6] 清除舊目錄...")
    # try to remove, if it fails because of permissions, warn
    try:
        shutil.rmtree(deploy_dir)
    except Exception as e:
        print(f"  清除失敗，請手動刪除: {e}")

os.makedirs(deploy_dir, exist_ok=True)

print("[2/6] 發佈 TRIO2026.App (Self-Contained)...")
app_proj = os.path.join(project_root, r"src\TRIO2026.App\TRIO2026.App.csproj")
subprocess.run(["dotnet", "publish", app_proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-o", os.path.join(deploy_dir, "App")], stdout=subprocess.DEVNULL, check=True)

print("[3/6] 發佈 PrivilegedService...")
svc_proj = os.path.join(project_root, r"src\TRIO2026.PrivilegedService\TRIO2026.PrivilegedService.csproj")
if os.path.exists(svc_proj):
    subprocess.run(["dotnet", "publish", svc_proj, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-o", os.path.join(deploy_dir, "Service")], stdout=subprocess.DEVNULL)

print("[4/6] 複製當前開發環境資料庫...")
db_target = os.path.join(deploy_dir, r"App\Database")
os.makedirs(db_target, exist_ok=True)
db_source = os.path.join(project_root, "Database")
for f in os.listdir(db_source):
    if f.endswith(".db"):
        shutil.copy2(os.path.join(db_source, f), db_target)

print("[5/6] 發佈所有 Tools (Self-Contained)...")
tools_out = os.path.join(deploy_dir, "Tools")
os.makedirs(tools_out, exist_ok=True)
tools_src = os.path.join(project_root, "Tools")

for root_dir, dirs, files in os.walk(tools_src):
    if r"\temp" in root_dir or "bin" in root_dir or "obj" in root_dir: continue
    for f in files:
        if f.endswith(".csproj"):
            proj_path = os.path.join(root_dir, f)
            tool_name = f.replace(".csproj", "")
            print(f"  - 發佈 {tool_name} ...")
            subprocess.run(["dotnet", "publish", proj_path, "-c", "Release", "-r", "win-x64", "--self-contained", "true", "-p:PublishSingleFile=true", "-o", os.path.join(tools_out, f"{tool_name}_bin")], stdout=subprocess.DEVNULL)

print("[6/6] 轉換並複製 .bat 執行腳本...")
for f in os.listdir(tools_src):
    if f.endswith(".bat"):
        src_bat = os.path.join(tools_src, f)
        dst_bat = os.path.join(tools_out, f)
        with open(src_bat, "r", encoding="utf-8") as file:
            content = file.read()
        
        # Replace `dotnet run --project "%~dp0ToolName"`
        content = re.sub(r'dotnet run --project\s+"?%~dp0([A-Za-z0-9_]+)"?(?:\s+--)?', r'"%~dp0\1_bin\\\1.exe"', content)
        # Replace `dotnet run -c Release --project UsbConfigTool\UsbConfigTool.csproj`
        content = re.sub(r'dotnet run -c Release --project\s+([A-Za-z0-9_]+)\\[A-Za-z0-9_]+\.csproj', r'"%~dp0\1_bin\\\1.exe"', content)
        
        with open(dst_bat, "w", encoding="utf-8-sig") as file:
            file.write(content)

start_bat = os.path.join(deploy_dir, "啟動App.bat")
with open(start_bat, "w", encoding="utf-8-sig") as file:
    file.write('@echo off\ncd /d "%~dp0"\nstart "" "%~dp0App\\TRIO2026.App.exe"')

print("==========================================")
print("  部署準備完成！")
print("==========================================")
