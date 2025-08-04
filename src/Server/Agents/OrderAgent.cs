// The goal of this agent is to manage the orders, including creating, updating, and retrieving order details.
using A2A;
using Server.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Server.Agents;

public class OrderAgent : IDisposable
{
    private readonly ILogger<OrderAgent> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ChatCompletionAgent _agent;
    private readonly Server.Tools.ProductsTool _productsTool;
    private readonly Server.Tools.OrderTool _orderTool;
    private ITaskManager? _taskManager;

    public OrderAgent(ILogger<OrderAgent> logger, IConfiguration configuration)
    {
        // Inject the logger
        _logger = logger as ILogger<OrderAgent> ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();
        _configuration = configuration;

        // Instantiate the ProductsTool
        _productsTool = new Server.Tools.ProductsTool(new Logger<Server.Tools.ProductsTool>(new LoggerFactory()));
        _orderTool = new Server.Tools.OrderTool(new Logger<Server.Tools.OrderTool>(new LoggerFactory()));

        // Initialize the agent
        _agent = InitializeAgent();
    }

    public void Attach(ITaskManager taskManager)
    {
        _taskManager = taskManager;
        _taskManager.OnAgentCardQuery = GetAgentCardAsync;
        _taskManager.OnMessageReceived = ProcessMessageAsync;
    }

    private Task<AgentCard> GetAgentCardAsync(string agentUrl, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<AgentCard>(cancellationToken);
        }

        var capabilities = new AgentCapabilities()
        {
            Streaming = true,
            PushNotifications = false,
        };

        var orderAgentSkill = new AgentSkill()
        {
            Id = "order-agent",
            Name = "Order Management",
            Description = "A skill that manages customer orders.",
            Tags = ["order", "customer", "management"],
            Examples =
            [
                "Return a list of all orders.",
                "Get order details by Customer ID.",
                "Get order details by Order ID.",
                "Update order status.",
                "Create a new order.",
            ],
        };

        return Task.FromResult(new AgentCard()
        {
            Name = "The Order Agent",
            Description = "An agent that manages customer orders.",
            Url = agentUrl,
            Version = "1.0.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = capabilities,
            Skills = [orderAgentSkill],

        });
    }

    public async Task<Message> ProcessMessageDirectly(MessageSendParams messageSendParams, CancellationToken cancellationToken)
    {
        return await ProcessMessageAsync(messageSendParams, cancellationToken);
    }

    private async Task<Message> ProcessMessageAsync(MessageSendParams messageSendParams, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new Message
            {
                Role = MessageRole.Agent,
                MessageId = Guid.NewGuid().ToString(),
                Parts = [new TextPart { Text = "Request cancelled." }]
            };
        }

        var message = messageSendParams.Message.Parts.OfType<TextPart>().First().Text;
        _logger.LogInformation("Processing message: {Message}", message);

        // Collect all response content from the agent
        List<Part> content = new List<Part>();

        try
        {
            _logger.LogInformation("Starting agent invocation...");

            await foreach (var response in _agent.InvokeAsync(message, cancellationToken: cancellationToken))
            {
                _logger.LogInformation("Received response from agent. Content null: {IsNull}, Content: {Content}",
                    response.Message.Content == null, response.Message.Content);

                if (!string.IsNullOrEmpty(response.Message.Content))
                {
                    content.Add(new TextPart { Text = response.Message.Content });
                    _logger.LogInformation("Added response part to content list");
                }
            }

            _logger.LogInformation("Finished iterating through agent responses. Total parts: {Count}", content.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during agent invocation");
            content.Add(new TextPart { Text = $"An error occurred while processing your request: {ex.Message}" });
        }

        // If no content was generated, return a default message
        if (content.Count == 0)
        {
            _logger.LogWarning("No content generated for message: {Message}", message);
            content.Add(new TextPart { Text = "I'm sorry, I couldn't process your request." });
        }

        _logger.LogInformation("Message processed successfully: {MessageId}", messageSendParams.Message.MessageId);

        return new Message
        {
            Role = MessageRole.Agent,
            MessageId = Guid.NewGuid().ToString(),
            ContextId = messageSendParams.Message.ContextId,
            Parts = content
        };


    }

    private ChatCompletionAgent InitializeAgent()
    {
        var deploymentName = Environment.GetEnvironmentVariable("DEPLOYMENT_NAME") ?? throw new ArgumentException("DEPLOYMENT_NAME must be provided");
        var endpoint = Environment.GetEnvironmentVariable("ENDPOINT") ?? throw new ArgumentException("ENDPOINT must be provided");
        var apiKey = Environment.GetEnvironmentVariable("API_KEY") ?? throw new ArgumentException("API_KEY must be provided");

        _logger.LogInformation("Initializing Semantic Kernel agent with model {deploymentName}", deploymentName);

        var builder = Kernel.CreateBuilder();
        builder.AddAzureOpenAIChatCompletion(deploymentName, endpoint, apiKey);
        builder.Plugins.AddFromObject(_productsTool);
        builder.Plugins.AddFromObject(_orderTool);

        var kernel = builder.Build();

        // Log registered plugins for debugging
        _logger.LogInformation("Registered plugins: {PluginCount}", kernel.Plugins.Count);
        foreach (var plugin in kernel.Plugins)
        {
            _logger.LogInformation("Plugin: {PluginName} with {FunctionCount} functions", plugin.Name, plugin.Count());
        }

        var orderAgent = new ChatCompletionAgent()
        {
            Kernel = kernel,
            Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            Name = "OrderAgent", // Good to keep in mind here that there CAN NOT be a space in the name.
            Description = "An agent that manages the orders, including creating, updating, and retrieving order details.",
            Instructions =
            """
            You are an order management agent. Your tasks include creating new orders, updating existing orders, and retrieving order details.
            You can also manage order statuses and handle order-related queries.
            Use the provided tools to interact with the order database and respond to user queries.
            Before creating a new order, ensure that the product exists and is in stock.
            If creating an order is successful, update the stock of the product accordingly.
            When updating an order, validate the order ID and ensure the new status is valid.
            When retrieving orders, you can filter by customer ID or order ID.
            """
        };

        _logger.LogInformation("Order Agent created successfully");
        return orderAgent;
    }

    public void Dispose()
    {
        throw new NotImplementedException("Dispose method is not implemented yet.");
    }
}