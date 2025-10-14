using System;
using System.Threading.Tasks;
using PatternKit.Behavioral.Template;
using Xunit;

namespace PatternKit.Tests.Behavioral
{
    public class TemplateMethodTests
    {
        private class TestTemplate : TemplateMethod<string, string>
        {
            public bool BeforeCalled { get; private set; }
            public bool AfterCalled { get; private set; }

            protected override void OnBefore(string context)
            {
                BeforeCalled = true;
            }

            protected override string Step(string context)
            {
                return context.ToUpperInvariant();
            }

            protected override void OnAfter(string context, string result)
            {
                AfterCalled = true;
            }
        }

        [Fact]
        public void ExecutesAlgorithmAndHooks()
        {
            var template = new TestTemplate();
            var result = template.Execute("test");
            Assert.Equal("TEST", result);
            Assert.True(template.BeforeCalled);
            Assert.True(template.AfterCalled);
        }

        [Fact]
        public void IsThreadSafe()
        {
            var template = new TestTemplate();
            var tasks = new Task<string>[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = Task.Run(() => template.Execute($"thread-{i}"));
            }
            Task.WaitAll(tasks);
            foreach (var task in tasks)
            {
                Assert.StartsWith("THREAD-", task.Result);
            }
        }
    }
}

