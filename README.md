# 硅钢附着力测试 WinForms 上位机

本项目采用传统的 `.NET Framework 4.8` WinForms 工程格式，可由 Visual Studio 2022/2026 直接识别，已经包含：

- SQLite 初始化、三类用户、登录及操作日志；
- 仿真 PLC 与 S7.NET 实体 PLC 双驱动；
- 按《PC交互表20260708.xlsx》绑定 DB5120/DB4120 关键地址；
- 生产主界面、S1~S4 状态、模式/启停/回原位/复位命令；
- 管理员调试页和权限隐藏；
- PLC 轮询、脉冲防抖与断线故障入库。
- Windows Per-Monitor V2 高 DPI 感知，适配 100%/125%/150% 缩放，避免界面位图拉伸模糊。

生产主界面已按现场 WinCC“装配站”参考布局重构：顶部原位/模式状态、中部四工位扫码拍照交互、下部步骤和运行/就绪/完成灯、右侧连续/启动，以及底部黄色模式操作栏。

主界面包含由仿真/PLC快照驱动的八节点流程条：AGV送料、S1来料、读取二维码、二维码校验、相机拍照、视觉分类、工位加工、等待来料。颜色约定为绿色已完成、蓝色执行中、白色未执行、黄色暂停、红色故障。

默认构建为 `SimulationOnly=true` 的零外部依赖仿真版，不需要先安装 S7.NET 或 SQLite NuGet 包即可生成、登录和运行。实体设备发布时再执行 `msbuild /p:SimulationOnly=false`，还原 S7netplus 与 System.Data.SQLite.Core 后生成。

## 可视化前端

登录页、生产主界面、调试诊断页均采用标准 WinForms `Form.cs + Form.Designer.cs` 结构。在 Visual Studio 的解决方案资源管理器中展开 `Forms`，双击 `LoginForm.cs`、`MainForm.cs` 或 `DebugForm.cs` 即可进入窗体设计器拖拽修改控件。前端预览图位于交付包的 `Previews` 文件夹。

## 默认账号

| 角色 | 账号 | 初始密码 |
|---|---|---|
| 操作员 | operator | 123456 |
| 电气调试员 | engineer | 123456 |
| 超级管理员 | admin | Admin@123 |

首次运行后请立即修改正式环境密码。

## 运行模式

`App.config` 默认 `PlcMode=Simulation`，不连接现场设备即可观察页面变化。实体 PLC 联调前将其改为 `S7`，PLC IP 已预填 `192.168.3.2`。机架/槽号目前按 `0/1` 配置，现场 CPU 若不同需要核对。

> 安全提示：实体模式下按钮会写入真实 PLC。首次联调必须在机械/电气安全条件满足、急停有效且现场工程师监护下进行。

## 系统设置文件

程序首次启动时会自动创建现场覆盖配置：

```text
src\SiliconSteelAdhesionTester\bin\Debug\Data\SystemSettings.xml
```

不同构建配置会使用对应输出目录下的 `Data` 文件夹。管理员或电气调试员也可以在程序的“系统设置 → 站点与接口”页面查看完整路径，并使用“打开目录”或“复制路径”。每次覆盖保存现有设置时会保留 `SystemSettings.xml.bak` 备份。

文件不存在时使用 `App.config` 和代码缺省值；文件格式损坏时程序不会直接崩溃，而会在设置窗口显示警告并回退到缺省值。仿真构建固定使用仿真 PLC，不能在界面中切换为实体 PLC。

## 构建

1. 完整解压压缩包，不能直接在压缩软件预览窗口里打开。
2. 确认 Visual Studio Installer 已安装“.NET 桌面开发”工作负载和“.NET Framework 4.8 开发工具”。
3. 使用 Visual Studio 打开根目录下的 `SiliconSteelAdhesionTester.sln`，也可直接打开 `src\SiliconSteelAdhesionTester\SiliconSteelAdhesionTester.csproj`。
4. 直接选择“生成解决方案”，然后按 F5；默认仿真版无需还原 NuGet 包。

交付包已包含通过编译和启动检查的 `bin\Debug\SiliconSteelAdhesionTester.exe`，也可直接双击根目录的 `启动仿真版.cmd` 运行。

不要把 `.sln` 单独拖出，否则解决方案会找不到 `src` 下的项目。命令行可运行：

```powershell
dotnet restore SiliconSteelAdhesionTester.sln
dotnet build SiliconSteelAdhesionTester.sln -c Debug
```

## 已确认的关键地址

- DB5120：整机模式、启动、暂停、回原位、复位；
- DB4120：四工位状态、自动步骤号、扫码/拍照握手；
- DB20：4 组伺服；
- DB1001：步进轴；
- DB8120：31 组气缸。

当前交互表没有给出二维码内容、视觉结果数值、产量和统一故障码的 DB 地址，因此首版没有虚构这些实体 PLC 点位；它们补齐后再接入生产业务服务。
