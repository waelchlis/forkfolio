using System.Threading.RateLimiting;
using FoodHelper.Api.Authorization;
using FoodHelper.Api.Data;
using FoodHelper.Api.Options;
using FoodHelper.Api.Services;
using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Load local settings in Development and production settings for all other environments.
var environmentSettingsFile = builder.Environment.IsDevelopment()
    ? "appsettings.Development.json"
    : "appsettings.Production.json";
builder.Configuration.AddJsonFile(
    environmentSettingsFile,
    optional: true,
    reloadOnChange: builder.Environment.IsDevelopment());

builder.Services.Configure<FirebaseOptions>(builder.Configuration.GetSection("Firebase"));
builder.Services.Configure<GoogleOidcOptions>(builder.Configuration.GetSection("GoogleOidc"));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", cors =>
    {
        cors.WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var googleOidc = builder.Configuration.GetSection("GoogleOidc").Get<GoogleOidcOptions>() ?? new GoogleOidcOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = googleOidc.Authority;
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = googleOidc.ValidIssuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(googleOidc.Audience),
            ValidAudience = googleOidc.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "name"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.Requirements.Add(new AdminRequirement()));
});
builder.Services.AddSingleton<IAuthorizationHandler, AdminRequirementHandler>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Too many requests. Please slow down and try again shortly." },
            cancellationToken);
    };

    options.AddPolicy(RateLimitPolicies.Writes, context =>
        RateLimitPartition.GetFixedWindowLimiter(RateLimitKeyResolver.Resolve(context), _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 40,
            QueueLimit = 0,
        }));
});

var firebase = builder.Configuration.GetSection("Firebase").Get<FirebaseOptions>() ?? new FirebaseOptions();
if (!string.IsNullOrWhiteSpace(firebase.GoogleApplicationCredentialsPath))
{
    Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", firebase.GoogleApplicationCredentialsPath);
}

builder.Logging.AddConsole();

var storageMode = StorageMode.InMemory;

if (!string.IsNullOrWhiteSpace(firebase.ProjectId))
{
    try
    {
        var firestore = FirestoreDb.Create(firebase.ProjectId);
        builder.Services.AddSingleton(firestore);
        builder.Services.AddSingleton<IRecipeStore, FirestoreRecipeStore>();
        builder.Services.AddSingleton<IShoppingListStore, FirestoreShoppingListStore>();
        builder.Services.AddSingleton<IAdminStore, FirestoreAdminStore>();
        builder.Services.AddSingleton<IIngredientWordStore, FirestoreIngredientWordStore>();
        builder.Services.AddSingleton<ICategoryStore, FirestoreCategoryStore>();
        builder.Services.AddSingleton<IMealPlanStore, FirestoreMealPlanStore>();
        builder.Services.AddSingleton<IFavoriteStore, FirestoreFavoriteStore>();
        builder.Services.AddSingleton<IRecipeRevisionStore, FirestoreRecipeRevisionStore>();

        var storageBucket = builder.Configuration.GetValue<string>("Storage:BucketName");
        if (!string.IsNullOrWhiteSpace(storageBucket))
        {
            var storageClient = StorageClient.Create();
            builder.Services.AddSingleton(storageClient);
            builder.Services.AddSingleton<IImageStore, FirebaseImageStore>();
            builder.Services.AddHostedService<TrashCleanupService>();
        }
        else
        {
            builder.Services.AddSingleton<IImageStore, InMemoryImageStore>();
        }

        storageMode = StorageMode.Firestore;
    }
    catch (Exception ex)
    {
        builder.Services.AddSingleton<IRecipeStore, InMemoryRecipeStore>();
        builder.Services.AddSingleton<IShoppingListStore, InMemoryShoppingListStore>();
        builder.Services.AddSingleton<IAdminStore, InMemoryAdminStore>();
        builder.Services.AddSingleton<IImageStore, InMemoryImageStore>();
        builder.Services.AddSingleton<IIngredientWordStore, InMemoryIngredientWordStore>();
        builder.Services.AddSingleton<ICategoryStore, InMemoryCategoryStore>();
        builder.Services.AddSingleton<IMealPlanStore, InMemoryMealPlanStore>();
        builder.Services.AddSingleton<IFavoriteStore, InMemoryFavoriteStore>();
        builder.Services.AddSingleton<IRecipeRevisionStore, InMemoryRecipeRevisionStore>();

        // Logged via a temporary bootstrap logger since the DI-provided ILogger isn't available
        // until the host is built. Firestore misconfiguration otherwise fails silently into
        // in-memory storage — see planning/epic-nf3-backend-reliability-observability.md.
        using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
        bootstrapLoggerFactory.CreateLogger("Startup")
            .LogError(ex, "Firestore initialization failed. Falling back to in-memory stores for every domain.");
    }
}
else
{
    builder.Services.AddSingleton<IRecipeStore, InMemoryRecipeStore>();
    builder.Services.AddSingleton<IShoppingListStore, InMemoryShoppingListStore>();
    builder.Services.AddSingleton<IAdminStore, InMemoryAdminStore>();
    builder.Services.AddSingleton<IImageStore, InMemoryImageStore>();
    builder.Services.AddSingleton<IIngredientWordStore, InMemoryIngredientWordStore>();
    builder.Services.AddSingleton<ICategoryStore, InMemoryCategoryStore>();
    builder.Services.AddSingleton<IMealPlanStore, InMemoryMealPlanStore>();
    builder.Services.AddSingleton<IFavoriteStore, InMemoryFavoriteStore>();
    builder.Services.AddSingleton<IRecipeRevisionStore, InMemoryRecipeRevisionStore>();
}

builder.Services.AddSingleton(new StorageModeHolder(storageMode));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Food Helper API v1");
        options.RoutePrefix = "swagger";
    });
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", async (IServiceProvider services, StorageModeHolder storageModeHolder) =>
{
    var mode = storageModeHolder.Mode;
    var firestoreReachable = (bool?)null;

    if (mode == StorageMode.Firestore)
    {
        try
        {
            var firestore = services.GetRequiredService<FirestoreDb>();
            await firestore.Collection("categories").Limit(1).GetSnapshotAsync();
            firestoreReachable = true;
        }
        catch
        {
            firestoreReachable = false;
        }
    }

    return Results.Ok(new
    {
        status = "ok",
        storageMode = mode.ToString().ToLowerInvariant(),
        firestoreReachable,
    });
});
app.MapControllers();

// Seed default ingredient words
using (var scope = app.Services.CreateScope())
{
    var wordStore = scope.ServiceProvider.GetRequiredService<IIngredientWordStore>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        await wordStore.SeedAsync(DefaultIngredients.German, CancellationToken.None);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ingredient seeding failed");
    }
}

await app.RunAsync();

public enum StorageMode
{
    InMemory,
    Firestore,
}

public sealed class StorageModeHolder(StorageMode mode)
{
    public StorageMode Mode { get; } = mode;
}

// Exposed so WebApplicationFactory-based integration tests (see FoodHelper.Api.Tests) can
// reference the entry point type.
public partial class Program;
