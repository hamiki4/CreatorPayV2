using CreatorPay.Application;
using CreatorPay.Application.Operations;
using CreatorPay.Infrastructure;
using CreatorPay.Worker;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("CreatorPayDatabase"))) throw new InvalidOperationException("ConnectionStrings:CreatorPayDatabase is required.");
builder.Logging.ClearProviders(); builder.Logging.AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });
builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration); builder.Services.AddHostedService<OperationalWorker>();
await builder.Build().RunAsync();
