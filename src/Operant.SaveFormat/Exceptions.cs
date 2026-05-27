namespace Operant.SaveFormat;

public class SaveFormatException : Exception
{
    public SaveFormatException(string message) : base(message) { }
    public SaveFormatException(string message, Exception inner) : base(message, inner) { }
}

public class SaveChecksumException : SaveFormatException
{
    public string FileChecksum { get; }
    public string ComputedChecksum { get; }

    public SaveChecksumException(string chunk, string fileChecksum, string computedChecksum)
        : base($"{chunk} checksum mismatch: file={fileChecksum} computed={computedChecksum}")
    {
        FileChecksum = fileChecksum;
        ComputedChecksum = computedChecksum;
    }
}
