# Message Expiration

Message Expiration stamps messages with a deadline and lets consumers reject stale work before it mutates state. Use it at queue, inbox, handler, or workflow boundaries where a command has business value only for a limited time.

```csharp
var expiration = MessageExpiration<OrderCommand>.Create()
    .Name("order-message-expiration")
    .Header("x-order-expires-at")
    .DefaultTtl(TimeSpan.FromMinutes(20))
    .ExpiredReason("Order command expired before fulfillment accepted it.")
    .Build();

var accepted = expiration.Stamp(Message<OrderCommand>.Create(command));
var result = expiration.Evaluate(accepted);
```

`Stamp` preserves an existing deadline by default so upstream transport or gateway policies keep precedence. Call `PreserveExisting(false)` when the local boundary owns the deadline.

## Production Notes

- Store the expiration deadline as message metadata, not payload state, so routers, filters, and consumers can evaluate it consistently.
- Use a deterministic `Clock` in tests and a UTC clock in production.
- Treat expired messages as rejected work: route to a dead-letter channel, audit log, or compensating workflow instead of silently dropping state-changing commands.
