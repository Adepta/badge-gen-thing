namespace DocumentGenerator.Editor.Web.Services;

/// <summary>
/// Extension methods for safely invoking multicast Func&lt;Task&gt; delegates.
/// 
/// When a Func&lt;Task&gt; event has multiple subscribers, calling .Invoke() only 
/// returns the Task from the LAST subscriber in the invocation list. Earlier 
/// subscribers' tasks are fire-and-forget and exceptions are silently lost.
/// These methods iterate all delegates and await each one.
/// </summary>
public static class EventExtensions
{
    /// <summary>
    /// Invokes all subscribers of a Func&lt;Task&gt; event and awaits each one.
    /// Returns immediately if the handler is null.
    /// </summary>
    public static async Task InvokeAllAsync(this Func<Task>? handler)
    {
        if (handler is null) return;

        var delegates = handler.GetInvocationList();
        foreach (var d in delegates)
        {
            await ((Func<Task>)d).Invoke();
        }
    }

    /// <summary>
    /// Invokes all subscribers of a Func&lt;T, Task&gt; event and awaits each one.
    /// Returns immediately if the handler is null.
    /// </summary>
    public static async Task InvokeAllAsync<T>(this Func<T, Task>? handler, T arg)
    {
        if (handler is null) return;

        var delegates = handler.GetInvocationList();
        foreach (var d in delegates)
        {
            await ((Func<T, Task>)d).Invoke(arg);
        }
    }
}
