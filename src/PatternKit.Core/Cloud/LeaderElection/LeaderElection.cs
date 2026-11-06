namespace PatternKit.Cloud.LeaderElection;

public sealed class LeaderLease
{
    public LeaderLease(string candidateId, long term, DateTimeOffset expiresAt)
        => (CandidateId, Term, ExpiresAt) = (candidateId, term, expiresAt);

    public string CandidateId { get; }

    public long Term { get; }

    public DateTimeOffset ExpiresAt { get; }

    public LeaderLease Renew(DateTimeOffset expiresAt) => new(CandidateId, Term, expiresAt);
}

public sealed class LeaderElectionResult
{
    private LeaderElectionResult(string electionName, string candidateId, LeaderLease? lease, Exception? exception, bool acquired, bool renewed, bool released)
        => (ElectionName, CandidateId, Lease, Exception, Acquired, Renewed, Released) = (electionName, candidateId, lease, exception, acquired, renewed, released);

    public string ElectionName { get; }

    public string CandidateId { get; }

    public LeaderLease? Lease { get; }

    public Exception? Exception { get; }

    public bool Acquired { get; }

    public bool Renewed { get; }

    public bool Released { get; }

    public bool Succeeded => Exception is null;

    public bool Failed => !Succeeded;

    public static LeaderElectionResult Acquisition(string electionName, string candidateId, LeaderLease lease)
        => new(electionName, candidateId, lease ?? throw new ArgumentNullException(nameof(lease)), null, acquired: true, renewed: false, released: false);

    public static LeaderElectionResult Renewal(string electionName, string candidateId, LeaderLease lease)
        => new(electionName, candidateId, lease ?? throw new ArgumentNullException(nameof(lease)), null, acquired: false, renewed: true, released: false);

    public static LeaderElectionResult Release(string electionName, string candidateId)
        => new(electionName, candidateId, null, null, acquired: false, renewed: false, released: true);

    public static LeaderElectionResult Failure(string electionName, string candidateId, Exception exception, LeaderLease? lease = null)
        => new(electionName, candidateId, lease, exception ?? throw new ArgumentNullException(nameof(exception)), acquired: false, renewed: false, released: false);
}

public sealed class LeaderElectionCandidate<TContext>
{
    internal LeaderElectionCandidate(string candidateId, TContext context, Action<LeaderLease, TContext>? onAcquired, Action<LeaderLease, TContext>? onRenewed, Action<TContext>? onReleased)
        => (CandidateId, Context, OnAcquired, OnRenewed, OnReleased) = (candidateId, context, onAcquired, onRenewed, onReleased);

    public string CandidateId { get; }

    public TContext Context { get; }

    internal Action<LeaderLease, TContext>? OnAcquired { get; }

    internal Action<LeaderLease, TContext>? OnRenewed { get; }

    internal Action<TContext>? OnReleased { get; }
}

public sealed class LeaderElection<TContext>
{
    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _leaseDuration;
    private LeaderLease? _currentLease;

    private LeaderElection(string name, TimeSpan leaseDuration, Func<DateTimeOffset>? clock)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Leader election name is required.", nameof(name));
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");

        Name = name;
        _leaseDuration = leaseDuration;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public string Name { get; }

    public LeaderLease? CurrentLease
    {
        get
        {
            ExpireIfNeeded();
            return _currentLease;
        }
    }

    public LeaderElectionResult TryAcquire(LeaderElectionCandidate<TContext> candidate)
    {
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate));

        ExpireIfNeeded();
        if (_currentLease is not null && !IsLeader(candidate.CandidateId))
            return LeaderElectionResult.Failure(Name, candidate.CandidateId, new InvalidOperationException($"Leadership is held by '{_currentLease.CandidateId}'."), _currentLease);

        var lease = new LeaderLease(candidate.CandidateId, (_currentLease?.Term ?? 0) + 1, _clock().Add(_leaseDuration));
        _currentLease = lease;
        candidate.OnAcquired?.Invoke(lease, candidate.Context);
        return LeaderElectionResult.Acquisition(Name, candidate.CandidateId, lease);
    }

    public LeaderElectionResult Renew(LeaderElectionCandidate<TContext> candidate)
    {
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate));

        ExpireIfNeeded();
        if (_currentLease is null)
            return LeaderElectionResult.Failure(Name, candidate.CandidateId, new InvalidOperationException("No active leadership lease exists."));
        if (!IsLeader(candidate.CandidateId))
            return LeaderElectionResult.Failure(Name, candidate.CandidateId, new InvalidOperationException($"Candidate '{candidate.CandidateId}' is not the current leader."), _currentLease);

        var lease = _currentLease.Renew(_clock().Add(_leaseDuration));
        _currentLease = lease;
        candidate.OnRenewed?.Invoke(lease, candidate.Context);
        return LeaderElectionResult.Renewal(Name, candidate.CandidateId, lease);
    }

    public LeaderElectionResult Release(LeaderElectionCandidate<TContext> candidate)
    {
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate));

        ExpireIfNeeded();
        if (_currentLease is null)
            return LeaderElectionResult.Failure(Name, candidate.CandidateId, new InvalidOperationException("No active leadership lease exists."));
        if (!IsLeader(candidate.CandidateId))
            return LeaderElectionResult.Failure(Name, candidate.CandidateId, new InvalidOperationException($"Candidate '{candidate.CandidateId}' is not the current leader."), _currentLease);

        _currentLease = null;
        candidate.OnReleased?.Invoke(candidate.Context);
        return LeaderElectionResult.Release(Name, candidate.CandidateId);
    }

    public bool IsLeader(string candidateId)
    {
        if (string.IsNullOrWhiteSpace(candidateId))
            throw new ArgumentException("Candidate id is required.", nameof(candidateId));

        ExpireIfNeeded();
        return _currentLease is not null && string.Equals(_currentLease.CandidateId, candidateId, StringComparison.Ordinal);
    }

    public static Builder Create(string name = "leader-election") => new(name);

    private void ExpireIfNeeded()
    {
        if (_currentLease is not null && _currentLease.ExpiresAt <= _clock())
            _currentLease = null;
    }

    public sealed class Builder
    {
        private readonly string _name;
        private TimeSpan _leaseDuration = TimeSpan.FromSeconds(30);
        private Func<DateTimeOffset>? _clock;

        internal Builder(string name) => _name = name;

        public Builder LeaseDuration(TimeSpan leaseDuration)
        {
            _leaseDuration = leaseDuration;
            return this;
        }

        public Builder Clock(Func<DateTimeOffset> clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            return this;
        }

        public LeaderElection<TContext> Build() => new(_name, _leaseDuration, _clock);
    }
}

public static class LeaderElectionCandidate
{
    public static Builder<TContext> Create<TContext>(string candidateId, TContext context)
        => new(candidateId, context);

    public sealed class Builder<TContext>
    {
        private readonly string _candidateId;
        private readonly TContext _context;
        private Action<LeaderLease, TContext>? _onAcquired;
        private Action<LeaderLease, TContext>? _onRenewed;
        private Action<TContext>? _onReleased;

        internal Builder(string candidateId, TContext context)
        {
            if (string.IsNullOrWhiteSpace(candidateId))
                throw new ArgumentException("Candidate id is required.", nameof(candidateId));

            _candidateId = candidateId;
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Builder<TContext> OnAcquired(Action<LeaderLease, TContext> onAcquired)
        {
            _onAcquired = onAcquired ?? throw new ArgumentNullException(nameof(onAcquired));
            return this;
        }

        public Builder<TContext> OnRenewed(Action<LeaderLease, TContext> onRenewed)
        {
            _onRenewed = onRenewed ?? throw new ArgumentNullException(nameof(onRenewed));
            return this;
        }

        public Builder<TContext> OnReleased(Action<TContext> onReleased)
        {
            _onReleased = onReleased ?? throw new ArgumentNullException(nameof(onReleased));
            return this;
        }

        public LeaderElectionCandidate<TContext> Build()
            => new(_candidateId, _context, _onAcquired, _onRenewed, _onReleased);
    }
}
