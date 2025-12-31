namespace SimLogger.Core.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public int ThreadId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RawLine { get; set; } = string.Empty;
}
