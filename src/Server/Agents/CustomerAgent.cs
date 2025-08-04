using A2A;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;

namespace Server.Agents;

public class CustomerAgent : IDisposable
{
    private readonly ILogger<CustomerAgent> _logger;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ChatCompletionAgent _agent;
    private readonly Server.Tools.CustomerTool _customersTool;
    private ITaskManager? _taskManager;

    public CustomerAgent(ILogger<CustomerAgent> logger, IConfiguration configuration)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClient = new HttpClient();
        _customersTool = new Server.Tools.CustomerTool(_configuration, new Logger<Server.Tools.CustomerTool>(new LoggerFactory()));

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

        var customersAgentSkill = new AgentSkill()
        {
            Id = "customers-agent",
            Name = "Customer Management",
            Description = "A skill that manages customer information.",
            Tags = ["customer", "management"],
            Examples =
            [
                "Return a list of all customers.",
                "Get customer details by Customer ID.",
                "Update customer information.",
            ],
        };

        return Task.FromResult(new AgentCard()
        {
            Name = "The Customer Agent",
            Description = "An agent that manages customer information.",
            Url = agentUrl,
            Version = "1.0.0",
            DefaultInputModes = ["text"],
            DefaultOutputModes = ["text"],
            Capabilities = capabilities,
            Skills = [customersAgentSkill],
        });
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
        builder.Plugins.AddFromObject(_customersTool);

        var kernel = builder.Build();


        // Log registered plugins for debugging
        _logger.LogInformation("Registered plugins: {PluginCount}", kernel.Plugins.Count);
        foreach (var plugin in kernel.Plugins)
        {
            _logger.LogInformation("Plugin: {PluginName} with {FunctionCount} functions", plugin.Name, plugin.Count());
        }

        var customerAgent = new ChatCompletionAgent()
        {
            Kernel = kernel,
            Arguments = new KernelArguments(new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }),
            Name = "CustomerAgent", // Good to keep in mind here that there CAN NOT be a space in the name.
            Description = "An agent that manages customer interactions, including creating, updating, and retrieving customer details.",
            Instructions =
            """
            You are a customer management agent. Your tasks include creating new customers, updating existing customer information, and retrieving customer details.
            You can also manage customer statuses and handle customer-related queries.
            Use the provided tools to interact with the customer database and respond to user queries.
            Before creating a new customer, ensure that the customer does not already exist.
            If creating a customer is successful, send a welcome message to the customer.
            When updating a customer, validate the customer ID and ensure the new information is valid.
            When retrieving customers, you can filter by customer ID or other attributes.
            """
        };

        _logger.LogInformation("Customer Agent created successfully");
        return customerAgent;
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing CustomersAgent resources");
        _httpClient.Dispose();
        // Dispose of any resources if needed
        //_agent?.Dispose();
        // CustomerAgent doesn't implement IDisposable, so we skip it
    }
}