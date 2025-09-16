using PatternKit.Structural.Adapter;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Tests.Structural.Adapters;

[Feature("Creational - Adapter<TIn,TOut> (mapping + validation)")]
public sealed class AdapterTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Source(string First, string Last, int Age);

    private sealed class Dest
    {
        public string? FullName { get; set; }
        public int Age { get; set; }
        public bool Adult { get; set; }
        public List<string> Log { get; } = new();
    }

    private static Adapter<Source, Dest> BuildBasic()
        => Adapter<Source, Dest>
            .Create(static () => new Dest())
            .Map(static (in s, d) => { d.FullName = $"{s.First} {s.Last}"; d.Log.Add("map:name"); })
            .Map(static (in s, d) => { d.Age = s.Age; d.Log.Add("map:age"); })
            .Map(static (in s, d) => { d.Adult = s.Age >= 18; d.Log.Add("map:adult"); })
            .Require(static (in _, d) => string.IsNullOrWhiteSpace(d.FullName) ? "name required" : null)
            .Require(static (in _, d) => d.Age is < 0 or > 130 ? $"age out of range: {d.Age}" : null)
            .Build();

    [Scenario("Mapping steps run in registration order and validations pass")]
    [Fact]
    public Task Map_Order_And_Validate_Pass()
        => Given("a basic adapter", BuildBasic)
            .When("adapting (Ada Lovelace, 30)", a => a.Adapt(new Source("Ada", "Lovelace", 30)))
            .Then("full name and fields set", d => d.FullName == "Ada Lovelace" && d.Age == 30 && d.Adult)
            .And("log shows 3 maps in order", d => string.Join("|", d.Log) == "map:name|map:age|map:adult")
            .AssertPassed();

    [Scenario("First failing validator throws with its message in Adapt")]
    [Fact]
    public Task Require_First_Failure_Wins_Adapt()
        => Given("an adapter with validators", BuildBasic)
            .When("adapting with age -1", a => Record.Exception(() => a.Adapt(new Source("A", "B", -1))))
            .Then("throws InvalidOperationException", ex => ex is InvalidOperationException)
            .And("message mentions age out of range", ex => ex!.Message.Contains("age out of range"))
            .AssertPassed();

    [Scenario("TryAdapt returns false with error and does not throw")]
    [Fact]
    public Task TryAdapt_False_With_Error()
        => Given("an adapter with validators", BuildBasic)
            .When("TryAdapt with empty name via SeedFrom", _ =>
            {
                var a = Adapter<Source, Dest>
                    .Create(static (in _) => new Dest())
                    .Map(static (in s, d) => d.FullName = $"{s.First} {s.Last}")
                    .Require(static (in _, d) => string.IsNullOrWhiteSpace(d.FullName) ? "name required" : null)
                    .Build();
                var ok = a.TryAdapt(new Source("", "", 10), out var dest, out var err);
                return (ok, dest, err);
            })
            .Then("returns false", r => r.ok == false)
            .And("output is null/default", r => r.dest is null)
            .And("error propagated", r => r.err == "name required")
            .AssertPassed();

    [Scenario("SeedFrom initializes destination from input before maps")] 
    [Fact]
    public Task SeedFrom_Uses_Input()
        => Given("an adapter seeded from input", () =>
            Adapter<Source, Dest>
                .Create(static (in s) => new Dest { Age = s.Age })
                .Map(static (in s, d) => d.FullName = s.First)
                .Build())
            .When("adapt with age 21", a => a.Adapt(new Source("X", "Y", 21)))
            .Then("dest had seed-applied age before maps", d => d.Age == 21 && d.FullName == "X")
            .AssertPassed();

    [Scenario("Adapter reuse: distinct outputs with same adapter")]
    [Fact]
    public Task Reuse_Produces_Distinct_Outputs()
        => Given("a reusable adapter", BuildBasic)
            .When("adapt twice", a => (a.Adapt(new Source("A", "B", 20)), a.Adapt(new Source("C", "D", 25))))
            .Then("not same reference", t => !ReferenceEquals(t.Item1, t.Item2))
            .And("both valid", t => t.Item1.FullName == "A B" && t.Item2.FullName == "C D")
            .AssertPassed();
}
