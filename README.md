# 校园网自动认证

面向 Windows 10/11 的校园网托盘工具。连接目标 Wi-Fi 且无法访问指定 HTTPS 地址时，应用使用 Playwright 启动系统 Microsoft Edge 的独立校园网配置，自动填写凭据并执行录制的“登录 -> 运营商”操作。

## 使用流程

1. 打开设置，确认默认 Wi-Fi `NSU-SDN`、入口 `http://2.2.2.2` 和检测地址 `https://www.yuanshen.com`。
2. 选择或输入运营商。
3. 账号密码可保存到 Windows 凭据管理器；也可以在专用 Edge 中保存，浏览器自动填充优先。
4. 点击“重新录制”，按状态提示依次点击账号框、密码框、登录按钮和运营商按钮。
5. 点击“测试认证”。成功后程序会关闭自动化 Edge，平时驻留系统托盘。

## 隐私

普通配置文件不保存账号或密码。凭据只保存在 Windows 凭据管理器或 Edge 专用配置中；程序默认不生成截图、视频或 Playwright Trace。

## 本地构建

项目使用 `.dotnet\dotnet.exe`（SDK 8.0.425）和根目录 `NuGet.Config`。发布命令：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
```

脚本会生成自包含应用，并使用纯 .NET 安装器生成 `artifacts\installer\校园网自动认证-Setup.exe`。安装器写入当前用户目录，不要求管理员权限。
