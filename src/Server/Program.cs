using A2A;
using A2A.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
var app = builder.Build();

// Get services after building the app
var configuration = app.Configuration;
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var productsAgentLogger = loggerFactory.CreateLogger<Server.Agents.ProductsAgent>();
var orderAgentLogger = loggerFactory.CreateLogger<Server.Agents.OrderAgent>();
var customerAgentLogger = loggerFactory.CreateLogger<Server.Agents.CustomerAgent>();

var productsAgent = new Server.Agents.ProductsAgent(productsAgentLogger, configuration);
var orderAgent = new Server.Agents.OrderAgent(orderAgentLogger, configuration);
var customerAgent = new Server.Agents.CustomerAgent(customerAgentLogger, configuration);
var taskManager = new TaskManager();

// Use the router agent to handle all messages and route to appropriate agents
productsAgent.Attach(taskManager);
orderAgent.Attach(taskManager);
customerAgent.Attach(taskManager);

app.MapA2A(taskManager, "/");

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTimeOffset.UtcNow }));

app.Run();