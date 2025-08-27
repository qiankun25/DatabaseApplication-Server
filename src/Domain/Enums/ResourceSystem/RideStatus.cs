namespace DbApp.Domain.Enums.ResourceSystem;

/// <summary>
/// 游乐设施运行状态
/// </summary>
public enum RideStatus
{
    /// <summary>
    /// 运行中
    /// </summary>
    Operating = 1,

    /// <summary>
    /// 维护中
    /// </summary>
    Maintenance = 2,

    /// <summary>
    /// 已关闭
    /// </summary>
    Closed = 3,

    /// <summary>
    /// 测试中
    /// </summary>
    Testing = 4
}
