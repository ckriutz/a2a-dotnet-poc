using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Server.Models;

namespace Server.Tools
{
    public class CustomerTool
    {
        private readonly ILogger<CustomerTool> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _storeDataPath;

        public CustomerTool(IConfiguration configuration, ILogger<CustomerTool> logger, string? storeDataPath = null)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = new HttpClient();
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

        [KernelFunction("GetAllCustomers")]
        [Description("Get all customers from the store data")]
        public async Task<List<Customer>> GetAllCustomersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retrieving all customers from store data.");
            try
            {
                if (!File.Exists(_storeDataPath))
                {
                    _logger.LogWarning("Store data file not found at: {Path}", _storeDataPath);
                    return new List<Customer>();
                }

                var jsonContent = await File.ReadAllTextAsync(_storeDataPath, cancellationToken);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var storeData = JsonSerializer.Deserialize<StoreData>(jsonContent, options);

                if (storeData?.Customers == null)
                {
                    _logger.LogWarning("No customers found in store data");
                    return new List<Customer>();
                }

                _logger.LogInformation("Successfully retrieved {Count} customers from store", storeData.Customers.Count);
                return storeData.Customers;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse store data JSON");
                throw new InvalidOperationException("Invalid store data format", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving customers");
                throw;
            }
        }

        [KernelFunction("GetCustomerById")]
        [Description("Get a customer by ID from the store data")]
        public async Task<Customer?> GetCustomerByIdAsync(string customerId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retrieving customer with ID: {CustomerId}", customerId);
            try
            {
                var customers = await GetAllCustomersAsync(cancellationToken);
                return customers.FirstOrDefault(c => c.CustomerId == customerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving customer");
                throw;
            }
        }

        [KernelFunction("GetCustomerByEmail")]
        [Description("Get a customer by email from the store data")]
        public async Task<Customer?> GetCustomerByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Retrieving customer with email: {Email}", email);
            try
            {
                var customers = await GetAllCustomersAsync(cancellationToken);
                return customers.FirstOrDefault(c => c.Email == email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving customer");
                throw;
            }
        }

        [KernelFunction("AddCustomer")]
        [Description("Add a new customer to the store data")]
        public async Task<Customer> AddCustomerAsync(Customer newCustomer, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Adding new customer: {Customer}", newCustomer);
            try
            {
                var storeData = await LoadStoreDataAsync();

                newCustomer.CustomerId = CreateNewCustomerId();

                storeData.Customers.Add(newCustomer);
                await SaveStoreDataAsync(storeData);
                return newCustomer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while adding customer");
                throw;
            }
        }

        [KernelFunction("UpdateCustomer")]
        [Description("Update an existing customer in the store data")]
        public async Task<Customer?> UpdateCustomerAsync(Customer updatedCustomer, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Updating customer: {CustomerId}", updatedCustomer.CustomerId);
            try
            {
                var storeData = await LoadStoreDataAsync();

                var existingCustomer = storeData.Customers.FirstOrDefault(c => c.CustomerId == updatedCustomer.CustomerId);
                if (existingCustomer == null)
                {
                    _logger.LogWarning("Customer not found: {CustomerId}", updatedCustomer.CustomerId);
                    return null;
                }

                // Update the customer details
                existingCustomer.Name = updatedCustomer.Name;
                existingCustomer.Email = updatedCustomer.Email;

                await SaveStoreDataAsync(storeData);
                _logger.LogInformation("Successfully updated customer: {CustomerId}", updatedCustomer.CustomerId);
                return existingCustomer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating customer");
                throw;
            }
        }

        private string CreateNewCustomerId()
        {
            //Should be in the format "CUST-<random>"
            return $"CUST-{Guid.NewGuid().ToString().Split('-')[0]}";
        }
    }
}