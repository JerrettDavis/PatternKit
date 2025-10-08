# Flyweight Glyph Cache Demo

This demo shows how to render a sentence using a **shared glyph flyweight** so each character shape is allocated **once** no matter how many times it appears.

---
## Why
Rendering text naively can allocate one object per character occurrence. The flyweight approach stores each unique glyph once (intrinsic) and supplies its position (extrinsic) during drawing.

---
## Code (from `FlyweightDemo`)
```csharp
var layout = FlyweightDemo.RenderSentence("HELLO HELLO");
foreach (var (glyph, x) in layout)
    Console.WriteLine($"Char={glyph.Char} Width={glyph.Width} X={x}");

var reuse = FlyweightDemo.AnalyzeReuse("HELLO HELLO");
Console.WriteLine($"Total={reuse.total} Unique={reuse.unique} Ratio={reuse.reuseRatio:0.00}");

var (s1, s2, same) = FlyweightDemo.DemonstrateCaseInsensitiveStyles();
Console.WriteLine($"Styles reused: {same} ({s1.Name}/{s2.Name})");
```

---
## Example Output (abridged)
```
Char=H Width=6 X=0
Char=E Width=6 X=6
Char=L Width=6 X=12
Char=L Width=6 X=18
Char=O Width=6 X=24
Char=  Width=3 X=30
Char=H Width=6 X=33
...
Total=11 Unique=6 Ratio=0.55
Styles reused: True (HEADER/HEADER)
```

---
## Intrinsic vs Extrinsic
| Aspect | Intrinsic (shared) | Extrinsic (per draw) |
|--------|--------------------|----------------------|
| Glyph object | Character + width | X position, color/effects |
| Style object | Canonical name    | Where / how it's applied |

---
## Factory & Preload
```csharp
var glyphs = Flyweight<char, Glyph>.Create()
    .Preload(' ', Glyph.Space) // hot space preloaded
    .WithFactory(c => c == ' ' ? Glyph.Space : new Glyph(c, InferWidth(c)))
    .Build();
```
Preloading hot keys avoids a lock on first space usage.

---
## Reuse Analysis
`AnalyzeReuse(text)` returns `(total, unique, reuseRatio)` so you can gauge how effective sharing is for a given string corpus.
Lower ratios mean more savings.

---
## Case-Insensitive Style Flyweight
```csharp
var (a, b, same) = FlyweightDemo.DemonstrateCaseInsensitiveStyles("header", "HEADER");
Debug.Assert(same); // same reference
```

---
## Testing Concepts
Tests validate:
- Same instance reused per key (`ReferenceEquals`)
- Reuse ratio < 1 when duplicates exist
- Case-insensitive key merging
- `TryGetExisting` does not create

See: `FlyweightDemoTests` & structural `FlyweightTests` for deeper coverage.

---
## Takeaways
Flyweight reduces object churn & memory when repetition is high. Combine with Decorator (styling layers) or Strategy (choose factories) for more complex systems.

