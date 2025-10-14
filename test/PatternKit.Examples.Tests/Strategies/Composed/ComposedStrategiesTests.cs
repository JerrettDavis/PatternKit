using PatternKit.Behavioral.Strategy;
using PatternKit.Examples.Strategies.Composed;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.Strategies.Composed;
// ----------------- Fakes -----------------

sealed class FakeIdentity : IIdentityService
{
    public bool VerifiedEmail;
    public bool SmsOptIn;
    public bool PushToken;

    public ValueTask<bool> HasVerifiedEmailAsync(Guid userId, CancellationToken ct) => new(VerifiedEmail);
    public ValueTask<bool> HasSmsOptInAsync(Guid userId, CancellationToken ct) => new(SmsOptIn);
    public ValueTask<bool> HasPushTokenAsync(Guid userId, CancellationToken ct) => new(PushToken);
}

sealed class FakePresence : IPresenceService
{
    public bool OnlineIm;
    public bool DoNotDisturb;

    public int OnlineImCalls;
    public int DoNotDisturbCalls;

    public ValueTask<bool> IsOnlineInImAsync(Guid userId, CancellationToken ct)
    {
        OnlineImCalls++;
        return new(OnlineIm);
    }

    public ValueTask<bool> IsDoNotDisturbAsync(Guid userId, CancellationToken ct)
    {
        DoNotDisturbCalls++;
        return new(DoNotDisturb);
    }
}

sealed class FakeRateLimiter : IRateLimiter
{
    private readonly Dictionary<Channel, bool> _allowed = new()
    {
        [Channel.Email] = true,
        [Channel.Sms] = true,
        [Channel.Push] = true,
        [Channel.Im] = true,
    };

    public void Set(Channel ch, bool allowed) => _allowed[ch] = allowed;

    public ValueTask<bool> CanSendAsync(Channel channel, Guid userId, CancellationToken ct)
        => new(_allowed.TryGetValue(channel, out var ok) && ok);
}

sealed class FakePrefs : IPreferenceService
{
    private Channel[] _order = [];
    public void Set(Channel[] order) => _order = order;
    public ValueTask<Channel[]> GetPreferredOrderAsync(Guid userId, CancellationToken ct) => new(_order);
}

abstract class CapturingSenderBase
{
    public int Calls;
    public SendContext? LastContext;
    public bool ResultSuccess = true;
    public string? Info;

    protected ValueTask<SendResult> CaptureAndReturn(SendContext ctx, CancellationToken ct, Channel ch)
    {
        Calls++;
        LastContext = ctx;
        return new(new SendResult(ch, ResultSuccess, Info));
    }
}

sealed class FakeEmailSender : CapturingSenderBase, IEmailSender
{
    public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
        CaptureAndReturn(ctx, ct, Channel.Email);
}

sealed class FakeSmsSender : CapturingSenderBase, ISmsSender
{
    public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
        CaptureAndReturn(ctx, ct, Channel.Sms);
}

sealed class FakePushSender : CapturingSenderBase, IPushSender
{
    public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
        CaptureAndReturn(ctx, ct, Channel.Push);
}

sealed class FakeImSender : CapturingSenderBase, IImSender
{
    public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
        CaptureAndReturn(ctx, ct, Channel.Im);
}

// ----------------- BDD Tests -----------------

[Feature("Preference-aware composed strategies (TinyBDD)")]
public sealed class ComposedStrategiesBddTests(ITestOutputHelper output) : TinyBddXunitBase(output)
{
    private sealed record Harness(
        FakeIdentity Id,
        FakePresence Presence,
        FakeRateLimiter Rate,
        FakePrefs Prefs,
        FakeEmailSender Email,
        FakeSmsSender Sms,
        FakePushSender Push,
        FakeImSender Im,
        AsyncStrategy<SendContext, SendResult> Strategy
    );

    private static Harness CreateHarness(Action<Harness>? configure = null)
    {
        var id = new FakeIdentity();
        var presence = new FakePresence();
        var rate = new FakeRateLimiter();
        var prefs = new FakePrefs();
        var email = new FakeEmailSender();
        var sms = new FakeSmsSender();
        var push = new FakePushSender();
        var im = new FakeImSender();

        var strategy = ComposedStrategies.BuildPreferenceAware(
            id, presence, rate, prefs, email, sms, push, im);

        var h = new Harness(id, presence, rate, prefs, email, sms, push, im, strategy);
        configure?.Invoke(h);
        return h;
    }

    private static SendContext Ctx(bool critical = false) =>
        new(Guid.NewGuid(), "Hello!", critical);

    // Utility: execute strategy and return (Harness, Result) for easy multi-Then checks
    private static async Task<(Harness H, SendResult R)> Run(Harness h)
        => (h, await h.Strategy.ExecuteAsync(Ctx(), CancellationToken.None));

    private static async Task<(Harness H, SendResult R)> Run(Harness h, bool critical)
        => (h, await h.Strategy.ExecuteAsync(Ctx(critical), CancellationToken.None));

    // ---------- Scenarios ----------

    [Scenario("Preference order: first viable -> Push")]
    [Fact]
    public async Task PrefOrder_FirstViable_Push()
    {
        await Given("a harness with Push first in prefs and all push guards passing", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Push, Channel.Im, Channel.Email]);
                    h.Id.PushToken = true;
                    h.Presence.DoNotDisturb = false;
                    h.Rate.Set(Channel.Push, true);
                }))
            .When("executing the strategy", Run)
            .Then("result channel should be Push", x => x.R.Channel == Channel.Push)
            .And("push called exactly once", x => x.H.Push.Calls == 1)
            .And("no other senders called", x => x.H.Im.Calls == 0 && x.H.Email.Calls == 0 && x.H.Sms.Calls == 0)
            .AssertPassed();
    }

    [Scenario("Preference order: skip non-viable Push and try next -> Im")]
    [Fact]
    public async Task PrefOrder_SkipNonViable_TryNext_Im()
    {
        await Given("push first but DND is on; IM is online and allowed", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Push, Channel.Im, Channel.Email]);
                    h.Id.PushToken = true;
                    h.Presence.DoNotDisturb = true; // NotDnd => false
                    h.Rate.Set(Channel.Push, true);

                    h.Presence.OnlineIm = true;
                    h.Rate.Set(Channel.Im, true);
                }))
            .When("executing the strategy", Run)
            .Then("result channel should be Im", x => x.R.Channel == Channel.Im)
            .And("push not called", x => x.H.Push.Calls == 0)
            .And("im called once", x => x.H.Im.Calls == 1)
            .And("email not called", x => x.H.Email.Calls == 0)
            .AssertPassed();
    }

    [Scenario("Critical: SMS is prepended regardless of prefs")]
    [Fact]
    public async Task Critical_Sms_First_RegardlessOfPrefs()
    {
        await Given("prefs omit Sms; Sms is viable", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Email, Channel.Push, Channel.Im]);
                    h.Id.SmsOptIn = true;
                    h.Rate.Set(Channel.Sms, true);
                }))
            .When("executing the strategy as critical", h => Run(h, critical: true))
            .Then("result channel should be Sms", x => x.R.Channel == Channel.Sms)
            .And("only Sms called", x => x.H.Sms.Calls == 1 && x.H.Email.Calls == 0 && x.H.Push.Calls == 0 && x.H.Im.Calls == 0)
            .AssertPassed();
    }

    [Scenario("Critical but SMS not viable -> falls back to default Email send")]
    [Fact]
    public async Task Critical_Sms_NotViable_FallsBack_To_DefaultEmailSend()
    {
        await Given("sms gate(s) fail entirely", () =>
                CreateHarness(h =>
                {
                    h.Id.SmsOptIn = false;          // gate fails
                    h.Rate.Set(Channel.Sms, false); // rate fails too
                }))
            .When("executing the strategy as critical", h => Run(h, critical: true))
            .Then("result channel should be Email", x => x.R.Channel == Channel.Email)
            .And("no Sms call, one Email call", x => x.H.Sms.Calls == 0 && x.H.Email.Calls == 1)
            .AssertPassed();
    }

    [Scenario("Empty prefs -> defaults to Email")]
    [Fact]
    public async Task Prefs_Empty_Order_Defaults_To_Email()
    {
        await Given("no preferences set", () => CreateHarness(h => h.Prefs.Set([])))
            .When("executing the strategy", Run)
            .Then("result channel should be Email", x => x.R.Channel == Channel.Email)
            .And("email called once, others zero", x => x.H.Email.Calls == 1 && x.H.Push.Calls == 0 && x.H.Im.Calls == 0 && x.H.Sms.Calls == 0)
            .AssertPassed();
    }

    [Scenario("Dedup: preserves first occurrence; attempts each gate once; sends next viable (Sms)")]
    [Fact]
    public async Task Dedup_Preserves_FirstOccurrence_AttemptsEachOnce()
    {
        await Given("prefs with duplicates; Im and Email gates fail; Sms viable", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Im, Channel.Email, Channel.Im, Channel.Sms, Channel.Email]);
                    h.Presence.OnlineIm = false; // IM gate fails
                    h.Id.VerifiedEmail = false;  // Email gate fails
                    h.Id.SmsOptIn = true;
                    h.Rate.Set(Channel.Sms, true);
                }))
            .When("executing the strategy", Run)
            .Then("result channel should be Sms", x => x.R.Channel == Channel.Sms)
            .And("IM sender not called (gate failed)", x => x.H.Im.Calls == 0)
            .And("IM gate evaluated once despite duplicates", x => x.H.Presence.OnlineImCalls == 1)
            .And("Email not called (gate failed), Sms called once", x => x.H.Email.Calls == 0 && x.H.Sms.Calls == 1)
            .AssertPassed();
    }

    [Scenario("Email gate respected when first in order -> skips to next (Sms)")]
    [Fact]
    public async Task EmailGate_Respected_WhenInOrder_SkipsToNext()
    {
        await Given("email first but gate fails; sms viable", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Email, Channel.Sms]);
                    h.Id.VerifiedEmail = false; // email gate fails
                    h.Id.SmsOptIn = true;
                    h.Rate.Set(Channel.Sms, true);
                }))
            .When("executing the strategy", Run)
            .Then("result channel should be Sms", x => x.R.Channel == Channel.Sms)
            .And("email not called; sms called once", x => x.H.Email.Calls == 0 && x.H.Sms.Calls == 1)
            .AssertPassed();
    }

    [Scenario("Push gate requires: token, not DND, and rate (progressive checks)")]
    [Fact]
    public async Task PushGate_RequiresToken_NotDnd_Rate_ThenPasses()
    {
        // Start with Push preferred and Email verified for fallback
        var baseHarness = CreateHarness(h =>
        {
            h.Prefs.Set([Channel.Push, Channel.Email]);
            h.Id.VerifiedEmail = true;
        });

        // 1) No token -> Email
        await Given("no push token", () => baseHarness)
            .When("executing", Run)
            .Then("falls back to Email", x => x.R.Channel == Channel.Email)
            .AssertPassed();

        // 2) Token but DND on -> Email
        await Given("push token present, DND on", () =>
            {
                baseHarness.Id.PushToken = true;
                baseHarness.Presence.DoNotDisturb = true;
                return baseHarness;
            })
            .When("executing", Run)
            .Then("still Email due to DND", x => x.R.Channel == Channel.Email)
            .AssertPassed();

        // 3) Token, not DND, but rate limited -> Email
        await Given("token, not DND, push rate limited", () =>
            {
                baseHarness.Presence.DoNotDisturb = false;
                baseHarness.Rate.Set(Channel.Push, false);
                return baseHarness;
            })
            .When("executing", Run)
            .Then("still Email due to rate limit", x => x.R.Channel == Channel.Email)
            .AssertPassed();

        // 4) All good -> Push (and push called once overall)
        await Given("all push guards pass", () =>
            {
                baseHarness.Rate.Set(Channel.Push, true);
                return baseHarness;
            })
            .When("executing", Run)
            .Then("now Push is selected", x => x.R.Channel == Channel.Push)
            .And("push called exactly once", x => x.H.Push.Calls == 1)
            .AssertPassed();
    }

    [Scenario("Im gate requires: online and rate")]
    [Fact]
    public async Task ImGate_RequiresOnline_AndRate()
    {
        var baseHarness = CreateHarness(h =>
        {
            h.Prefs.Set([Channel.Im, Channel.Email]);
            h.Id.VerifiedEmail = true; // email fallback
        });

        // 1) Offline IM -> Email
        await Given("IM offline", () => baseHarness)
            .When("executing", Run)
            .Then("fallback Email", x => x.R.Channel == Channel.Email)
            .AssertPassed();

        // 2) Online but rate limited -> Email
        await Given("IM online but rate limited", () =>
            {
                baseHarness.Presence.OnlineIm = true;
                baseHarness.Rate.Set(Channel.Im, false);
                return baseHarness;
            })
            .When("executing", Run)
            .Then("fallback Email", x => x.R.Channel == Channel.Email)
            .AssertPassed();

        // 3) Online and allowed -> Im (once)
        await Given("IM online and rate allowed", () =>
            {
                baseHarness.Rate.Set(Channel.Im, true);
                return baseHarness;
            })
            .When("executing", Run)
            .Then("select Im", x => x.R.Channel == Channel.Im)
            .And("im called once", x => x.H.Im.Calls == 1)
            .AssertPassed();
    }

    [Scenario("Rate limiter applies per channel; picks first allowed (Sms)")]
    [Fact]
    public async Task RateLimiter_Applies_PerChannel()
    {
        await Given("email disabled; sms allowed; im/push disabled", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Email, Channel.Sms, Channel.Im, Channel.Push]);

                    h.Id.VerifiedEmail = true;
                    h.Id.SmsOptIn = true;
                    h.Presence.OnlineIm = true;
                    h.Id.PushToken = true;
                    h.Presence.DoNotDisturb = false;

                    h.Rate.Set(Channel.Email, false);
                    h.Rate.Set(Channel.Sms, true);
                    h.Rate.Set(Channel.Im, false);
                    h.Rate.Set(Channel.Push, false);
                }))
            .When("executing", Run)
            .Then("Sms chosen", x => x.R.Channel == Channel.Sms)
            .And("email not called", x => x.H.Email.Calls == 0)
            .And("sms called once", x => x.H.Sms.Calls == 1)
            .And("im/push not called", x => x.H.Im.Calls == 0 && x.H.Push.Calls == 0)
            .AssertPassed();
    }

    [Scenario("Default Email fallback ignores Email gate by design")]
    [Fact]
    public async Task DefaultEmailFallback_IgnoresEmailGate_ByDesign()
    {
        await Given("push is preferred but fails; email gate would fail but fallback still sends", () =>
                CreateHarness(h =>
                {
                    h.Prefs.Set([Channel.Push]); // Push will fail gate
                    h.Id.PushToken = false;
                    h.Id.VerifiedEmail = false; // email gate would fail, but fallback still sends
                }))
            .When("executing", Run)
            .Then("email selected via fallback", x => x.R.Channel == Channel.Email)
            .And("email called once", x => x.H.Email.Calls == 1)
            .AssertPassed();
    }
}