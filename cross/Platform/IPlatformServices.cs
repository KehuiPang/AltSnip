using System;

namespace AltSnip.Platform;

/// <summary>
/// 每个操作系统需要各自实现的原生能力：全局热键、屏幕捕获、图片剪贴板。
/// UI 与标注逻辑是可移植的共享层，只通过这个接口访问系统。
/// </summary>
public interface IPlatformServices
{
    string Name { get; }

    // 里程碑 1 起逐个实现（当前为占位）：
    // - 注册全局热键 Alt+A，回调触发截图
    // - 捕获整个（虚拟）屏幕为位图
    // - 把图片写入系统剪贴板
    //
    // 定义会随移植推进补上，先保持接口最小以让骨架编译通过。
}
