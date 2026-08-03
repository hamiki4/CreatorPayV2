using CreatorPay.Application;
using CreatorPay.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new
    {
        status = "Healthy",
        service = "CreatorPay API"
    }))
    .WithName("GetHealth")
    .WithTags("Health");

app.Run();

public partial class Program;
