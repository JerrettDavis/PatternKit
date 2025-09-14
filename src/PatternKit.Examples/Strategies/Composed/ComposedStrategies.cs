
using PatternKit.Behavioral.Strategy;

namespace PatternKit.Examples.Strategies.Composed;

/// <summary>
/// Delivery channels supported by the composed strategies example.
/// </summary>
public enum Channel
{
    /// <summary>Email channel.</summary>
    Email,
    /// <summary>SMS (text message) channel.</summary>
    Sms,
    /// <summary>Push notification channel.</summary>
    Push,
    /// <summary>Instant messaging (IM) channel.</summary>
    Im
}

/// <summary>
/// Input passed to the composed strategies when attempting a send.
/// </summary>
/// <param name="UserId">The user receiving the message.</param>
/// <param name="Message">The message body to deliver.</param>
/// <param name="IsCritical">
/// Whether the message is critical. Critical messages may alter ordering (e.g., prepend SMS).
/// </param>
/// <param name="Locale">
/// Optional locale hint for downstream handlers (not used in this example).
/// </param>
public sealed record SendContext(Guid UserId, string Message, bool IsCritical, string? Locale = null);

/// <summary>
/// Result returned by channel handlers indicating outcome and metadata.
/// </summary>
/// <param name="Channel">The channel that was ultimately chosen / attempted.</param>
/// <param name="Success">Whether the send operation succeeded.</param>
/// <param name="Info">Optional diagnostic or provider-supplied information.</param>
public readonly record struct SendResult(Channel Channel, bool Success, string? Info = null);

#region Dependencies

/// <summary>
/// Identity/consent checks that may gate specific channels (e.g., verified email, SMS opt-in).
/// Implementations may be synchronous or asynchronous.
/// </summary>
public interface IIdentityService
{
    /// <summary>Returns whether the user has a verified email address.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> HasVerifiedEmailAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns whether the user has opted in to receive SMS.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> HasSmsOptInAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns whether the user has a registered push token.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> HasPushTokenAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Presence/availability checks (e.g., IM online, Do-Not-Disturb).
/// Implementations may be synchronous or asynchronous.
/// </summary>
public interface IPresenceService
{
    /// <summary>Returns whether the user is currently online in IM.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> IsOnlineInImAsync(Guid userId, CancellationToken ct);

    /// <summary>Returns whether the user is currently in Do-Not-Disturb mode.</summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> IsDoNotDisturbAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// Per-channel rate limiting abstraction.
/// </summary>
public interface IRateLimiter
{
    /// <summary>Returns whether the given <paramref name="channel"/> may send to <paramref name="userId"/> now.</summary>
    /// <param name="channel">Channel being evaluated.</param>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<bool> CanSendAsync(Channel channel, Guid userId, CancellationToken ct);
}

/// <summary>
/// Retrieves user-preferred channel ordering.
/// </summary>
public interface IPreferenceService
{
    /// <summary>
    /// Gets the user's preferred channel order. The strategy will deduplicate while preserving
    /// first occurrences and evaluate gates in that order.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Zero or more channels, ordered by preference.</returns>
    ValueTask<Channel[]> GetPreferredOrderAsync(Guid userId, CancellationToken ct);
}

/// <summary>Email sender.</summary>
public interface IEmailSender
{
    /// <summary>Sends an email using the supplied context.</summary>
    ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct);
}

/// <summary>SMS sender.</summary>
public interface ISmsSender
{
    /// <summary>Sends an SMS using the supplied context.</summary>
    ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct);
}

/// <summary>Push notification sender.</summary>
public interface IPushSender
{
    /// <summary>Sends a push notification using the supplied context.</summary>
    ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct);
}

/// <summary>Instant messaging (IM) sender.</summary>
public interface IImSender
{
    /// <summary>Sends an IM using the supplied context.</summary>
    ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct);
}

#endregion

/// <summary>
/// Bundles the <em>gate</em> (eligibility checks) and <em>send</em> handler for a channel.
/// </summary>
/// <remarks>
/// A policy is composed of:
/// <list type="bullet">
/// <item><description><see cref="Gate"/>: an <see cref="AsyncStrategy{TIn,TOut}"/> that returns <c>true</c> when all checks pass.</description></item>
/// <item><description><see cref="Send"/>: the handler invoked if the gate allows the channel.</description></item>
/// </list>
/// </remarks>
public readonly struct ChannelPolicy
{
    /// <summary>
    /// Gate strategy that returns <see langword="true"/> only when all checks pass for the channel.
    /// </summary>
    public readonly AsyncStrategy<SendContext, bool> Gate;

    /// <summary>
    /// Send handler invoked when the channel is selected (after the gate allows it).
    /// </summary>
    public readonly AsyncStrategy<SendContext, SendResult>.Handler Send;

    /// <summary>
    /// Creates a new <see cref="ChannelPolicy"/> with the specified <paramref name="gate"/> and <paramref name="send"/> handler.
    /// </summary>
    public ChannelPolicy(
        AsyncStrategy<SendContext, bool> gate,
        AsyncStrategy<SendContext, SendResult>.Handler send)
    {
        Gate = gate;
        Send = send;
    }
}

/// <summary>
/// Factory that constructs gate strategies and send handlers for all channels,
/// wiring in the required services.
/// </summary>
/// <param name="id">Identity/consent service.</param>
/// <param name="presence">Presence service (IM online, DND).</param>
/// <param name="rate">Per-channel rate limiter.</param>
/// <param name="email">Email sender.</param>
/// <param name="sms">SMS sender.</param>
/// <param name="push">Push sender.</param>
/// <param name="im">IM sender.</param>
/// <remarks>
/// Instances are lightweight. Call <see cref="CreateAll"/> once and reuse the resulting policies.
/// </remarks>
public sealed class ChannelPolicyFactory(
    IIdentityService id,
    IPresenceService presence,
    IRateLimiter rate,
    IEmailSender email,
    ISmsSender sms,
    IPushSender push,
    IImSender im
)
{
    /// <summary>
    /// Builds policies (gate + send) for every <see cref="Channel"/>.
    /// </summary>
    /// <returns>A dictionary keyed by <see cref="Channel"/> containing ready-to-use policies.</returns>
    /// <remarks>
    /// Gates are composed so that all checks must pass (short-circuiting on first failure).
    /// </remarks>
    public IReadOnlyDictionary<Channel, ChannelPolicy> CreateAll()
    {
        // Build once. Explicit delegate arrays avoid inference pitfalls and limit allocations.
        var pushGate = Gate([HasPushToken, NotDnd, RateOkPush]);
        var imGate = Gate([OnlineIm, RateOkIm]);
        var emailGate = Gate([HasVerifiedEmail, RateOkEmail]);
        var smsGate = Gate([HasSmsOptIn, RateOkSms]);

        return new Dictionary<Channel, ChannelPolicy>
        {
            [Channel.Push] = new(pushGate, SendPush),
            [Channel.Im] = new(imGate, SendIm),
            [Channel.Email] = new(emailGate, SendEmail),
            [Channel.Sms] = new(smsGate, SendSms),
        };
    }

    // ---------- Guards (named methods; no inline lambdas) ----------
    private ValueTask<bool> HasPushToken(SendContext c, CancellationToken ct) => id.HasPushTokenAsync(c.UserId, ct);
    private ValueTask<bool> NotDnd(SendContext c, CancellationToken ct) => presence.IsDoNotDisturbAsync(c.UserId, ct).Continue(Not);
    private ValueTask<bool> RateOkPush(SendContext c, CancellationToken ct) => rate.CanSendAsync(Channel.Push, c.UserId, ct);

    private ValueTask<bool> OnlineIm(SendContext c, CancellationToken ct) => presence.IsOnlineInImAsync(c.UserId, ct);
    private ValueTask<bool> RateOkIm(SendContext c, CancellationToken ct) => rate.CanSendAsync(Channel.Im, c.UserId, ct);

    private ValueTask<bool> HasVerifiedEmail(SendContext c, CancellationToken ct) => id.HasVerifiedEmailAsync(c.UserId, ct);
    private ValueTask<bool> RateOkEmail(SendContext c, CancellationToken ct) => rate.CanSendAsync(Channel.Email, c.UserId, ct);

    private ValueTask<bool> HasSmsOptIn(SendContext c, CancellationToken ct) => id.HasSmsOptInAsync(c.UserId, ct);
    private ValueTask<bool> RateOkSms(SendContext c, CancellationToken ct) => rate.CanSendAsync(Channel.Sms, c.UserId, ct);

    private static bool Not(bool v) => !v;

    // ---------- Senders (method groups) ----------
    private ValueTask<SendResult> SendPush(SendContext c, CancellationToken ct) => push.SendAsync(c, ct);
    private ValueTask<SendResult> SendIm(SendContext c, CancellationToken ct) => im.SendAsync(c, ct);
    private ValueTask<SendResult> SendEmail(SendContext c, CancellationToken ct) => email.SendAsync(c, ct);
    private ValueTask<SendResult> SendSms(SendContext c, CancellationToken ct) => sms.SendAsync(c, ct);

    /// <summary>
    /// Creates a gate strategy that returns <see langword="true"/> only if <b>all</b>
    /// supplied checks pass (short-circuiting on first failure).
    /// </summary>
    /// <param name="allOf">Checks to evaluate sequentially.</param>
    /// <returns>An <see cref="AsyncStrategy{TIn, TOut}"/> producing <see cref="bool"/>.</returns>
    /// <remarks>
    /// The resulting strategy:
    /// <list type="number">
    /// <item>Evaluates each check sequentially, respecting short-circuit semantics.</item>
    /// <item>Returns <see langword="false"/> on the first failure.</item>
    /// <item>Returns <see langword="true"/> if all checks pass.</item>
    /// </list>
    /// </remarks>
    private static AsyncStrategy<SendContext, bool> Gate(
        IEnumerable<Func<SendContext, CancellationToken, ValueTask<bool>>> allOf)
    {
        var checks = allOf as Func<SendContext, CancellationToken, ValueTask<bool>>[] ?? allOf.ToArray();

        return AsyncStrategy<SendContext, bool>.Create()
            .When(AnyFailureAsync)
            .Then(ReturnFalse)   // had a failure → NOT allowed
            .Default(ReturnTrue) // no failures → allowed
            .Build();

        static ValueTask<bool> ReturnFalse(SendContext _, CancellationToken __) => new(false);
        static ValueTask<bool> ReturnTrue(SendContext _, CancellationToken __) => new(true);

        async ValueTask<bool> AnyFailureAsync(SendContext c, CancellationToken ct)
        {
            // Sequential checks to preserve short-circuit semantics; easy to switch to parallel if desired.
            foreach (var check in checks)
                if (!await check(c, ct).ConfigureAwait(false))
                    return true;
            return false;
        }
    }
}

/// <summary>
/// Entry point for building the composed, preference-aware strategy.
/// </summary>
public static class ComposedStrategies
{
    /// <summary>
    /// Builds a strategy that:
    /// <list type="number">
    /// <item>Prepends SMS for critical messages (if viable), otherwise</item>
    /// <item>Uses the user's preferred channel order (deduplicated, preserving first occurrence)</item>
    /// <item>Evaluates per-channel gates and executes the first viable channel</item>
    /// <item>Falls back to Email's send handler if nothing is viable</item>
    /// </list>
    /// </summary>
    /// <param name="id">Identity/consent service.</param>
    /// <param name="presence">Presence/DND service.</param>
    /// <param name="rate">Per-channel rate limiter.</param>
    /// <param name="prefs">Preference service providing channel ordering.</param>
    /// <param name="email">Email sender.</param>
    /// <param name="sms">SMS sender.</param>
    /// <param name="push">Push sender.</param>
    /// <param name="im">IM sender.</param>
    /// <returns>An <see cref="AsyncStrategy{TIn, TOut}"/> that returns <see cref="SendResult"/>.</returns>
    /// <remarks>
    /// <para>
    /// Default fallback is Email's send handler. This fallback is invoked even if the email gate would fail,
    /// by design, to guarantee a final delivery attempt.
    /// </para>
    /// <para>
    /// The returned strategy is immutable and safe for concurrent use, given thread-safe dependencies.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var strategy = ComposedStrategies.BuildPreferenceAware(id, presence, rate, prefs, email, sms, push, im);
    /// var result = await strategy.ExecuteAsync(new SendContext(userId, "Hello", false), CancellationToken.None);
    /// </code>
    /// </example>
    public static AsyncStrategy<SendContext, SendResult> BuildPreferenceAware(
        IIdentityService id,
        IPresenceService presence,
        IRateLimiter rate,
        IPreferenceService prefs,
        IEmailSender email,
        ISmsSender sms,
        IPushSender push,
        IImSender im)
    {
        // Build policies once; methods inside factory are instance members (no per-call closures)
        var factory = new ChannelPolicyFactory(id, presence, rate, email, sms, push, im);
        var policies = factory.CreateAll();

        return AsyncStrategy<SendContext, SendResult>.Create()
            .When(IsCritical)
            .Then(ExecuteCritical)
            .Default(ExecuteByPrefs)
            .Build();

        // Top-level predicates/executors (named local methods; clear and testable)
        static ValueTask<bool> IsCritical(SendContext c, CancellationToken _) => new(c.IsCritical);

        ValueTask<SendResult> ExecuteCritical(SendContext c, CancellationToken t)
        {
            static ValueTask<Channel[]> CriticalOrder(SendContext _) =>
                PrependIfMissing(Channel.Sms, []);

            // Default fallback is Email policy's send handler.
            var def = policies[Channel.Email].Send;
            return ExecuteOrderedAsync(c, t, CriticalOrder, policies, def);
        }

        ValueTask<Channel[]> PreferredOrder(SendContext c, CancellationToken t) =>
            prefs.GetPreferredOrderAsync(c.UserId, t);

        ValueTask<SendResult> ExecuteByPrefs(SendContext c, CancellationToken t)
        {
            var def = policies[Channel.Email].Send;
            return ExecuteOrderedAsync(c, t, ctx => PreferredOrder(ctx, t), policies, def);
        }
    }

    // Compose an ordered strategy at runtime, based on gates; no switches.
    private static async ValueTask<SendResult> ExecuteOrderedAsync(
        SendContext ctx,
        CancellationToken ct,
        Func<SendContext, ValueTask<Channel[]>> orderFactory,
        IReadOnlyDictionary<Channel, ChannelPolicy> policies,
        AsyncStrategy<SendContext, SendResult>.Handler @default)
    {
        var order = await orderFactory(ctx).ConfigureAwait(false);

        // De-dupe while preserving order
        var distinct = new List<Channel>(order.Length);
        distinct.AddRange(order.Distinct());

        var b = AsyncStrategy<SendContext, SendResult>.Create();
        b = distinct
            .Select(ch => policies[ch])
            .Aggregate(b, (current, policy)
                => current
                    .When(policy.Gate.ExecuteAsync) // method group on the instance gate
                    .Then(policy.Send));

        var strat = b.Default(@default).Build();
        return await strat.ExecuteAsync(ctx, ct).ConfigureAwait(false);
    }

    private static ValueTask<Channel[]> PrependIfMissing(Channel ch, Channel[] order)
    {
        if (order.Length != 0 && order[0] == ch)
            return new ValueTask<Channel[]>(order);

        var arr = new Channel[order.Length + 1];
        arr[0] = ch;
        Array.Copy(order, 0, arr, 1, order.Length);
        return new ValueTask<Channel[]>(arr);
    }

    /// <summary>
    /// Applies a continuation to a <see cref="ValueTask{Boolean}"/> without additional allocations
    /// when already completed successfully.
    /// </summary>
    /// <param name="t">The task to continue.</param>
    /// <param name="f">Continuation mapping <c>bool → bool</c>.</param>
    /// <returns>A continued <see cref="ValueTask{Boolean}"/>.</returns>
    /// <remarks>
    /// Used by the DND inversion check to avoid extra <c>await</c>/lambda allocations
    /// when the source <see cref="ValueTask{Boolean}"/> is already completed.
    /// </remarks>
    internal static ValueTask<bool> Continue(this ValueTask<bool> t, Func<bool, bool> f) =>
        t.IsCompletedSuccessfully ? new ValueTask<bool>(f(t.Result)) : Awaited(t, f);

    private static async ValueTask<bool> Awaited(ValueTask<bool> t, Func<bool, bool> f) =>
        f(await t.ConfigureAwait(false));
}
