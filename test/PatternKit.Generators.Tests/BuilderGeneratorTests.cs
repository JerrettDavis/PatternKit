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
}
