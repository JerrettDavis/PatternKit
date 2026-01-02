namespace PatternKit.Generators.Tests;

/// <summary>
/// Tests for PatternKit.Generators.Abstractions attributes to ensure proper
/// attribute construction and property assignment.
/// </summary>
public class AbstractionsTests
{
    #region GenerateStrategyAttribute Tests

    [Fact]
    public void GenerateStrategyAttribute_Action_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("TestStrategy", typeof(string), StrategyKind.Action);

        Assert.Equal("TestStrategy", attr.Name);
        Assert.Equal(typeof(string), attr.InType);
        Assert.Null(attr.OutType);
        Assert.Equal(StrategyKind.Action, attr.Kind);
    }

    [Fact]
    public void GenerateStrategyAttribute_Result_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("TestStrategy", typeof(int), typeof(string), StrategyKind.Result);

        Assert.Equal("TestStrategy", attr.Name);
        Assert.Equal(typeof(int), attr.InType);
        Assert.Equal(typeof(string), attr.OutType);
        Assert.Equal(StrategyKind.Result, attr.Kind);
    }

    [Fact]
    public void GenerateStrategyAttribute_Try_Constructor_Sets_Properties()
    {
        var attr = new GenerateStrategyAttribute("Parser", typeof(string), typeof(int), StrategyKind.Try);

        Assert.Equal("Parser", attr.Name);
        Assert.Equal(typeof(string), attr.InType);
        Assert.Equal(typeof(int), attr.OutType);
        Assert.Equal(StrategyKind.Try, attr.Kind);
    }

    [Fact]
    public void GenerateStrategyAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(GenerateStrategyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Theory]
    [InlineData(StrategyKind.Action)]
    [InlineData(StrategyKind.Result)]
    [InlineData(StrategyKind.Try)]
    public void StrategyKind_Enum_Values_Are_Valid(StrategyKind kind)
    {
        Assert.True(Enum.IsDefined(typeof(StrategyKind), kind));
    }

    #endregion

    #region GenerateBuilderAttribute Tests

    [Fact]
    public void GenerateBuilderAttribute_Default_Properties()
    {
        var attr = new Builders.GenerateBuilderAttribute();

        Assert.Null(attr.BuilderTypeName);
        Assert.Equal("New", attr.NewMethodName);
        Assert.Equal("Build", attr.BuildMethodName);
        Assert.Equal(Builders.BuilderModel.MutableInstance, attr.Model);
        Assert.False(attr.GenerateBuilderMethods);
        Assert.False(attr.ForceAsync);
        Assert.False(attr.IncludeFields);
    }

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

        Assert.Equal("PersonBuilder", attr.BuilderTypeName);
        Assert.Equal("Create", attr.NewMethodName);
        Assert.Equal("Construct", attr.BuildMethodName);
        Assert.Equal(Builders.BuilderModel.StateProjection, attr.Model);
        Assert.True(attr.GenerateBuilderMethods);
        Assert.True(attr.ForceAsync);
        Assert.True(attr.IncludeFields);
    }

    [Fact]
    public void GenerateBuilderAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.GenerateBuilderAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Struct, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [Theory]
    [InlineData(Builders.BuilderModel.MutableInstance)]
    [InlineData(Builders.BuilderModel.StateProjection)]
    public void BuilderModel_Enum_Values_Are_Valid(Builders.BuilderModel model)
    {
        Assert.True(Enum.IsDefined(typeof(Builders.BuilderModel), model));
    }

    #endregion

    #region BuilderConstructorAttribute Tests

    [Fact]
    public void BuilderConstructorAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderConstructorAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void BuilderConstructorAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderConstructorAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Constructor, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    #endregion

    #region BuilderIgnoreAttribute Tests

    [Fact]
    public void BuilderIgnoreAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderIgnoreAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void BuilderIgnoreAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderIgnoreAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    #endregion

    #region BuilderRequiredAttribute Tests

    [Fact]
    public void BuilderRequiredAttribute_Default_Properties()
    {
        var attr = new Builders.BuilderRequiredAttribute();

        Assert.Null(attr.Message);
    }

    [Fact]
    public void BuilderRequiredAttribute_Custom_Message()
    {
        var attr = new Builders.BuilderRequiredAttribute
        {
            Message = "This field is required."
        };

        Assert.Equal("This field is required.", attr.Message);
    }

    [Fact]
    public void BuilderRequiredAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderRequiredAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Property | AttributeTargets.Field, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    #endregion

    #region BuilderProjectorAttribute Tests

    [Fact]
    public void BuilderProjectorAttribute_Can_Be_Created()
    {
        var attr = new Builders.BuilderProjectorAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void BuilderProjectorAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Builders.BuilderProjectorAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    #endregion

    #region FactoryMethodAttribute Tests

    [Fact]
    public void FactoryMethodAttribute_Constructor_Sets_KeyType()
    {
        var attr = new Factories.FactoryMethodAttribute(typeof(string));

        Assert.Equal(typeof(string), attr.KeyType);
        Assert.Equal("Create", attr.CreateMethodName);
        Assert.True(attr.CaseInsensitiveStrings);
    }

    [Fact]
    public void FactoryMethodAttribute_Custom_Properties()
    {
        var attr = new Factories.FactoryMethodAttribute(typeof(int))
        {
            CreateMethodName = "Make",
            CaseInsensitiveStrings = false
        };

        Assert.Equal(typeof(int), attr.KeyType);
        Assert.Equal("Make", attr.CreateMethodName);
        Assert.False(attr.CaseInsensitiveStrings);
    }

    [Fact]
    public void FactoryMethodAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryMethodAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    #endregion

    #region FactoryCaseAttribute Tests

    [Fact]
    public void FactoryCaseAttribute_Constructor_Sets_Key()
    {
        var attr = new Factories.FactoryCaseAttribute("testKey");

        Assert.Equal("testKey", attr.Key);
    }

    [Fact]
    public void FactoryCaseAttribute_With_Int_Key()
    {
        var attr = new Factories.FactoryCaseAttribute(42);

        Assert.Equal(42, attr.Key);
    }

    [Fact]
    public void FactoryCaseAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryCaseAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
    }

    #endregion

    #region FactoryDefaultAttribute Tests

    [Fact]
    public void FactoryDefaultAttribute_Can_Be_Created()
    {
        var attr = new Factories.FactoryDefaultAttribute();
        Assert.NotNull(attr);
    }

    [Fact]
    public void FactoryDefaultAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryDefaultAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
    }

    #endregion

    #region FactoryClassAttribute Tests

    [Fact]
    public void FactoryClassAttribute_Constructor_Sets_KeyType()
    {
        var attr = new Factories.FactoryClassAttribute(typeof(string));

        Assert.Equal(typeof(string), attr.KeyType);
        Assert.Null(attr.FactoryTypeName);
        Assert.True(attr.GenerateTryCreate);
        Assert.False(attr.GenerateEnumKeys);
    }

    [Fact]
    public void FactoryClassAttribute_Custom_Properties()
    {
        var attr = new Factories.FactoryClassAttribute(typeof(int))
        {
            FactoryTypeName = "MessageFactory",
            GenerateTryCreate = false,
            GenerateEnumKeys = true
        };

        Assert.Equal(typeof(int), attr.KeyType);
        Assert.Equal("MessageFactory", attr.FactoryTypeName);
        Assert.False(attr.GenerateTryCreate);
        Assert.True(attr.GenerateEnumKeys);
    }

    [Fact]
    public void FactoryClassAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryClassAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Interface | AttributeTargets.Class, usage.ValidOn);
    }

    #endregion

    #region FactoryClassKeyAttribute Tests

    [Fact]
    public void FactoryClassKeyAttribute_Constructor_Sets_Key()
    {
        var attr = new Factories.FactoryClassKeyAttribute("email");

        Assert.Equal("email", attr.Key);
    }

    [Fact]
    public void FactoryClassKeyAttribute_With_Int_Key()
    {
        var attr = new Factories.FactoryClassKeyAttribute(123);

        Assert.Equal(123, attr.Key);
    }

    [Fact]
    public void FactoryClassKeyAttribute_Has_Correct_AttributeUsage()
    {
        var usage = typeof(Factories.FactoryClassKeyAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class, usage.ValidOn);
    }

    #endregion
}
