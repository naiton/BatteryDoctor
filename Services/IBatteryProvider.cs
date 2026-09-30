using BatteryDoctor.Models;

// File responsibility: Abstraction for a source that can read one current battery snapshot.

namespace BatteryDoctor.Services;

/// <summary>
/// Abstraction for a source that can read one current battery snapshot.
/// </summary>
public interface IBatteryProvider
{
    Task<BatterySnapshot> ReadAsync(CancellationToken cancellationToken = default);
}
