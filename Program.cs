using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using SankalpAPI.Helper;
using SankalpAPI.Interface;
using SankalpAPI.Middleware;
using SankalpAPI.Repository;
using SankalpAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            if (context.HttpContext.Request.Path.StartsWithSegments("/api/User/Register", StringComparison.OrdinalIgnoreCase))
            {
                return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new SankalpAPI.DTO.RegisterUserResponse
                {
                    Success = false,
                    Message = "Invalid registration data."
                });
            }

            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(context.ModelState);
        };
    });

builder.Services.AddEndpointsApiExplorer();

// ============================================================
// FIREBASE ADMIN SDK INITIALIZATION (SINGLETON)
// Keep the service-account JSON on the backend server only.
// Configure via appsettings.json ("Firebase:ServiceAccountKeyPath")
// or the GOOGLE_APPLICATION_CREDENTIALS environment variable.
// ============================================================
if (FirebaseApp.DefaultInstance == null)
{
    try
    {
        string? serviceAccountPath = builder.Configuration["Firebase:ServiceAccountKeyPath"]
            ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

        GoogleCredential credential = !string.IsNullOrWhiteSpace(serviceAccountPath) && File.Exists(serviceAccountPath)
            ? GoogleCredential.FromFile(serviceAccountPath)
            : GoogleCredential.GetApplicationDefault();

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential
        });
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[Firebase Warning] FirebaseApp could not be initialized at startup: {ex.Message}");
    }
}

builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    options.SchemaFilter<RemoveAdditionalPropertiesSchemaFilter>();
    options.AddSecurityDefinition(
        "ClientToken",
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "X-Client-Token",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Enter ClientToken."
        });

    options.AddSecurityRequirement(
        new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference =
                        new Microsoft.OpenApi.Models.OpenApiReference
                        {
                            Type =
                                Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,

                            Id = "ClientToken"
                        }
                },
                Array.Empty<string>()
            }
        });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Angular", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Database
builder.Services.AddScoped<clsDataAccess>();

// HTTP Clients
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ISmsNotificationService, SmsNotificationService>();

// Repositories
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IClientTokenRepository, ClientTokenRepository>();
builder.Services.AddScoped<IMasterDataService, MasterRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPortalRepository, PortalRepository>();
builder.Services.AddScoped<ITotpRepository, TotpRepository>();
builder.Services.AddScoped<IOtpRepository, OtpRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IApplicantRepository, ApplicantRepository>();
builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();

// Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<MasterService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<ClientTokenService>();
builder.Services.AddScoped<TotpService>();
builder.Services.AddScoped<IOtpService, OtpService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<BroadcastAttachmentStorageService>();
builder.Services.AddScoped<PortalService>();
builder.Services.AddScoped<IApplicantService, ApplicantService>();
builder.Services.AddScoped<ApplicantService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<IFcmNotificationService, FcmNotificationService>();
builder.Services.AddScoped<IFirebaseNotificationService, FirebaseNotificationService>();
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddHostedService<CampaignSchedulerWorker>();

builder.Services.AddScoped<ExceptionLogging>();

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.UseCors("Angular");

app.UseStaticFiles();

app.UseRouting();

// Client Token Authentication
app.UseMiddleware<ClientTokenMiddleware>();

app.MapControllers();

app.Run();
