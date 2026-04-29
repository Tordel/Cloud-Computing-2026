using Google.Cloud.Storage.V1;
using Google.Apis.Storage.v1.Data;

namespace CloudNote.Services;

public class CloudStorageService(IConfiguration config)
{
    private readonly StorageClient _storageClient = StorageClient.Create();
    private readonly string _bucketName = config["GoogleCloud:StorageBucket"]
                                          ?? throw new InvalidOperationException("GoogleCloud:StorageBucket not configured.");

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        string objectName = $"attachments/{Guid.NewGuid()}_{SanitizeFileName(fileName)}";

        await _storageClient.UploadObjectAsync(
            bucket: _bucketName,
            objectName: objectName,
            contentType: contentType,
            source: fileStream,
            options: new UploadObjectOptions
            {
                PredefinedAcl = PredefinedObjectAcl.PublicRead
            });

        return $"https://storage.googleapis.com/{_bucketName}/{objectName}";
    }

    public async Task DeleteFileAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return;

        string prefix = $"https://storage.googleapis.com/{_bucketName}/";
        if (!fileUrl.StartsWith(prefix)) return;

        string objectName = fileUrl[prefix.Length..];
        try
        {
            await _storageClient.DeleteObjectAsync(_bucketName, objectName);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {

        }
    }

    public string GetSignedUrl(string objectName, TimeSpan duration)
    {
        UrlSigner urlSigner = UrlSigner.FromServiceAccountCredential(
            Google.Apis.Auth.OAuth2.GoogleCredential.GetApplicationDefault()
                .UnderlyingCredential as Google.Apis.Auth.OAuth2.ServiceAccountCredential
            ?? throw new InvalidOperationException("Application Default Credentials must be a service account."));

        return urlSigner.Sign(_bucketName, objectName, duration, HttpMethod.Get);
    }

    public async Task EnsureBucketExistsAsync(string projectId)
    {
        try
        {
            await _storageClient.GetBucketAsync(_bucketName);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await _storageClient.CreateBucketAsync(projectId, new Bucket
            {
                Name = _bucketName,
                Location = "EU",
                StorageClass = "STANDARD"
            });
        }
    }

    private static string SanitizeFileName(string fileName)
        => System.Text.RegularExpressions.Regex.Replace(
            Path.GetFileName(fileName), @"[^a-zA-Z0-9._-]", "_");
}
