using System;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

public partial class Program
{
    static async Task Main()
    {
        string url = "https://www.url.com/wp-json/wc/v3/orders"; // Correct URL
        string consumerKey = "consumerKey";
        string consumerSecret = "consumerSecret";

        int page = 1;
        int perPage = 100;  // Number of orders per API request

        // Loop to fetch all orders, handling pagination
        while (true)
        {
            string apiUrl = $"{url}?per_page={perPage}&page={page}";

            using (HttpClient client = new HttpClient())
            {
                // Add Basic Authentication
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes($"{consumerKey}:{consumerSecret}")));

                try
                {
                    // Send the GET request to fetch orders
                    HttpResponseMessage response = await client.GetAsync(apiUrl);
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        // Deserialize the JSON response into a dynamic object
                        dynamic orders = JsonConvert.DeserializeObject(jsonResponse);

                        if (orders.Count == 0)  // No more orders, exit the loop
                            break;

                        // Process each order
                        foreach (var order in orders)
                        {
                            // Extract relevant order details
                            string orderId = order.id ?? "Unknown";
                            string orderStatus = order.status ?? "Unknown";
                            string customerName = (order.billing?.first_name ?? "Unknown") + " " + (order.billing?.last_name ?? "Unknown");
                            string customerEmail = order.billing?.email ?? "Unknown";
                            string customerPhone = order.billing?.phone ?? "Unknown";
                            string billingAddress = (order.billing?.address_1 ?? "") + ", " + (order.billing?.city ?? "");
                            string shippingAddress = (order.shipping?.address_1 ?? "") + ", " + (order.shipping?.city ?? "");
                            string orderTotal = order.total ?? "0";

                            // Safely access shipping_lines (avoid index out of range)
                            string shippingMethod = order.shipping_lines != null && order.shipping_lines.Count > 0 ? order.shipping_lines[0]?.method_title ?? "Unknown" : "Unknown";
                            string shippingTotal = order.shipping_lines != null && order.shipping_lines.Count > 0 ? order.shipping_lines[0]?.total ?? "0" : "0";

                            // Insert or update the order details in SQL Server
                            InsertOrUpdateOrderInSqlServer(orderId, customerName, customerEmail, customerPhone, billingAddress, shippingAddress, orderStatus, orderTotal, shippingMethod, shippingTotal);

                            // Process and insert order items
                            if (order.line_items != null)
                            {
                                foreach (var item in order.line_items)
                                {
                                    string productName = item.name ?? "Unknown";
                                    int quantity = item.quantity ?? 0;
                                    decimal price = item.price ?? 0;
                                    decimal totalPrice = item.total ?? 0;

                                    // Check if there are variations (if any)
                                    string variationDetails = string.Empty;
                                    string productAttributes = string.Empty;
                                    if (item.variation_id != null)
                                    {
                                        variationDetails = "Variation ID: " + item.variation_id;

                                        // Process and store custom variation data (like pa_age, pa_children, pa_infant)
                                        if (item.meta_data != null)
                                        {
                                            foreach (var meta in item.meta_data)
                                            {
                                                if (meta.key != null && meta.value != null)
                                                {
                                                    productAttributes += $"{meta.key}: {meta.value}, ";
                                                }
                                            }

                                            // Remove trailing comma and space from attributes
                                            if (productAttributes.EndsWith(", "))
                                            {
                                                productAttributes = productAttributes.Substring(0, productAttributes.Length - 2);
                                            }
                                        }
                                    }

                                    // Insert order item details into SQL Server
                                    InsertOrderItemInSqlServer(orderId, productName, quantity, price, totalPrice, variationDetails, productAttributes);
                                }
                            }
                        }

                        // Move to the next page
                        page++;
                    }
                    else
                    {
                        Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    break;  // Exit the loop if there's an error
                }
            }
        }
    }

    // Insert or update order details in SQL Server with detailed information
    static void InsertOrUpdateOrderInSqlServer(
        string orderId,
        string customerName,
        string customerEmail,
        string customerPhone,
        string billingAddress,
        string shippingAddress,
        string orderStatus,
        string orderTotal,
        string shippingMethod,
        string shippingTotal
    )
    {
        string connectionString = "Data Source=.;Initial Catalog=test;Integrated Security=True;User ID=123;Password=###;TrustServerCertificate=True";

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            try
            {
                conn.Open();

                // Check if the order already exists in the Orders table
                string checkQuery = "SELECT COUNT(*) FROM Orders WHERE OrderID = @OrderID";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@OrderID", orderId);
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count > 0)
                    {
                        // Update existing order record if the order status has changed
                        string updateQuery = "UPDATE Orders SET " +
                                             "CustomerName = @CustomerName, " +
                                             "CustomerEmail = @CustomerEmail, " +
                                             "CustomerPhone = @CustomerPhone, " +
                                             "BillingAddress = @BillingAddress, " +
                                             "ShippingAddress = @ShippingAddress, " +
                                             "OrderStatus = @OrderStatus, " +
                                             "OrderTotal = @OrderTotal, " +
                                             "ShippingMethod = @ShippingMethod, " +
                                             "ShippingTotal = @ShippingTotal " +
                                             "WHERE OrderID = @OrderID AND OrderStatus != @OrderStatus"; // Only update if status changed

                        using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                        {
                            updateCmd.Parameters.AddWithValue("@OrderID", orderId);
                            updateCmd.Parameters.AddWithValue("@CustomerName", customerName);
                            updateCmd.Parameters.AddWithValue("@CustomerEmail", customerEmail);
                            updateCmd.Parameters.AddWithValue("@CustomerPhone", customerPhone);
                            updateCmd.Parameters.AddWithValue("@BillingAddress", billingAddress);
                            updateCmd.Parameters.AddWithValue("@ShippingAddress", shippingAddress);
                            updateCmd.Parameters.AddWithValue("@OrderStatus", orderStatus);
                            updateCmd.Parameters.AddWithValue("@OrderTotal", orderTotal);
                            updateCmd.Parameters.AddWithValue("@ShippingMethod", shippingMethod);
                            updateCmd.Parameters.AddWithValue("@ShippingTotal", shippingTotal);

                            updateCmd.ExecuteNonQuery();
                            Console.WriteLine($"Order {orderId} updated successfully.");
                        }
                    }
                    else
                    {
                        // Insert new order record if it doesn't exist
                        string insertQuery = "INSERT INTO Orders (OrderID, CustomerName, CustomerEmail, CustomerPhone, BillingAddress, ShippingAddress, OrderStatus, OrderTotal, ShippingMethod, ShippingTotal) " +
                                             "VALUES (@OrderID, @CustomerName, @CustomerEmail, @CustomerPhone, @BillingAddress, @ShippingAddress, @OrderStatus, @OrderTotal, @ShippingMethod, @ShippingTotal)";

                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@OrderID", orderId);
                            insertCmd.Parameters.AddWithValue("@CustomerName", customerName);
                            insertCmd.Parameters.AddWithValue("@CustomerEmail", customerEmail);
                            insertCmd.Parameters.AddWithValue("@CustomerPhone", customerPhone);
                            insertCmd.Parameters.AddWithValue("@BillingAddress", billingAddress);
                            insertCmd.Parameters.AddWithValue("@ShippingAddress", shippingAddress);
                            insertCmd.Parameters.AddWithValue("@OrderStatus", orderStatus);
                            insertCmd.Parameters.AddWithValue("@OrderTotal", orderTotal);
                            insertCmd.Parameters.AddWithValue("@ShippingMethod", shippingMethod);
                            insertCmd.Parameters.AddWithValue("@ShippingTotal", shippingTotal);

                            insertCmd.ExecuteNonQuery();
                            Console.WriteLine($"Order {orderId} inserted successfully.");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("SQL Error: " + ex.Message);
            }
        }
    }

    static void InsertOrderItemInSqlServer(
        string orderId,
        string productName,
        int quantity,
        decimal price,
        decimal totalPrice,
        string variationDetails,
        string productAttributes
    )
    {
        string connectionString = "Data Source=.;Initial Catalog=test;Integrated Security=True;User ID=123;Password=###;TrustServerCertificate=True";

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            try
            {
                conn.Open();

                // Check if the order item already exists in the OrderItems table
                string checkQuery = "SELECT COUNT(*) FROM OrderItems WHERE OrderID = @OrderID AND ProductName = @ProductName AND VariationDetails = @VariationDetails";
                using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@OrderID", orderId);
                    checkCmd.Parameters.AddWithValue("@ProductName", productName);
                    checkCmd.Parameters.AddWithValue("@VariationDetails", variationDetails);
                    int count = (int)checkCmd.ExecuteScalar();

                    if (count == 0)
                    {
                        // Insert new order item record if not already exists
                        string insertQuery = "INSERT INTO OrderItems (OrderID, ProductName, Quantity, Price, TotalPrice, VariationDetails, ProductAttributes) " +
                                             "VALUES (@OrderID, @ProductName, @Quantity, @Price, @TotalPrice, @VariationDetails, @ProductAttributes)";

                        using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                        {
                            insertCmd.Parameters.AddWithValue("@OrderID", orderId);
                            insertCmd.Parameters.AddWithValue("@ProductName", productName);
                            insertCmd.Parameters.AddWithValue("@Quantity", quantity);
                            insertCmd.Parameters.AddWithValue("@Price", price);
                            insertCmd.Parameters.AddWithValue("@TotalPrice", totalPrice);
                            insertCmd.Parameters.AddWithValue("@VariationDetails", variationDetails);
                            insertCmd.Parameters.AddWithValue("@ProductAttributes", productAttributes);

                            insertCmd.ExecuteNonQuery();
                            Console.WriteLine($"Order item for product {productName} inserted.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("SQL Error: " + ex.Message);
            }
        }
    }
}
