using System;
using System.Collections.Generic;
using PatternKit.Generators.Facade;

namespace PatternKit.Examples.Generators.Facade;

// ============================================================================
// EXAMPLE 2: Shipping Facade - Host-First Pattern with [FacadeExpose]
// ============================================================================
// This demonstrates the Host-First approach where you define the implementation
// methods first and expose them through a generated facade using [FacadeExpose].
//
// KEY CONCEPTS:
// 1. Define static partial class with [GenerateFacade(FacadeTypeName = "X")]
// 2. Mark methods with [FacadeExpose] to include in facade
// 3. Method parameters: dependencies FIRST, then business parameters
// 4. Generator creates facade class with constructor for dependencies
// 5. Generated facade methods have ONLY business parameters (no dependencies)
// ============================================================================

// Subsystem Services

/// <summary>
/// Calculates shipping rates based on weight and distance.
/// </summary>
public class RateCalculator
{
    public decimal CalculateBaseRate(string destination)
    {
        // Simplified rate calculation
        return destination.ToLower() switch
        {
            "local" => 5.99m,
            "regional" => 12.99m,
            "national" => 19.99m,
            _ => 29.99m
        };
    }

    public decimal CalculateWeightSurcharge(decimal weight)
    {
        // $0.50 per lb over 5 lbs
        return weight > 5m ? (weight - 5m) * 0.50m : 0m;
    }
}

/// <summary>
/// Estimates delivery timeframes.
/// </summary>
public class DeliveryEstimator
{
    public int EstimateDays(string destination, string speed)
    {
        var baseDays = speed.ToLower() switch
        {
            "express" => 1,
            "priority" => 3,
            "standard" => 7,
            _ => 10
        };

        // Add days for non-local deliveries
        if (destination.ToLower() != "local")
            baseDays += 2;

        return baseDays;
    }
}

/// <summary>
/// Validates shipping restrictions.
/// </summary>
public class ShippingValidator
{
    public bool ValidateWeight(decimal weight)
    {
        return weight > 0 && weight <= 150m;
    }

    public bool ValidateDestination(string destination)
    {
        return !string.IsNullOrWhiteSpace(destination);
    }
}

// Host-First Facade

/// <summary>
/// Shipping operations host - methods marked with [FacadeExpose] are included in generated facade.
/// </summary>
/// <remarks>
/// The generator will create a "ShippingFacade" class with:
/// - Constructor accepting all unique dependencies from all methods
/// - Methods with the same signatures but WITHOUT dependency parameters
/// </remarks>
[GenerateFacade(FacadeTypeName = "ShippingFacade")]
public static partial class ShippingHost
{
    /// <summary>
    /// Calculates complete shipping cost including base rate and surcharges.
    /// </summary>
    /// <remarks>
    /// Dependencies (RateCalculator) come FIRST, then business parameters.
    /// Generated facade signature: CalculateShippingCost(string destination, decimal weight)
    /// </remarks>
    [FacadeExpose]
    public static decimal CalculateShippingCost(
        RateCalculator rateCalc,
        string destination,
        decimal weight)
    {
        var baseRate = rateCalc.CalculateBaseRate(destination);
        var surcharge = rateCalc.CalculateWeightSurcharge(weight);
        return baseRate + surcharge;
    }

    /// <summary>
    /// Estimates delivery time for a shipment.
    /// </summary>
    /// <remarks>
    /// Generated facade signature: EstimateDeliveryDays(string destination, string speed)
    /// </remarks>
    [FacadeExpose]
    public static int EstimateDeliveryDays(
        DeliveryEstimator estimator,
        string destination,
        string speed)
    {
        return estimator.EstimateDays(destination, speed);
    }

    /// <summary>
    /// Validates if a shipment can be processed.
    /// </summary>
    /// <remarks>
    /// Generated facade signature: ValidateShipment(string destination, decimal weight)
    /// </remarks>
    [FacadeExpose]
    public static bool ValidateShipment(
        ShippingValidator validator,
        string destination,
        decimal weight)
    {
        return validator.ValidateDestination(destination) &&
               validator.ValidateWeight(weight);
    }

    /// <summary>
    /// Gets a formatted shipping quote.
    /// </summary>
    /// <remarks>
    /// This method uses multiple dependencies and demonstrates coordination.
    /// Generated facade signature: GetQuote(string destination, decimal weight, string speed)
    /// </remarks>
    [FacadeExpose]
    public static string GetQuote(
        RateCalculator rateCalc,
        DeliveryEstimator estimator,
        ShippingValidator validator,
        string destination,
        decimal weight,
        string speed)
    {
        if (!validator.ValidateDestination(destination) || !validator.ValidateWeight(weight))
            return "Invalid shipment parameters";

        var cost = CalculateShippingCost(rateCalc, destination, weight);
        var days = EstimateDeliveryDays(estimator, destination, speed);

        return $"${cost:F2} - Delivery in {days} business days";
    }
}

// Demo Class

/// <summary>
/// Demonstrates the Shipping Facade pattern (Host-First with [FacadeExpose]).
/// </summary>
public static class ShippingFacadeDemo
{
    public static void Run()
    {
        Console.WriteLine("=== Shipping Facade Example (Host-First with [FacadeExpose]) ===\n");

        // Initialize subsystems
        var rateCalc = new RateCalculator();
        var estimator = new DeliveryEstimator();
        var validator = new ShippingValidator();

        // Create facade - generator creates ShippingFacade class with constructor
        // Constructor parameters are in alphabetical order by type name
        var facade = new ShippingFacade(
            estimator,      // DeliveryEstimator
            rateCalc,       // RateCalculator
            validator);     // ShippingValidator

        Console.WriteLine("1. Calculating shipping cost:");
        var cost1 = facade.CalculateShippingCost(destination: "local", weight: 3.5m);
        Console.WriteLine($"   Local, 3.5 lbs: ${cost1:F2}");

        var cost2 = facade.CalculateShippingCost(destination: "national", weight: 12.0m);
        Console.WriteLine($"   National, 12 lbs: ${cost2:F2}");

        Console.WriteLine("\n2. Estimating delivery times:");
        var days1 = facade.EstimateDeliveryDays(destination: "local", speed: "express");
        Console.WriteLine($"   Local express: {days1} days");

        var days2 = facade.EstimateDeliveryDays(destination: "national", speed: "standard");
        Console.WriteLine($"   National standard: {days2} days");

        Console.WriteLine("\n3. Validating shipments:");
        var valid1 = facade.ValidateShipment(destination: "regional", weight: 25.0m);
        Console.WriteLine($"   Regional, 25 lbs: {(valid1 ? "✓ Valid" : "✗ Invalid")}");

        var valid2 = facade.ValidateShipment(destination: "", weight: 5.0m);
        Console.WriteLine($"   Empty destination: {(valid2 ? "✓ Valid" : "✗ Invalid")}");

        Console.WriteLine("\n4. Getting complete quotes:");
        var quote1 = facade.GetQuote(destination: "local", weight: 8.0m, speed: "priority");
        Console.WriteLine($"   {quote1}");

        var quote2 = facade.GetQuote(destination: "national", weight: 15.0m, speed: "standard");
        Console.WriteLine($"   {quote2}");

        Console.WriteLine("\n=== Key Takeaways ===");
        Console.WriteLine("- Mark static partial class with [GenerateFacade(FacadeTypeName = \"X\")]");
        Console.WriteLine("- Mark methods with [FacadeExpose] to include in facade");
        Console.WriteLine("- Method signatures: dependencies FIRST, then business parameters");
        Console.WriteLine("- Generated facade methods have ONLY business parameters");
        Console.WriteLine("- Constructor injects all unique dependencies from all methods");
    }
}
