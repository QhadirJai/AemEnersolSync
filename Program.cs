using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using AemEnersolSync.Data;
using AemEnersolSync.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AemEnersolApiOptions>(
    builder.Configuration.GetSection(AemEnersolApiOptions.SectionName));

var apiOptions = builder.Configuration
    .GetSection(AemEnersolApiOptions.SectionName)
    .Get<AemEnersolApiOptions>() ?? new AemEnersolApiOptions();

builder.Services.AddHttpClient<IAemEnersolApiClient, AemEnersolApiClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(apiOptions.BaseUrl))
    {
        // A trailing slash keeps the configured paths relative rather than replacing the last segment.
        client.BaseAddress = new Uri(apiOptions.BaseUrl.TrimEnd('/') + "/");
    }

    client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddScoped<ISyncService, SyncService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Platform -> Wells -> Platform is a cycle; ignore it rather than dropping the navigation.
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
