using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Server.Models;

namespace Server.Tools
{
    public class ProductsTool
    {
        private readonly ILogger<ProductsTool> _logger;
        private readonly string _storeDataPath;

        public ProductsTool(ILogger<ProductsTool> logger, string? storeDataPath = null)
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
        /// Retrieves all products from the store.json file
        /// </summary>
        /// <returns>List of all products</returns>
        [KernelFunction("GetAllProducts")]
        [Description("Retrieves all products from the store")]
        public async Task<List<Product>> GetAllProductsAsync()
        {
            _logger.LogInformation("Retrieving all products from store data.");
            try
            {
                if (!File.Exists(_storeDataPath))
                {
                    _logger.LogWarning("Store data file not found at: {Path}", _storeDataPath);
                    return new List<Product>();
                }

                var jsonContent = await File.ReadAllTextAsync(_storeDataPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var storeData = JsonSerializer.Deserialize<StoreData>(jsonContent, options);

                if (storeData?.Products == null)
                {
                    _logger.LogWarning("No products found in store data");
                    return new List<Product>();
                }

                _logger.LogInformation("Successfully retrieved {Count} products from store", storeData.Products.Count);
                return storeData.Products;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse store data JSON");
                throw new InvalidOperationException("Invalid store data format", ex);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Failed to read store data file");
                throw new InvalidOperationException("Unable to read store data file", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while retrieving products");
                throw;
            }
        }

        /// <summary>
        /// Retrieves products filtered by category
        /// </summary>
        /// <param name="category">Category to filter by</param>
        /// <returns>List of products in the specified category</returns>
        [KernelFunction("GetProductsByCategory")]
        [Description("Retrieves all products from the store by category. Category is case-insensitive.")]
        public async Task<List<Product>> GetProductsByCategoryAsync(string category)
        {
            _logger.LogInformation("Retrieving all products from store by category: {Category}", category);
            var allProducts = await GetAllProductsAsync();
            return allProducts.Where(p => string.Equals(p.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        /// <summary>
        /// Retrieves a specific product by ID
        /// </summary>
        /// <param name="productId">Product ID to search for</param>
        /// <returns>Product if found, null otherwise</returns>
        [KernelFunction("GetProductById")]
        [Description("Retrieves a specific product from the store by ID.")]
        public async Task<Product?> GetProductByIdAsync(string productId)
        {
            _logger.LogInformation("Retrieving product by ID: {ProductId}", productId);
            var allProducts = await GetAllProductsAsync();
            return allProducts.FirstOrDefault(p => string.Equals(p.Id, productId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Retrieves products that are in stock
        /// </summary>
        /// <returns>List of products with stock > 0</returns>
        [KernelFunction("GetProductsInStock")]
        [Description("Retrieves all products from the store that are in stock. Stock is considered in stock if it is greater than 0.")]
        public async Task<List<Product>> GetProductsInStockAsync()
        {
            _logger.LogInformation("Retrieving all products from store that are in stock.");
            var allProducts = await GetAllProductsAsync();
            return allProducts.Where(p => p.Stock > 0).ToList();
        }

        /// <summary>
        /// Retrieves products that are low in stock (stock <= threshold)
        /// </summary>
        /// <param name="threshold">Stock threshold (default: 5)</param>
        /// <returns>List of products with low stock</returns>
        [KernelFunction("GetLowStockProducts")]
        [Description("Retrieves all products from the store that are low in stock. Stock is considered low if it is less than or equal to the specified threshold.")]
        public async Task<List<Product>> GetLowStockProductsAsync(int threshold = 5)
        {
            _logger.LogInformation("Retrieving all products from store that are low in stock (threshold: {Threshold})", threshold);
            var allProducts = await GetAllProductsAsync();
            return allProducts.Where(p => p.Stock <= threshold && p.Stock > 0).ToList();
        }
        
        /// <summary>
        /// Adds a new product to the store
        /// </summary>
        /// <param name="product">Product to add</param>
        /// <returns>True if product was added successfully, false otherwise</returns>
        [KernelFunction("AddProduct")]
        [Description("Adds a new product to the store. Returns true if successful, false otherwise.")]
        public async Task<bool> AddProductAsync(Product product)
        {
            _logger.LogInformation("Adding new product: {ProductName}", product.Name);
            if (product == null)
            {           
                _logger.LogError("Product cannot be null");
                return false;
            }

            try
            {
                // Load existing store data
                var storeData = await LoadStoreDataAsync();
                
                // Check if product with same ID already exists
                if (storeData.Products.Any(p => string.Equals(p.Id, product.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogError("Product with ID {ProductId} already exists", product.Id);
                    return false;
                }

                // Add the new product
                storeData.Products.Add(product);

                // Save the updated store data
                await SaveStoreDataAsync(storeData);

                _logger.LogInformation("Successfully added product with ID: {ProductId}", product.Id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add product: {ProductName}", product.Name);
                return false;
            }
        }

        /// <summary>
        /// Updates an existing product in the store
        /// </summary>
        /// <param name="product">Product to update</param>
        /// <returns>True if product was updated successfully, false otherwise</returns>
        [KernelFunction("UpdateProductStock")]
        [Description("Updates the stock of an existing product in the store. Returns true if successful, false otherwise.")]
        public async Task<bool> UpdateProductStockAsync(string productId, int newStock)
        {
            _logger.LogInformation("Updating stock for product: {ProductId}", productId);
            if (string.IsNullOrEmpty(productId))
            {
                _logger.LogError("Product ID cannot be null or empty");
                return false;
            }

            try
            {
                // Load existing store data
                var storeData = await LoadStoreDataAsync();

                // Find the existing product
                var existingProduct = storeData.Products.FirstOrDefault(p => string.Equals(p.Id, productId, StringComparison.OrdinalIgnoreCase));
                if (existingProduct == null)
                {
                    _logger.LogError("Product with ID {ProductId} not found", productId);
                    return false;
                }

                // Update the product stock
                existingProduct.Stock = newStock;

                // Save the updated store data
                await SaveStoreDataAsync(storeData);

                _logger.LogInformation("Successfully updated stock for product with ID: {ProductId}", productId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update stock for product: {ProductId}", productId);
                return false;
            }
        }
    }
}
