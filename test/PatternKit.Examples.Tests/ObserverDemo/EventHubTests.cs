using PatternKit.Examples.ObserverDemo;

namespace PatternKit.Examples.Tests.ObserverDemoTests;

public sealed class EventHubTests
{
    [Fact]
    public void CreateDefault_Returns_NonNull()
    {
        var hub = EventHub<int>.CreateDefault();

        Assert.NotNull(hub);
    }

    [Fact]
    public void Publish_Invokes_Subscriber()
    {
        var hub = EventHub<int>.CreateDefault();
        var received = new List<int>();
        hub.On((in int e) => received.Add(e));

        hub.Publish(42);

        Assert.Single(received);
        Assert.Equal(42, received[0]);
    }

    [Fact]
    public void Publish_Invokes_Multiple_Subscribers()
    {
        var hub = EventHub<string>.CreateDefault();
        var log1 = new List<string>();
        var log2 = new List<string>();
        hub.On((in string e) => log1.Add(e));
        hub.On((in string e) => log2.Add(e));

        hub.Publish("hello");

        Assert.Single(log1);
        Assert.Single(log2);
        Assert.Equal("hello", log1[0]);
        Assert.Equal("hello", log2[0]);
    }

    [Fact]
    public void Dispose_Removes_Subscription()
    {
        var hub = EventHub<int>.CreateDefault();
        var received = new List<int>();
        var sub = hub.On((in int e) => received.Add(e));

        hub.Publish(1);
        sub.Dispose();
        hub.Publish(2);

        Assert.Single(received);
        Assert.Equal(1, received[0]);
    }

    [Fact]
    public void On_With_Predicate_Filters_Events()
    {
        var hub = EventHub<int>.CreateDefault();
        var received = new List<int>();
        hub.On((in int e) => e > 10, (in int e) => received.Add(e));

        hub.Publish(5);
        hub.Publish(15);
        hub.Publish(8);
        hub.Publish(20);

        Assert.Equal(2, received.Count);
        Assert.Equal(15, received[0]);
        Assert.Equal(20, received[1]);
    }

    [Fact]
    public void Multiple_Subscriptions_With_Different_Predicates()
    {
        var hub = EventHub<int>.CreateDefault();
        var evens = new List<int>();
        var positives = new List<int>();
        hub.On((in int e) => e % 2 == 0, (in int e) => evens.Add(e));
        hub.On((in int e) => e > 0, (in int e) => positives.Add(e));

        hub.Publish(-2);
        hub.Publish(3);
        hub.Publish(4);

        Assert.Equal(2, evens.Count); // -2 and 4
        Assert.Equal(2, positives.Count); // 3 and 4
    }
}

public sealed class UserEventTests
{
    [Fact]
    public void UserEvent_Record_Works()
    {
        var evt = new UserEvent(123, "login");

        Assert.Equal(123, evt.Id);
        Assert.Equal("login", evt.Action);
    }

    [Fact]
    public void EventHub_With_UserEvent()
    {
        var hub = EventHub<UserEvent>.CreateDefault();
        var received = new List<UserEvent>();
        hub.On((in UserEvent e) => received.Add(e));

        hub.Publish(new UserEvent(1, "login"));
        hub.Publish(new UserEvent(2, "logout"));

        Assert.Equal(2, received.Count);
        Assert.Equal("login", received[0].Action);
        Assert.Equal("logout", received[1].Action);
    }

    [Fact]
    public void EventHub_UserEvent_Filtered_By_Action()
    {
        var hub = EventHub<UserEvent>.CreateDefault();
        var logins = new List<UserEvent>();
        hub.On((in UserEvent e) => e.Action == "login", (in UserEvent e) => logins.Add(e));

        hub.Publish(new UserEvent(1, "login"));
        hub.Publish(new UserEvent(2, "logout"));
        hub.Publish(new UserEvent(3, "login"));

        Assert.Equal(2, logins.Count);
        Assert.All(logins, e => Assert.Equal("login", e.Action));
    }

    [Fact]
    public void EventHub_UserEvent_Filtered_By_Id()
    {
        var hub = EventHub<UserEvent>.CreateDefault();
        var user1Events = new List<UserEvent>();
        hub.On((in UserEvent e) => e.Id == 1, (in UserEvent e) => user1Events.Add(e));

        hub.Publish(new UserEvent(1, "login"));
        hub.Publish(new UserEvent(2, "login"));
        hub.Publish(new UserEvent(1, "logout"));

        Assert.Equal(2, user1Events.Count);
        Assert.All(user1Events, e => Assert.Equal(1, e.Id));
    }
}
