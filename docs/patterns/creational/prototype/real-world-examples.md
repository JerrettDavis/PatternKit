# Prototype Pattern Real-World Examples

Production-ready examples demonstrating the Prototype pattern in real-world scenarios.

---

## Example 1: Test Data Factory

### The Problem

A test suite needs to create complex domain objects with sensible defaults, allowing individual tests to customize only the properties they care about.

### The Solution

Use Prototype to define base test objects with default values and per-test customizations.

### The Code

```csharp
public class TestDataFactory
{
    private readonly Prototype<Customer> _customer;
    private readonly Prototype<Order> _order;
    private readonly Prototype<Product> _product;

    public TestDataFactory()
    {
        _customer = Prototype<Customer>
            .Create(new Customer
            {
                Name = "Test Customer",
                Email = "test@example.com",
                Status = CustomerStatus.Active,
                Address = new Address
                {
                    Street = "123 Test St",
                    City = "Test City",
                    Country = "US",
                    ZipCode = "12345"
                },
                PaymentMethods = new List<PaymentMethod>()
            }, CloneCustomer)
            .With(c => c.Id = Guid.NewGuid())
            .With(c => c.CreatedAt = DateTime.UtcNow)
            .Build();

        _order = Prototype<Order>
            .Create(new Order
            {
                Status = OrderStatus.Pending,
                Items = new List<OrderItem>(),
                ShippingMethod = "standard",
                Currency = "USD"
            }, CloneOrder)
            .With(o => o.Id = Guid.NewGuid())
            .With(o => o.OrderNumber = GenerateOrderNumber())
            .With(o => o.CreatedAt = DateTime.UtcNow)
            .Build();

        _product = Prototype<Product>
            .Create(new Product
            {
                Name = "Test Product",
                Description = "A test product",
                Price = 9.99m,
                Currency = "USD",
                StockQuantity = 100,
                IsActive = true
            }, CloneProduct)
            .With(p => p.Id = Guid.NewGuid())
            .With(p => p.Sku = GenerateSku())
            .Build();
    }

    public Customer CreateCustomer(Action<Customer>? customize = null)
        => _customer.Create(customize);

    public Order CreateOrder(Customer customer, Action<Order>? customize = null)
        => _order.Create(o =>
        {
            o.CustomerId = customer.Id;
            o.CustomerName = customer.Name;
            customize?.Invoke(o);
        });

    public Product CreateProduct(Action<Product>? customize = null)
        => _product.Create(customize);

    public OrderItem CreateOrderItem(Product product, int quantity = 1) => new()
    {
        ProductId = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        UnitPrice = product.Price,
        Quantity = quantity
    };

    private static Customer CloneCustomer(in Customer c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Email = c.Email,
        Status = c.Status,
        CreatedAt = c.CreatedAt,
        Address = c.Address != null ? new Address
        {
            Street = c.Address.Street,
            City = c.Address.City,
            Country = c.Address.Country,
            ZipCode = c.Address.ZipCode
        } : null,
        PaymentMethods = new List<PaymentMethod>(c.PaymentMethods ?? new())
    };

    // ... other clone methods
}

// Usage in tests
[Fact]
public async Task Should_process_order_for_premium_customer()
{
    // Arrange
    var customer = _factory.CreateCustomer(c =>
    {
        c.Status = CustomerStatus.Premium;
        c.LoyaltyPoints = 5000;
    });

    var product = _factory.CreateProduct(p => p.Price = 99.99m);
    var order = _factory.CreateOrder(customer, o =>
    {
        o.Items.Add(_factory.CreateOrderItem(product, quantity: 2));
    });

    // Act & Assert...
}
```

### Why This Pattern

- **Consistent defaults**: All tests start with valid, realistic data
- **Minimal customization**: Tests only specify what they care about
- **Isolated mutations**: Each test gets independent copies
- **Deep cloning**: Nested objects properly isolated

---

## Example 2: Email Template System

### The Problem

A marketing platform needs to create email campaigns from predefined templates, with each campaign customizing specific elements while preserving base styling and structure.

### The Solution

Use Prototype registry to store email templates by type, with mutations for common customizations.

### The Code

```csharp
public class EmailTemplateFactory
{
    private readonly Prototype<string, EmailTemplate> _templates;

    public EmailTemplateFactory(ITemplateRepository repository)
    {
        _templates = Prototype<string, EmailTemplate>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("welcome", new EmailTemplate
            {
                Type = "welcome",
                Subject = "Welcome to {{company}}!",
                HtmlBody = repository.LoadHtml("welcome.html"),
                TextBody = repository.LoadText("welcome.txt"),
                FromName = "{{company}} Team",
                FromEmail = "hello@{{domain}}",
                ReplyTo = "support@{{domain}}",
                Styles = new EmailStyles
                {
                    PrimaryColor = "#007bff",
                    FontFamily = "Arial, sans-serif",
                    LogoUrl = "{{cdn}}/logo.png"
                },
                TrackingEnabled = true,
                Category = "transactional"
            }, Clone)
            .Map("newsletter", new EmailTemplate
            {
                Type = "newsletter",
                Subject = "{{company}} Newsletter - {{date}}",
                HtmlBody = repository.LoadHtml("newsletter.html"),
                TextBody = repository.LoadText("newsletter.txt"),
                FromName = "{{company}}",
                FromEmail = "newsletter@{{domain}}",
                Styles = new EmailStyles
                {
                    PrimaryColor = "#28a745",
                    FontFamily = "Georgia, serif",
                    LogoUrl = "{{cdn}}/newsletter-logo.png"
                },
                TrackingEnabled = true,
                Category = "marketing",
                UnsubscribeRequired = true
            }, Clone)
            .Map("password-reset", new EmailTemplate
            {
                Type = "password-reset",
                Subject = "Reset your {{company}} password",
                HtmlBody = repository.LoadHtml("password-reset.html"),
                TextBody = repository.LoadText("password-reset.txt"),
                FromEmail = "security@{{domain}}",
                FromName = "{{company}} Security",
                Priority = EmailPriority.High,
                ExpiresInHours = 1,
                TrackingEnabled = false,  // Security email, no tracking
                Category = "transactional"
            }, Clone)
            .Map("invoice", new EmailTemplate
            {
                Type = "invoice",
                Subject = "Invoice #{{invoice_number}} from {{company}}",
                HtmlBody = repository.LoadHtml("invoice.html"),
                TextBody = repository.LoadText("invoice.txt"),
                FromEmail = "billing@{{domain}}",
                FromName = "{{company}} Billing",
                AttachPdf = true,
                Category = "transactional"
            }, Clone)
            .Default(new EmailTemplate
            {
                Type = "generic",
                Subject = "Message from {{company}}",
                HtmlBody = "<p>{{content}}</p>",
                TextBody = "{{content}}",
                FromEmail = "info@{{domain}}",
                Category = "transactional"
            }, Clone)
            .Build();
    }

    public EmailTemplate GetTemplate(string templateType, BrandSettings brand)
    {
        return _templates.Create(templateType, t =>
        {
            // Apply brand-specific customizations
            t.Variables["company"] = brand.CompanyName;
            t.Variables["domain"] = brand.Domain;
            t.Variables["cdn"] = brand.CdnUrl;

            if (brand.CustomColors != null)
            {
                t.Styles.PrimaryColor = brand.CustomColors.Primary;
                t.Styles.SecondaryColor = brand.CustomColors.Secondary;
            }

            if (!string.IsNullOrEmpty(brand.CustomLogoUrl))
            {
                t.Styles.LogoUrl = brand.CustomLogoUrl;
            }
        });
    }

    public EmailTemplate GetCampaignTemplate(
        string templateType,
        Campaign campaign,
        BrandSettings brand)
    {
        return _templates.Create(templateType, t =>
        {
            // Brand settings
            t.Variables["company"] = brand.CompanyName;
            t.Variables["domain"] = brand.Domain;

            // Campaign settings
            t.CampaignId = campaign.Id;
            t.Subject = campaign.Subject ?? t.Subject;
            t.Variables["date"] = campaign.SendDate.ToString("MMMM yyyy");

            if (campaign.AbTest != null)
            {
                t.AbTestVariant = campaign.AbTest.CurrentVariant;
            }

            // Override tracking if campaign specifies
            if (campaign.DisableTracking)
                t.TrackingEnabled = false;
        });
    }

    private static EmailTemplate Clone(in EmailTemplate t) => new()
    {
        Type = t.Type,
        Subject = t.Subject,
        HtmlBody = t.HtmlBody,
        TextBody = t.TextBody,
        FromName = t.FromName,
        FromEmail = t.FromEmail,
        ReplyTo = t.ReplyTo,
        Priority = t.Priority,
        TrackingEnabled = t.TrackingEnabled,
        Category = t.Category,
        UnsubscribeRequired = t.UnsubscribeRequired,
        AttachPdf = t.AttachPdf,
        ExpiresInHours = t.ExpiresInHours,
        Styles = t.Styles != null ? new EmailStyles
        {
            PrimaryColor = t.Styles.PrimaryColor,
            SecondaryColor = t.Styles.SecondaryColor,
            FontFamily = t.Styles.FontFamily,
            LogoUrl = t.Styles.LogoUrl
        } : null,
        Variables = new Dictionary<string, string>(t.Variables ?? new())
    };
}

// Usage
var template = factory.GetTemplate("welcome", brandSettings);
var email = emailService.Render(template, userData);
await emailService.SendAsync(email);
```

### Why This Pattern

- **Template isolation**: Each campaign gets independent template copy
- **Brand customization**: Apply brand settings without modifying originals
- **Campaign overrides**: Per-campaign customizations layer on top
- **Deep cloning**: Nested Styles and Variables properly copied

---

## Example 3: Game Character Factory

### The Problem

A game needs to spawn enemies, NPCs, and items from predefined archetypes, with each instance having unique IDs and spawn-time modifications.

### The Solution

Use Prototype registry for each entity type with spawn-time mutations.

### The Code

```csharp
public class EntityFactory
{
    private readonly Prototype<EnemyType, Enemy> _enemies;
    private readonly Prototype<ItemType, Item> _items;
    private readonly Prototype<string, Npc> _npcs;

    public EntityFactory(IAssetLoader assets)
    {
        _enemies = Prototype<EnemyType, Enemy>
            .Create()
            .Map(EnemyType.Slime, new Enemy
            {
                Name = "Slime",
                BaseHealth = 20,
                BaseDamage = 3,
                Speed = 0.8f,
                XpReward = 5,
                LootTable = "slime_loot",
                Sprite = assets.LoadSprite("enemies/slime"),
                Behaviors = new[] { "wander", "chase_player" },
                Resistances = new Dictionary<DamageType, float>
                {
                    [DamageType.Physical] = 0.5f,
                    [DamageType.Fire] = -0.5f  // Weak to fire
                }
            }, CloneEnemy)
            .Map(EnemyType.Skeleton, new Enemy
            {
                Name = "Skeleton",
                BaseHealth = 40,
                BaseDamage = 8,
                Speed = 1.0f,
                XpReward = 15,
                LootTable = "skeleton_loot",
                Sprite = assets.LoadSprite("enemies/skeleton"),
                Behaviors = new[] { "patrol", "chase_player", "ranged_attack" },
                Resistances = new Dictionary<DamageType, float>
                {
                    [DamageType.Physical] = 0.2f,
                    [DamageType.Holy] = -1.0f
                }
            }, CloneEnemy)
            .Map(EnemyType.Dragon, new Enemy
            {
                Name = "Ancient Dragon",
                BaseHealth = 1000,
                BaseDamage = 50,
                Speed = 1.5f,
                XpReward = 500,
                LootTable = "dragon_loot",
                Sprite = assets.LoadSprite("enemies/dragon"),
                Behaviors = new[] { "fly", "breath_attack", "tail_swipe" },
                IsBoss = true,
                Resistances = new Dictionary<DamageType, float>
                {
                    [DamageType.Fire] = 1.0f,  // Immune
                    [DamageType.Ice] = -0.5f
                }
            }, CloneEnemy)
            .Build();

        _items = Prototype<ItemType, Item>
            .Create()
            .Map(ItemType.HealthPotion, new Item
            {
                Name = "Health Potion",
                Description = "Restores 50 HP",
                Icon = assets.LoadIcon("items/health_potion"),
                StackSize = 99,
                Consumable = true,
                Effect = new HealEffect { Amount = 50 }
            }, CloneItem)
            .Map(ItemType.IronSword, new Item
            {
                Name = "Iron Sword",
                Description = "A sturdy iron blade",
                Icon = assets.LoadIcon("items/iron_sword"),
                StackSize = 1,
                EquipSlot = EquipSlot.MainHand,
                Stats = new ItemStats { Damage = 15, Speed = 1.0f }
            }, CloneItem)
            .Build();
    }

    public Enemy SpawnEnemy(EnemyType type, Vector3 position, int areaLevel)
    {
        return _enemies.Create(type, e =>
        {
            e.Id = Guid.NewGuid();
            e.Position = position;
            e.SpawnTime = DateTime.UtcNow;

            // Scale to area level
            var levelMod = 1 + (areaLevel * 0.1f);
            e.Health = (int)(e.BaseHealth * levelMod);
            e.MaxHealth = e.Health;
            e.Damage = (int)(e.BaseDamage * levelMod);
            e.Level = areaLevel;
            e.XpReward = (int)(e.XpReward * levelMod);
        });
    }

    public Enemy SpawnElite(EnemyType type, Vector3 position, int areaLevel)
    {
        return _enemies.Create(type, e =>
        {
            e.Id = Guid.NewGuid();
            e.Position = position;
            e.IsElite = true;
            e.Name = "Elite " + e.Name;

            // Elite scaling
            var levelMod = 1 + (areaLevel * 0.15f);
            e.Health = (int)(e.BaseHealth * levelMod * 2);
            e.MaxHealth = e.Health;
            e.Damage = (int)(e.BaseDamage * levelMod * 1.5f);
            e.Level = areaLevel;
            e.XpReward = (int)(e.XpReward * levelMod * 3);
        });
    }

    public Item CreateItem(ItemType type, int quantity = 1)
    {
        return _items.Create(type, i =>
        {
            i.Id = Guid.NewGuid();
            i.Quantity = Math.Min(quantity, i.StackSize);
        });
    }

    private static Enemy CloneEnemy(in Enemy e) => new()
    {
        Name = e.Name,
        BaseHealth = e.BaseHealth,
        BaseDamage = e.BaseDamage,
        Speed = e.Speed,
        XpReward = e.XpReward,
        LootTable = e.LootTable,
        Sprite = e.Sprite,  // Shared reference OK for immutable assets
        IsBoss = e.IsBoss,
        Behaviors = e.Behaviors.ToArray(),
        Resistances = new Dictionary<DamageType, float>(e.Resistances)
    };

    private static Item CloneItem(in Item i) => new()
    {
        Name = i.Name,
        Description = i.Description,
        Icon = i.Icon,
        StackSize = i.StackSize,
        Consumable = i.Consumable,
        EquipSlot = i.EquipSlot,
        Effect = i.Effect,  // Shared reference for stateless effects
        Stats = i.Stats != null ? new ItemStats
        {
            Damage = i.Stats.Damage,
            Speed = i.Stats.Speed
        } : null
    };
}

// Usage
var dragon = entityFactory.SpawnEnemy(EnemyType.Dragon, bossRoom.SpawnPoint, dungeonLevel);
var eliteSkeleton = entityFactory.SpawnElite(EnemyType.Skeleton, spawn, areaLevel);
var potion = entityFactory.CreateItem(ItemType.HealthPotion, 5);
```

### Why This Pattern

- **Fast spawning**: Clone cheaper than full construction
- **Level scaling**: Apply spawn-time modifications
- **Asset sharing**: Sprites/icons shared safely
- **Variant support**: Elite/boss modifications

---

## Example 4: Cloud Resource Templates

### The Problem

An infrastructure-as-code tool needs to create cloud resources from predefined templates with environment-specific and deployment-specific customizations.

### The Solution

Use Prototype to define resource templates with layered customizations.

### The Code

```csharp
public class ResourceTemplateFactory
{
    private readonly Prototype<string, ResourceTemplate> _templates;

    public ResourceTemplateFactory()
    {
        _templates = Prototype<string, ResourceTemplate>
            .Create(StringComparer.OrdinalIgnoreCase)
            .Map("web-app", new ResourceTemplate
            {
                ResourceType = "Microsoft.Web/sites",
                ApiVersion = "2022-03-01",
                Kind = "app,linux",
                Sku = new ResourceSku { Name = "P1v2", Tier = "PremiumV2" },
                Properties = new Dictionary<string, object>
                {
                    ["httpsOnly"] = true,
                    ["minTlsVersion"] = "1.2",
                    ["http20Enabled"] = true,
                    ["ftpsState"] = "Disabled"
                },
                Tags = new Dictionary<string, string>
                {
                    ["managed-by"] = "terraform",
                    ["template"] = "web-app"
                }
            }, Clone)
            .Map("function-app", new ResourceTemplate
            {
                ResourceType = "Microsoft.Web/sites",
                ApiVersion = "2022-03-01",
                Kind = "functionapp,linux",
                Sku = new ResourceSku { Name = "Y1", Tier = "Dynamic" },
                Properties = new Dictionary<string, object>
                {
                    ["httpsOnly"] = true,
                    ["minTlsVersion"] = "1.2",
                    ["reserved"] = true
                },
                Tags = new Dictionary<string, string>
                {
                    ["managed-by"] = "terraform",
                    ["template"] = "function-app"
                }
            }, Clone)
            .Map("storage-account", new ResourceTemplate
            {
                ResourceType = "Microsoft.Storage/storageAccounts",
                ApiVersion = "2022-09-01",
                Sku = new ResourceSku { Name = "Standard_LRS" },
                Properties = new Dictionary<string, object>
                {
                    ["supportsHttpsTrafficOnly"] = true,
                    ["minimumTlsVersion"] = "TLS1_2",
                    ["allowBlobPublicAccess"] = false
                },
                Tags = new Dictionary<string, string>
                {
                    ["managed-by"] = "terraform",
                    ["template"] = "storage-account"
                }
            }, Clone)
            .Map("sql-database", new ResourceTemplate
            {
                ResourceType = "Microsoft.Sql/servers/databases",
                ApiVersion = "2022-05-01-preview",
                Sku = new ResourceSku { Name = "GP_Gen5_2", Tier = "GeneralPurpose" },
                Properties = new Dictionary<string, object>
                {
                    ["collation"] = "SQL_Latin1_General_CP1_CI_AS",
                    ["maxSizeBytes"] = 34359738368,
                    ["zoneRedundant"] = false
                },
                Tags = new Dictionary<string, string>
                {
                    ["managed-by"] = "terraform",
                    ["template"] = "sql-database"
                }
            }, Clone)
            .Build();
    }

    public ResourceTemplate GetTemplate(
        string templateName,
        EnvironmentConfig env,
        DeploymentConfig deployment)
    {
        return _templates.Create(templateName, t =>
        {
            // Environment-specific settings
            t.Location = env.Region;
            t.Tags["environment"] = env.Name;
            t.Tags["cost-center"] = env.CostCenter;

            // Deployment-specific settings
            t.Name = $"{deployment.ResourcePrefix}-{templateName}-{env.Name}";
            t.ResourceGroup = deployment.ResourceGroup;
            t.Tags["deployment-id"] = deployment.Id;
            t.Tags["deployed-by"] = deployment.DeployedBy;
            t.Tags["deployed-at"] = DateTime.UtcNow.ToString("O");

            // Environment tier adjustments
            if (env.Name == "production")
            {
                t.Properties["zoneRedundant"] = true;
                if (t.Sku.Tier == "PremiumV2")
                    t.Sku = new ResourceSku { Name = "P2v2", Tier = "PremiumV2" };
            }
            else if (env.Name == "development")
            {
                // Downgrade for dev
                if (t.Sku.Tier == "PremiumV2")
                    t.Sku = new ResourceSku { Name = "B1", Tier = "Basic" };
                if (t.Sku.Name.StartsWith("GP_Gen5"))
                    t.Sku = new ResourceSku { Name = "Basic", Tier = "Basic" };
            }
        });
    }

    private static ResourceTemplate Clone(in ResourceTemplate t) => new()
    {
        ResourceType = t.ResourceType,
        ApiVersion = t.ApiVersion,
        Kind = t.Kind,
        Location = t.Location,
        Name = t.Name,
        ResourceGroup = t.ResourceGroup,
        Sku = t.Sku != null ? new ResourceSku
        {
            Name = t.Sku.Name,
            Tier = t.Sku.Tier
        } : null,
        Properties = new Dictionary<string, object>(t.Properties),
        Tags = new Dictionary<string, string>(t.Tags)
    };
}

// Usage
var webApp = factory.GetTemplate("web-app", prodEnv, deployment);
var storage = factory.GetTemplate("storage-account", devEnv, deployment);

await deployer.DeployAsync(new[] { webApp, storage });
```

### Why This Pattern

- **Template reuse**: Base templates shared across environments
- **Layered customization**: Environment + deployment settings
- **Isolation**: Each deployment gets independent resources
- **Consistency**: Common properties enforced

---

## Key Takeaways

1. **Clone, don't construct**: Prototype is cheaper when setup is expensive
2. **Deep vs shallow**: Understand what needs independent copies
3. **Layered mutations**: Compose default + per-call customizations
4. **Registry for families**: Use keyed prototypes for related archetypes
5. **Thread-safe access**: Built prototypes safe for concurrent use

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
