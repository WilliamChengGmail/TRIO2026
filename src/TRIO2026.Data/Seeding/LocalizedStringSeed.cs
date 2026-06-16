using TRIO2026.Core.Entities;

namespace TRIO2026.Data.Seeding;

/// <summary>
/// 多語系字串種子資料
/// 
/// 語系代碼：
///   - en      英文（預設）
///   - zh-TW   繁體中文
///   - zh-CN   簡體中文
///   - ja      日語
///
/// Module 分類：
///   - Common   共用字串（OK、Cancel 等跨功能使用）
///   - UV       UV Decontamination 功能專用
///   - Login    登入功能（預留）
///   - Menu     主選單功能（預留）
///   - Error    通用錯誤訊息（預留）
///
/// 新增語系步驟：
///   1. 在此檔案新增對應 LanguageCode 的資料列
///   2. 重新執行 DbInitializer 或手動 INSERT
///   3. 在 SystemConfig 中設定 system.language 為新語系代碼
///
/// 製作者: Office of William
/// </summary>
public static class LocalizedStringSeed
{
    private static readonly string[] Languages = { "en", "zh-TW", "zh-CN", "ja" };

    public static List<LocalizedString> GetSeedData()
    {
        var seeds = new List<LocalizedString>();
        int id = 1;

        // ══════════════════════════════════════════
        // Common 模組 — 跨功能共用字串
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Common", "OK",
            "OK", "確定", "确定", "OK"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Cancel",
            "Cancel", "取消", "取消", "キャンセル"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Confirm",
            "Confirm", "確認", "确认", "確認"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Back",
            "Back", "返回", "返回", "戻る"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Close",
            "Close", "關閉", "关闭", "閉じる"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Ready",
            "● Ready", "● 就緒", "● 就绪", "● 準備完了"));
        seeds.AddRange(CreateGroup(ref id, "Common", "Yes",
            "Yes", "是", "是", "はい"));
        seeds.AddRange(CreateGroup(ref id, "Common", "No",
            "No", "否", "否", "いいえ"));

        // ══════════════════════════════════════════
        // UV 模組 — UV Decontamination 功能
        // ══════════════════════════════════════════

        // 頁面
        seeds.AddRange(CreateGroup(ref id, "UV", "Title",
            "UV Decontamination", "UV 消毒", "UV 消毒", "UV 除染"));
        seeds.AddRange(CreateGroup(ref id, "UV", "HomeTooltip",
            "Back to HOME", "返回主畫面", "返回主界面", "ホームに戻る"));

        // 按鈕
        seeds.AddRange(CreateGroup(ref id, "UV", "Start",
            "Start", "開始", "开始", "スタート"));
        seeds.AddRange(CreateGroup(ref id, "UV", "Stop",
            "Stop", "停止", "停止", "ストップ"));

        // 完成提示
        seeds.AddRange(CreateGroup(ref id, "UV", "CompleteTitle",
            "UV Decontamination", "UV 消毒", "UV 消毒", "UV 除染"));
        seeds.AddRange(CreateGroup(ref id, "UV", "CompleteMessage",
            "UV light is completed. Please back to HOME screen.",
            "UV 照射完成。請返回主畫面。",
            "UV 照射完成。请返回主界面。",
            "UV照射が完了しました。ホーム画面に戻ってください。"));

        // 停止確認
        seeds.AddRange(CreateGroup(ref id, "UV", "StopConfirmTitle",
            "Information", "提示", "提示", "情報"));
        seeds.AddRange(CreateGroup(ref id, "UV", "StopConfirmMessage",
            "Are you certain about stopping the UV light process?",
            "確定要停止 UV 照射嗎？",
            "确定要停止 UV 照射吗？",
            "UV照射を停止してもよろしいですか？"));

        // 門板警示
        seeds.AddRange(CreateGroup(ref id, "UV", "DoorErrorTitle",
            "Error!", "錯誤！", "错误！", "エラー！"));
        seeds.AddRange(CreateGroup(ref id, "UV", "DoorErrorMessage",
            "The door is open. Please close the door to proceed.",
            "門板已開啟，請關閉門板以繼續。",
            "门板已打开，请关闭门板以继续。",
            "ドアが開いています。続行するにはドアを閉じてください。"));
        seeds.AddRange(CreateGroup(ref id, "UV", "DoorNotClosed",
            "The door is not closed.", "門板尚未關閉", "门板尚未关闭", "ドアが閉まっていません"));

        // ══════════════════════════════════════════
        // UserMenu 模組 — 共用使用者選單
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "Home",
            "Home", "返回主畫面", "返回主界面", "ホーム"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "SwitchLanguage",
            "Switch Language", "切換語系", "切换语言", "言語切替"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "Logout",
            "Logout", "登出", "登出", "ログアウト"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "CloseApp",
            "Close Application", "關閉應用程式", "关闭应用程序", "アプリを閉じる"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "LogoutConfirmTitle",
            "Logout Confirmation", "登出確認", "登出确认", "ログアウト確認"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "LogoutConfirmMessage",
            "Do you want to logout?", "是否要登出系統？", "是否要登出系统？", "ログアウトしますか？"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "LogoutAndClose",
            "Logout & Close", "登出並關閉系統", "登出并关闭系统", "ログアウトして閉じる"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "CloseConfirmTitle",
            "Close Application", "關閉視窗", "关闭窗口", "アプリを閉じる"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "CloseConfirmMessage",
            "Do you want to close the application?",
            "是否要關閉應用程式？",
            "是否要关闭应用程序？",
            "アプリケーションを閉じますか？"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "InsufficientPermission",
            "Insufficient Permission", "權限不足", "权限不足", "権限不足"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "InsufficientPermissionMessage",
            "Your account does not have permission to close the application.\nService level or above is required.",
            "您的帳號權限不足，無法關閉應用程式。\n需要 Service 等級以上的權限。",
            "您的账号权限不足，无法关闭应用程序。\n需要 Service 等级以上的权限。",
            "アカウントの権限が不足しています。\nService レベル以上の権限が必要です。"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "AuthTitle",
            "Identity Verification", "身分驗證", "身份验证", "本人確認"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "AuthMessage",
            "Please enter your credentials to confirm close permission.",
            "請輸入帳號密碼以確認關閉權限。",
            "请输入账号密码以确认关闭权限。",
            "閉じる権限を確認するため、認証情報を入力してください。"));

        // ══════════════════════════════════════════
        // Login 模組 — 登入驗證錯誤訊息
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Login", "UserNotFound",
            "Account not found", "帳號不存在", "账号不存在", "アカウントが見つかりません"));
        seeds.AddRange(CreateGroup(ref id, "Login", "WrongPassword",
            "Wrong password", "密碼錯誤", "密码错误", "パスワードが間違っています"));
        seeds.AddRange(CreateGroup(ref id, "Login", "AccountDisabled",
            "Account disabled", "帳號已停用", "账号已停用", "アカウントが無効です"));
        seeds.AddRange(CreateGroup(ref id, "Login", "AccountLocked",
            "Account locked, please try again later",
            "帳號已鎖定，請稍後再試",
            "账号已锁定，请稍后再试",
            "アカウントがロックされています。しばらくしてから再試行してください"));
        seeds.AddRange(CreateGroup(ref id, "Login", "AuthFailed",
            "Authentication failed", "驗證失敗", "验证失败", "認証に失敗しました"));

        // ══════════════════════════════════════════
        // Error 模組 — 錯誤提示視窗用
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Error", "Title",
            "TRIO2026 Error", "TRIO2026 錯誤", "TRIO2026 错误", "TRIO2026 エラー"));
        seeds.AddRange(CreateGroup(ref id, "Error", "ReportHint",
            "Please report this Error ID to technical support.",
            "請將此 Error ID 提供給技術支援人員。",
            "请将此 Error ID 提供给技术支持人员。",
            "このエラーIDを技術サポートに報告してください。"));

        // 各 ErrorId 對應使用者訊息
        seeds.AddRange(CreateGroup(ref id, "Error", "ERR-1001",
            "An unexpected error occurred. Please restart the application.",
            "發生未預期的錯誤，請重新啟動應用程式。",
            "发生未预期的错误，请重新启动应用程序。",
            "予期しないエラーが発生しました。アプリケーションを再起動してください。"));
        seeds.AddRange(CreateGroup(ref id, "Error", "ERR-1002",
            "Database connection failed. Please restart the application.",
            "資料庫連線失敗，請重新啟動應用程式。",
            "数据库连接失败，请重新启动应用程序。",
            "データベース接続に失敗しました。アプリケーションを再起動してください。"));
        seeds.AddRange(CreateGroup(ref id, "Error", "ERR-3004",
            "Door opened during UV operation. Close the door to resume.",
            "UV 運行期間門板被開啟，請關閉門板以恢復。",
            "UV 运行期间门板被打开，请关闭门板以恢复。",
            "UV動作中にドアが開きました。ドアを閉じて再開してください。"));
        seeds.AddRange(CreateGroup(ref id, "Error", "ERR-3005",
            "UV lamp failed to start. Please contact maintenance.",
            "UV 燈管啟動失敗，請聯繫維護團隊。",
            "UV 灯管启动失败，请联系维护团队。",
            "UVランプの起動に失敗しました。メンテナンスに連絡してください。"));
        seeds.AddRange(CreateGroup(ref id, "Error", "ERR-9000",
            "An unknown error occurred. Please report the Error ID.",
            "發生未知錯誤，請回報 Error ID。",
            "发生未知错误，请回报 Error ID。",
            "不明なエラーが発生しました。エラーIDを報告してください。"));

        // ══════════════════════════════════════════
        // Menu 模組 — 主選單頁面
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Menu", "IntelliPlex",
            "IntelliPlex", "IntelliPlex", "IntelliPlex", "IntelliPlex"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "IntelliPlexSub",
            "Program", "程式設定", "程序设置", "プログラム"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "Custom",
            "Custom", "自訂", "自定义", "カスタム"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "CustomSub",
            "Program", "程式設定", "程序设置", "プログラム"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "Data",
            "Data", "數據", "数据", "データ"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "DataSub",
            "History & Results", "歷史紀錄與結果", "历史记录与结果", "履歴と結果"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "Setting",
            "Setting", "設定", "设置", "設定"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "SettingSub",
            "System Config", "系統設定", "系统设置", "システム設定"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "UV",
            "UV", "UV", "UV", "UV"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "UVSub",
            "Decontamination", "消毒", "消毒", "除染"));
        seeds.AddRange(CreateGroup(ref id, "Menu", "StatusReady",
            "● Ready", "● 就緒", "● 就绪", "● 準備完了"));

        // ══════════════════════════════════════════
        // UserMenu 模組 — Service Mode 切換
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ServiceMode",
            "Service Mode", "Service Mode", "Service Mode", "サービスモード"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ServiceModeTitle",
            "Service Mode Login", "Service Mode 登入", "Service Mode 登录", "サービスモード ログイン"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ServiceModeMessage",
            "Enter Service credentials to proceed.",
            "請輸入 Service 帳號密碼以進入。",
            "请输入 Service 账号密码以进入。",
            "サービスアカウントの認証情報を入力してください。"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ServiceModeInsufficientRole",
            "Service role or higher is required.",
            "需要 Service 以上角色權限。",
            "需要 Service 以上角色权限。",
            "Service 以上の権限が必要です。"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ExitServiceModeTitle",
            "Exit Service Mode", "退出 Service Mode", "退出 Service Mode", "サービスモード終了"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ExitServiceModeMessage",
            "Do you want to exit Service Mode and return to normal operation?",
            "是否要退出 Service Mode 並返回一般操作模式？",
            "是否要退出 Service Mode 并返回一般操作模式？",
            "サービスモードを終了して通常操作に戻りますか？"));

        // ══════════════════════════════════════════
        // ServiceMode 模組 — Service Mode 頁面
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "Title",
            "Service Mode", "Service Mode", "Service Mode", "サービスモード"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "Placeholder",
            "This page is under development.", "此頁面功能開發中。", "此页面功能开发中。", "このページは開発中です。"));

        // ══════════════════════════════════════════
        // ServiceMode 模組 — 功能殼
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "MachineSetting",
            "Machine Setting", "機器設定", "机器设置", "マシン設定"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "FlowSetting",
            "Flow Setting", "流程設定", "流程设置", "フロー設定"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "Communication",
            "Communication", "通訊設定", "通讯设置", "通信設定"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "ResetAdminPassword",
            "Reset Admin Password", "重設管理者密碼", "重设管理者密码", "管理者パスワードリセット"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "ComingSoonTitle",
            "Under Development", "功能開發中", "功能开发中", "開発中"));
        seeds.AddRange(CreateGroup(ref id, "ServiceMode", "ComingSoonMessage",
            "This feature is under development. Stay tuned.",
            "此功能正在開發中，敬請期待。",
            "此功能正在开发中，敬请期待。",
            "この機能は開発中です。今しばらくお待ちください。"));

        // ══════════════════════════════════════════
        // UserMenu 模組 — 新增節點
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "ChangePassword",
            "Change Password", "變更密碼", "更改密码", "パスワード変更"));
        seeds.AddRange(CreateGroup(ref id, "UserMenu", "AccountMgmt",
            "Account Management", "帳號管理", "账号管理", "アカウント管理"));

        // ══════════════════════════════════════════
        // NumericKeypad 模組 — 動態數字鍵盤
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "NumericKeypad", "Title",
            "Enter Password", "輸入密碼", "输入密码", "パスワード入力"));
        seeds.AddRange(CreateGroup(ref id, "NumericKeypad", "Confirm",
            "✓ Confirm", "✓ 確認", "✓ 确认", "✓ 確認"));
        seeds.AddRange(CreateGroup(ref id, "NumericKeypad", "Cancel",
            "✕ Cancel", "✕ 取消", "✕ 取消", "✕ キャンセル"));

        // ══════════════════════════════════════════
        // TouchKeyboard 模組 — 觸控全鍵盤
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "TouchKeyboard", "TitleAccount",
            "Enter Username", "輸入帳號", "输入账号", "ユーザー名入力"));
        seeds.AddRange(CreateGroup(ref id, "TouchKeyboard", "TitlePassword",
            "Enter Password", "輸入密碼", "输入密码", "パスワード入力"));
        seeds.AddRange(CreateGroup(ref id, "TouchKeyboard", "Confirm",
            "OK", "確認", "确认", "確認"));
        seeds.AddRange(CreateGroup(ref id, "TouchKeyboard", "Space",
            "Space", "空白", "空格", "スペース"));
        seeds.AddRange(CreateGroup(ref id, "TouchKeyboard", "Clear",
            "Clear", "清除", "清除", "クリア"));

        // ══════════════════════════════════════════
        // AccountMgmt 模組 — 帳號管理頁面
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Title",
            "Account Management", "帳號管理", "账号管理", "アカウント管理"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "AddAccount",
            "+ Add Account", "+ 新增帳號", "+ 新增账号", "+ アカウント追加"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SelectPrompt",
            "Select an account to manage", "請選擇帳號以進行操作", "请选择账号以进行操作", "管理するアカウントを選択"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Disable",
            "Disable Account", "停用帳號", "停用账号", "アカウント無効化"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Enable",
            "Enable Account", "啟用帳號", "启用账号", "アカウント有効化"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Delete",
            "Delete Account", "刪除帳號", "删除账号", "アカウント削除"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Lock",
            "Lock Account", "鎖定帳號", "锁定账号", "アカウントロック"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "Unlock",
            "Unlock Account", "解鎖帳號", "解锁账号", "アカウントロック解除"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ResetPassword",
            "Reset Password", "重設密碼", "重设密码", "パスワードリセット"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ChangePassword",
            "Change Password", "變更密碼", "更改密码", "パスワード変更"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ViewDetails",
            "View Details", "檢視詳細資料", "查看详细资料", "詳細を表示"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "BackToOps",
            "Back to Operations", "返回操作", "返回操作", "操作に戻る"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "StatusActive",
            "Active", "啟用", "启用", "有効"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "StatusDisabled",
            "Disabled", "已停用", "已停用", "無効"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "StatusLocked",
            "Locked", "已鎖定", "已锁定", "ロック中"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "StatusDeleted",
            "Deleted", "已刪除", "已删除", "削除済み"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "RoleOperator",
            "Operator", "操作員", "操作员", "オペレーター"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "RoleService",
            "Service", "服務工程師", "服务工程师", "サービスエンジニア"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "RoleAdmin",
            "Admin", "管理員", "管理员", "管理者"));

        // 帳號管理 — 對話框訊息
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "TempPasswordTitle",
            "Temporary Password", "臨時密碼", "临时密码", "一時パスワード"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "TempPasswordNote",
            "Please provide this to the user. They will be required to change it on first login.",
            "請將此密碼提供給使用者。首次登入後系統將強制要求變更密碼。",
            "请将此密码提供给用户。首次登录后系统将强制要求更改密码。",
            "このパスワードをユーザーに提供してください。初回ログイン時にパスワード変更が要求されます。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ConfirmDelete",
            "Are you sure you want to delete this account? This action cannot be undone.",
            "確定要刪除此帳號？此操作無法復原。",
            "确定要删除此账号？此操作无法恢复。",
            "このアカウントを削除しますか？この操作は元に戻せません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ConfirmDisable",
            "Are you sure you want to disable this account?",
            "確定要停用此帳號？",
            "确定要停用此账号？",
            "このアカウントを無効にしますか？"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ConfirmEnable",
            "Are you sure you want to enable this account?",
            "確定要啟用此帳號？",
            "确定要启用此账号？",
            "このアカウントを有効にしますか？"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ConfirmLock",
            "Locking the account will prevent the user from logging in until manually unlocked.",
            "鎖定後該使用者將無法登入，直到手動解鎖為止。",
            "锁定后该用户将无法登录，直到手动解锁为止。",
            "アカウントをロックすると、手動でロック解除するまでユーザーはログインできません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ConfirmResetPassword",
            "Are you sure you want to reset the password for this account?",
            "確定要重設此帳號的密碼？",
            "确定要重设此账号的密码？",
            "このアカウントのパスワードをリセットしますか？"));

        // 帳號管理 — 錯誤訊息
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ErrorSelf",
            "Cannot perform this action on your own account.",
            "無法對自己的帳號執行此操作。",
            "无法对自己的账号执行此操作。",
            "自分のアカウントに対してこの操作は実行できません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ErrorLastAdmin",
            "Cannot remove the last active admin account.",
            "無法移除最後一個啟用的管理員帳號。",
            "无法移除最后一个启用的管理员账号。",
            "最後の有効な管理者アカウントを削除できません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "UsernameExists",
            "Username already exists.",
            "帳號名稱已存在。",
            "账号名称已存在。",
            "ユーザー名は既に存在します。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "InvalidUsername",
            "Username must be 3-20 alphanumeric characters or underscores.",
            "帳號名稱須為 3~20 個英數字或底線。",
            "账号名称须为 3~20 个英数字或下划线。",
            "ユーザー名は3～20文字の英数字またはアンダースコアである必要があります。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ReservedUsername",
            "This username is reserved for the system and cannot be used.",
            "此帳號名稱為系統保留，無法使用。",
            "此账号名称为系统保留，无法使用。",
            "このユーザー名はシステム予約済みのため使用できません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessCreated",
            "Account created successfully.",
            "帳號建立成功。",
            "账号创建成功。",
            "アカウントが正常に作成されました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessDeleted",
            "Account deleted successfully.",
            "帳號已刪除。",
            "账号已删除。",
            "アカウントが削除されました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessDisabled",
            "Account disabled successfully.",
            "帳號已停用。",
            "账号已停用。",
            "アカウントが無効化されました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessEnabled",
            "Account enabled successfully.",
            "帳號已啟用。",
            "账号已启用。",
            "アカウントが有効化されました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessLocked",
            "Account locked successfully.",
            "帳號已鎖定。",
            "账号已锁定。",
            "アカウントがロックされました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessUnlocked",
            "Account unlocked successfully.",
            "帳號已解鎖。",
            "账号已解锁。",
            "アカウントのロックが解除されました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "SuccessPasswordReset",
            "Password has been reset.",
            "密碼已重設。",
            "密码已重设。",
            "パスワードがリセットされました。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ErrorServiceDelete",
            "Service accounts cannot be deleted from the UI.",
            "Service 帳號無法從介面刪除。",
            "Service 账号无法从界面删除。",
            "サービスアカウントはUIから削除できません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ErrorSystemAccount",
            "This is a system account and cannot be modified or deleted.",
            "此為系統帳號，無法修改或刪除。",
            "此为系统账号，无法修改或删除。",
            "これはシステムアカウントのため変更・削除できません。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ErrorServiceAdd",
            "Service accounts are managed by IT/DB directly.",
            "Service 帳號由 IT/DB 直接管理。",
            "Service 账号由 IT/DB 直接管理。",
            "サービスアカウントはIT/DBで直接管理されます。"));

        // 帳號管理 — 新增帳號 Overlay
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "CreateTitle",
            "Create Account", "新增帳號", "新增账号", "アカウント作成"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "LabelUsername",
            "Username", "帳號名稱", "账号名称", "ユーザー名"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "LabelDisplayName",
            "Display Name", "顯示名稱", "显示名称", "表示名"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "LabelRole",
            "Role", "角色", "角色", "ロール"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "ServiceNotice",
            "Service accounts are managed by Plexbio directly.",
            "ℹ️ Service 帳號由 Plexbio 直接管理",
            "ℹ️ Service 账号由 Plexbio 直接管理",
            "ℹ️ サービスアカウントはPlexbioで直接管理"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "InitPasswordNotice",
            "Initial password will be generated and displayed once.",
            "初始密碼由系統隨機產生並顯示一次。",
            "初始密码由系统随机生成并显示一次。",
            "初期パスワードはシステムが自動生成し、一度だけ表示されます。"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "CreateButton",
            "Create", "建立", "创建", "作成"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "TempPasswordCopied",
            "I have recorded, close", "我已記錄，關閉", "我已记录，关闭", "記録しました、閉じる"));

        // 帳號管理 — 檢視詳細資料欄位標籤
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailUsername",
            "Username", "帳號名稱", "账号名称", "ユーザー名"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailDisplayName",
            "Display Name", "顯示名稱", "显示名称", "表示名"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailRole",
            "Role Level", "角色等級", "角色等级", "ロールレベル"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailStatus",
            "Status", "狀態", "状态", "ステータス"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailEmployeeId",
            "Employee ID", "員工編號", "员工编号", "従業員ID"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailDepartment",
            "Department", "部門", "部门", "部門"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailEmail",
            "Email", "電子郵件", "电子邮件", "メール"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailLastLogin",
            "Last Login", "最後登入", "最后登录", "最終ログイン"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailPasswordChanged",
            "Password Changed", "密碼變更時間", "密码更改时间", "パスワード変更日時"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailForceChange",
            "Force Password Change", "強制變更密碼", "强制更改密码", "パスワード変更を強制"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailLockedUntil",
            "Locked Until", "鎖定至", "锁定至", "ロック期限"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailFailedCount",
            "Failed Login Count", "失敗次數", "失败次数", "ログイン失敗回数"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailCreated",
            "Created", "建立時間", "创建时间", "作成日時"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailCreatedBy",
            "Created By", "建立者", "创建者", "作成者"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "DetailNotes",
            "Notes", "備註", "备注", "備考"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "NotLocked",
            "Not Locked", "未鎖定", "未锁定", "ロックなし"));
        seeds.AddRange(CreateGroup(ref id, "AccountMgmt", "None",
            "None", "無", "无", "なし"));

        // ══════════════════════════════════════════
        // Login 模組 — 登入頁錯誤訊息
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Login", "InvalidCredentials",
            "Invalid username or password.",
            "帳號或密碼錯誤。",
            "账号或密码错误。",
            "ユーザー名またはパスワードが正しくありません。"));
        seeds.AddRange(CreateGroup(ref id, "Login", "AccountDisabled",
            "This account has been disabled. Please contact your administrator.",
            "此帳號已停用，請聯絡管理員。",
            "此账号已停用，请联系管理员。",
            "このアカウントは無効です。管理者にお問い合わせください。"));
        seeds.AddRange(CreateGroup(ref id, "Login", "AccountLocked",
            "Account is locked. Please try again later.",
            "帳號已鎖定，請稍後再試。",
            "账号已锁定，请稍后再试。",
            "アカウントがロックされています。しばらくしてから再試行してください。"));
        seeds.AddRange(CreateGroup(ref id, "Login", "SystemError",
            "System error",
            "系統錯誤",
            "系统错误",
            "システムエラー"));
        seeds.AddRange(CreateGroup(ref id, "Login", "GuestHint",
            "Guest account — tap Login to continue.",
            "訪客帳號 — 請直接點選登入。",
            "访客账号 — 请直接点击登录。",
            "ゲストアカウント — ログインをタップしてください。"));
        seeds.AddRange(CreateGroup(ref id, "Login", "Username",
            "Username", "帳號", "账号", "ユーザー名"));
        seeds.AddRange(CreateGroup(ref id, "Login", "Password",
            "Password", "密碼", "密码", "パスワード"));
        seeds.AddRange(CreateGroup(ref id, "Login", "RememberPassword",
            "Remember Password", "記住密碼", "记住密码", "パスワードを記憶"));
        seeds.AddRange(CreateGroup(ref id, "Login", "Submit",
            "Login", "登　入", "登　录", "ログイン"));
        seeds.AddRange(CreateGroup(ref id, "Login", "Close",
            "Close Application", "關閉應用程式", "关闭应用程序", "アプリを閉じる"));

        // ══════════════════════════════════════════
        // Lock 模組 — 鎖定畫面
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "Lock", "Title",
            "Screen Locked", "螢幕已鎖定", "屏幕已锁定", "画面ロック中"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "Subtitle",
            "Enter password to unlock", "請輸入密碼解鎖", "请输入密码解锁", "パスワードを入力してロック解除"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "LockedAt",
            "Locked at {0}", "鎖定時間：{0}", "锁定时间：{0}", "ロック時刻：{0}"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "Unlock",
            "Unlock", "解鎖", "解锁", "ロック解除"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "SwitchUser",
            "Admin Override", "Admin 介入", "Admin 介入", "管理者介入"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "InvalidPassword",
            "Incorrect password.", "密碼錯誤。", "密码错误。", "パスワードが正しくありません。"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "TimeoutWarning",
            "Locking in {0}s...", "{0} 秒後鎖定...", "{0} 秒后锁定...", "{0}秒後にロック..."));
        seeds.AddRange(CreateGroup(ref id, "Lock", "WorkStatus",
            "In Progress", "進行中", "进行中", "実行中"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "CountdownLabel",
            "Lock in {0}", "{0} 後鎖定", "{0} 后锁定", "{0}後にロック"));

        // Lock 模組 — Admin 介入
        seeds.AddRange(CreateGroup(ref id, "Lock", "AdminUsernamePrompt",
            "Enter Admin Username", "請輸入管理員帳號", "请输入管理员账号", "管理者アカウントを入力"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "AdminPasswordPrompt",
            "Enter Admin Password", "請輸入管理員密碼", "请输入管理员密码", "管理者パスワードを入力"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "AdminAuthFailed",
            "Authentication failed.", "驗證失敗。", "验证失败。", "認証に失敗しました。"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "AdminInsufficientRole",
            "Admin privileges required.", "需要管理員權限。", "需要管理员权限。", "管理者権限が必要です。"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "ConfirmLogoutTitle",
            "Force Logout Warning", "強制登出警告", "强制登出警告", "強制ログアウト警告"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "ConfirmLogoutMessage",
            "You are about to force logout the current user.\nThe device may still be running tasks.\nPlease ensure all operations are safe before proceeding.",
            "即將強制登出目前使用者。\n設備可能仍在運行中。\n請確認所有操作安全後再繼續。",
            "即将强制登出当前用户。\n设备可能仍在运行中。\n请确认所有操作安全后再继续。",
            "現在のユーザーを強制ログアウトします。\nデバイスがまだ稼働中の可能性があります。\nすべての操作が安全であることを確認してから続行してください。"));
        seeds.AddRange(CreateGroup(ref id, "Lock", "ConfirmLogoutButton",
            "Confirm Logout", "確認登出", "确认登出", "ログアウト確認"));

        // ══════════════════════════════════════════
        // PasswordUI 模組 — 密碼變更 Overlay
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "Title",
            "Change Password", "變更密碼", "更改密码", "パスワード変更"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "Subtitle",
            "Please enter a new password to complete the change.",
            "請輸入新密碼以完成變更。",
            "请输入新密码以完成更改。",
            "新しいパスワードを入力して変更を完了してください。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "ForceSubtitle",
            "You must change your password before proceeding.",
            "您必須先變更密碼才能繼續操作。",
            "您必须先更改密码才能继续操作。",
            "続行する前にパスワードを変更する必要があります。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "CurrentPassword",
            "Current Password", "當前密碼", "当前密码", "現在のパスワード"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "NewPassword",
            "New Password", "新密碼", "新密码", "新しいパスワード"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "ConfirmPassword",
            "Confirm Password", "確認密碼", "确认密码", "パスワード確認"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "Submit",
            "Change Password", "確認變更", "确认更改", "パスワード変更"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "PasswordMismatch",
            "Passwords do not match.",
            "兩次密碼不一致。",
            "两次密码不一致。",
            "パスワードが一致しません。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "SamePassword",
            "New password cannot be the same as current password.",
            "新密碼不可與當前密碼相同。",
            "新密码不可与当前密码相同。",
            "新しいパスワードは現在のパスワードと同じにできません。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "SuccessTitle",
            "Password Changed", "密碼已變更", "密码已更改", "パスワード変更完了"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "SuccessMessage",
            "Your password has been updated successfully.",
            "密碼已成功更新。",
            "密码已成功更新。",
            "パスワードが正常に更新されました。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "ForceSuccessMessage",
            "Password changed. Please log in again with your new password.",
            "密碼已變更，請以新密碼重新登入。",
            "密码已更改，请以新密码重新登录。",
            "パスワードが変更されました。新しいパスワードで再ログインしてください。"));

        // PasswordUI — 即時驗證規則提示
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleMinLength",
            "At least {0} characters", "至少 {0} 碼", "至少 {0} 位", "最低{0}文字"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleMaxLength",
            "No more than {0} characters", "不超過 {0} 碼", "不超过 {0} 位", "{0}文字以内"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleRequireMixed",
            "Contains letters and numbers", "包含英文字母與數字", "包含英文字母与数字", "英字と数字を含む"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleRequireUpper",
            "Contains uppercase letter", "包含大寫字母", "包含大写字母", "大文字を含む"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleRequireLower",
            "Contains lowercase letter", "包含小寫字母", "包含小写字母", "小文字を含む"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleRequireDigit",
            "Contains a number", "包含數字", "包含数字", "数字を含む"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "RuleRequireSpecial",
            "Contains special character", "包含特殊符號", "包含特殊符号", "特殊文字を含む"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "WrongCurrentPassword",
            "Current password is incorrect.",
            "當前密碼錯誤。",
            "当前密码错误。",
            "現在のパスワードが正しくありません。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "PasswordEmpty",
            "Password cannot be empty.",
            "密碼不可為空。",
            "密码不能为空。",
            "パスワードを入力してください。"));
        seeds.AddRange(CreateGroup(ref id, "PasswordUI", "BCryptLimit",
            "Password must not exceed 72 characters.",
            "密碼不可超過 72 個字元。",
            "密码不能超过 72 个字符。",
            "パスワードは72文字以内で入力してください。"));

        // ══════════════════════════════════════════
        // UsbSecurity 模組 — USB 資安專碟專用
        // ══════════════════════════════════════════
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatTitle",
            "USB Dedicated Drive", "USB 專碟專用", "USB 专碟专用", "USB 専用ドライブ"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatDetected",
            "Removable USB drive detected ({0})",
            "偵測到 USB 隨身碟 ({0})",
            "检测到 USB 随身碟 ({0})",
            "USB リムーバブルドライブを検出 ({0})"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatWarning",
            "To ensure information security, a quick format will be performed.\nThis operation will erase all data on the drive.",
            "為確保資訊安全，即將執行快速格式化。\n此操作將清除隨身碟上所有資料。",
            "为确保信息安全，即将执行快速格式化。\n此操作将清除随身碟上所有数据。",
            "情報セキュリティのため、クイックフォーマットを実行します。\nこの操作によりドライブ上のすべてのデータが消去されます。"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatVolumeInfo",
            "Volume: {0} ({1})", "磁碟區: {0} ({1})", "卷: {0} ({1})", "ボリューム: {0} ({1})"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatExecute",
            "Format", "執行格式化", "执行格式化", "フォーマット実行"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatCountdown",
            "Available in {0}s", "{0} 秒後啟用", "{0} 秒后启用", "{0}秒後に有効"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatSuccess",
            "Quick format completed successfully.", "快速格式化完成。", "快速格式化完成。", "クイックフォーマットが完了しました。"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatFailed",
            "Quick format failed. The drive may be write-protected or damaged.",
            "快速格式化失敗。隨身碟可能有寫入保護或損壞。",
            "快速格式化失败。随身碟可能有写入保护或损坏。",
            "クイックフォーマットに失敗しました。ドライブが書き込み保護されているか、破損している可能性があります。"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "FormatBlockedNotRemovable",
            "This drive is not a removable USB disk. Format operation blocked.",
            "此磁碟非可卸除式 USB 隨身碟，已阻擋格式化操作。",
            "此磁盘非可移除式 USB 随身碟，已阻止格式化操作。",
            "このドライブはリムーバブル USB ディスクではありません。フォーマットをブロックしました。"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "ScanThreatDetected",
            "Threat detected: {0}", "偵測到風險檔案: {0}", "检测到风险文件: {0}", "脅威を検出: {0}"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "ScanSuspicious",
            "Suspicious file: {0}", "可疑檔案: {0}", "可疑文件: {0}", "不審なファイル: {0}"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "DeviceQueued",
            "Another USB is being processed. This device is queued.",
            "另一個 USB 正在處理中，此裝置已排入佇列等待。",
            "另一个 USB 正在处理中，此设备已排入队列等待。",
            "別の USB が処理中です。このデバイスはキューに追加されました。"));
        seeds.AddRange(CreateGroup(ref id, "UsbSecurity", "GuestBlocked",
            "USB functions are disabled in Guest mode. Please log in with an authorized account.",
            "Guest 模式下所有 USB 功能已禁用，請以具權限的帳號登入後操作。",
            "Guest 模式下所有 USB 功能已禁用，请以具权限的账号登录后操作。",
            "ゲストモードでは USB 機能が無効です。権限のあるアカウントでログインしてください。"));

        // ══════════════════════════════════════════
        // Data 模組 — 數據紀錄頁面
        // ══════════════════════════════════════════

        // 清單頁 — 標題與導覽
        seeds.AddRange(CreateGroup(ref id, "Data", "PageTitle",
            "Data Records", "數據紀錄", "数据记录", "データ記録"));
        seeds.AddRange(CreateGroup(ref id, "Data", "NoRecords",
            "No Records Found", "尚無紀錄", "暂无记录", "記録がありません"));

        // 清單頁 — 篩選
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterMy",
            "My Records", "我的紀錄", "我的记录", "自分の記録"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterAll",
            "All Records", "全部紀錄", "全部记录", "すべての記録"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterByUser",
            "By Operator", "依操作員", "按操作员", "オペレーター別"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterDateRange",
            "Date Range", "日期範圍", "日期范围", "日付範囲"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterReportType",
            "Report Type", "報告類型", "报告类型", "レポート種別"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterStatus",
            "Status", "狀態", "状态", "ステータス"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterProgram",
            "Program", "萃取程式", "萃取程序", "プログラム"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterAllOption",
            "All", "全部", "全部", "すべて"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterApply",
            "Apply", "套用", "应用", "適用"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FilterReset",
            "Reset", "重設", "重置", "リセット"));

        // 清單頁 — 卡片資訊
        seeds.AddRange(CreateGroup(ref id, "Data", "Samples",
            "samples", "個樣本", "个样本", "サンプル"));
        seeds.AddRange(CreateGroup(ref id, "Data", "StatusCompleted",
            "Completed", "完成", "完成", "完了"));
        seeds.AddRange(CreateGroup(ref id, "Data", "StatusError",
            "Error", "錯誤", "错误", "エラー"));
        seeds.AddRange(CreateGroup(ref id, "Data", "StatusAborted",
            "Aborted", "已中止", "已中止", "中止"));

        // 清單頁 — 多選與下載
        seeds.AddRange(CreateGroup(ref id, "Data", "Select",
            "Select", "選取", "选取", "選択"));
        seeds.AddRange(CreateGroup(ref id, "Data", "CancelSelect",
            "Cancel", "取消選取", "取消选取", "選択解除"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadSelected",
            "Download Selected ({0})", "下載選取項目 ({0})", "下载选取项目 ({0})", "選択をダウンロード ({0})"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadConfirm",
            "Download {0} records?", "確認下載 {0} 筆紀錄？", "确认下载 {0} 笔记录？", "{0}件の記録をダウンロードしますか？"));

        // 清單頁 — 版面配置
        seeds.AddRange(CreateGroup(ref id, "Data", "LayoutCard",
            "Card", "卡片", "卡片", "カード"));
        seeds.AddRange(CreateGroup(ref id, "Data", "LayoutCompact",
            "Compact", "緊湊", "紧凑", "コンパクト"));
        seeds.AddRange(CreateGroup(ref id, "Data", "LayoutTable",
            "Table", "表格", "表格", "テーブル"));

        // 清單頁 — 表格表頭
        seeds.AddRange(CreateGroup(ref id, "Data", "HeaderDate",
            "Date", "日期", "日期", "日付"));
        seeds.AddRange(CreateGroup(ref id, "Data", "HeaderType",
            "Type", "類型", "类型", "タイプ"));
        seeds.AddRange(CreateGroup(ref id, "Data", "HeaderSamples",
            "Samples", "樣本", "样本", "サンプル"));
        seeds.AddRange(CreateGroup(ref id, "Data", "HeaderStatus",
            "Status", "狀態", "状态", "ステータス"));
        seeds.AddRange(CreateGroup(ref id, "Data", "HeaderOperator",
            "Operator", "操作員", "操作员", "オペレーター"));

        // 詳情頁
        seeds.AddRange(CreateGroup(ref id, "Data", "DetailTitle",
            "Report Detail", "報告詳情", "报告详情", "レポート詳細"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DetailSetting",
            "Experiment Settings", "實驗設定", "实验设定", "実験設定"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DetailResults",
            "Sample Results", "樣本結果", "样本结果", "サンプル結果"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DetailAudit",
            "Audit Trail", "操作紀錄", "操作记录", "操作履歴"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DetailDownload",
            "Download", "下載", "下载", "ダウンロード"));

        // USB 選擇器
        seeds.AddRange(CreateGroup(ref id, "Data", "UsbSelect",
            "Select USB Drive", "選擇目標 USB 隨身碟", "选择目标 USB 随身碟", "USB ドライブを選択"));
        seeds.AddRange(CreateGroup(ref id, "Data", "UsbNone",
            "Please insert USB drive", "請插入 USB 隨身碟", "请插入 USB 随身碟", "USB ドライブを挿入してください"));

        // 下載流程
        seeds.AddRange(CreateGroup(ref id, "Data", "UsbPreparing",
            "Preparing USB...", "準備 USB 中...", "准备 USB 中...", "USB 準備中..."));
        seeds.AddRange(CreateGroup(ref id, "Data", "Exporting",
            "Exporting...", "匯出中...", "导出中...", "エクスポート中..."));
        seeds.AddRange(CreateGroup(ref id, "Data", "ExportingCurrent",
            "Generating: {0}", "正在產生: {0}", "正在生成: {0}", "生成中: {0}"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadDone",
            "Download Complete", "下載完成", "下载完成", "ダウンロード完了"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadDoneMsg",
            "Exported {0} reports to {1}", "已匯出 {0} 筆報告至 {1}", "已导出 {0} 笔报告至 {1}", "{0}件のレポートを{1}にエクスポートしました"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadFail",
            "Download Failed", "下載失敗", "下载失败", "ダウンロード失敗"));
        seeds.AddRange(CreateGroup(ref id, "Data", "DownloadFailSpace",
            "Insufficient USB space.\nRequired: {0} / Available: {1}",
            "USB 空間不足。\n需要: {0} / 可用: {1}",
            "USB 空间不足。\n需要: {0} / 可用: {1}",
            "USB 容量不足。\n必要: {0} / 空き: {1}"));
        seeds.AddRange(CreateGroup(ref id, "Data", "CyberBlocked",
            "Security Check Failed", "安全檢查未通過", "安全检查未通过", "セキュリティチェック失敗"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FormatSkipped",
            "USB empty, format skipped", "USB 為空，略過格式化", "USB 为空，跳过格式化", "USB が空のため、フォーマットをスキップ"));
        seeds.AddRange(CreateGroup(ref id, "Data", "FormatConfirm",
            "USB has existing content. Format will erase all data.\nProceed?",
            "USB 有既有內容，格式化後將清除所有資料。\n確定繼續？",
            "USB 有既有内容，格式化后将清除所有数据。\n确定继续？",
            "USB に既存データがあります。フォーマットするとすべてのデータが消去されます。\n続行しますか？"));
        seeds.AddRange(CreateGroup(ref id, "Data", "UsbRemoved",
            "USB removed during export", "USB 已在匯出過程中移除", "USB 已在导出过程中移除", "エクスポート中に USB が取り外されました"));
        seeds.AddRange(CreateGroup(ref id, "Data", "UsbRemovedMsg",
            "Completed: {0} / {1}\nInterrupted at: {2}",
            "已完成: {0} / {1} 筆\n中斷於: {2}",
            "已完成: {0} / {1} 笔\n中断于: {2}",
            "完了: {0} / {1}\n中断: {2}"));

        return seeds;
    }

    /// <summary>
    /// 建立同一 ResourceKey 的四語系資料群組
    /// </summary>
    /// <param name="id">自動遞增 ID 計數器</param>
    /// <param name="module">功能模組</param>
    /// <param name="key">資源鍵值</param>
    /// <param name="en">英文</param>
    /// <param name="zhTw">繁體中文</param>
    /// <param name="zhCn">簡體中文</param>
    /// <param name="ja">日語</param>
    private static List<LocalizedString> CreateGroup(
        ref int id, string module, string key,
        string en, string zhTw, string zhCn, string ja)
    {
        var values = new[] { en, zhTw, zhCn, ja };
        var result = new List<LocalizedString>();

        for (int i = 0; i < Languages.Length; i++)
        {
            result.Add(new LocalizedString
            {
                Id = id++,
                Module = module,
                ResourceKey = key,
                LanguageCode = Languages[i],
                Value = values[i],
                Description = $"{module}.{key}"
            });
        }

        return result;
    }
}
