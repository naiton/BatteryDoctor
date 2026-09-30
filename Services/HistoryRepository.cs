using System.IO;
using BatteryDoctor.Models;
using Microsoft.Data.Sqlite;

// File responsibility: SQLite persistence layer for long-term battery health and trend samples.

namespace BatteryDoctor.Services;

/// <summary>
/// SQLite persistence layer for long-term battery health and trend samples.
/// </summary>
public sealed class HistoryRepository
{
    private readonly string _dbPath;

    /// <summary>
    /// Opens the portable SQLite location and ensures the history schema is ready before use.
    /// </summary>
    public HistoryRepository()
    {
        Directory.CreateDirectory(PortablePaths.DataDirectory);
        _dbPath = PortablePaths.DatabasePath;
        Initialize();
    }

    private string ConnectionString => $"Data Source={_dbPath}";

    /// <summary>
    /// Creates the history table/index and performs additive schema upgrades for newer telemetry fields.
    /// </summary>
    private void Initialize()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS battery_samples (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    captured_at TEXT NOT NULL,
                    design_capacity_mwh INTEGER NULL,
                    full_charge_capacity_mwh INTEGER NULL,
                    remaining_capacity_mwh INTEGER NULL,
                    cycle_count INTEGER NULL,
                    voltage_mv INTEGER NULL,
                    charge_percent INTEGER NULL,
                    charging INTEGER NULL,
                    discharging INTEGER NULL
                );
                CREATE INDEX IF NOT EXISTS ix_battery_samples_captured_at
                ON battery_samples(captured_at DESC);
                """;
            cmd.ExecuteNonQuery();
        }

        AddColumnIfMissing(connection, "charge_rate_mw", "INTEGER NULL");
        AddColumnIfMissing(connection, "discharge_rate_mw", "INTEGER NULL");
        AddColumnIfMissing(connection, "power_online", "INTEGER NULL");
        AddColumnIfMissing(connection, "temperature_c", "REAL NULL");
        AddColumnIfMissing(connection, "estimated_current_a", "REAL NULL");
    }

    /// <summary>
    /// Checks PRAGMA table_info and adds a column only when an older database schema does not contain it.
    /// </summary>
    private static void AddColumnIfMissing(SqliteConnection connection, string column, string definition)
    {
        var exists = false;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA table_info(battery_samples);";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }

        if (exists) return;
        using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE battery_samples ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    /// <summary>
    /// Persists one normalized battery snapshot into SQLite using parameters and nullable-to-DB conversion.
    /// </summary>
    public async Task AddAsync(BatterySnapshot s, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO battery_samples (
                captured_at, design_capacity_mwh, full_charge_capacity_mwh,
                remaining_capacity_mwh, cycle_count, voltage_mv, charge_percent,
                charging, discharging, charge_rate_mw, discharge_rate_mw, power_online,
                temperature_c, estimated_current_a
            ) VALUES (
                $captured, $design, $full, $remaining, $cycle, $voltage, $percent,
                $charging, $discharging, $chargeRate, $dischargeRate, $powerOnline,
                $temperatureC, $estimatedCurrentA
            );
            """;
        cmd.Parameters.AddWithValue("$captured", s.CapturedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$design", Db(s.DesignCapacityMWh));
        cmd.Parameters.AddWithValue("$full", Db(s.FullChargeCapacityMWh));
        cmd.Parameters.AddWithValue("$remaining", Db(s.RemainingCapacityMWh));
        cmd.Parameters.AddWithValue("$cycle", Db(s.CycleCount));
        cmd.Parameters.AddWithValue("$voltage", Db(s.VoltageMV));
        cmd.Parameters.AddWithValue("$percent", Db(s.EstimatedChargePercent));
        cmd.Parameters.AddWithValue("$charging", Db(s.Charging));
        cmd.Parameters.AddWithValue("$discharging", Db(s.Discharging));
        cmd.Parameters.AddWithValue("$chargeRate", Db(s.ChargeRateMW));
        cmd.Parameters.AddWithValue("$dischargeRate", Db(s.DischargeRateMW));
        cmd.Parameters.AddWithValue("$powerOnline", Db(s.PowerOnline));
        cmd.Parameters.AddWithValue("$temperatureC", Db(s.TemperatureC));
        cmd.Parameters.AddWithValue("$estimatedCurrentA", Db(ElectricalDiagnosticsAnalyzer.EstimateSignedCurrentA(s)));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Returns the most recent health/charge history points for compact history displays.
    /// </summary>
    public async Task<IReadOnlyList<HistoryPoint>> GetRecentAsync(int limit = 30, CancellationToken cancellationToken = default)
    {
        var result = new List<HistoryPoint>();
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT captured_at, design_capacity_mwh, full_charge_capacity_mwh, charge_percent
            FROM battery_samples
            ORDER BY captured_at DESC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 365));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var captured = DateTimeOffset.Parse(reader.GetString(0));
            double? health = null;
            if (!reader.IsDBNull(1) && !reader.IsDBNull(2))
            {
                var design = reader.GetInt64(1);
                var full = reader.GetInt64(2);
                if (design > 0) health = Math.Clamp(full * 100.0 / design, 0, 120);
            }
            int? charge = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            result.Add(new HistoryPoint(captured, health, charge));
        }
        return result;
    }

    /// <summary>
    /// Returns one representative sample per day for the requested trend window, including capacity, voltage, and signed power.
    /// </summary>
    public async Task<IReadOnlyList<HistorySamplePoint>> GetTrendAsync(int days = 90, int limit = 1000, CancellationToken cancellationToken = default)
    {
        var result = new List<HistorySamplePoint>();
        var cutoff = DateTimeOffset.Now.AddDays(-Math.Clamp(days, 1, 3650));

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT b.captured_at, b.design_capacity_mwh, b.full_charge_capacity_mwh, b.charge_percent,
                   b.voltage_mv, b.charge_rate_mw, b.discharge_rate_mw, b.charging, b.discharging,
                   b.temperature_c, b.estimated_current_a
            FROM battery_samples b
            INNER JOIN (
                SELECT substr(captured_at, 1, 10) AS sample_day, MAX(captured_at) AS max_captured_at
                FROM battery_samples
                WHERE captured_at >= $cutoff
                GROUP BY substr(captured_at, 1, 10)
            ) daily ON b.captured_at = daily.max_captured_at
            ORDER BY b.captured_at ASC
            LIMIT $limit;
            """;
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        cmd.Parameters.AddWithValue("$limit", Math.Clamp(limit, 10, 5000));

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var captured = DateTimeOffset.Parse(reader.GetString(0));
            double? health = null;
            double? fullWh = null;
            if (!reader.IsDBNull(2)) fullWh = reader.GetInt64(2) / 1000.0;
            if (!reader.IsDBNull(1) && !reader.IsDBNull(2))
            {
                var design = reader.GetInt64(1);
                var full = reader.GetInt64(2);
                if (design > 0) health = Math.Clamp(full * 100.0 / design, 0, 120);
            }

            int? charge = reader.IsDBNull(3) ? null : reader.GetInt32(3);
            double? voltage = reader.IsDBNull(4) ? null : reader.GetInt64(4) / 1000.0;
            int? chargeRate = reader.IsDBNull(5) ? null : reader.GetInt32(5);
            int? dischargeRate = reader.IsDBNull(6) ? null : reader.GetInt32(6);
            bool charging = !reader.IsDBNull(7) && reader.GetInt32(7) != 0;
            bool discharging = !reader.IsDBNull(8) && reader.GetInt32(8) != 0;
            double? temperatureC = reader.IsDBNull(9) ? null : reader.GetDouble(9);
            double? estimatedCurrentA = reader.IsDBNull(10) ? null : reader.GetDouble(10);
            double? power = null;
            if (discharging && dischargeRate is { } d) power = -Math.Abs(d) / 1000.0;
            else if (charging && chargeRate is { } c) power = Math.Abs(c) / 1000.0;

            result.Add(new HistorySamplePoint(captured, health, charge, fullWh, voltage, power, temperatureC, estimatedCurrentA));
        }
        return result;
    }

    public string DatabasePath => _dbPath;

    /// <summary>
    /// Converts nullable CLR values into DBNull.Value for parameterized SQLite inserts.
    /// </summary>
    private static object Db(object? value) => value ?? DBNull.Value;
}
