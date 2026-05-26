using BenchmarkDotNet.Attributes;
using PatternKit.Behavioral.Memento;
using PatternKit.Generators;

namespace PatternKit.Benchmarks.Behavioral;

[BenchmarkCategory("Behavioral", "GoF", "Memento")]
public class MementoBenchmarks
{
    private static readonly EditableInvoice Invoice = new() { InvoiceId = "INV-100", Total = 125m, Status = "Draft" };

    [Benchmark(Baseline = true, Description = "Fluent: create memento history")]
    [BenchmarkCategory("Fluent", "Construction")]
    public Memento<EditableInvoiceState> Fluent_CreateMemento()
        => Memento<EditableInvoiceState>.Create()
            .CloneWith(static (in EditableInvoiceState state) => state)
            .Capacity(16)
            .Build();

    [Benchmark(Description = "Generated: create memento history")]
    [BenchmarkCategory("Generated", "Construction")]
    public EditableInvoiceHistory Generated_CreateMemento()
        => new(Invoice);

    [Benchmark(Description = "Fluent: capture and restore state")]
    [BenchmarkCategory("Fluent", "Execution")]
    public EditableInvoiceState Fluent_CaptureAndRestore()
    {
        var history = Fluent_CreateMemento();
        var state = new EditableInvoiceState("INV-100", 125m, "Draft");
        history.Save(state, "draft");
        state = state with { Status = "Approved" };
        history.Save(state, "approved");
        history.Undo(ref state);
        return state;
    }

    [Benchmark(Description = "Generated: capture and restore state")]
    [BenchmarkCategory("Generated", "Execution")]
    public EditableInvoice Generated_CaptureAndRestore()
        => EditableInvoiceMemento.Capture(in Invoice).RestoreNew();
}

public readonly record struct EditableInvoiceState(string InvoiceId, decimal Total, string Status);

[Memento(GenerateCaretaker = true, Capacity = 16)]
public partial class EditableInvoice
{
    public string InvoiceId { get; set; } = string.Empty;

    public decimal Total { get; set; }

    public string Status { get; set; } = string.Empty;
}
