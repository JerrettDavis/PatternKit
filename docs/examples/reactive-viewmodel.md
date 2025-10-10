# Reactive ViewModel with Observer

This example shows how to build dependent properties and control enablement without `INotifyPropertyChanged`, using PatternKit’s Observer-based primitives.

What it demonstrates
- Dependent properties: `FullName` derived from `FirstName` + `LastName`
- Control enablement: `CanSave` toggles based on input validity
- Lightweight property-changed hub for UI bindings

Code
```csharp
using PatternKit.Examples.ObserverDemo;

var vm = new ProfileViewModel();

// Subscribe to name changes
var unsub = vm.PropertyChanged.Subscribe(name => Console.WriteLine($"Changed: {name}"));

vm.FirstName.Value = "Ada";
vm.LastName.Value  = "Lovelace";

Console.WriteLine(vm.FullName.Value); // "Ada Lovelace"
Console.WriteLine(vm.CanSave.Value);  // true

unsub.Dispose();
```

How it works
- `ObservableVar<T>` emits a `(old,new)` event on changes.
- The viewmodel subscribes to `FirstName` and `LastName`, recomputes `FullName` and `CanSave`, and raises name-based notifications via a small `PropertyChangedHub`.
- No reflection, no `INotifyPropertyChanged` interface required.

Where to look
- Source: `src/PatternKit.Examples/ObserverDemo/ReactiveTransaction.cs` (contains `ProfileViewModel`)
- Tests: `test/PatternKit.Examples.Tests/ObserverDemo/ReactiveViewModelTests.cs`

