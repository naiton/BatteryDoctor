// File responsibility: UI option describing a Quick, Standard, Deep, or Collapse Watch test profile.

namespace BatteryDoctor.Models;

/// <summary>
/// UI option describing a Quick, Standard, Deep, or Collapse Watch test profile.
/// </summary>
public sealed record TestProfileOption(
    string Code,
    string DisplayName,
    int TargetMinutes,
    bool CollapseWatch = false);
