// ------------------------------------------------------------- //
// Data Transfer Object for device reporting configuration       //
// Contains all parameters needed to configure Zigbee reporting  //
// ------------------------------------------------------------- //

namespace Elijah.Domain.Models;

// -------------------------------------------------- //
// Constructor initializing all reporting parameters  //
// -------------------------------------------------- //
public class ReportConfig(
    string? address,
    string? cluster,
    string? attribute,
    string? maximumReportInterval,
    string? minimumReportInterval,
    string? reportableChange,
    string? endpoint)
{
    // Device IEEE address
    public string? Address { get; set; } = address;

    // Zigbee cluster ID (e.g., "msTemperatureMeasurement")
    public string? Cluster { get; set; } = cluster;

    // Attribute within the cluster (e.g., "measuredValue")
    public string? Attribute { get; set; } = attribute;

    // Maximum time between reports (seconds)
    public string? MaximumReportInterval { get; set; } = maximumReportInterval;

    // Minimum time between reports (seconds)
    public string? MinimumReportInterval { get; set; } = minimumReportInterval;

    // Minimum change required to trigger a report
    public string? ReportableChange { get; set; } = reportableChange;

    // Zigbee endpoint number (e.g., "1")
    public string? Endpoint { get; set; } = endpoint;
    
}