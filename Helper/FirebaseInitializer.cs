using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SankalpAPI.Helper
{
    public static class FirebaseInitializer
    {
        private static readonly object _initLock = new();
        public static string? ResolvedCredentialPath { get; private set; }
        public static string? InitializationError { get; private set; }

        public static bool EnsureInitialized(IConfiguration configuration, ILogger? logger = null)
        {
            if (FirebaseApp.DefaultInstance != null && FirebaseMessaging.DefaultInstance != null)
            {
                return true;
            }

            lock (_initLock)
            {
                if (FirebaseApp.DefaultInstance != null && FirebaseMessaging.DefaultInstance != null)
                {
                    return true;
                }

                try
                {
                    string configuredPath = configuration.GetValue<string>("Firebase:ServiceAccountKeyPath") ?? string.Empty;
                    string projectId = configuration.GetValue<string>("Firebase:ProjectId") ?? "sankalpsetu-33a50";

                    var candidatePaths = new List<string>();

                    if (!string.IsNullOrWhiteSpace(configuredPath))
                    {
                        candidatePaths.Add(configuredPath);
                        if (!Path.IsPathRooted(configuredPath))
                        {
                            candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, configuredPath));
                        }
                    }

                    // Automatic search locations relative to application
                    candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "SecureKey", "firebase-adminsdk.json"));
                    candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "firebase-adminsdk.json"));
                    candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, "..", "SecureKey", "firebase-adminsdk.json"));

                    // Common publish & absolute fallback paths
                    candidatePaths.Add(@"D:\Publish\SecureKey\firebase-adminsdk.json");
                    candidatePaths.Add(@"D:\Publish\Host\SecureKey\firebase-adminsdk.json");
                    candidatePaths.Add(@"C:\Publish\SecureKey\firebase-adminsdk.json");

                    string? envPath = Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");
                    if (!string.IsNullOrWhiteSpace(envPath))
                    {
                        candidatePaths.Add(envPath);
                    }

                    string? matchedPath = null;
                    foreach (var path in candidatePaths)
                    {
                        try
                        {
                            string full = Path.GetFullPath(path);
                            if (File.Exists(full))
                            {
                                matchedPath = full;
                                break;
                            }
                        }
                        catch
                        {
                            // Ignore invalid path syntax in candidates
                        }
                    }

                    if (matchedPath != null)
                    {
                        ResolvedCredentialPath = matchedPath;

                        if (FirebaseApp.DefaultInstance == null)
                        {
                            FirebaseApp.Create(new AppOptions
                            {
                                Credential = GoogleCredential.FromFile(matchedPath),
                                ProjectId = projectId
                            });
                        }

                        InitializationError = null;
                        logger?.LogInformation("FirebaseApp successfully initialized from {KeyPath} with ProjectId {ProjectId}", matchedPath, projectId);
                        return true;
                    }
                    else
                    {
                        InitializationError = $"Firebase credential JSON file not found in any checked location: {string.Join(", ", candidatePaths.Distinct())}";
                        logger?.LogWarning(InitializationError);
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    InitializationError = ex.Message;
                    logger?.LogError(ex, "Failed to initialize FirebaseAdmin SDK.");
                    return false;
                }
            }
        }

        public static object GetDiagnostics(IConfiguration configuration)
        {
            EnsureInitialized(configuration);

            return new
            {
                IsInitialized = FirebaseApp.DefaultInstance != null && FirebaseMessaging.DefaultInstance != null,
                FirebaseAppName = FirebaseApp.DefaultInstance?.Name,
                ProjectId = configuration.GetValue<string>("Firebase:ProjectId") ?? "sankalpsetu-33a50",
                ConfiguredKeyPath = configuration.GetValue<string>("Firebase:ServiceAccountKeyPath"),
                ResolvedCredentialPath = ResolvedCredentialPath,
                InitializationError = InitializationError,
                ApplicationBaseDirectory = AppContext.BaseDirectory,
                AssemblyBuildTime = File.GetLastWriteTime(Assembly.GetExecutingAssembly().Location).ToString("yyyy-MM-dd HH:mm:ss")
            };
        }
    }
}

