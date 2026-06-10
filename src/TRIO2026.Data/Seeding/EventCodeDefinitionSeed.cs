using TRIO2026.Core.Entities;

namespace TRIO2026.Data.Seeding;

/// <summary>
/// 事件定義種子資料 — 預定義的系統事件代碼
/// 
/// 命名規則：
///   INF-XNNN = Info（資訊事件）
///   WRN-XNNN = Warning（警告事件）
///   ERR-XNNN = Error / Fatal（錯誤/致命）
///   X = 分類碼（1=System, 2=Auth, 3=UV, 4=Hardware, 5=Config, 6=Navigation）
/// 
/// 新增步驟：
///   1. 在此檔案新增一筆 EventCodeDefinition
///   2. 在 ErrorCodes.cs 新增對應常數
///   3. 在 LocalizedStringSeed.cs 新增多語系字串（若有 UserMessageKey）
///   4. 執行 DbInitializer 同步至 DB
/// 
/// 製作者: Office of William
/// </summary>
public static class EventCodeDefinitionSeed
{
    public static List<EventCodeDefinition> GetSeedData()
    {
        return new List<EventCodeDefinition>
        {
            // ══════════════════════════════════
            // 1xxx — System 系統級
            // ══════════════════════════════════
            new()
            {
                Id = 1, Code = "ERR-1001", Category = "System", Severity = "Fatal",
                Title = "Unhandled Exception",
                Description = "應用程式發生未捕獲的例外，可能導致功能異常",
                Resolution = "重啟應用程式。若持續發生，請提供 Error ID 給技術支援",
                UserMessageKey = "Error.ERR-1001",
                UserMessageFallback = "An unexpected error occurred. Please restart the application."
            },
            new()
            {
                Id = 2, Code = "ERR-1002", Category = "System", Severity = "Error",
                Title = "Database Connection Failure",
                Description = "無法連線至 SQLite 資料庫檔案",
                Resolution = "確認資料庫檔案未被鎖定或損壞，重啟應用程式",
                UserMessageKey = "Error.ERR-1002",
                UserMessageFallback = "Database connection failed. Please restart the application."
            },
            new()
            {
                Id = 3, Code = "WRN-1003", Category = "System", Severity = "Warning",
                Title = "EventLog Write Failure",
                Description = "事件日誌無法寫入 DB，已降級至 Dead Letter 檔案",
                Resolution = "檢查磁碟空間與 system_event.db 檔案狀態",
                UserMessageKey = "Error.WRN-1003",
                UserMessageFallback = "Log write failed. The system will continue operating."
            },
            new()
            {
                Id = 4, Code = "INF-1004", Category = "System", Severity = "Info",
                Title = "Application Startup",
                Description = "應用程式正常啟動",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 5, Code = "INF-1005", Category = "System", Severity = "Info",
                Title = "Application Shutdown",
                Description = "應用程式正常關閉",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 6, Code = "WRN-1006", Category = "System", Severity = "Warning",
                Title = "Abnormal Shutdown Detected",
                Description = "偵測到上次非正常關閉（heartbeat 檔案殘留），可能因強制終止、系統崩潰或斷電",
                Resolution = "檢查 Logs/crash-logs/ 是否有 fallback 日誌；確認系統穩定性",
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 2xxx — Auth 認證相關
            // ══════════════════════════════════
            new()
            {
                Id = 10, Code = "WRN-2001", Category = "Auth", Severity = "Warning",
                Title = "Login Failed",
                Description = "使用者登入失敗（密碼錯誤或帳號不存在）",
                Resolution = "確認帳號密碼正確",
                UserMessageKey = "Error.WRN-2001",
                UserMessageFallback = "Login failed. Please check your credentials."
            },
            new()
            {
                Id = 11, Code = "INF-2002", Category = "Auth", Severity = "Info",
                Title = "Login Success",
                Description = "使用者成功登入",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 12, Code = "INF-2003", Category = "Auth", Severity = "Info",
                Title = "User Logout",
                Description = "使用者登出",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 3xxx — UV 照射相關
            // ══════════════════════════════════
            new()
            {
                Id = 20, Code = "INF-3001", Category = "UV", Severity = "Info",
                Title = "UV Start",
                Description = "UV 照射啟動",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 21, Code = "WRN-3002", Category = "UV", Severity = "Warning",
                Title = "UV Stop",
                Description = "UV 照射手動停止",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 22, Code = "INF-3003", Category = "UV", Severity = "Info",
                Title = "UV Complete",
                Description = "UV 照射倒數完成",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 23, Code = "ERR-3004", Category = "UV", Severity = "Error",
                Title = "UV Door Interrupted",
                Description = "UV 照射期間門板被開啟，照射暫停",
                Resolution = "關閉門板後照射將自動恢復",
                UserMessageKey = "Error.ERR-3004",
                UserMessageFallback = "Door opened during UV operation. Close the door to resume."
            },
            new()
            {
                Id = 24, Code = "ERR-3005", Category = "UV", Severity = "Error",
                Title = "UV Lamp Failure",
                Description = "UV 燈管啟動失敗",
                Resolution = "檢查 UV 燈管硬體連線，若持續失敗請聯繫維護團隊",
                UserMessageKey = "Error.ERR-3005",
                UserMessageFallback = "UV lamp failed to start. Please contact maintenance."
            },
            new()
            {
                Id = 25, Code = "ERR-3006", Category = "UV", Severity = "Error",
                Title = "UV Config Load Failure",
                Description = "UV 時間選項從資料庫載入失敗",
                Resolution = "執行 DbInitializer 重新初始化資料庫",
                UserMessageKey = "Error.ERR-3006",
                UserMessageFallback = "Failed to load UV configuration."
            },

            // ══════════════════════════════════
            // 4xxx — Hardware 硬體相關
            // ══════════════════════════════════
            new()
            {
                Id = 30, Code = "ERR-4001", Category = "Hardware", Severity = "Error",
                Title = "Hardware Communication Failure",
                Description = "與硬體裝置通訊失敗",
                Resolution = "檢查硬體連線與通訊埠設定",
                UserMessageKey = "Error.ERR-4001",
                UserMessageFallback = "Hardware communication error. Check connections."
            },

            // ══════════════════════════════════
            // 5xxx — Config 設定相關
            // ══════════════════════════════════
            new()
            {
                Id = 40, Code = "WRN-5001", Category = "Config", Severity = "Warning",
                Title = "Config Load Failure",
                Description = "系統設定載入失敗，使用預設值",
                Resolution = "檢查 system_config.db 是否正常",
                UserMessageKey = "Error.WRN-5001",
                UserMessageFallback = "Configuration load failed. Using defaults."
            },

            // ══════════════════════════════════
            // 6xxx — Navigation 導航相關
            // ══════════════════════════════════
            new()
            {
                Id = 50, Code = "INF-6001", Category = "Navigation", Severity = "Info",
                Title = "Page Navigation",
                Description = "頁面導航事件",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 2xxx — Auth 擴充
            // ══════════════════════════════════
            new()
            {
                Id = 51, Code = "INF-2004", Category = "Auth", Severity = "Info",
                Title = "Service Mode Login",
                Description = "使用者透過身分驗證進入 Service Mode",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 52, Code = "INF-2005", Category = "Auth", Severity = "Info",
                Title = "Exit Service Mode",
                Description = "使用者退出 Service Mode",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 53, Code = "INF-2006", Category = "Auth", Severity = "Info",
                Title = "Force Password Change",
                Description = "使用者完成強制密碼變更",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 54, Code = "INF-2007", Category = "Auth", Severity = "Info",
                Title = "Password Changed",
                Description = "使用者自主變更密碼",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 55, Code = "WRN-2008", Category = "Auth", Severity = "Warning",
                Title = "Password Change Failed",
                Description = "密碼變更失敗（原密碼錯誤或不符原則）",
                Resolution = "確認原密碼正確，新密碼符合密碼原則",
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 7xxx — UI / Interaction
            // ══════════════════════════════════
            new()
            {
                Id = 60, Code = "INF-7001", Category = "UI", Severity = "Info",
                Title = "Button Click",
                Description = "使用者點擊按鈕（稽核追蹤）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 61, Code = "INF-7002", Category = "UI", Severity = "Info",
                Title = "Menu Action",
                Description = "使用者操作選單（開啟/關閉）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 62, Code = "INF-7003", Category = "UI", Severity = "Info",
                Title = "User Input",
                Description = "使用者輸入欄位內容（稽核追蹤）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 8xxx — Account Management
            // ══════════════════════════════════
            new()
            {
                Id = 70, Code = "INF-8001", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Created",
                Description = "Admin 新增使用者帳號",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 71, Code = "INF-8002", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Deleted",
                Description = "Admin 刪除使用者帳號（假刪除）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 72, Code = "INF-8003", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Disabled",
                Description = "Admin 停用使用者帳號",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 73, Code = "INF-8004", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Enabled",
                Description = "Admin 啟用使用者帳號",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 74, Code = "INF-8005", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Locked",
                Description = "Admin 手動鎖定使用者帳號",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 75, Code = "INF-8006", Category = "AccountMgmt", Severity = "Info",
                Title = "Account Unlocked",
                Description = "Admin 手動解鎖使用者帳號",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 76, Code = "INF-8007", Category = "AccountMgmt", Severity = "Info",
                Title = "Password Reset",
                Description = "Admin 重設使用者密碼",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 9xxx — Guest / Access Control
            // ══════════════════════════════════
            new()
            {
                Id = 77, Code = "INF-9001", Category = "Guest", Severity = "Info",
                Title = "Guest Login Success",
                Description = "Guest 帳號免密碼登入成功",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 78, Code = "WRN-9002", Category = "Guest", Severity = "Warning",
                Title = "Guest Login Blocked",
                Description = "Guest 登入被阻擋（功能停用或帳號不存在）",
                Resolution = "檢查 SystemSetting guest_login_enabled 設定",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 79, Code = "WRN-9003", Category = "Guest", Severity = "Warning",
                Title = "Guest Navigation Blocked",
                Description = "Guest 帳號嘗試存取受限頁面（UV/Setting/AccountMgmt）",
                Resolution = "正常安全行為，無需處理",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 80, Code = "INF-9004", Category = "Guest", Severity = "Info",
                Title = "Guest Restriction Applied",
                Description = "Guest 功能限制已套用（密碼框停用/功能按鈕停用）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 81, Code = "WRN-9005", Category = "Guest", Severity = "Warning",
                Title = "System Account Guard",
                Description = "嘗試對系統帳號（guest/local_operator）執行受保護操作",
                Resolution = "系統帳號不可修改或刪除",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 82, Code = "INF-9006", Category = "Session", Severity = "Info",
                Title = "Session Locked",
                Description = "使用者閒置超時，系統自動鎖定畫面",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 83, Code = "INF-9007", Category = "Session", Severity = "Info",
                Title = "Session Unlocked",
                Description = "使用者輸入正確密碼，成功解鎖畫面",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 84, Code = "WRN-9008", Category = "Session", Severity = "Warning",
                Title = "Lock Screen - Invalid Password",
                Description = "鎖定畫面密碼驗證失敗",
                Resolution = "多次失敗可能為未授權存取嘗試",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 85, Code = "INF-9009", Category = "Session", Severity = "Info",
                Title = "Lock Screen - Switch User",
                Description = "使用者在鎖定畫面選擇切換使用者",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 86, Code = "INF-9010", Category = "Session", Severity = "Info",
                Title = "Lock Screen - Passthrough Message",
                Description = "鎖定期間顯示穿透訊息（如門板警告）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 87, Code = "INF-9011", Category = "Session", Severity = "Info",
                Title = "Lock Screen - Admin Auth Success",
                Description = "管理員在鎖定畫面驗證成功，準備執行強制登出或代理解鎖",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 88, Code = "WRN-9012", Category = "Session", Severity = "Warning",
                Title = "Lock Screen - Admin Auth Failed",
                Description = "鎖定畫面 Admin 驗證失敗（密碼錯誤、帳號不存在或權限不足）",
                Resolution = "確認使用 Admin 等級帳號登入",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 89, Code = "WRN-9013", Category = "Session", Severity = "Warning",
                Title = "Lock Screen - Admin Force Logout",
                Description = "管理員強制登出另一使用者的鎖定中 Session（敏感操作）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 91, Code = "WRN-9014", Category = "Session", Severity = "Warning",
                Title = "Lock Screen - Admin Proxy Unlock",
                Description = "管理員代理解鎖另一使用者的鎖定中 Session（敏感操作）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 4xxx — USB Cybersecurity
            // ══════════════════════════════════
            new()
            {
                Id = 92, Code = "INF-4010", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Device Inserted",
                Description = "USB 儲存裝置插入偵測（含佇列狀態）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 93, Code = "INF-4011", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Quick Format Success",
                Description = "USB 隨身碟快速格式化成功（exFAT）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 94, Code = "WRN-4012", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Quick Format Failed",
                Description = "USB 隨身碟快速格式化失敗",
                Resolution = "檢查隨身碟是否有寫入保護或損壞",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 95, Code = "INF-4013", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Format Cancelled",
                Description = "使用者取消格式化操作（含裝置拔除自動取消）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 96, Code = "WRN-4014", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Format Blocked - Non-Removable",
                Description = "偵測到非可卸除式磁碟，已阻擋格式化操作（安全防護）",
                Resolution = "僅限 Removable Disk 類型的 USB 隨身碟",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 97, Code = "INF-4015", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Scan Clean",
                Description = "USB 內容掃描通過，未發現威脅檔案",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 98, Code = "WRN-4016", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Scan Threat Detected",
                Description = "USB 掃描偵測到風險檔案（副檔名黑名單命中）",
                Resolution = "移除隨身碟上的可疑檔案後重試",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 99, Code = "WRN-4017", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Scan Suspicious File",
                Description = "USB 掃描偵測到可疑檔案（不在安全白名單中）",
                Resolution = "確認檔案是否為合法用途，如需放行請加入安全名單",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 100, Code = "INF-4018", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Device Removed",
                Description = "USB 儲存裝置拔除",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 101, Code = "WRN-4019", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Guest Mode Blocked",
                Description = "Guest 模式下所有 USB 儲存功能被禁用",
                Resolution = "請以具權限的帳號登入後操作",
                UserMessageKey = "UsbSecurity.GuestBlocked",
                UserMessageFallback = "USB functions are disabled in Guest mode."
            },
            new()
            {
                Id = 102, Code = "INF-4020", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Format Prompt Shown",
                Description = "格式化確認面板已彈出，等待使用者回應",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 103, Code = "INF-4021", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Format User Confirmed",
                Description = "使用者已確認執行格式化（格式化指令即將執行）",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 104, Code = "WRN-4022", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Blocked - Not Authenticated",
                Description = "未登入狀態下 USB 儲存裝置操作被攔截（安全防護）",
                Resolution = "請先登入系統後再操作 USB 裝置",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 105, Code = "WRN-4023", Category = "UsbSecurity", Severity = "Warning",
                Title = "USB Blocked - Session Locked",
                Description = "畫面鎖定（Session Timeout）時 USB 儲存裝置操作被攔截",
                Resolution = "請先解鎖畫面後再操作 USB 裝置",
                UserMessageKey = null, UserMessageFallback = null
            },
            new()
            {
                Id = 106, Code = "INF-4024", Category = "UsbSecurity", Severity = "Information",
                Title = "USB Format Skipped - Already Clean",
                Description = "USB 已經是目標檔案系統且無使用者檔案，跳過不必要的重複格式化",
                Resolution = null,
                UserMessageKey = null, UserMessageFallback = null
            },

            // ══════════════════════════════════
            // 9xxx — Dynamic / Unknown（動態註冊保留區段）
            // ══════════════════════════════════
            new()
            {
                Id = 90, Code = "ERR-9000", Category = "System", Severity = "Error",
                Title = "Unknown Error",
                Description = "未歸類的系統錯誤（動態註冊的錯誤將從 ERR-9001 開始）",
                Resolution = "提供 Error ID 給技術支援人員",
                UserMessageKey = "Error.ERR-9000",
                UserMessageFallback = "An unknown error occurred. Please report the Error ID."
            },
        };
    }
}
