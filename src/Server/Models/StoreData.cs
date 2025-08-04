namespace Server.Models;

public class StoreData
{
    public List<Product> Products { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public List<Customer> Customers { get; set; } = new();
}