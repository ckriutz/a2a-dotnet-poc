using System.Text.Json;
using A2A;

// Discover agent and create client
var cardResolver = new A2ACardResolver(new Uri("http://localhost:5000/"));
var agentCard = await cardResolver.GetAgentCardAsync();
var client = new A2AClient(new Uri(agentCard.Url));

// 4. Display agent details
Console.WriteLine("\nAgent card details:");
Console.WriteLine(JsonSerializer.Serialize(agentCard, new JsonSerializerOptions(A2AJsonUtilities.DefaultOptions)
{
    WriteIndented = true
}));

Message userMessage = new()
{
    Role = MessageRole.User,
    MessageId = Guid.NewGuid().ToString(),
    
    Parts = [
        new TextPart
            {
                Text = "Which product has the most items in stock?",
            }
    ]
};

// Send message
await SendMessageAsync(client, userMessage);

/// <summary>
/// Demonstrates non-streaming message communication with an A2A agent.
/// </summary>
static async Task SendMessageAsync(A2AClient agentClient, Message userMessage)
{
    Console.WriteLine($"Sending message: {((TextPart)userMessage.Parts[0]).Text}");

    // Send the message and get the response
    Message agentResponse = (Message)await agentClient.SendMessageAsync(new MessageSendParams { Message = userMessage });
    
    // Display the response
    Console.WriteLine($" Received complete response from agent: {((TextPart)agentResponse.Parts[0]).Text}");
}