// File responsibility: Health-condition result and individual explanatory findings.

namespace BatteryDoctor.Models;

/// <summary>
/// Health-condition result and individual explanatory findings.
/// </summary>
public sealed record HealthAssessment(
    double? Score,
    string ConditionKey,
    string SummaryKey,
    IReadOnlyList<HealthFinding> Findings);

/// <summary>
/// Data/application type used by HealthAssessment.
/// </summary>
public sealed record HealthFinding(string Severity, string TitleKey, string DetailKey);
