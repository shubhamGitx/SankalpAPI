using FirebaseAdmin.Messaging;

namespace SankalpAPI.Services
{
    /// <summary>
    /// Creates the common FCM notification payload used by direct and campaign sends.
    /// Keeping these settings in one place prevents Android tap/image behavior from
    /// drifting between notification endpoints.
    /// </summary>
    public sealed class FcmMessageFactory
    {
        private const string DefaultAndroidChannelId = "sankalp_default";

        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<FcmMessageFactory> _logger;

        public FcmMessageFactory(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            ILogger<FcmMessageFactory> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public Notification CreateNotification(string title, string body, string? imageUrl = null)
        {
            return new Notification
            {
                Title = title.Trim(),
                Body = body.Trim(),
                ImageUrl = ResolvePublicUrl(imageUrl)
            };
        }

        public AndroidConfig CreateAndroidConfig()
        {
            string channelId = _configuration["Firebase:DefaultChannelId"]?.Trim()
                ?? DefaultAndroidChannelId;

            if (string.IsNullOrWhiteSpace(channelId))
            {
                channelId = DefaultAndroidChannelId;
            }

            return new AndroidConfig
            {
                Priority = Priority.High,
                Notification = new AndroidNotification
                {
                    ChannelId = channelId,
                    Sound = "default"
                }
            };
        }

        /// <summary>
        /// Converts an application-relative attachment path into a URL that a mobile
        /// device can download. Firebase:PublicBaseUrl is preferred so background
        /// scheduled campaigns work without an active HTTP request.
        /// </summary>
        public string? ResolvePublicUrl(string? pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                return null;
            }

            string value = pathOrUrl.Trim();
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? absoluteUri)
                && (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.ToString();
            }

            string? publicBaseUrl = _configuration["Firebase:PublicBaseUrl"]?.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                HttpRequest? request = _httpContextAccessor.HttpContext?.Request;
                if (request != null && request.Host.HasValue)
                {
                    publicBaseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
                }
            }

            if (!string.IsNullOrWhiteSpace(publicBaseUrl)
                && Uri.TryCreate($"{publicBaseUrl}/{value.TrimStart('/')}", UriKind.Absolute, out Uri? resolvedUri)
                && (resolvedUri.Scheme == Uri.UriSchemeHttp || resolvedUri.Scheme == Uri.UriSchemeHttps))
            {
                return resolvedUri.ToString();
            }

            _logger.LogWarning(
                "FCM image path {ImagePath} is relative but Firebase:PublicBaseUrl is not configured and no HTTP request is available. The notification will be sent without an image.",
                value);

            return null;
        }
    }
}
