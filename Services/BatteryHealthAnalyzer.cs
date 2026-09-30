using BatteryDoctor.Models;

// File responsibility: Converts firmware-reported capacity/health data into user-facing health findings.

namespace BatteryDoctor.Services;

/// <summary>
/// Converts firmware-reported capacity/health data into user-facing health findings.
/// </summary>
public sealed class BatteryHealthAnalyzer
{
    /// <summary>
    /// Classifies firmware-reported health into condition bands and builds explanatory findings without treating the score as proof of electrical stability.
    /// </summary>
    public HealthAssessment Analyze(BatterySnapshot s, double? healthOverride = null)
    {
        var findings = new List<HealthFinding>();
        var health = healthOverride ?? s.HealthPercent;

        string condition;
        string summary;

        if (health is null)
        {
            condition = "ConditionUnknown";
            summary = "SummaryCapacityUnavailable";
            findings.Add(new("info", "FindingLimitedDataTitle", "FindingLimitedDataDetail"));
        }
        else if (health >= 90)
        {
            condition = "ConditionExcellent";
            summary = "SummaryExcellent";
            findings.Add(new("good", "FindingCapacityGoodTitle", "FindingCapacityGoodDetail"));
        }
        else if (health >= 80)
        {
            condition = "ConditionGood";
            summary = "SummaryGood";
            findings.Add(new("good", "FindingCapacityGoodTitle", "FindingCapacityGoodDetail"));
        }
        else if (health >= 70)
        {
            condition = "ConditionFair";
            summary = "SummaryFair";
            findings.Add(new("warn", "FindingCapacityWearTitle", "FindingCapacityWearDetail"));
        }
        else if (health >= 60)
        {
            condition = "ConditionPoor";
            summary = "SummaryPoor";
            findings.Add(new("warn", "FindingCapacityPoorTitle", "FindingCapacityPoorDetail"));
        }
        else
        {
            condition = "ConditionReplaceSoon";
            summary = "SummaryReplaceSoon";
            findings.Add(new("bad", "FindingCapacityCriticalTitle", "FindingCapacityCriticalDetail"));
        }

        if (s.Critical == true)
            findings.Add(new("bad", "FindingCriticalTitle", "FindingCriticalDetail"));

        if (s.CycleCount is null)
            findings.Add(new("info", "FindingCycleUnavailableTitle", "FindingCycleUnavailableDetail"));
        else
            findings.Add(new("info", "FindingCycleTitle", "FindingCycleDetail"));

        if (s.VoltageMV is > 0)
            findings.Add(new("good", "FindingVoltageTitle", "FindingVoltageDetail"));
        else
            findings.Add(new("info", "FindingVoltageUnavailableTitle", "FindingVoltageUnavailableDetail"));

        return new HealthAssessment(health, condition, summary, findings);
    }
}
