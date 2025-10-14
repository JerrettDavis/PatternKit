using System;
using PatternKit.Behavioral.Template;

namespace PatternKit.Examples.TemplateDemo
{
    // Example: DataProcessor using TemplateMethod
    public class DataProcessor : TemplateMethod<string, int>
    {
        protected override void OnBefore(string context)
        {
            Console.WriteLine($"Preparing to process: {context}");
        }

        protected override int Step(string context)
        {
            // Simulate processing: count words
            return context.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        }

        protected override void OnAfter(string context, int result)
        {
            Console.WriteLine($"Processed '{context}' with result: {result}");
        }
    }

    // Demo runner
    public static class TemplateMethodDemo
    {
        public static void Run()
        {
            var processor = new DataProcessor();
            int result = processor.Execute("The quick brown fox jumps over the lazy dog");
            Console.WriteLine($"Word count: {result}");
        }
    }
}

