namespace Issue98.Contracts;

public class SharedDto
{
    public Guid CorrelationId { get; set; }

    public string Payload { get; set; } = string.Empty;
}
