// The goal of this agent is to manage inventory levels, track stock, and handle restocking.
using A2A;
using Server.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Server.Agents;

public class ProductsAgent : IDisposable
{
    private readonly ILogger<ProductsAgent> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ChatCompletionAgent _agent;
    private readonly ProductsTool _productsTool;
    private ITaskManager? _taskManager;

    public ProductsAgent(ILogger logger, IConfiguration configuration)
    {
        // Inject the logger
        _logger = logger as ILogger<ProductsAgent> ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = new HttpClient();
        _configuration = configuration;

        // Instantiate the ProductsTool
        _productsTool = new ProductsTool(new Logger<ProductsTool>(new LoggerFactory()));

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

        var inventoryAgentSkill = new AgentSkill()
        {
            Id = "inventory-agent",
            Name = "Inventory Management",
            Description = "A skill that manages inventory levels and stock.",
            Tags = ["inventory", "stock", "products"],
            Examples =
            [
                "Return a list of items in the stock.",
                "List items based on category.",
            ],
        };

        return Task.FromResult(new AgentCard()
        {
            Name = "The Inventory Agent",
            Description = "An agent that manages inventory levels and stock, along with listing products.",
            Url = agentUrl,
            Version = "1.0.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = capabilities,
            Skills = [inventoryAgentSkill],
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

        var kernel = builder.Build();

        // Log registered plugins for debugging
        _logger.LogInformation("Registered plugins: {PluginCount}", kernel.Plugins.Count);
        foreach (var plugin in kernel.Plugins)
        {
            _logger.LogInformation("Plugin: {PluginName} with {FunctionCount} functions", plugin.Name, plugin.Count());
        }

        var inventoryAgent = new ChatCompletionAgent()
        {
            Kernel = kernel,
            Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            Name = "ProductsAgent", // Good to keep in mind here that there CAN NOT be a space in the name.
            Description = "An agent that manages inventory levels and stock of products, along with listing products.",
            Instructions =
            """
            You are a product inventory management agent. Your tasks include getting a list of products, 
            managing stock levels, tracking inventory, getting products by category, get products that are low in stock,
            get products by ID, and getting products in stock. You can also add products to the inventory, and update product details.
            The wholesale prices of products are also available for reference, so you can make informed decisions about pricing and promotions.
            The price is how much custoers pay, while the wholesale price is how much the store pays for the product.
            Use the provided tools to interact with the product database and respond to user queries.
            """
        };

        _logger.LogInformation("ChatCompletionAgent created successfully");
        return inventoryAgent;

    }
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}