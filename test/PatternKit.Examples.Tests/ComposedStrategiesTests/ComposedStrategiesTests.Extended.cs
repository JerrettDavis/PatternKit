using PatternKit.Behavioral.Strategy;
using PatternKit.Examples.Strategies.Composed;
using TinyBDD;
using TinyBDD.Xunit;
using Xunit.Abstractions;

namespace PatternKit.Examples.Tests.ComposedStrategiesTests
{
    // Service spies to verify short-circuit behavior in Push/IM gates.
    sealed class SpyIdentity : IIdentityService
    {
        public int HasPushTokenCalls;
        public int HasVerifiedEmailCalls;
        public int HasSmsOptInCalls;

        public bool PushToken;
        public bool VerifiedEmail;
        public bool SmsOptIn;

        public ValueTask<bool> HasVerifiedEmailAsync(Guid userId, CancellationToken ct)
        {
            HasVerifiedEmailCalls++;
            return new(VerifiedEmail);
        }

        public ValueTask<bool> HasSmsOptInAsync(Guid userId, CancellationToken ct)
        {
            HasSmsOptInCalls++;
            return new(SmsOptIn);
        }

        public ValueTask<bool> HasPushTokenAsync(Guid userId, CancellationToken ct)
        {
            HasPushTokenCalls++;
            return new(PushToken);
        }
    }

    sealed class SpyPresence : IPresenceService
    {
        public int OnlineCalls;
        public int DndCalls;
        public bool OnlineIm;
        public bool DoNotDisturb;

        public ValueTask<bool> IsOnlineInImAsync(Guid userId, CancellationToken ct)
        {
            OnlineCalls++;
            return new(OnlineIm);
        }

        public ValueTask<bool> IsDoNotDisturbAsync(Guid userId, CancellationToken ct)
        {
            DndCalls++;
            return new(DoNotDisturb);
        }
    }

    sealed class SpyRateLimiter : IRateLimiter
    {
        public int EmailCalls;
        public int SmsCalls;
        public int PushCalls;
        public int ImCalls;

        public bool EmailAllowed = true;
        public bool SmsAllowed = true;
        public bool PushAllowed = true;
        public bool ImAllowed = true;

        public ValueTask<bool> CanSendAsync(Channel channel, Guid userId, CancellationToken ct)
        {
            switch (channel)
            {
                case Channel.Email:
                    EmailCalls++;
                    return new(EmailAllowed);
                case Channel.Sms:
                    SmsCalls++;
                    return new(SmsAllowed);
                case Channel.Push:
                    PushCalls++;
                    return new(PushAllowed);
                case Channel.Im:
                    ImCalls++;
                    return new(ImAllowed);
                default: throw new ArgumentOutOfRangeException(nameof(channel), channel, null);
            }
        }
    }

    sealed class ThrowsEmailSender : IEmailSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
            ValueTask.FromCanceled<SendResult>(new CancellationToken(true)); // throws OCE on await
    }

    sealed class FailingImSender : IImSender
    {
        public ValueTask<SendResult> SendAsync(SendContext ctx, CancellationToken ct) =>
            new(new SendResult(Channel.Im, false, "simulated failure"));
    }

    [Feature("Preference-aware composed strategies — extended behaviors (TinyBDD)")]
    public sealed class ComposedStrategiesBddTests_Extended(ITestOutputHelper output) : TinyBddXunitBase(output)
    {
        private static SendContext Ctx() => new(Guid.NewGuid(), "hi", false);

        // ---------- 1) Push gate short-circuits when no token ----------
        [Scenario("Push gate short-circuits: no token -> never checks DND or rate; falls back to Email")]
        [Fact]
        public async Task PushGate_ShortCircuits_WhenNoToken()
        {
            await Given("push is first; no token; DND off; push rate would be allowed", () =>
                {
                    var id = new SpyIdentity { PushToken = false };
                    var presence = new SpyPresence { DoNotDisturb = false };
                    var rate = new SpyRateLimiter { PushAllowed = true };
                    var prefs = new FakePrefs();
                    prefs.Set([Channel.Push, Channel.Email]);

                    var email = new FakeEmailSender();
                    var sms = new FakeSmsSender();
                    var push = new FakePushSender();
                    var im = new FakeImSender();

                    var strategy = ComposedStrategies.BuildPreferenceAware(id, presence, rate, prefs, email, sms, push, im);
                    return (id, presence, rate, email, sms, push, im, strategy);
                })
                .When("executing the strategy",
                    async Task<((SpyIdentity id, SpyPresence presence, SpyRateLimiter rate, FakeEmailSender email, FakeSmsSender sms, FakePushSender push
                        , FakeImSender im, AsyncStrategy<SendContext, SendResult> strategy) t, SendResult r)> (t) =>
                    {
                        var r = await t.strategy.ExecuteAsync(Ctx(), CancellationToken.None);
                        return (t, r);
                    })
                .Then("channel is Email", x => x.r.Channel == Channel.Email)
                .And("push token checked once", x => x.t.id.HasPushTokenCalls == 1)
                .And("DND not checked", x => x.t.presence.DndCalls == 0)
                .And("push rate not checked", x => x.t.rate.PushCalls == 0)
                .AssertPassed();
        }

        // ---------- 2) IM sender failure does not fall through ----------
        [Scenario("IM chosen but sender fails -> stays on IM (no fall-through)")]
        [Fact]
        public async Task ImSender_Failure_DoesNot_FallThrough()
        {
            await Given("IM first; IM gate passes; IM sender returns Success=false; Email viable as fallback", () =>
                {
                    var id = new FakeIdentity { VerifiedEmail = true };
                    var presence = new FakePresence { OnlineIm = true };
                    var rate = new FakeRateLimiter();
                    rate.Set(Channel.Im, true);
                    var prefs = new FakePrefs();
                    prefs.Set([Channel.Im, Channel.Email]);

                    var email = new FakeEmailSender();
                    var sms = new FakeSmsSender();
                    var push = new FakePushSender();
                    var im = new FailingImSender(); // returns Success=false

                    var strategy = ComposedStrategies.BuildPreferenceAware(id, presence, rate, prefs, email, sms, push, im);
                    return (email, strategy);
                })
                .When("executing the strategy",
                    async Task<((FakeEmailSender email, AsyncStrategy<SendContext, SendResult> strategy) t, SendResult r)> (t) =>
                    {
                        var r = await t.strategy.ExecuteAsync(Ctx(), CancellationToken.None);
                        return (t, r);
                    })
                .Then("IM remains the selected channel", x => x.r.Channel == Channel.Im)
                .And("result is unsuccessful", x => !x.r.Success)
                .And("no fall-through to Email", x => x.t.email.Calls == 0)
                .AssertPassed();
        }

        // ---------- 3) Fallback Email throws -> cancellation propagates ----------
        [Scenario("All preferred channels blocked -> fallback Email throws -> propagates TaskCanceledException")]
        [Fact]
        public async Task Throwing_DefaultEmail_PropagatesCancellation()
        {
            await Given("Push/IM/SMS blocked so fallback to Email; Email sender throws OCE", () =>
                {
                    var id = new SpyIdentity();
                    var presence = new SpyPresence();
                    var rate = new SpyRateLimiter { PushAllowed = false, ImAllowed = false, SmsAllowed = false };
                    var prefs = new FakePrefs();
                    prefs.Set([Channel.Push, Channel.Im, Channel.Sms]); // forces fallback

                    var email = new ThrowsEmailSender(); // throws on await
                    var sms = new FakeSmsSender();
                    var push = new FakePushSender();
                    var im = new FakeImSender();

                    var strategy = ComposedStrategies.BuildPreferenceAware(id, presence, rate, prefs, email, sms, push, im);
                    return strategy;
                })
                .When("executing (expecting cancellation from fallback Email)", async Task<(bool threw, Exception? ex)> (strategy) =>
                {
                    try
                    {
                        await strategy.ExecuteAsync(Ctx(), CancellationToken.None);
                        return (threw: false, ex: null);
                    }
                    catch (Exception ex)
                    {
                        return (threw: true, ex);
                    }
                })
                .Then("an exception was thrown", x => x.threw)
                .And("it is TaskCanceledException", x => x.ex is TaskCanceledException)
                .AssertPassed();
        }

        // ---------- 4) Preference order wins among ties ----------
        [Scenario("All channels viable: selection follows declared preference order (Sms first)")]
        [Fact]
        public async Task Preference_Order_Ties_BreakByOrder_NotCapability()
        {
            await Given("all gates/rates pass; preferences = [Sms, Push, Email, Im]", () =>
                {
                    var id = new FakeIdentity { SmsOptIn = true, PushToken = true, VerifiedEmail = true };
                    var presence = new FakePresence { OnlineIm = true, DoNotDisturb = false };
                    var rate = new FakeRateLimiter();

                    var prefs = new FakePrefs();
                    prefs.Set([Channel.Sms, Channel.Push, Channel.Email, Channel.Im]);

                    var email = new FakeEmailSender();
                    var sms = new FakeSmsSender();
                    var push = new FakePushSender();
                    var im = new FakeImSender();

                    var strategy = ComposedStrategies.BuildPreferenceAware(id, presence, rate, prefs, email, sms, push, im);
                    return (email, sms, push, im, strategy);
                })
                .When("executing the strategy",
                    async Task<((FakeEmailSender email, FakeSmsSender sms, FakePushSender push, FakeImSender im, AsyncStrategy<SendContext, SendResult>
                        strategy) t, SendResult r)> (t) =>
                    {
                        var r = await t.strategy.ExecuteAsync(Ctx(), CancellationToken.None);
                        return (t, r);
                    })
                .Then("Sms (first in order) is selected", x => x.r.Channel == Channel.Sms)
                .And("Sms called once", x => x.t.sms.Calls == 1)
                .And("others not called", x => x.t.push.Calls == 0 && x.t.email.Calls == 0 && x.t.im.Calls == 0)
                .AssertPassed();
        }
    }
}