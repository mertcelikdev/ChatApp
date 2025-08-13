using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ChatApp.Services;
using Amazon.S3;
using Amazon.S3.Model;

namespace ChatApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class S3TestController : ControllerBase
    {
        private readonly IAmazonS3 _s3Client;
        private readonly IS3Service _s3Service;
        private readonly IConfiguration _configuration;
        private readonly ILogger<S3TestController> _logger;

        public S3TestController(IAmazonS3 s3Client, IS3Service s3Service, IConfiguration configuration, ILogger<S3TestController> logger)
        {
            _s3Client = s3Client;
            _s3Service = s3Service;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpGet("config-info")]
        public IActionResult GetConfigInfo()
        {
            try
            {
                var bucketName = _configuration["AWS:S3:BucketName"];
                var baseUrl = _configuration["AWS:S3:BaseUrl"];
                var region = _configuration["AWS:Region"];
                var accessKey = _configuration["AWS:AccessKey"];

                return Ok(new
                {
                    success = true,
                    bucketName = bucketName,
                    baseUrl = baseUrl,
                    region = region,
                    accessKey = accessKey?.Substring(0, 8) + "...", // İlk 8 karakter
                    currentRegion = _s3Client.Config.RegionEndpoint?.SystemName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get config info");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to get config info: {ex.Message}"
                });
            }
        }

        [HttpGet("test-connection")]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                var bucketName = _configuration["AWS:S3:BucketName"];
                _logger.LogInformation($"🧪 Testing S3 connection to bucket: {bucketName}");

                // S3 connection test
                var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    MaxKeys = 1
                });

                _logger.LogInformation($"✅ S3 connection successful! Status: {response.HttpStatusCode}");

                return Ok(new
                {
                    success = true,
                    message = "S3 connection successful",
                    bucketName = bucketName,
                    region = _s3Client.Config.RegionEndpoint?.SystemName,
                    objectCount = response.KeyCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ S3 connection failed");
                return BadRequest(new
                {
                    success = false,
                    message = $"S3 connection failed: {ex.Message}",
                    error = ex.GetType().Name
                });
            }
        }

        [HttpGet("bucket-info")]
        public async Task<IActionResult> GetBucketInfo()
        {
            try
            {
                var bucketName = _configuration["AWS:S3:BucketName"];
                
                // Bucket location
                var locationResponse = await _s3Client.GetBucketLocationAsync(bucketName);

                return Ok(new
                {
                    success = true,
                    bucketName = bucketName,
                    region = locationResponse.Location.Value
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to get bucket info");
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to get bucket info: {ex.Message}"
                });
            }
        }

        [HttpPost("debug-upload")]
        public async Task<IActionResult> DebugUpload(IFormFile file)
        {
            try
            {
                var bucketName = _configuration["AWS:S3:BucketName"];
                var baseUrl = _configuration["AWS:S3:BaseUrl"];
                var region = _configuration["AWS:Region"];

                _logger.LogInformation($"🔍 Debug Upload - Bucket: {bucketName}");
                _logger.LogInformation($"🔍 Debug Upload - BaseUrl: {baseUrl}");
                _logger.LogInformation($"🔍 Debug Upload - Region: {region}");

                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Dosya seçilmedi" });
                }

                // Test dosyası yükle
                var fileName = $"test/debug-{DateTime.UtcNow:yyyyMMdd-HHmmss}.jpg";
                
                using var stream = file.OpenReadStream();
                var request = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = fileName,
                    InputStream = stream,
                    ContentType = file.ContentType
                    // CannedACL = S3CannedACL.PublicRead // ACL kaldırıldı
                };

                _logger.LogInformation($"🚀 Uploading debug file: {fileName}");
                var response = await _s3Client.PutObjectAsync(request);
                _logger.LogInformation($"📡 Upload response: {response.HttpStatusCode}");

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    var url = $"{baseUrl}{fileName}";
                    _logger.LogInformation($"✅ Debug upload successful! URL: {url}");
                    
                    return Ok(new
                    {
                        success = true,
                        message = "Debug upload successful",
                        fileName = fileName,
                        url = url,
                        bucketName = bucketName,
                        region = region,
                        statusCode = response.HttpStatusCode
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = $"Upload failed with status: {response.HttpStatusCode}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Debug upload failed");
                return BadRequest(new
                {
                    success = false,
                    message = $"Debug upload failed: {ex.Message}",
                    error = ex.GetType().Name
                });
            }
        }

        [HttpPost("test-upload")]
        public async Task<IActionResult> TestUpload()
        {
            try
            {
                var bucketName = _configuration["AWS:S3:BucketName"];
                var testKey = $"test/test-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                var testContent = "Test file for S3 connection verification";

            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = testKey,
                ContentBody = testContent,
                ContentType = "text/plain"
                // CannedACL = S3CannedACL.PublicRead // ACL kaldırıldı
            };                var response = await _s3Client.PutObjectAsync(request);

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    var url = $"{_configuration["AWS:S3:BaseUrl"]}{testKey}";
                    
                    // Test dosyasını sil
                    await _s3Client.DeleteObjectAsync(bucketName, testKey);

                    return Ok(new
                    {
                        success = true,
                        message = "Test upload successful",
                        testKey = testKey,
                        url = url
                    });
                }

                return BadRequest(new
                {
                    success = false,
                    message = $"Upload failed with status: {response.HttpStatusCode}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Test upload failed");
                return BadRequest(new
                {
                    success = false,
                    message = $"Test upload failed: {ex.Message}"
                });
            }
        }
    }
}
