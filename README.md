# 校园网自动认证——适配浏览器认证校园网

### School_Net_Auto_Auth——SNAA

面向 Windows 10/11 的校园网托盘工具。连接目标 Wi-Fi 且无法访问指定 HTTPS 地址时，应用使用 Playwright[^playwright] 启动系统 Microsoft Edge 的独立校园网配置，自动填写凭据并按首次录制的点击顺序完成认证。

## 使用流程

视频教程[SANN演示视频_哔哩哔哩_bilibili](https://www.bilibili.com/video/BV12eeg6bEcg/?vd_source=6bd5ae9c5fdcebb1777c3a0dd437f087)

1. 打开设置，确认默认 Wi-Fi SSID、校园网入口和检测地址。
2. 账号密码可保存到 Windows 凭据管理器；也可以在专用 Edge 中保存，浏览器自动填充优先。
3. 点击“开始录制”，先按提示选择账号框和密码框，再按真实认证流程完成所有必要点击。
4. 回到软件点击“结束录制”；如有误点，可删除选中步骤或清除后重新录制。
5. 点击“立即认证”进行测试。成功后程序会关闭自动化 Edge，平时驻留系统托盘。

认证期间如果网页要求电话验证或提示在线设备达到上限，程序会先复查联网状态。尚未联网时，右下角通知会显示经过脱敏和截断的网页提示，并保留当前 Edge 页面供处理；程序在 5 分钟后自动重试。点击“立即认证”可以跳过等待并马上重试。连接成功后，桌面右下角会显示成功通知。

主界面使用 WinUI 3 和 NavigationView，分为网络认证、课程表和通用设置。网络认证包含状态、认证流程、网络设置和高级设置；通用设置提供版本检查、作者链接、背景图像和交流群信息。

## 课程表

课程表支持手动维护课程和从文件导入。软件会按照“学期开始日期”计算当前周次，并显示选中日期所在周的课程；主视图按 14 节课的节次与时间排列，课程会根据开始、结束时间跨行显示。

### 支持的上传格式

| 格式 | 用途 | 文件要求 |
| --- | --- | --- |
| `.docx` | 导入 Word 课程表 | 文件内需包含原生 Word 表格，表格至少有 2 个星期表头，并包含类似 `第1-2节 (08:00-09:40)` 的节次时间；课程单元格需包含课程名称和类似 `1-16周`、`1-16周(单)` 的周次信息。最大 20 MiB，不支持旧版 `.doc`。 |
| `.xls` / `.xlsx` | 导入 Excel 课程表 | 工作表需包含星期表头和类似 `第1节(08:20-09:00)` 的节次时间；课程单元格需包含课程名称和类似 `1-16周`、`1-16周(单)` 的周次信息。支持旧版 `.xls` 与新版 `.xlsx`，最大 20 MiB。 |
| `.json` | 导入或导出完整课程表 | 推荐使用本软件导出的 JSON 文件，包含学期开始日期及完整课程字段。 |
| `.csv` | 导入或导出课程列表 | 必须包含 `Name`、`Weekday`、`StartTime`、`EndTime`、`FirstWeek`、`LastWeek` 列；可选 `Teacher`、`Location`、`Parity` 列。时间格式为 `HH:mm`，`Weekday` 使用 `Monday` 至 `Sunday`，`Parity` 使用 `All`、`Odd`、`Even`。 |
| `.ics` | 导入或导出日历事件 | 支持标准 `VCALENDAR`/`VEVENT` 事件，课程应包含开始时间、结束时间、课程名称和每周重复规则。导入 ICS 时会根据最早课程日期推算学期开始日期。 |

### 操作方式

1. 在左侧导航中打开“课程表”。
2. 在“学期开始”下拉列表中选择实际开学日期。导入 DOCX、Excel 和 CSV 前应先确认该日期。
3. 点击“导入”，选择 `.docx`、`.xls`、`.xlsx`、`.json`、`.csv` 或 `.ics` 文件。导入会替换当前课程表，确认后才会写入。
4. 使用“查看日期”切换要查看的周次，或点击“今天”回到当前日期；右侧会显示当天课程和下一节课。
5. 点击“新增”手动添加课程，填写课程名称、教师、地点、星期、开始/结束时间、周次和单双周；选中已有课程后可直接修改并点击“保存课程”。
6. 选中课程后点击删除按钮可移除课程。点击“显示周末”可切换周一到周五或完整一周视图。
7. 点击 `JSON`、`CSV` 或 `ICS` 按钮可导出当前课程表，方便备份或导入其他日历软件。

课程表默认保存在 `%LOCALAPPDATA%\SchoolNetAutoAuth\schedules\current.json`，不会上传到云端。

## 隐私

普通配置文件不保存账号或密码。凭据只保存在 Windows 凭据管理器或 Edge 专用配置中；程序默认不生成截图、视频或 Playwright Trace。

运行日志位于 `%LOCALAPPDATA%\SchoolNetAutoAuth\SchoolNetAutoAuth.log`，最大 1 MiB，超限后自动删除最早内容。日志仅记录固定状态和原因码，不记录账号、密码、Cookie、Token 或网页原文。

课程表的导入和导出仅在用户主动选择文件时执行。

# 疑问

有任何疑问或者简介请加入下方群聊
群号:976295578

<img src="img\QQ.png" width = "33%" />

## 本地构建

项目使用 .NET 10 LTS、Windows App SDK、CommunityToolkit.Mvvm 和根目录 `NuGet.Config`。发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

脚本会生成自包含应用，并使用纯 .NET 安装器生成 `artifacts\installer\校园网自动认证-Setup.exe`。安装器写入当前用户目录，不要求管理员权限。

## 卸载

可从 Windows“已安装的应用”或开始菜单中的“卸载校园网自动认证”运行主程序内置的卸载入口。卸载默认保留账号凭据、录制配置、日志和 Edge 专用登录状态；只有主动勾选“同时删除用户数据”时才会一并清除。

[^playwright]: 本项目自动化测试基于 Playwright 技术：[Microsoft Playwright](https://github.com/microsoft/playwright)。
