using TinyBDD;

namespace PatternKit.Generators.Tests;

/// <summary>
/// Tests for PatternKit.Generators.Abstractions attributes to ensure proper
/// attribute construction and property assignment.
/// </summary>
public class AbstractionsTests
{
    #region GenerateCacheStampedeProtectionAttribute Tests

    [Scenario("GenerateCacheStampedeProtectionAttribute Constructor Sets Properties")]
    [Fact]
    public void GenerateCacheStampedeProtectionAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.CacheStampedeProtection.GenerateCacheStampedeProtectionAttribute(typeof(string))
        {
            FactoryMethodName = "CreateCatalogSingleFlight",
            PolicyName = "catalog-single-flight"
        };

        ScenarioExpect.Equal(typeof(string), attr.ResultType);
        ScenarioExpect.Equal("CreateCatalogSingleFlight", attr.FactoryMethodName);
        ScenarioExpect.Equal("catalog-single-flight", attr.PolicyName);
        ScenarioExpect.Throws<ArgumentNullException>(() => new PatternKit.Generators.CacheStampedeProtection.GenerateCacheStampedeProtectionAttribute(null!));
    }

    [Scenario("GenerateCacheStampedeProtectionAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateCacheStampedeProtectionAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(PatternKit.Generators.CacheStampedeProtection.GenerateCacheStampedeProtectionAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region GenerateReadWriteThroughCachePolicyAttribute Tests

    [Scenario("GenerateReadWriteThroughCachePolicyAttribute Constructor Sets Properties")]
    [Fact]
    public void GenerateReadWriteThroughCachePolicyAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.ReadWriteThroughCache.GenerateReadWriteThroughCachePolicyAttribute(typeof(string))
        {
            FactoryMethodName = "CreateCatalogCache",
            PolicyName = "catalog-read-write-through",
            TimeToLiveMilliseconds = 500
        };

        ScenarioExpect.Equal(typeof(string), attr.ResultType);
        ScenarioExpect.Equal("CreateCatalogCache", attr.FactoryMethodName);
        ScenarioExpect.Equal("catalog-read-write-through", attr.PolicyName);
        ScenarioExpect.Equal(500, attr.TimeToLiveMilliseconds);
        ScenarioExpect.Throws<ArgumentNullException>(() => new PatternKit.Generators.ReadWriteThroughCache.GenerateReadWriteThroughCachePolicyAttribute(null!));
    }

    [Scenario("GenerateReadWriteThroughCachePolicyAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateReadWriteThroughCachePolicyAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(PatternKit.Generators.ReadWriteThroughCache.GenerateReadWriteThroughCachePolicyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region GenerateManualTaskGateAttribute Tests

    [Scenario("GenerateManualTaskGateAttribute Constructor Sets Properties")]
    [Fact]
    public void GenerateManualTaskGateAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.ManualTaskGates.GenerateManualTaskGateAttribute(typeof(Guid))
        {
            FactoryMethodName = "CreateApprovalGate",
            GateName = "approval-gate"
        };

        ScenarioExpect.Equal(typeof(Guid), attr.KeyType);
        ScenarioExpect.Equal("CreateApprovalGate", attr.FactoryMethodName);
        ScenarioExpect.Equal("approval-gate", attr.GateName);
    }

    [Scenario("GenerateManualTaskGateAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateManualTaskGateAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(PatternKit.Generators.ManualTaskGates.GenerateManualTaskGateAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region GenerateTimeoutManagerAttribute Tests

    [Scenario("GenerateTimeoutManagerAttribute Constructor Sets Properties")]
    [Fact]
    public void GenerateTimeoutManagerAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.Timeouts.GenerateTimeoutManagerAttribute(typeof(Guid))
        {
            FactoryMethodName = "CreateReservationTimeouts",
            ManagerName = "reservation-timeouts"
        };

        ScenarioExpect.Equal(typeof(Guid), attr.KeyType);
        ScenarioExpect.Equal("CreateReservationTimeouts", attr.FactoryMethodName);
        ScenarioExpect.Equal("reservation-timeouts", attr.ManagerName);
    }

    [Scenario("GenerateTimeoutManagerAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateTimeoutManagerAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(PatternKit.Generators.Timeouts.GenerateTimeoutManagerAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region GenerateSnapshotCheckpointManagerAttribute Tests

    [Scenario("GenerateSnapshotCheckpointManagerAttribute Constructor Sets Properties")]
    [Fact]
    public void GenerateSnapshotCheckpointManagerAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.SnapshotCheckpoints.GenerateSnapshotCheckpointManagerAttribute(typeof(Guid), typeof(string))
        {
            FactoryMethodName = "CreateOrderReplayCheckpoints",
            ManagerName = "order-replay-checkpoints"
        };

        ScenarioExpect.Equal(typeof(Guid), attr.KeyType);
        ScenarioExpect.Equal(typeof(string), attr.SnapshotType);
        ScenarioExpect.Equal("CreateOrderReplayCheckpoints", attr.FactoryMethodName);
        ScenarioExpect.Equal("order-replay-checkpoints", attr.ManagerName);
    }

    [Scenario("GenerateSnapshotCheckpointManagerAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateSnapshotCheckpointManagerAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(PatternKit.Generators.SnapshotCheckpoints.GenerateSnapshotCheckpointManagerAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region WorkflowOrchestrationAttribute Tests

    [Scenario("WorkflowOrchestrationAttribute Constructor Sets Properties")]
    [Fact]
    public void WorkflowOrchestrationAttribute_Constructor_Sets_Properties()
    {
        var attr = new PatternKit.Generators.WorkflowOrchestration.WorkflowOrchestrationAttribute
        {
            FactoryMethodName = "CreateFulfillment",
            WorkflowName = "fulfillment"
        };
        var step = new PatternKit.Generators.WorkflowOrchestration.WorkflowStepAttribute("reserve", 1)
        {
            MaxAttempts = 3,
            Condition = "ShouldReserve",
            Compensation = "ReleaseInventory"
        };

        ScenarioExpect.Equal("CreateFulfillment", attr.FactoryMethodName);
        ScenarioExpect.Equal("fulfillment", attr.WorkflowName);
        ScenarioExpect.Equal("reserve", step.Name);
        ScenarioExpect.Equal(1, step.Order);
        ScenarioExpect.Equal(3, step.MaxAttempts);
        ScenarioExpect.Equal("ShouldReserve", step.Condition);
        ScenarioExpect.Equal("ReleaseInventory", step.Compensation);
    }

    [Scenario("WorkflowOrchestrationAttributes Have Correct AttributeUsage")]
    [Fact]
    public void WorkflowOrchestrationAttributes_Have_Correct_AttributeUsage()
    {
        var orchestrationUsage = typeof(PatternKit.Generators.WorkflowOrchestration.WorkflowOrchestrationAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();
        var stepUsage = typeof(PatternKit.Generators.WorkflowOrchestration.WorkflowStepAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, orchestrationUsage.ValidOn);
        ScenarioExpect.False(orchestrationUsage.Inherited);
        ScenarioExpect.Equal(AttributeTargets.Method, stepUsage.ValidOn);
        ScenarioExpect.False(stepUsage.Inherited);
    }

    #endregion

    #region GenerateStrategyAttribute Tests

    [Scenario("GenerateStrategyAttribute Action Constructor Sets Properties")]
    [Fact]
    public void GenerateStrategyAttribute_Action_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("TestStrategy", typeof(string), StrategyKind.Action);

        ScenarioExpect.Equal("TestStrategy", attr.Name);
        ScenarioExpect.Equal(typeof(string), attr.InType);
        ScenarioExpect.Null(attr.OutType);
        ScenarioExpect.Equal(StrategyKind.Action, attr.Kind);
    }

    [Scenario("GenerateStrategyAttribute Result Constructor Sets Properties")]
    [Fact]
    public void GenerateStrategyAttribute_Result_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("TestStrategy", typeof(int), typeof(string), StrategyKind.Result);

        ScenarioExpect.Equal("TestStrategy", attr.Name);
        ScenarioExpect.Equal(typeof(int), attr.InType);
        ScenarioExpect.Equal(typeof(string), attr.OutType);
        ScenarioExpect.Equal(StrategyKind.Result, attr.Kind);
    }

    [Scenario("GenerateStrategyAttribute Try Constructor Sets Properties")]
    [Fact]
    public void GenerateStrategyAttribute_Try_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("Parser", typeof(string), typeof(int), StrategyKind.Try);

        ScenarioExpect.Equal("Parser", attr.Name);
        ScenarioExpect.Equal(typeof(string), attr.InType);
        ScenarioExpect.Equal(typeof(int), attr.OutType);
        ScenarioExpect.Equal(StrategyKind.Try, attr.Kind);
    }

    [Scenario("GenerateStrategyAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateStrategyAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(GenerateStrategyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class, usage.ValidOn);
        ScenarioExpect.True(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    [Scenario("StrategyKind Enum Values Are Valid")]
    [Theory]
    [InlineData(StrategyKind.Action)]
    [InlineData(StrategyKind.Result)]
    [InlineData(StrategyKind.Try)]
    public void StrategyKind_Enum_Values_Are_Valid(StrategyKind kind)
    {
        ScenarioExpect.True(Enum.IsDefined(typeof(StrategyKind), kind));
    }

    #endregion

    #region GenerateBuilderAttribute Tests

    [Scenario("GenerateBuilderAttribute Default Properties")]
    [Fact]
    public void GenerateBuilderAttribute_Default_Properties()
    {
        var attr = new Builders.GenerateBuilderAttribute();

        ScenarioExpect.Null(attr.BuilderTypeName);
        ScenarioExpect.Equal("New", attr.NewMethodName);
        ScenarioExpect.Equal("Build", attr.BuildMethodName);
        ScenarioExpect.Equal(Builders.BuilderModel.MutableInstance, attr.Model);
        ScenarioExpect.False(attr.GenerateBuilderMethods);
        ScenarioExpect.False(attr.ForceAsync);
        ScenarioExpect.False(attr.IncludeFields);
    }

    [Scenario("GenerateBuilderAttribute Custom Properties")]
    [Fact]
    public void GenerateBuilderAttribute_Custom_Properties()
    {
        var attr = new Builders.GenerateBuilderAttribute
        {
            BuilderTypeName = "PersonBuilder",
            NewMethodName = "Create",
            BuildMethodName = "Construct",
            Model = Builders.BuilderModel.StateProjection,
            GenerateBuilderMethods = true,
            ForceAsync = true,
            IncludeFields = true
        };

        ScenarioExpect.Equal("PersonBuilder", attr.BuilderTypeName);
        ScenarioExpect.Equal("Create", attr.NewMethodName);
        ScenarioExpect.Equal("Construct", attr.BuildMethodName);
        ScenarioExpect.Equal(Builders.BuilderModel.StateProjection, attr.Model);
        ScenarioExpect.True(attr.GenerateBuilderMethods);
        ScenarioExpect.True(attr.ForceAsync);
        ScenarioExpect.True(attr.IncludeFields);
    }

    [Scenario("GenerateBuilderAttribute Has Correct AttributeUsage")]
    [Fact]
    public void GenerateBuilderAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.GenerateBuilderAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        ScenarioExpect.False(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    [Scenario("BuilderModel Enum Values Are Valid")]
    [Theory]
    [InlineData(Builders.BuilderModel.MutableInstance)]
    [InlineData(Builders.BuilderModel.StateProjection)]
    public void BuilderModel_Enum_Values_Are_Valid(Builders.BuilderModel model)
    {
        ScenarioExpect.True(Enum.IsDefined(typeof(Builders.BuilderModel), model));
    }

    #endregion

    #region BuilderConstructorAttribute Tests

    [Scenario("BuilderConstructorAttribute Can Be Created")]
    [Fact]
    public void BuilderConstructorAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderConstructorAttribute();
        ScenarioExpect.NotNull(attr);
    }

    [Scenario("BuilderConstructorAttribute Has Correct AttributeUsage")]
    [Fact]
    public void BuilderConstructorAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderConstructorAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Constructor, usage.ValidOn);
        ScenarioExpect.False(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region BuilderIgnoreAttribute Tests

    [Scenario("BuilderIgnoreAttribute Can Be Created")]
    [Fact]
    public void BuilderIgnoreAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderIgnoreAttribute();
        ScenarioExpect.NotNull(attr);
    }

    [Scenario("BuilderIgnoreAttribute Has Correct AttributeUsage")]
    [Fact]
    public void BuilderIgnoreAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderIgnoreAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        ScenarioExpect.False(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region BuilderRequiredAttribute Tests

    [Scenario("BuilderRequiredAttribute Default Properties")]
    [Fact]
    public void BuilderRequiredAttribute_Default_Properties()
    {
        var attr = new Builders.BuilderRequiredAttribute();

        ScenarioExpect.Null(attr.Message);
    }

    [Scenario("BuilderRequiredAttribute Custom Message")]
    [Fact]
    public void BuilderRequiredAttribute_Custom_Message()
    {
        var attr = new Builders.BuilderRequiredAttribute
        {
            Message = "This field is required."
        };

        ScenarioExpect.Equal("This field is required.", attr.Message);
    }

    [Scenario("BuilderRequiredAttribute Has Correct AttributeUsage")]
    [Fact]
    public void BuilderRequiredAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderRequiredAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        ScenarioExpect.False(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region BuilderProjectorAttribute Tests

    [Scenario("BuilderProjectorAttribute Can Be Created")]
    [Fact]
    public void BuilderProjectorAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderProjectorAttribute();
        ScenarioExpect.NotNull(attr);
    }

    [Scenario("BuilderProjectorAttribute Has Correct AttributeUsage")]
    [Fact]
    public void BuilderProjectorAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderProjectorAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Method, usage.ValidOn);
        ScenarioExpect.False(usage.AllowMultiple);
        ScenarioExpect.False(usage.Inherited);
    }

    #endregion

    #region FactoryMethodAttribute Tests

    [Scenario("FactoryMethodAttribute Constructor Sets KeyType")]
    [Fact]
    public void FactoryMethodAttribute_Constructor_Sets_KeyType()
    {
        var attr = new Factories.FactoryMethodAttribute(typeof(string));

        ScenarioExpect.Equal(typeof(string), attr.KeyType);
        ScenarioExpect.Equal("Create", attr.CreateMethodName);
        ScenarioExpect.True(attr.CaseInsensitiveStrings);
    }

    [Scenario("FactoryMethodAttribute Custom Properties")]
    [Fact]
    public void FactoryMethodAttribute_Custom_Properties()
    {
        var attr = new Factories.FactoryMethodAttribute(typeof(int))
        {
            CreateMethodName = "Make",
            CaseInsensitiveStrings = false
        };

        ScenarioExpect.Equal(typeof(int), attr.KeyType);
        ScenarioExpect.Equal("Make", attr.CreateMethodName);
        ScenarioExpect.False(attr.CaseInsensitiveStrings);
    }

    [Scenario("FactoryMethodAttribute Has Correct AttributeUsage")]
    [Fact]
    public void FactoryMethodAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryMethodAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    #endregion

    #region FactoryCaseAttribute Tests

    [Scenario("FactoryCaseAttribute Constructor Sets Key")]
    [Fact]
    public void FactoryCaseAttribute_Constructor_Sets_Key()
    {
        var attr = new Factories.FactoryCaseAttribute("testKey");

        ScenarioExpect.Equal("testKey", attr.Key);
    }

    [Scenario("FactoryCaseAttribute With Int Key")]
    [Fact]
    public void FactoryCaseAttribute_With_Int_Key()
    {
        var attr = new Factories.FactoryCaseAttribute(42);

        ScenarioExpect.Equal(42, attr.Key);
    }

    [Scenario("FactoryCaseAttribute Has Correct AttributeUsage")]
    [Fact]
    public void FactoryCaseAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryCaseAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Method, usage.ValidOn);
        ScenarioExpect.True(usage.AllowMultiple);
    }

    #endregion

    #region FactoryDefaultAttribute Tests

    [Scenario("FactoryDefaultAttribute Can Be Created")]
    [Fact]
    public void FactoryDefaultAttribute_Can_Be_Created()
    {
        var attr = new Factories.FactoryDefaultAttribute();
        ScenarioExpect.NotNull(attr);
    }

    [Scenario("FactoryDefaultAttribute Has Correct AttributeUsage")]
    [Fact]
    public void FactoryDefaultAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryDefaultAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Method, usage.ValidOn);
    }

    #endregion

    #region FactoryClassAttribute Tests

    [Scenario("FactoryClassAttribute Constructor Sets KeyType")]
    [Fact]
    public void FactoryClassAttribute_Constructor_Sets_KeyType()
    {
        var attr = new Factories.FactoryClassAttribute(typeof(string));

        ScenarioExpect.Equal(typeof(string), attr.KeyType);
        ScenarioExpect.Null(attr.FactoryTypeName);
        ScenarioExpect.True(attr.GenerateTryCreate);
        ScenarioExpect.False(attr.GenerateEnumKeys);
    }

    [Scenario("FactoryClassAttribute Custom Properties")]
    [Fact]
    public void FactoryClassAttribute_Custom_Properties()
    {
        var attr = new Factories.FactoryClassAttribute(typeof(int))
        {
            FactoryTypeName = "MessageFactory",
            GenerateTryCreate = false,
            GenerateEnumKeys = true
        };

        ScenarioExpect.Equal(typeof(int), attr.KeyType);
        ScenarioExpect.Equal("MessageFactory", attr.FactoryTypeName);
        ScenarioExpect.False(attr.GenerateTryCreate);
        ScenarioExpect.True(attr.GenerateEnumKeys);
    }

    [Scenario("FactoryClassAttribute Has Correct AttributeUsage")]
    [Fact]
    public void FactoryClassAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryClassAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Interface | AttributeTargets.Class, usage.ValidOn);
    }

    #endregion

    #region FactoryClassKeyAttribute Tests

    [Scenario("FactoryClassKeyAttribute Constructor Sets Key")]
    [Fact]
    public void FactoryClassKeyAttribute_Constructor_Sets_Key()
    {
        var attr = new Factories.FactoryClassKeyAttribute("email");

        ScenarioExpect.Equal("email", attr.Key);
    }

    [Scenario("FactoryClassKeyAttribute With Int Key")]
    [Fact]
    public void FactoryClassKeyAttribute_With_Int_Key()
    {
        var attr = new Factories.FactoryClassKeyAttribute(123);

        ScenarioExpect.Equal(123, attr.Key);
    }

    [Scenario("FactoryClassKeyAttribute Has Correct AttributeUsage")]
    [Fact]
    public void FactoryClassKeyAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryClassKeyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        ScenarioExpect.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    #endregion
}
