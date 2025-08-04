using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Server.Models;

namespace Server.Tools
{
    public class OrderTool
    {
        private readonly ILogger<OrderTool> _logger;
        private readonly string _storeDataPath;

        public OrderTool(ILogger<OrderTool> logger, string? storeDataPath = null)
        {
            _logger = logger;
            _storeDataPath = storeDataPath ?? Path.Combine(Directory.GetCurrentDirectory(), "Data", "store.json");
        }

        /// <summary>
        /// Load store data from the JSON file
        /// </summary>
        /// <returns>StoreData object</returns>
        private async Task<StoreData> LoadStoreDataAsync()
        {
            if (!File.Exists(_storeDataPath))
            {
                _logger.LogWarning("Store data file not found at: {Path}, creating new store data", _storeDataPath);
                return new StoreData();
            }

            var jsonContent = await File.ReadAllTextAsync(_storeDataPath);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var storeData = JsonSerializer.Deserialize<StoreData>(jsonContent, options);
            return storeData ?? new StoreData();
        }

        /// <summary>
        /// Save store data to the JSON file
        /// </summary>
        /// <param name="storeData">StoreData to save</param>
        private async Task SaveStoreDataAsync(StoreData storeData)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var jsonContent = JsonSerializer.Serialize(storeData, options);
            await File.WriteAllTextAsync(_storeDataPath, jsonContent);
        }

        /// <summary>
        /// Retrieves all orders from the store.json file
        /// </summary>
        /// <returns>List of all orders</returns>
        [KernelFunction("GetAllOrders")]
        [Description("Retrieves all orders from the store")]
        public async Task<List<Order>> GetAllOrdersAsync()
        {
            _logger.LogInformation("Retrieving all orders from store data.");
            try
            {
                if (!File.Exists(_storeDataPath))
                {
                    _logger.LogWarning("Store data file not found at: {Path}", _storeDataPath);
                    return new List<Order>();
                }

                var jsonContent = await File.ReadAllTextAsync(_storeDataPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var storeData = JsonSerializer.Deserialize<StoreData>(jsonContent, options);

                if (storeData?.Orders == null)
                {
                    _logger.LogWarning("No orders found in store data");
                    return new List<Order>();
                }

                _logger.LogInformation("Successfully retrieved {Count} orders from store", storeData.Orders.Count);
                return storeData.Orders;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse store data JSON");
                throw new InvalidOperationException("Invalid store data format", ex);
            }
        }

        /// <summary>
        /// Retrieves a single order from the store.json file
        /// </summary>
        /// <returns>Single order</returns>
        [KernelFunction("GetOrderById")]
        [Description("Retrieves a single order from the store by ID")]
        public async Task<Order?> GetOrderByIdAsync(string orderId)
        {
            _logger.LogInformation("Retrieving order with ID: {OrderId}", orderId);
            var orders = await GetAllOrdersAsync();
            var order = orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order == null)
            {
                _logger.LogWarning("Order with ID {OrderId} not found", orderId);
            }
            return order;
        }

        /// <summary>
        /// Retrieves a single order from the store.json file
        /// </summary>
        /// <returns>Single order</returns>
        [KernelFunction("GetOrdersByCustomerId")]
        [Description("Retrieves a list of orders from the store by customer ID")]
        public async Task<List<Order>> GetOrdersByCustomerIdAsync(string customerId)
        {
            _logger.LogInformation("Retrieving orders for customer ID: {CustomerId}", customerId);
            var orders = await GetAllOrdersAsync();
            var customerOrders = orders.Where(o => o.CustomerId == customerId).ToList();
            if (!customerOrders.Any())
            {
                _logger.LogWarning("No orders found for customer ID {CustomerId}", customerId);
            }
            return customerOrders;
        }

        /// <summary>
        /// Get orders by status
        /// </summary>
        /// <param name="status">Order status to filter by</param>
        /// <returns>List of orders with the specified status</returns>
        [KernelFunction("GetOrdersByStatus")]
        [Description("Retrieves a list of orders from the store by status")]
        public async Task<List<Order>> GetOrdersByStatusAsync(string status)
        {
            _logger.LogInformation("Retrieving orders with status: {Status}", status);
            var orders = await GetAllOrdersAsync();
            var filteredOrders = orders.Where(o => string.Equals(o.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!filteredOrders.Any())
            {
                _logger.LogWarning("No orders found with status {Status}", status);
            }
            return filteredOrders;
        }

        /// <summary>
        /// Gets order by when it was created in a date range
        /// </summary>
        /// <param name="startDate">Start date of the range</param>
        /// <param name="endDate">End date of the range</param>
        /// <returns>List of orders created within the specified date range</returns>
        [KernelFunction("GetOrdersByDateRange")]
        [Description("Retrieves a list of orders from the store created within a specific date range")]
        public async Task<List<Order>> GetOrdersByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            _logger.LogInformation("Retrieving orders created between {StartDate} and {EndDate}", startDate, endDate);
            var orders = await GetAllOrdersAsync();
            var filteredOrders = orders.Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate).ToList();
            if (!filteredOrders.Any())
            {
                _logger.LogWarning("No orders found between {StartDate} and {EndDate}", startDate, endDate);
            }
            return filteredOrders;
        }

        /// <summary>
        /// Adds a new order to the store
        /// </summary>
        /// <param name="order">Order to add</param>
        /// <returns>True if order was added successfully, false otherwise</returns>
        [KernelFunction("AddOrder")]
        [Description("Adds a new order to the store. Returns true if successful, false otherwise.")]
        public async Task<bool> AddOrderAsync(Order order)
        {
            _logger.LogInformation("Adding new order: {OrderId}", order.OrderId);
            if (order == null)
            {
                _logger.LogError("Order cannot be null");
                return false;
            }

            try
            {
                // Load existing store data
                var storeData = await LoadStoreDataAsync();

                // Check if order with same ID already exists
                if (storeData.Orders.Any(o => string.Equals(o.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogError("Order with ID {OrderId} already exists", order.OrderId);
                    return false;
                }

                order.OrderId = CreateNewOrderId(); // Generate a new unique order ID
                order.CreatedAt = DateTime.UtcNow; // Set creation date to now
                // Add the new order
                storeData.Orders.Add(order);

                // Save the updated store data
                await SaveStoreDataAsync(storeData);

                _logger.LogInformation("Successfully added order with ID: {OrderId}", order.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add order: {OrderId}", order.OrderId);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing order in the store
        /// </summary>
        /// <param name="order">Order to update</param>
        /// <returns>True if order was updated successfully, false otherwise</returns>
        [KernelFunction("UpdateOrder")]
        [Description("Updates an existing order in the store. Returns true if successful, false otherwise.")]
        public async Task<bool> UpdateOrderAsync(Order order)
        {
            _logger.LogInformation("Updating order: {OrderId}", order.OrderId);
            if (order == null)
            {
                _logger.LogError("Order cannot be null");
                return false;
            }

            try
            {
                // Load existing store data
                var storeData = await LoadStoreDataAsync();

                // Find the existing order
                var existingOrderIndex = storeData.Orders.FindIndex(o => string.Equals(o.OrderId, order.OrderId, StringComparison.OrdinalIgnoreCase));
                if (existingOrderIndex == -1)
                {
                    _logger.LogError("Order with ID {OrderId} not found", order.OrderId);
                    return false;
                }

                // Update the order
                storeData.Orders[existingOrderIndex] = order;

                // Save the updated store data
                await SaveStoreDataAsync(storeData);

                _logger.LogInformation("Successfully updated order with ID: {OrderId}", order.OrderId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update order: {OrderId}", order.OrderId);
                return false;
            }
        }

        /// <summary>
        /// Helper function to create a new unique order ID
        /// </summary>
        /// <returns>New unique order ID</returns>
        private string CreateNewOrderId()
        {
            //Should be in the format "ORD-<random>"
            return $"ORD-{Guid.NewGuid().ToString().Split('-')[0]}";
        }
    }
}