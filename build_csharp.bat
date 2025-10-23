@echo off
echo 开始构建C# WPF版本的WorkTime...

REM 检查.NET SDK是否安装
dotnet --version >nul 2>&1
if %errorlevel% neq 0 (
    echo 错误: 未找到.NET SDK，请先安装.NET 8.0 SDK
    pause
    exit /b 1
)

REM 恢复NuGet包
echo 正在恢复NuGet包...
dotnet restore

REM 构建项目
echo 正在构建项目...
dotnet build --configuration Release

if %errorlevel% neq 0 (
    echo 构建失败！
    pause
    exit /b 1
)

REM 发布项目
echo 正在发布项目...
dotnet publish --configuration Release --runtime win-x64 --self-contained true --output ./publish

if %errorlevel% neq 0 (
    echo 发布失败！
    pause
    exit /b 1
)

echo 构建完成！可执行文件位于 publish 目录中
echo 运行 publish\WorkTimeWPF.exe 启动应用程序
pause
