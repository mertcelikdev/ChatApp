using Amazon.S3;
using Amazon.S3.Model;

namespace ChatApp.Services;

public interface IS3Service
{
    Task<string> UploadProfileImageAsync(IFormFile file, int userId);
    Task<string> UploadGroupImageAsync(IFormFile file, int groupId);
    Task<bool> DeleteImageAsync(string imageKey);
    Task<string> GetImageUrlAsync(string imageKey);
    string GetDefaultAvatarUrl();
}

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<S3Service> _logger;
    private readonly string _bucketName;
    private readonly string _baseUrl;

    public S3Service(IAmazonS3 s3Client, IConfiguration configuration, ILogger<S3Service> logger)
    {
        _s3Client = s3Client;
        _configuration = configuration;
        _logger = logger;
        _bucketName = _configuration["AWS:S3:BucketName"] ?? throw new InvalidOperationException("S3 bucket name not configured");
        _baseUrl = _configuration["AWS:S3:BaseUrl"] ?? throw new InvalidOperationException("S3 base URL not configured");
        
        _logger.LogInformation($"🪣 S3Service initialized - Bucket: {_bucketName}, BaseUrl: {_baseUrl}");
    }

    public async Task<string> UploadProfileImageAsync(IFormFile file, int userId)
    {
        try
        {
            _logger.LogInformation($"📸 Starting profile image upload for user {userId}");
            
            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("❌ File is null or empty");
                throw new ArgumentException("Dosya boş olamaz");
            }

            // Dosya uzantısını kontrol et
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            
            if (!allowedExtensions.Contains(extension))
            {
                _logger.LogWarning($"❌ Unsupported file extension: {extension}");
                throw new ArgumentException($"Desteklenmeyen dosya formatı. İzin verilen: {string.Join(", ", allowedExtensions)}");
            }

            // Dosya boyutunu kontrol et (5MB limit)
            if (file.Length > 5 * 1024 * 1024)
            {
                _logger.LogWarning($"❌ File too large: {file.Length} bytes");
                throw new ArgumentException("Dosya boyutu 5MB'dan büyük olamaz");
            }

            // Benzersiz dosya adı oluştur
            var fileName = $"profiles/user_{userId}_{Guid.NewGuid()}{extension}";
            _logger.LogInformation($"📁 Generated filename: {fileName}");

            using var stream = file.OpenReadStream();
            
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = fileName,
                InputStream = stream,
                ContentType = file.ContentType,
                // CannedACL = S3CannedACL.PublicRead, // ACL kaldırıldı - Bucket Policy kullanılacak
                Metadata =
                {
                    ["uploaded-by"] = userId.ToString(),
                    ["upload-date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["file-type"] = "profile-image"
                }
            };

            _logger.LogInformation($"🚀 Uploading to S3 bucket: {_bucketName}");
            var response = await _s3Client.PutObjectAsync(request);
            _logger.LogInformation($"📡 S3 Response: {response.HttpStatusCode}");

            if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
            {
                var url = $"{_baseUrl}{fileName}";
                _logger.LogInformation($"✅ Upload successful! URL: {url}");
                return url;
            }

            throw new Exception($"S3 yükleme hatası: {response.HttpStatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"❌ Error uploading profile image for user {userId}");
            throw;
        }
    }

    public async Task<string> UploadGroupImageAsync(IFormFile file, int groupId)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("Dosya boş olamaz");

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
            throw new ArgumentException($"Desteklenmeyen dosya formatı. İzin verilen: {string.Join(", ", allowedExtensions)}");

        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("Dosya boyutu 5MB'dan büyük olamaz");

        var fileName = $"groups/group_{groupId}_{Guid.NewGuid()}{extension}";

        using var stream = file.OpenReadStream();
        
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = stream,
            ContentType = file.ContentType,
            // CannedACL = S3CannedACL.PublicRead, // ACL kaldırıldı
            Metadata =
            {
                ["uploaded-for-group"] = groupId.ToString(),
                ["upload-date"] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                ["file-type"] = "group-image"
            }
        };

        var response = await _s3Client.PutObjectAsync(request);

        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        {
            return $"{_baseUrl}{fileName}";
        }

        throw new Exception($"S3 yükleme hatası: {response.HttpStatusCode}");
    }

    public async Task<bool> DeleteImageAsync(string imageKey)
    {
        try
        {
            // URL'den key'i çıkar
            var key = imageKey.Replace(_baseUrl, "");
            
            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = key
            };

            var response = await _s3Client.DeleteObjectAsync(request);
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task<string> GetImageUrlAsync(string imageKey)
    {
        if (string.IsNullOrEmpty(imageKey))
            return Task.FromResult(GetDefaultAvatarUrl());

        try
        {
            // URL zaten tam ise direkt döndür
            if (imageKey.StartsWith("http"))
                return Task.FromResult(imageKey);

            // Key'den URL oluştur
            return Task.FromResult($"{_baseUrl}{imageKey}");
        }
        catch
        {
            return Task.FromResult(GetDefaultAvatarUrl());
        }
    }

    public string GetDefaultAvatarUrl()
    {
        // Varsayılan avatar URL'si (gravatar tarzı)
        return "https://ui-avatars.com/api/?background=0D8ABC&color=fff&size=128&rounded=true&name=User";
    }
}
