目的: 透過自動安裝程序達成以下設定
說明: 過程中可能需要一到數次的重新開機, 請確保在重新開機後自動安裝程序可以繼續執行, 並在log中顯示目前執行到哪個步驟.
設定過程中的相關細節或結果請記錄在log中(必要的時候, 需要透過檢查機制來確認設定是否成功)
log路徑: (視後續的流程討論再決定, 但務必記錄下來, 當前預設為C:\OperationLog\, 該路徑只有admin有權限讀寫.)
準備執行的script過程中, 請確認是否需要重新開機, 如果需要而沒有特別提出, 請補充. 另外, 根據你的經驗, 哪些步驟會需要重新開機? 把這些步驟合併在一起, 減少重新開機的次數.

根據spec.md 建立AppRunner本機帳號
啟用Remote Desktop
    - 啟用防火牆
    - 在log中確認AppRunner account可以登入
    - 在log中顯示 (Get-NetFirewallRule -DisplayGroup "Remote Desktop") 結果
檢查當前使用的系統管理員帳號是否具有RDP權限
啟用WinRM
檢查當前所有local user的list (Get-LocalUser), 需要包含Name, Enabled, Description, SID
檢查所有group的list (Get-LocalGroup), 需要包含Name, Description, SID
檢查當前所有local group member的list (Get-LocalGroupMember), 需要包含Name, SID
啟用 Shell Launcher Feature (完成後重新開機一次)
