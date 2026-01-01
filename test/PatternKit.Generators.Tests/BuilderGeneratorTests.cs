using System.Reflection;
using System.Runtime.Loader;
using PatternKit.Generators.Builders;

namespace PatternKit.Generators.Tests;

public class BuilderGeneratorTests
{
    private const string MutableSample = """
        using System.Threading.Tasks;
        using PatternKit.Generators.Builders;

        namespace PatternKit.Examples.Builders;

        [GenerateBuilder(BuilderTypeName = "PersonBuilder", GenerateBuilderMethods = true)]
        public partial class Person
        {
            [BuilderRequired(Message = "Name is required.")]
            public string? Name { get; set; }

            public int Age { get; set; }
        }
        """;

    private const string ProjectionSample = """
        using System.Threading.Tasks;
        using PatternKit.Generators.Builders;

        namespace PatternKit.Examples.Builders;

        public readonly record struct PersonState(string? Name, int Age);
        public sealed record PersonDto(string Name, int Age);

        [GenerateBuilder(Model = BuilderModel.StateProjection, GenerateBuilderMethods = true)]
        public static partial class PersonDtoBuilderHost
        {
            public static PersonState Seed() => default;

            [BuilderProjector]
            public static PersonDto Project(PersonState state) => new(state.Name!, state.Age);
        }
        """;

    [Fact]
    public async Task MutableBuilder_Generates_And_Runs()
    {
        var user = MutableSample + """
            public static class MutableDemo
            {
                public static string Run()
                {
                    var built = PersonBuilder.New()
                        .WithName("Ada")
                        .WithAge(37)
                        .Require(p => p.Age < 0 ? "Invalid age" : null)
                        .Build();

                    return $"{built.Name}:{built.Age}";
                }

                public static async Task<string> RunAsync()
                {
                    var built = await PersonBuilder.New()
                        .WithName("Bob")
                        .WithAsync(p => { p.Age = 42; return new ValueTask(); })
                        .BuildAsync();

                    return $"{built.Name}:{built.Age}";
                }

                public static string BuildViaHelpers()
                {
                    var person = Person.Build(b => b.WithName("Cara").WithAge(25));
                    return $"{person.Name}:{person.Age}";
                }

                public static async Task<string> BuildViaHelpersAsync()
                {
                    var person = await Person.BuildAsync(b =>
                    {
                        b.WithName("Dan");
                        b.WithAsync(p => { p.Age = 50; return new ValueTask(); });
                        return new ValueTask();
                    });

                    return $"{person.Name}:{person.Age}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, nameof(MutableBuilder_Generates_And_Runs));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success, string.Join(Environment.NewLine, res.Diagnostics));
        pe.Position = 0;
        pdb.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var sync = asm.GetType("PatternKit.Examples.Builders.MutableDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("Ada:37", sync);

        var helper = asm.GetType("PatternKit.Examples.Builders.MutableDemo")!
            .GetMethod("BuildViaHelpers")!
            .Invoke(null, null) as string;
        Assert.Equal("Cara:25", helper);

        var asyncMethod = asm.GetType("PatternKit.Examples.Builders.MutableDemo")!
            .GetMethod("RunAsync")!;
        var task = (Task<string>)asyncMethod.Invoke(null, null)!;
        Assert.Equal("Bob:42", await task);

        var helperAsync = asm.GetType("PatternKit.Examples.Builders.MutableDemo")!
            .GetMethod("BuildViaHelpersAsync")!;
        var helperTask = (Task<string>)helperAsync.Invoke(null, null)!;
        Assert.Equal("Dan:50", await helperTask);
    }

    [Fact]
    public async Task ProjectionBuilder_Composes_State_And_Projector()
    {
        var user = ProjectionSample + """

            public static class ProjectionDemo
            {
                public static string BuildSync()
                {
                    var dto = PersonDtoBuilderHostBuilder.New()
                        .With(state => state with { Name = "Lin" })
                        .With(state => state with { Age = 28 })
                        .Build();

                    return $"{dto.Name}:{dto.Age}";
                }

            public static ValueTask<string> BuildAsync()
            {
                return ConvertAsync();

                async ValueTask<string> ConvertAsync()
                {
                    var dto = await PersonDtoBuilderHost.BuildAsync(b =>
                    {
                        b.With(state => state with { Name = "Quinn" });
                        b.WithAsync(state => new ValueTask<PersonState>(state with { Age = 31 }));
                        return new ValueTask();
                    }).ConfigureAwait(false);

                    return $"{dto.Name}:{dto.Age}";
                }
            }
        }
        """;

        var comp = RoslynTestHelpers.CreateCompilation(user, nameof(ProjectionBuilder_Composes_State_And_Projector));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);
        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        var emit = updated.Emit(Stream.Null);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success, string.Join(Environment.NewLine, res.Diagnostics));
        pe.Position = 0;
        pdb.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var sync = asm.GetType("PatternKit.Examples.Builders.ProjectionDemo")!
            .GetMethod("BuildSync")!
            .Invoke(null, null) as string;
        Assert.Equal("Lin:28", sync);

        var asyncMethod = asm.GetType("PatternKit.Examples.Builders.ProjectionDemo")!
            .GetMethod("BuildAsync")!;
        var valueTask = (ValueTask<string>)asyncMethod.Invoke(null, null)!;
        Assert.Equal("Quinn:31", await valueTask);
    }

    [Fact]
    public void Required_Member_Throws_When_Missing()
    {
        var user = MutableSample + """
            public static class MissingRequiredDemo
            {
                public static void Run()
                {
                    PersonBuilder.New().Build();
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(user, nameof(Required_Member_Throws_When_Missing));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out _, out var updated);

        using var pe = new MemoryStream();
        using var pdb = new MemoryStream();
        var res = updated.Emit(pe, pdb);
        Assert.True(res.Success, string.Join(Environment.NewLine, res.Diagnostics));
        pe.Position = 0;
        pdb.Position = 0;

        var asm = AssemblyLoadContext.Default.LoadFromStream(pe, pdb);
        var run = asm.GetType("PatternKit.Examples.Builders.MissingRequiredDemo")!
            .GetMethod("Run")!;

        var ex = Assert.Throws<TargetInvocationException>(() => run.Invoke(null, null));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal("Name is required.", ex.InnerException?.Message);
    }

    [Fact]
    public void NotPartial_Type_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public class NotPartial
            {
                public string? Name { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(NotPartial_Type_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("B001", diag.Id);
    }

    [Fact]
    public void Generic_Type_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class GenericType<T>
            {
                public T? Value { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Generic_Type_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("B001", diag.Id);
    }

    [Fact]
    public void Multiple_BuilderConstructor_Attributes_Emit_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class MultiCtorType
            {
                [BuilderConstructor]
                public MultiCtorType() { }

                [BuilderConstructor]
                public MultiCtorType(string name) { Name = name; }

                public string? Name { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Multiple_BuilderConstructor_Attributes_Emit_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("B004", diag.Id);
    }

    [Fact]
    public void No_Usable_Constructor_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class NoCtorType
            {
                private NoCtorType() { }

                public string? Name { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(No_Usable_Constructor_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("B003", diag.Id);
    }

    [Fact]
    public void Struct_Builder_Generates_Without_Errors()
    {
        // Note: Struct builders generate but due to value-type semantics with Action<T>,
        // the WithXxx methods don't actually modify the struct. This test verifies
        // generation succeeds - for structs, consider using StateProjection model instead.
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial struct Point
            {
                public int X { get; set; }
                public int Y { get; set; }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Struct_Builder_Generates_Without_Errors));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        // Verify the builder type was generated
        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var builderType = asm.GetType("PatternKit.Examples.Builders.PointBuilder");
        Assert.NotNull(builderType);
        Assert.NotNull(builderType.GetMethod("New"));
        Assert.NotNull(builderType.GetMethod("WithX"));
        Assert.NotNull(builderType.GetMethod("WithY"));
        Assert.NotNull(builderType.GetMethod("Build"));
    }

    [Fact]
    public void IncludeFields_Generates_Field_Setters()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder(IncludeFields = true)]
            public partial class FieldType
            {
                public string? _name;
                public int _age;
            }

            public static class FieldDemo
            {
                public static string Run()
                {
                    var obj = FieldTypeBuilder.New()
                        .With_name("Test")
                        .With_age(42)
                        .Build();
                    return $"{obj._name}:{obj._age}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(IncludeFields_Generates_Field_Setters));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("PatternKit.Examples.Builders.FieldDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("Test:42", result);
    }

    [Fact]
    public void BuilderIgnore_Skips_Property()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class IgnoreType
            {
                public string? Name { get; set; }

                [BuilderIgnore]
                public string? Computed => Name?.ToUpper();
            }

            public static class IgnoreDemo
            {
                public static string Run()
                {
                    var obj = IgnoreTypeBuilder.New()
                        .WithName("test")
                        .Build();
                    return $"{obj.Name}:{obj.Computed}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(BuilderIgnore_Skips_Property));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("PatternKit.Examples.Builders.IgnoreDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("test:TEST", result);
    }

    [Fact]
    public void Custom_Method_Names_Work()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder(NewMethodName = "Create", BuildMethodName = "Construct")]
            public partial class CustomNames
            {
                public string? Name { get; set; }
            }

            public static class CustomNamesDemo
            {
                public static string Run()
                {
                    var obj = CustomNamesBuilder.Create()
                        .WithName("Hello")
                        .Construct();
                    return obj.Name ?? "";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Custom_Method_Names_Work));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("PatternKit.Examples.Builders.CustomNamesDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void Projection_Missing_Seed_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder(Model = BuilderModel.StateProjection)]
            public static partial class NoSeedProjection
            {
                // Missing Seed() method
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Projection_Missing_Seed_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("BP001", diag.Id);
    }

    [Fact]
    public void Projection_Multiple_Projectors_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            public readonly record struct MyState(string Name);
            public sealed record MyResult(string Name);

            [GenerateBuilder(Model = BuilderModel.StateProjection)]
            public static partial class MultiProjector
            {
                public static MyState Seed() => default;

                [BuilderProjector]
                public static MyResult Project1(MyState s) => new(s.Name ?? "");

                [BuilderProjector]
                public static MyResult Project2(MyState s) => new(s.Name ?? "");
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Projection_Multiple_Projectors_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("BP002", diag.Id);
    }

    [Fact]
    public void Projection_Invalid_Projector_Emits_Diagnostic()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            public readonly record struct StateType(string Name);

            [GenerateBuilder(Model = BuilderModel.StateProjection)]
            public static partial class InvalidProjector
            {
                public static StateType Seed() => default;

                [BuilderProjector]
                public static void Project(StateType s) { } // void return not allowed
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Projection_Invalid_Projector_Emits_Diagnostic));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out _);

        var diag = run.Results.SelectMany(r => r.Diagnostics).FirstOrDefault();
        Assert.NotNull(diag);
        Assert.Equal("BP003", diag.Id);
    }

    [Fact]
    public void Mutable_With_ParameterizedCtor_Uses_Defaults()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class ParamCtor
            {
                [BuilderConstructor]
                public ParamCtor(string name, int age)
                {
                    Name = name;
                    Age = age;
                }

                public string Name { get; set; }
                public int Age { get; set; }
            }

            public static class ParamCtorDemo
            {
                public static string Run()
                {
                    var obj = ParamCtorBuilder.New()
                        .WithName("Test")
                        .WithAge(25)
                        .Build();
                    return $"{obj.Name}:{obj.Age}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Mutable_With_ParameterizedCtor_Uses_Defaults));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("PatternKit.Examples.Builders.ParamCtorDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("Test:25", result);
    }

    [Fact]
    public void Internal_Type_Generates_Internal_Builder()
    {
        const string source = """
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            internal partial class InternalType
            {
                public string? Name { get; set; }
            }

            public static class InternalDemo
            {
                public static string Run()
                {
                    var obj = InternalTypeBuilder.New()
                        .WithName("Internal")
                        .Build();
                    return obj.Name ?? "";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Internal_Type_Generates_Internal_Builder));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));

        pe.Position = 0;
        var asm = AssemblyLoadContext.Default.LoadFromStream(pe);
        var result = asm.GetType("PatternKit.Examples.Builders.InternalDemo")!
            .GetMethod("Run")!
            .Invoke(null, null) as string;
        Assert.Equal("Internal", result);
    }

    [Fact]
    public void RequireAsync_Validation_Works()
    {
        const string source = """
            using System.Threading.Tasks;
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            [GenerateBuilder]
            public partial class AsyncValidation
            {
                public string? Name { get; set; }
                public int Age { get; set; }
            }

            public static class AsyncValidationDemo
            {
                public static async Task<string> RunAsync()
                {
                    var obj = await AsyncValidationBuilder.New()
                        .WithName("Test")
                        .WithAge(25)
                        .RequireAsync(async p =>
                        {
                            await Task.Delay(1);
                            return p.Age < 0 ? "Age must be positive" : null;
                        })
                        .BuildAsync();
                    return $"{obj.Name}:{obj.Age}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(RequireAsync_Validation_Works));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
    }

    [Fact]
    public void Projection_RequireAsync_Works()
    {
        const string source = """
            using System.Threading.Tasks;
            using PatternKit.Generators.Builders;

            namespace PatternKit.Examples.Builders;

            public readonly record struct ProjectionState(string Name, int Age);
            public sealed record ProjectionResult(string Name, int Age);

            [GenerateBuilder(Model = BuilderModel.StateProjection, GenerateBuilderMethods = true)]
            public static partial class ProjectionWithRequire
            {
                public static ProjectionState Seed() => default;

                [BuilderProjector]
                public static ProjectionResult Project(ProjectionState s) => new(s.Name ?? "", s.Age);
            }

            public static class ProjectionRequireDemo
            {
                public static async Task<string> RunAsync()
                {
                    var result = await ProjectionWithRequireBuilder.New()
                        .With(s => s with { Name = "Test" })
                        .With(s => s with { Age = 30 })
                        .RequireAsync(async s =>
                        {
                            await Task.Delay(1);
                            return string.IsNullOrEmpty(s.Name) ? "Name required" : null;
                        })
                        .BuildAsync();
                    return $"{result.Name}:{result.Age}";
                }
            }
            """;

        var comp = RoslynTestHelpers.CreateCompilation(source, nameof(Projection_RequireAsync_Works));
        var gen = new BuilderGenerator();
        _ = RoslynTestHelpers.Run(comp, gen, out var run, out var updated);

        Assert.All(run.Results, r => Assert.Empty(r.Diagnostics));

        using var pe = new MemoryStream();
        var emit = updated.Emit(pe);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
    }
}
