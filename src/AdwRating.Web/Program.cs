using System.Text.Json;
using System.Text.Json.Serialization;
using AdwRating.ApiClient;
using AdwRating.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Blazor Static SSR — no interactive server/WASM components
builder.Services.AddRazorComponents();

// Register AdwRatingApiClient as a typed HttpClient
builder.Services.AddHttpClient<AdwRatingApiClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
});

// Configure JSON serialization to match API conventions (camelCase, enums as strings)
builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
