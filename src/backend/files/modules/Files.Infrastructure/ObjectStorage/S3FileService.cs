using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Files.Domain;
using Microsoft.Extensions.Configuration;

namespace Files.Infrastructure;

public class S3FileService(IAmazonS3 s3Client, IConfiguration configuration) : IFileService
{
    private string Bucket =>
        configuration["S3_BUCKET"]!;

    public async Task<string> SaveAsync(string fileName, byte[] fileContent,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var fileHash = Convert.ToHexString(SHA1.HashData(fileContent)).ToLower();

            var fileKey = $"{fileHash[..2]}/{fileHash}{Path.GetExtension(fileName)}";

            using var ms = new MemoryStream(fileContent);
            var response = await s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = Bucket,
                InputStream = ms,
                CannedACL = S3CannedACL.PublicRead,
                StorageClass = S3StorageClass.Standard,
                Key = fileKey
            }, cancellationToken);

            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(
                    $"S3 storage error: unable put object: http status code {response.HttpStatusCode}");

            return fileKey;
        }
        catch (AmazonS3Exception ex)
        {
            throw new ExcecuteCommandException(ex.StatusCode, ex.Message);
        }
    }
}