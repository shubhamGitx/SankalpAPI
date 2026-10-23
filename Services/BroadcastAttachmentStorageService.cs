using Microsoft.AspNetCore.Http;

namespace SankalpAPI.Services
{
    public sealed record StoredBroadcastAttachment(string RelativePath, string PhysicalPath);

    public class BroadcastAttachmentStorageService
    {
        private sealed record AttachmentPolicy(long MaxBytes, HashSet<string> Extensions);

        private static readonly IReadOnlyDictionary<string, AttachmentPolicy> Policies =
            new Dictionary<string, AttachmentPolicy>(StringComparer.OrdinalIgnoreCase)
            {
                ["photo"] = new(5 * 1024 * 1024, new(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png"
                }),
                ["video"] = new(25 * 1024 * 1024, new(StringComparer.OrdinalIgnoreCase)
                {
                    ".mp4", ".webm", ".mov"
                }),
                ["document"] = new(5 * 1024 * 1024, new(StringComparer.OrdinalIgnoreCase)
                {
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv"
                }),
                ["audio"] = new(5 * 1024 * 1024, new(StringComparer.OrdinalIgnoreCase)
                {
                    ".mp3", ".wav", ".m4a", ".aac", ".ogg"
                })
            };

        private readonly IWebHostEnvironment _environment;

        public BroadcastAttachmentStorageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<StoredBroadcastAttachment> SaveAsync(
            IFormFile file,
            string attachmentType,
            string fileNameStem,
            CancellationToken cancellationToken)
        {
            string extension = Validate(file, attachmentType);

            if (string.IsNullOrWhiteSpace(fileNameStem)
                || fileNameStem.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
            {
                throw new InvalidDataException("The generated attachment file name is invalid.");
            }

            DateTime utcNow = DateTime.UtcNow;
            string relativeDirectory = Path.Combine(
                "uploads",
                "broadcast",
                attachmentType.ToLowerInvariant(),
                utcNow.ToString("yyyy"),
                utcNow.ToString("MM"));
            string webRoot = _environment.WebRootPath
                ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
            string physicalDirectory = Path.Combine(webRoot, relativeDirectory);
            Directory.CreateDirectory(physicalDirectory);

            string storedFileName = $"{fileNameStem}{extension}";
            string physicalPath = Path.Combine(physicalDirectory, storedFileName);

            try
            {
                await using var stream = new FileStream(
                    physicalPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous);
                await file.CopyToAsync(stream, cancellationToken);
            }
            catch
            {
                if (File.Exists(physicalPath)) File.Delete(physicalPath);
                throw;
            }

            string relativePath = "/" + Path.Combine(relativeDirectory, storedFileName).Replace('\\', '/');
            return new StoredBroadcastAttachment(relativePath, physicalPath);
        }

        public string Validate(IFormFile file, string attachmentType)
        {
            if (!Policies.TryGetValue(attachmentType, out AttachmentPolicy? policy))
                throw new InvalidDataException("Unsupported attachment type.");

            if (file == null || file.Length == 0)
                throw new InvalidDataException($"The selected {attachmentType} file is empty.");

            if (file.Length > policy.MaxBytes)
                throw new InvalidDataException($"The selected {attachmentType} file exceeds the allowed size.");

            string extension = Path.GetExtension(Path.GetFileName(file.FileName)).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !policy.Extensions.Contains(extension))
                throw new InvalidDataException($"The selected {attachmentType} file type is not allowed.");

            return extension;
        }

        public void DeleteIfExists(StoredBroadcastAttachment attachment)
        {
            try
            {
                if (File.Exists(attachment.PhysicalPath))
                    File.Delete(attachment.PhysicalPath);
            }
            catch
            {
                // Cleanup failure must not hide the original request failure.
            }
        }
    }
}
