using McpValidatorClient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson();

builder.Services.AddEndpointsApiExplorer();

// MCP HTTP client — points at the ARK JSON Validator server
builder.Services.AddHttpClient<McpClientService>(client =>
{
    var baseUrl = builder.Configuration["McpServer:BaseUrl"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Application services
builder.Services.AddScoped<AzureOpenAiService>();
builder.Services.AddScoped<ProcessExecutorService>();
builder.Services.AddScoped<ComplaintPipelineService>();


builder.Services.AddCors(opt => opt.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.MapControllers();

app.Run();
