var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

// Simple welcome endpoint
app.MapGet("/", () => Results.Ok(new 
{ 
    message = "C# Vulnerable Web App",
    endpoints = new[] 
    {
        "GET  /vulnerable - Info",
        "POST /vulnerable/process - Submit base64 encoded serialized data"
    }
}));

app.Run();
