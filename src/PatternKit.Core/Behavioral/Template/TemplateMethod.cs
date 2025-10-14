using System;

namespace PatternKit.Behavioral.Template
{
    /// <summary>
    /// Generic Template Method base class.
    /// Defines the skeleton of an algorithm, allowing subclasses to override steps without changing the structure.
    /// </summary>
    /// <typeparam name="TContext">The type of the context passed to the algorithm.</typeparam>
    /// <typeparam name="TResult">The type of the result produced by the algorithm.</typeparam>
    public abstract class TemplateMethod<TContext, TResult>
    {
        private readonly object _sync = new object();

        /// <summary>
        /// Set to true to synchronize <see cref="Execute(TContext)"/> calls across threads.
        /// Default is false to allow concurrent executions when subclass is stateless or thread-safe.
        /// </summary>
        protected virtual bool Synchronized => false;

        /// <summary>
        /// Executes the algorithm using the provided context.
        /// Calls <see cref="OnBefore"/>, then <see cref="Step"/>, then <see cref="OnAfter"/>.
        /// </summary>
        public TResult Execute(TContext context)
        {
            if (Synchronized)
            {
                lock (_sync)
                {
                    OnBefore(context);
                    var result = Step(context);
                    OnAfter(context, result);
                    return result;
                }
            }

            OnBefore(context);
            var res = Step(context);
            OnAfter(context, res);
            return res;
        }

        /// <summary>
        /// Optional hook before the main step.
        /// </summary>
        protected virtual void OnBefore(TContext context) { }

        /// <summary>
        /// The main step of the algorithm. Must be implemented by subclasses.
        /// </summary>
        protected abstract TResult Step(TContext context);

        /// <summary>
        /// Optional hook after the main step.
        /// </summary>
        protected virtual void OnAfter(TContext context, TResult result) { }
    }
}
