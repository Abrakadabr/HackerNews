using AspNetCoreRateLimit;
using Microsoft.Extensions.Options;
using HackerNews.Api.Configuration;
using HackerNews.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add configuration
builder.Services.Configure<HackerNewsApiSettings>(
    builder.Configuration.GetSection(HackerNewsApiSettings.SectionName));
builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection(CacheSettings.SectionName));
builder.Services.Configure<ResilienceSettings>(
    builder.Configuration.GetSection(ResilienceSettings.SectionName));

// Add memory cache
// NOTE: In a distributed/production environment, consider using Redis (IDistributedCache) 
// for cache synchronization across multiple instances. IMemoryCache is suitable for 
// single-instance deployments but will not share cache across multiple servers.
builder.Services.AddMemoryCache();

// Add rate limiting
builder.Services.AddOptions();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// Add HttpClient for HackerNewsService with timeout configuration
builder.Services.AddHttpClient<IHackerNewsService, HackerNewsService>()
    .ConfigureHttpClient((serviceProvider, client) =>
    {
        var settings = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<HackerNewsApiSettings>>().Value;
        client.Timeout = TimeSpan.FromSeconds((double)settings.RequestTimeoutSeconds);
    });

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = " Hacker News API",
        Version = "v1",
        Description = "RESTful API to retrieve the best stories from Hacker News",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = " Developer",
            Email = "developer@santander.com"
        }
    });

    // Include XML comments for better documentation
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", " Hacker News API v1");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at the root
    });
}

// Add rate limiting middleware
app.UseIpRateLimiting();

app.MapControllers();

app.Run();

// Make the Program class accessible to integration tests
public partial class Program { }
