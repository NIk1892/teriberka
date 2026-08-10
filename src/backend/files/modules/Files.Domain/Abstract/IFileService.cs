namespace Files.Domain
{
    public interface IFileService
    {
        Task<string> SaveAsync(string fileName, byte[] fileContent,CancellationToken cancellationToken);
    }
}