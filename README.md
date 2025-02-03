# WooCommerce Order Importer to SQL Server

This project fetches WooCommerce orders using the WooCommerce REST API, processes the order data, and inserts or updates the order details and items into a SQL Server database.

## Features
- Fetch orders from a WooCommerce store using the WooCommerce REST API.
- Supports pagination for fetching large order datasets.
- Inserts or updates order details in a SQL Server database.
- Inserts order items into SQL Server, handling variations and product attributes.
- Basic authentication for secure API access.

## Requirements
- .NET 6 or later
- SQL Server instance
- WooCommerce store with API access
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json) for JSON parsing

## Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/your-username/woocommerce-order-importer.git
   cd woocommerce-order-importer
