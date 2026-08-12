工業電腦規格 (如下表)

| 規格項目        | 詳細數據                                       |
| ----------- | ------------------------------------------ |
| CPU 架構      | Zen 4（4nm 製程）                              |
| 核心 / 執行緒    | 6 核心 / 12 執行緒                              |
| 時脈          | 時脈 4.3 GHz / Boost 可達 5.0 GHz              |
| 快取          | L2: 6 MB / L3: 16 MB                       |
| 預設 TDP      | 35W – 54W（可由廠商自訂）                          |
| 內建顯卡 (iGPU) | AMD Radeon™ 760M (RDNA 3, 8 CUs, 2600 MHz) |
| AI 引擎       | 整合 Ryzen AI (XDNA, 約 10 TOPS)              |
| 記憶體支援       | DDR5-5600 / LPDDR5x-7500                   |
| 傳輸介面        | PCIe 4.0, USB4 (40Gbps)                    |

    - 記憶體: 16 GB

    - SSD: 256GB



觸控螢幕規格

| 型号：                              | HH-101-XSQ-A-V1              |
| -------------------------------- | ---------------------------- |
| 基本参数                             |                              |
| 尺寸:                              | 10.1寸                        |
| 宽屏:                              | 是                            |
| 触摸类型:                            | 非触摸/电容触摸/电阻触摸                |
| 黑白响应时间:                          | 5ms                          |
| 灰阶响应时间:                          | 3ms                          |
| 点距(mm):                          | 0.264                        |
| 接口                               | VGA+HDMI+USB触摸接口+DC 12V 电源接口 |
| 液晶屏                              |                              |
| 分辨率:                             | 1920*1200                    |
| 亮度:                              | 300尼特                        |
| 对比度:                             | 1000：1                       |
| 背光寿命:                            | 3万小时                         |
| 显示区域:                            | 216*135mm                    |
| 比例:                              | 16:10                        |
| 工作环境                             |                              |
| 工作温度:                            | -20℃～70℃                     |
| 工作湿度:                            | 10%～80%                      |
| 功率                               | 小于等于 15W                     |
| 触摸屏                              | 电容触摸屏（可支持戴手套，主动笔，防水，防油）触摸    |
| 防水等级:                            | 表面IP65防水                     |
|                                  | 工业级电容触摸屏，超强抗电磁干扰             |
| 防静电: | 接触6KV 空气8KV                  |
| 触摸点数:                            | 最大支持10点触摸                    |
| 支持系统                             | windows,liunx,Android        |



作業系統: Windows 11 IoT Enterprise LTSC

硬體供應商提供工業電腦時會將電腦設定為兩個partiotions
    - os partioton (windows installation)
    - app and data partition (user can install app and save data, TRIO Application、Data、Variable Parameters)
    - os partition size: 100GB (暫定, 後續機種可能會再行調整)

Device Lockdown Mode
    - Enable Device Lockdown Mode
    - Enable Keyboard Filter
    - Enable Assigned Access
    - Enable Shell Launcher
    - Enabled AppLocker
    - Disable Screen Saver
    - Enable Custom

Remote Desktop Setting
    - Enable Remote Desktop
    - Enable UDP Protocol

Account:
    - Admin: 視設備出廠時的設定再決定後續的調整策略
        - 透過 RDP 遠端管理作業系統內容設定與調整
    - TRIO Application User: AppRunner
        - AppRunner in Users local group
        - password = "i am apprunner of plexbio on trio"
        - fullname = "TRIO V2 App Runner"
        - AppRunner disable to remote desktop and cannot login as remote desktop user
        - description = "TRIO V2"
        - AppRunner 密碼永久有效, 不會過期, 使用者無法變更.