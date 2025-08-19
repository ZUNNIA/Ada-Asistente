public class StreamedBackendResponse
{
    public string? TextChunk { get; set; }
    public bool IsDone { get; set; }
    public string? ThreadId { get; set; }
    public string? ModelUsed { get; set; }
    public string? VectorStoreId { get; set; }
}