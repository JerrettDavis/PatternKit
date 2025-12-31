# Adapter Pattern Real-World Examples

Production-ready examples demonstrating the Adapter pattern in real-world scenarios.

---

## Example 1: API Request/Response Mapping

### The Problem

A REST API receives external requests and must transform them into internal commands, and internal query results into external responses, with validation at each boundary.

### The Solution

Use Adapter to create bidirectional mappers with integrated validation.

### The Code

```csharp
public class OrderApiAdapter
{
    private readonly Adapter<CreateOrderRequest, CreateOrderCommand> _requestAdapter;
    private readonly Adapter<Order, OrderResponse> _responseAdapter;

    public OrderApiAdapter()
    {
        _requestAdapter = Adapter<CreateOrderRequest, CreateOrderCommand>
            .Create(static () => new CreateOrderCommand())
            .Map(static (in CreateOrderRequest r, CreateOrderCommand c) =>
            {
                c.CustomerId = r.CustomerId;
                c.Items = r.Items.Select(i => new OrderItemCommand
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Notes = i.Notes?.Trim()
                }).ToList();
            })
            .Map(static (in CreateOrderRequest r, CreateOrderCommand c) =>
            {
                c.ShippingAddress = new AddressCommand
                {
                    Street = r.Shipping.Street.Trim(),
                    City = r.Shipping.City.Trim(),
                    State = r.Shipping.State.Trim().ToUpperInvariant(),
                    ZipCode = r.Shipping.ZipCode.Trim(),
                    Country = r.Shipping.Country?.ToUpperInvariant() ?? "US"
                };
            })
            .Map(static (in CreateOrderRequest r, CreateOrderCommand c) =>
            {
                c.PaymentMethod = r.Payment.Method;
                c.PaymentToken = r.Payment.Token;
            })
            .Map(static (in CreateOrderRequest r, CreateOrderCommand c) =>
            {
                c.RequestedDeliveryDate = r.RequestedDeliveryDate;
                c.Notes = r.Notes?.Trim();
            })
            .Require(static (in CreateOrderRequest _, CreateOrderCommand c) =>
                c.CustomerId <= 0 ? "Invalid customer ID" : null)
            .Require(static (in CreateOrderRequest _, CreateOrderCommand c) =>
                c.Items.Count == 0 ? "At least one item required" : null)
            .Require(static (in CreateOrderRequest _, CreateOrderCommand c) =>
                c.Items.Any(i => i.Quantity <= 0) ? "Invalid item quantity" : null)
            .Require(static (in CreateOrderRequest _, CreateOrderCommand c) =>
                string.IsNullOrEmpty(c.ShippingAddress.Street) ? "Street required" : null)
            .Require(static (in CreateOrderRequest _, CreateOrderCommand c) =>
                string.IsNullOrEmpty(c.ShippingAddress.ZipCode) ? "Zip code required" : null)
            .Build();

        _responseAdapter = Adapter<Order, OrderResponse>
            .Create(static () => new OrderResponse())
            .Map(static (in Order o, OrderResponse r) =>
            {
                r.Id = o.Id;
                r.OrderNumber = o.OrderNumber;
                r.Status = o.Status.ToString();
                r.CreatedAt = o.CreatedAt.ToString("O");
            })
            .Map(static (in Order o, OrderResponse r) =>
            {
                r.Customer = new CustomerInfo
                {
                    Id = o.Customer.Id,
                    Name = o.Customer.FullName,
                    Email = o.Customer.Email
                };
            })
            .Map(static (in Order o, OrderResponse r) =>
            {
                r.Items = o.Items.Select(i => new OrderItemResponse
                {
                    ProductId = i.ProductId,
                    ProductName = i.Product.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.Quantity * i.UnitPrice
                }).ToList();
            })
            .Map(static (in Order o, OrderResponse r) =>
            {
                r.Totals = new OrderTotals
                {
                    Subtotal = o.Subtotal,
                    Tax = o.Tax,
                    Shipping = o.ShippingCost,
                    Total = o.Total
                };
            })
            .Build();
    }

    public CreateOrderCommand ToCommand(CreateOrderRequest request) =>
        _requestAdapter.Adapt(request);

    public (bool Success, CreateOrderCommand? Command, string? Error) TryToCommand(CreateOrderRequest request)
    {
        if (_requestAdapter.TryAdapt(request, out var command, out var error))
            return (true, command, null);
        return (false, null, error);
    }

    public OrderResponse ToResponse(Order order) =>
        _responseAdapter.Adapt(order);
}

// Usage in controller
[HttpPost]
public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
{
    var (success, command, error) = _adapter.TryToCommand(request);
    if (!success)
        return BadRequest(new { Error = error });

    var order = await _orderService.CreateAsync(command!);
    return Ok(_adapter.ToResponse(order));
}
```

### Why This Pattern

- **Clear boundaries**: External ↔ internal types separated
- **Validation at entry**: Invalid requests rejected early
- **Transformation isolated**: Mapping logic in one place
- **Testable**: Adapters tested independently

---

## Example 2: Legacy System Integration

### The Problem

A modern microservice must integrate with a legacy COBOL mainframe system that uses fixed-width records with different data formats and encodings.

### The Solution

Use Adapter to transform legacy records to modern domain objects with proper parsing and validation.

### The Code

```csharp
public class LegacyOrderAdapter
{
    private readonly Adapter<MainframeOrderRecord, Order> _importAdapter;
    private readonly Adapter<Order, MainframeOrderRecord> _exportAdapter;

    public LegacyOrderAdapter()
    {
        _importAdapter = Adapter<MainframeOrderRecord, Order>
            .Create(static () => new Order())
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // Parse fixed-width COBOL numeric (PIC 9(10))
                o.LegacyId = r.ORDERNUM.Trim();
                o.Id = long.Parse(r.ORDERNUM.Trim());
            })
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // Parse COBOL date (YYYYMMDD)
                o.OrderDate = DateTime.ParseExact(
                    r.ORDDATE.Trim(),
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture);
            })
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // Customer name from separate first/last fields
                var first = r.CUSTFIRST.Trim();
                var last = r.CUSTLAST.Trim();
                o.CustomerName = $"{first} {last}".Trim();
            })
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // COBOL packed decimal (PIC S9(9)V99 COMP-3)
                // Stored as cents, convert to decimal
                var cents = long.Parse(r.AMOUNT.Replace(",", "").Trim());
                o.Amount = cents / 100m;
            })
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // Map status codes
                o.Status = r.ORDSTAT.Trim() switch
                {
                    "P" or "PE" => OrderStatus.Pending,
                    "A" or "AP" => OrderStatus.Approved,
                    "S" or "SH" => OrderStatus.Shipped,
                    "C" or "CO" => OrderStatus.Completed,
                    "X" or "CA" => OrderStatus.Cancelled,
                    _ => OrderStatus.Unknown
                };
            })
            .Map(static (in MainframeOrderRecord r, Order o) =>
            {
                // Parse address from single COBOL field (delimited by ^)
                var parts = r.SHIPADR.Split('^');
                o.ShippingAddress = new Address
                {
                    Street = parts.ElementAtOrDefault(0)?.Trim() ?? "",
                    City = parts.ElementAtOrDefault(1)?.Trim() ?? "",
                    State = parts.ElementAtOrDefault(2)?.Trim() ?? "",
                    ZipCode = parts.ElementAtOrDefault(3)?.Trim() ?? ""
                };
            })
            .Require(static (in MainframeOrderRecord _, Order o) =>
                o.Id <= 0 ? "Invalid order number" : null)
            .Require(static (in MainframeOrderRecord _, Order o) =>
                o.OrderDate == default ? "Invalid order date" : null)
            .Require(static (in MainframeOrderRecord _, Order o) =>
                o.Amount < 0 ? "Invalid amount" : null)
            .Build();

        _exportAdapter = Adapter<Order, MainframeOrderRecord>
            .Create(static () => new MainframeOrderRecord())
            .Map(static (in Order o, MainframeOrderRecord r) =>
            {
                // Format to fixed-width COBOL numeric
                r.ORDERNUM = o.Id.ToString("D10");
            })
            .Map(static (in Order o, MainframeOrderRecord r) =>
            {
                r.ORDDATE = o.OrderDate.ToString("yyyyMMdd");
            })
            .Map(static (in Order o, MainframeOrderRecord r) =>
            {
                var nameParts = o.CustomerName.Split(' ', 2);
                r.CUSTFIRST = (nameParts.ElementAtOrDefault(0) ?? "").PadRight(20);
                r.CUSTLAST = (nameParts.ElementAtOrDefault(1) ?? "").PadRight(25);
            })
            .Map(static (in Order o, MainframeOrderRecord r) =>
            {
                // Convert to cents
                var cents = (long)(o.Amount * 100);
                r.AMOUNT = cents.ToString("D11");
            })
            .Map(static (in Order o, MainframeOrderRecord r) =>
            {
                r.ORDSTAT = o.Status switch
                {
                    OrderStatus.Pending => "PE",
                    OrderStatus.Approved => "AP",
                    OrderStatus.Shipped => "SH",
                    OrderStatus.Completed => "CO",
                    OrderStatus.Cancelled => "CA",
                    _ => "UN"
                };
            })
            .Build();
    }

    public Order Import(MainframeOrderRecord record) =>
        _importAdapter.Adapt(record);

    public MainframeOrderRecord Export(Order order) =>
        _exportAdapter.Adapt(order);
}

// Usage
var legacyRecords = mainframeClient.FetchOrders();
var orders = legacyRecords.Select(r => adapter.Import(r)).ToList();

// Update and send back
foreach (var order in orders.Where(o => o.NeedsUpdate))
{
    var record = adapter.Export(order);
    mainframeClient.UpdateOrder(record);
}
```

### Why This Pattern

- **Format translation**: COBOL ↔ C# types handled
- **Bidirectional**: Import and export adapters
- **Validation**: Invalid legacy data caught early
- **Encapsulated complexity**: Parsing logic isolated

---

## Example 3: Multi-Source Data Aggregation

### The Problem

A dashboard service must aggregate data from multiple microservices (users, orders, inventory) into a unified view model, with each source having different data structures.

### The Solution

Use multiple adapters to normalize each source, then compose into the final view.

### The Code

```csharp
public class DashboardAdapter
{
    private readonly Adapter<UserServiceResponse, UserSummary> _userAdapter;
    private readonly Adapter<OrderServiceResponse, OrderSummary> _orderAdapter;
    private readonly Adapter<InventoryServiceResponse, InventorySummary> _inventoryAdapter;

    public DashboardAdapter()
    {
        _userAdapter = Adapter<UserServiceResponse, UserSummary>
            .Create(static () => new UserSummary())
            .Map(static (in UserServiceResponse r, UserSummary s) =>
            {
                s.TotalUsers = r.Users.Count;
                s.ActiveUsers = r.Users.Count(u => u.Status == "active");
                s.NewUsersToday = r.Users.Count(u =>
                    u.CreatedAt.Date == DateTime.UtcNow.Date);
            })
            .Map(static (in UserServiceResponse r, UserSummary s) =>
            {
                s.UsersByPlan = r.Users
                    .GroupBy(u => u.Plan)
                    .ToDictionary(g => g.Key, g => g.Count());
            })
            .Map(static (in UserServiceResponse r, UserSummary s) =>
            {
                s.TopRegions = r.Users
                    .GroupBy(u => u.Region)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => new RegionStats { Region = g.Key, Count = g.Count() })
                    .ToList();
            })
            .Build();

        _orderAdapter = Adapter<OrderServiceResponse, OrderSummary>
            .Create(static () => new OrderSummary())
            .Map(static (in OrderServiceResponse r, OrderSummary s) =>
            {
                s.TotalOrders = r.Orders.Count;
                s.TotalRevenue = r.Orders.Sum(o => o.Total);
                s.AverageOrderValue = r.Orders.Count > 0
                    ? s.TotalRevenue / r.Orders.Count
                    : 0;
            })
            .Map(static (in OrderServiceResponse r, OrderSummary s) =>
            {
                var today = DateTime.UtcNow.Date;
                var todayOrders = r.Orders.Where(o => o.CreatedAt.Date == today).ToList();
                s.OrdersToday = todayOrders.Count;
                s.RevenueToday = todayOrders.Sum(o => o.Total);
            })
            .Map(static (in OrderServiceResponse r, OrderSummary s) =>
            {
                s.OrdersByStatus = r.Orders
                    .GroupBy(o => o.Status)
                    .ToDictionary(g => g.Key, g => g.Count());
            })
            .Map(static (in OrderServiceResponse r, OrderSummary s) =>
            {
                s.RevenueByDay = r.Orders
                    .GroupBy(o => o.CreatedAt.Date)
                    .OrderBy(g => g.Key)
                    .Take(30)
                    .ToDictionary(
                        g => g.Key.ToString("yyyy-MM-dd"),
                        g => g.Sum(o => o.Total));
            })
            .Build();

        _inventoryAdapter = Adapter<InventoryServiceResponse, InventorySummary>
            .Create(static () => new InventorySummary())
            .Map(static (in InventoryServiceResponse r, InventorySummary s) =>
            {
                s.TotalProducts = r.Items.Count;
                s.TotalStock = r.Items.Sum(i => i.Quantity);
                s.TotalValue = r.Items.Sum(i => i.Quantity * i.UnitCost);
            })
            .Map(static (in InventoryServiceResponse r, InventorySummary s) =>
            {
                s.LowStockItems = r.Items
                    .Where(i => i.Quantity < i.ReorderThreshold)
                    .Select(i => new LowStockItem
                    {
                        ProductId = i.ProductId,
                        Name = i.Name,
                        Current = i.Quantity,
                        Threshold = i.ReorderThreshold
                    })
                    .ToList();
            })
            .Map(static (in InventoryServiceResponse r, InventorySummary s) =>
            {
                s.OutOfStock = r.Items.Count(i => i.Quantity == 0);
            })
            .Build();
    }

    public async Task<DashboardViewModel> BuildDashboardAsync(CancellationToken ct)
    {
        // Fetch from all services in parallel
        var userTask = _userService.GetUsersAsync(ct);
        var orderTask = _orderService.GetOrdersAsync(ct);
        var inventoryTask = _inventoryService.GetInventoryAsync(ct);

        await Task.WhenAll(userTask, orderTask, inventoryTask);

        // Adapt each response
        return new DashboardViewModel
        {
            Users = _userAdapter.Adapt(await userTask),
            Orders = _orderAdapter.Adapt(await orderTask),
            Inventory = _inventoryAdapter.Adapt(await inventoryTask),
            GeneratedAt = DateTime.UtcNow
        };
    }
}
```

### Why This Pattern

- **Source isolation**: Each adapter handles one service
- **Aggregation logic**: Complex transformations encapsulated
- **Parallel friendly**: Adapters can run on parallel results
- **Maintainable**: Changes to one source don't affect others

---

## Example 4: Event Sourcing Projection

### The Problem

An event-sourced system needs to project domain events into read models for different views (list, detail, report), with each view requiring different data shapes.

### The Solution

Use adapters to project events into view-specific read models.

### The Code

```csharp
public class OrderProjectionAdapter
{
    private readonly Adapter<OrderEvent, OrderListItem> _listAdapter;
    private readonly Adapter<OrderEvent, OrderDetail> _detailAdapter;
    private readonly Adapter<OrderEvent, OrderReportRow> _reportAdapter;

    public OrderProjectionAdapter()
    {
        _listAdapter = Adapter<OrderEvent, OrderListItem>
            .Create(static () => new OrderListItem())
            .Map(static (in OrderEvent e, OrderListItem i) =>
            {
                i.OrderId = e.AggregateId;
                i.OrderNumber = e.Data.OrderNumber;
                i.CustomerName = e.Data.CustomerName;
            })
            .Map(static (in OrderEvent e, OrderListItem i) =>
            {
                i.Total = e.Data.Items.Sum(x => x.Quantity * x.UnitPrice);
                i.ItemCount = e.Data.Items.Count;
            })
            .Map(static (in OrderEvent e, OrderListItem i) =>
            {
                i.Status = e.EventType switch
                {
                    "OrderCreated" => "Pending",
                    "OrderConfirmed" => "Confirmed",
                    "OrderShipped" => "Shipped",
                    "OrderDelivered" => "Delivered",
                    "OrderCancelled" => "Cancelled",
                    _ => i.Status // Keep previous
                };
                i.LastUpdated = e.Timestamp;
            })
            .Build();

        _detailAdapter = Adapter<OrderEvent, OrderDetail>
            .Create(static () => new OrderDetail())
            .Map(static (in OrderEvent e, OrderDetail d) =>
            {
                d.OrderId = e.AggregateId;
                d.OrderNumber = e.Data.OrderNumber;
                d.CreatedAt = e.Timestamp;
            })
            .Map(static (in OrderEvent e, OrderDetail d) =>
            {
                d.Customer = new CustomerInfo
                {
                    Id = e.Data.CustomerId,
                    Name = e.Data.CustomerName,
                    Email = e.Data.CustomerEmail,
                    Phone = e.Data.CustomerPhone
                };
            })
            .Map(static (in OrderEvent e, OrderDetail d) =>
            {
                d.ShippingAddress = new AddressInfo
                {
                    Street = e.Data.ShippingStreet,
                    City = e.Data.ShippingCity,
                    State = e.Data.ShippingState,
                    ZipCode = e.Data.ShippingZip,
                    Country = e.Data.ShippingCountry
                };
            })
            .Map(static (in OrderEvent e, OrderDetail d) =>
            {
                d.Items = e.Data.Items.Select(i => new OrderItemInfo
                {
                    ProductId = i.ProductId,
                    ProductName = i.ProductName,
                    Sku = i.Sku,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.Quantity * i.UnitPrice
                }).ToList();

                d.Subtotal = d.Items.Sum(i => i.LineTotal);
                d.Tax = e.Data.TaxAmount;
                d.Shipping = e.Data.ShippingCost;
                d.Total = d.Subtotal + d.Tax + d.Shipping;
            })
            .Build();

        _reportAdapter = Adapter<OrderEvent, OrderReportRow>
            .Create(static () => new OrderReportRow())
            .Map(static (in OrderEvent e, OrderReportRow r) =>
            {
                r.OrderId = e.AggregateId;
                r.OrderDate = e.Timestamp.Date;
                r.OrderMonth = new DateTime(e.Timestamp.Year, e.Timestamp.Month, 1);
            })
            .Map(static (in OrderEvent e, OrderReportRow r) =>
            {
                r.CustomerId = e.Data.CustomerId;
                r.Region = e.Data.ShippingState;
                r.Country = e.Data.ShippingCountry;
            })
            .Map(static (in OrderEvent e, OrderReportRow r) =>
            {
                r.ItemCount = e.Data.Items.Count;
                r.Quantity = e.Data.Items.Sum(i => i.Quantity);
                r.Revenue = e.Data.Items.Sum(i => i.Quantity * i.UnitPrice);
                r.Tax = e.Data.TaxAmount;
                r.Shipping = e.Data.ShippingCost;
            })
            .Map(static (in OrderEvent e, OrderReportRow r) =>
            {
                // Categorize for reporting
                r.Category = r.Revenue switch
                {
                    < 50 => "Small",
                    < 200 => "Medium",
                    < 1000 => "Large",
                    _ => "Enterprise"
                };
            })
            .Build();
    }

    public OrderListItem ToListItem(OrderEvent e) => _listAdapter.Adapt(e);
    public OrderDetail ToDetail(OrderEvent e) => _detailAdapter.Adapt(e);
    public OrderReportRow ToReportRow(OrderEvent e) => _reportAdapter.Adapt(e);
}

// Usage in event handler
public class OrderProjectionHandler
{
    private readonly OrderProjectionAdapter _adapter;
    private readonly IReadModelStore _store;

    public async Task HandleAsync(OrderEvent @event)
    {
        // Update list view
        var listItem = _adapter.ToListItem(@event);
        await _store.UpsertAsync("order-list", listItem.OrderId, listItem);

        // Update detail view
        var detail = _adapter.ToDetail(@event);
        await _store.UpsertAsync("order-detail", detail.OrderId, detail);

        // Update report materialized view
        var reportRow = _adapter.ToReportRow(@event);
        await _store.UpsertAsync("order-report", reportRow.OrderId, reportRow);
    }
}
```

### Why This Pattern

- **View-specific projections**: Each view has its own adapter
- **Event-driven**: Adapters project events to read models
- **CQRS friendly**: Read models optimized for queries
- **Independent evolution**: Views can change independently

---

## Key Takeaways

1. **Boundary mapping**: Adapters excel at system boundaries
2. **Validation integration**: Combine transformation with validation
3. **Bidirectional**: Create import and export adapters
4. **Composition**: Chain adapters for complex transformations
5. **Static lambdas**: Use for allocation-free hot paths

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
