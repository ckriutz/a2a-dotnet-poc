using System.Text.Json.Serialization;

namespace Server.Models;

public class Customer
{
    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public Address Address { get; set; } = new();
}

public class Address
{
    [JsonPropertyName("street")]
    public string Street { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("zip")]
    public string Zip { get; set; } = string.Empty;
}

public class CustomersData
{
    [JsonPropertyName("customers")]
    public List<Customer> Customers { get; set; } = new();
}
