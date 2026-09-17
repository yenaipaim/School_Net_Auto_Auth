# 校园网自动认证

面向 Windows 10/11 的校园网托盘工具。连接目标 Wi-Fi 且无法访问指定 HTTPS 地址时，应用使用 Playwright[^playwright] 启动系统 Microsoft Edge 的独立校园网配置，自动填写凭据并按首次录制的点击顺序完成认证。

## 使用流程

视频教程[SANN演示视频_哔哩哔哩_bilibili](https://www.bilibili.com/video/BV12eeg6bEcg/?vd_source=6bd5ae9c5fdcebb1777c3a0dd437f087)

1. 打开设置，确认默认 Wi-Fi `NSU-SDN`、入口 `http://2.2.2.2` 和检测地址 `https://www.yuanshen.com`。
2. 账号密码可保存到 Windows 凭据管理器；也可以在专用 Edge 中保存，浏览器自动填充优先。
3. 点击“开始录制”，先按提示选择账号框和密码框，再按真实认证流程完成所有必要点击。
4. 回到软件点击“结束录制”；如有误点，可删除选中步骤或清除后重新录制。
5. 点击“立即认证”进行测试。成功后程序会关闭自动化 Edge，平时驻留系统托盘。

## 隐私

普通配置文件不保存账号或密码。凭据只保存在 Windows 凭据管理器或 Edge 专用配置中；程序默认不生成截图、视频或 Playwright Trace。

# 疑问

有任何疑问或者简介请加入下方群聊

<img src="img\QQ.png" width = "33%" />

## 本地构建

项目使用 `.dotnet\dotnet.exe`（SDK 8.0.425）和根目录 `NuGet.Config`。发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

脚本会生成自包含应用，并使用纯 .NET 安装器生成 `artifacts\installer\校园网自动认证-Setup.exe`。安装器写入当前用户目录，不要求管理员权限。

## 卸载

可从 Windows“已安装的应用”或开始菜单中的“卸载校园网自动认证”运行专用卸载程序。卸载器默认保留账号凭据、录制配置和 Edge 专用登录状态；只有主动勾选“同时删除用户数据”时才会一并清除。

[^playwright]: 本项目自动化测试基于 Playwright 技术：[Microsoft Playwright](https://github.com/microsoft/playwright)。
