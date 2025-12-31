# State Machine Pattern Real-World Examples

Production-ready examples demonstrating the State Machine pattern in real-world scenarios.

---

## Example 1: Payment Processing Gateway

### The Problem

A payment gateway needs to manage complex transaction states including authorization, capture, refunds, and chargebacks with strict rules about what operations are allowed at each stage.

### The Solution

Use State Machine to enforce valid transaction state transitions.

### The Code

```csharp
public enum PaymentState
{
    Created, Authorizing, Authorized, Capturing, Captured,
    Refunding, Refunded, Voided, Declined, ChargebackPending, ChargebackLost
}

public enum PaymentEvent
{
    Authorize, AuthorizeSuccess, AuthorizeFailed,
    Capture, CaptureSuccess, CaptureFailed,
    Void, Refund, RefundSuccess, RefundFailed,
    ChargebackReceived, ChargebackWon, ChargebackLost
}

public class PaymentTransaction
{
    private readonly StateMachine<PaymentState, PaymentEvent> _machine;
    private readonly IPaymentGateway _gateway;
    private readonly IAuditLog _audit;

    public Guid TransactionId { get; }
    public decimal Amount { get; }
    public PaymentState State => _machine.CurrentState;

    public PaymentTransaction(Guid id, decimal amount, IPaymentGateway gateway, IAuditLog audit)
    {
        TransactionId = id;
        Amount = amount;
        _gateway = gateway;
        _audit = audit;

        _machine = StateMachine<PaymentState, PaymentEvent>.Create(PaymentState.Created)
            .State(PaymentState.Created)
                .OnEntry(() => _audit.Log(id, "Payment created"))
                .On(PaymentEvent.Authorize)
                    .Execute(() => StartAuthorization())
                    .TransitionTo(PaymentState.Authorizing)

            .State(PaymentState.Authorizing)
                .On(PaymentEvent.AuthorizeSuccess).TransitionTo(PaymentState.Authorized)
                .On(PaymentEvent.AuthorizeFailed).TransitionTo(PaymentState.Declined)

            .State(PaymentState.Authorized)
                .OnEntry(() => _audit.Log(id, "Payment authorized"))
                .On(PaymentEvent.Capture)
                    .Execute(() => StartCapture())
                    .TransitionTo(PaymentState.Capturing)
                .On(PaymentEvent.Void)
                    .Execute(() => VoidAuthorization())
                    .TransitionTo(PaymentState.Voided)

            .State(PaymentState.Capturing)
                .On(PaymentEvent.CaptureSuccess).TransitionTo(PaymentState.Captured)
                .On(PaymentEvent.CaptureFailed).TransitionTo(PaymentState.Authorized)

            .State(PaymentState.Captured)
                .OnEntry(() =>
                {
                    _audit.Log(id, "Payment captured");
                    SendReceipt();
                })
                .On(PaymentEvent.Refund)
                    .When(() => CanRefund())
                    .Execute(() => StartRefund())
                    .TransitionTo(PaymentState.Refunding)
                .On(PaymentEvent.ChargebackReceived)
                    .Execute(() => NotifyMerchant())
                    .TransitionTo(PaymentState.ChargebackPending)

            .State(PaymentState.Refunding)
                .On(PaymentEvent.RefundSuccess).TransitionTo(PaymentState.Refunded)
                .On(PaymentEvent.RefundFailed).TransitionTo(PaymentState.Captured)

            .State(PaymentState.ChargebackPending)
                .On(PaymentEvent.ChargebackWon).TransitionTo(PaymentState.Captured)
                .On(PaymentEvent.ChargebackLost).TransitionTo(PaymentState.ChargebackLost)

            // Terminal states
            .State(PaymentState.Declined)
                .OnEntry(() => _audit.Log(id, "Payment declined"))
            .State(PaymentState.Voided)
                .OnEntry(() => _audit.Log(id, "Payment voided"))
            .State(PaymentState.Refunded)
                .OnEntry(() => _audit.Log(id, "Payment refunded"))
            .State(PaymentState.ChargebackLost)
                .OnEntry(() => _audit.Log(id, "Chargeback lost"))

            .Build();
    }

    public async Task AuthorizeAsync(CancellationToken ct)
    {
        _machine.Fire(PaymentEvent.Authorize);
        var result = await _gateway.AuthorizeAsync(TransactionId, Amount, ct);
        _machine.Fire(result.Success ? PaymentEvent.AuthorizeSuccess : PaymentEvent.AuthorizeFailed);
    }

    public async Task CaptureAsync(CancellationToken ct)
    {
        _machine.Fire(PaymentEvent.Capture);
        var result = await _gateway.CaptureAsync(TransactionId, ct);
        _machine.Fire(result.Success ? PaymentEvent.CaptureSuccess : PaymentEvent.CaptureFailed);
    }

    private bool CanRefund() => (DateTime.UtcNow - CapturedAt) <= TimeSpan.FromDays(90);
}
```

### Why This Pattern

- **Enforced transitions**: Invalid operations rejected at state level
- **Clear audit trail**: Entry actions log all state changes
- **Business rules**: Guards enforce refund windows, etc.

---

## Example 2: CI/CD Pipeline Orchestrator

### The Problem

A CI/CD system needs to manage build pipelines through stages (checkout, build, test, deploy) with proper handling of failures, retries, and manual approvals.

### The Solution

Use State Machine to orchestrate pipeline stages with guards for approvals.

### The Code

```csharp
public enum PipelineState
{
    Queued, CheckingOut, Building, Testing, AwaitingApproval,
    Deploying, Deployed, Failed, Cancelled
}

public enum PipelineEvent
{
    Start, CheckoutComplete, CheckoutFailed,
    BuildComplete, BuildFailed,
    TestsPass, TestsFail,
    Approve, Reject,
    DeployComplete, DeployFailed,
    Retry, Cancel
}

public class Pipeline
{
    private readonly StateMachine<PipelineState, PipelineEvent> _machine;
    private int _retryCount;

    public string Id { get; }
    public PipelineState State => _machine.CurrentState;
    public List<string> Logs { get; } = new();

    public Pipeline(string id, PipelineConfig config, IApprovalService approvals)
    {
        Id = id;

        _machine = StateMachine<PipelineState, PipelineEvent>.Create(PipelineState.Queued)
            .State(PipelineState.Queued)
                .OnEntry(() => Log("Pipeline queued"))
                .On(PipelineEvent.Start)
                    .Execute(() => StartCheckout())
                    .TransitionTo(PipelineState.CheckingOut)
                .On(PipelineEvent.Cancel).TransitionTo(PipelineState.Cancelled)

            .State(PipelineState.CheckingOut)
                .OnEntry(() => Log("Checking out source"))
                .On(PipelineEvent.CheckoutComplete).TransitionTo(PipelineState.Building)
                .On(PipelineEvent.CheckoutFailed).TransitionTo(PipelineState.Failed)

            .State(PipelineState.Building)
                .OnEntry(() =>
                {
                    Log("Building");
                    StartBuild();
                })
                .On(PipelineEvent.BuildComplete).TransitionTo(PipelineState.Testing)
                .On(PipelineEvent.BuildFailed).TransitionTo(PipelineState.Failed)

            .State(PipelineState.Testing)
                .OnEntry(() =>
                {
                    Log("Running tests");
                    StartTests();
                })
                .On(PipelineEvent.TestsPass)
                    .When(() => config.RequiresApproval)
                    .TransitionTo(PipelineState.AwaitingApproval)
                .On(PipelineEvent.TestsPass)
                    .When(() => !config.RequiresApproval)
                    .TransitionTo(PipelineState.Deploying)
                .On(PipelineEvent.TestsFail).TransitionTo(PipelineState.Failed)

            .State(PipelineState.AwaitingApproval)
                .OnEntry(() =>
                {
                    Log("Awaiting approval");
                    approvals.RequestApproval(Id, config.Approvers);
                })
                .On(PipelineEvent.Approve)
                    .When(() => approvals.IsApproved(Id))
                    .TransitionTo(PipelineState.Deploying)
                .On(PipelineEvent.Reject).TransitionTo(PipelineState.Cancelled)

            .State(PipelineState.Deploying)
                .OnEntry(() =>
                {
                    Log($"Deploying to {config.Environment}");
                    StartDeploy();
                })
                .On(PipelineEvent.DeployComplete).TransitionTo(PipelineState.Deployed)
                .On(PipelineEvent.DeployFailed).TransitionTo(PipelineState.Failed)

            .State(PipelineState.Deployed)
                .OnEntry(() =>
                {
                    Log("Deployment successful");
                    NotifySuccess();
                })

            .State(PipelineState.Failed)
                .OnEntry(() =>
                {
                    Log("Pipeline failed");
                    NotifyFailure();
                })
                .On(PipelineEvent.Retry)
                    .When(() => _retryCount < config.MaxRetries)
                    .Execute(() => _retryCount++)
                    .TransitionTo(PipelineState.Queued)

            .State(PipelineState.Cancelled)
                .OnEntry(() => Log("Pipeline cancelled"))

            .Build();
    }

    private void Log(string message)
    {
        Logs.Add($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
    }
}
```

### Why This Pattern

- **Stage progression**: Clear flow through pipeline stages
- **Conditional paths**: Approval required only when configured
- **Retry logic**: Failed pipelines can retry with limits

---

## Example 3: Game Character Controller

### The Problem

A game character needs to manage states like idle, walking, running, jumping, and attacking with proper animations and collision handling.

### The Solution

Use State Machine to manage character states with physics-based guards.

### The Code

```csharp
public enum CharacterState
{
    Idle, Walking, Running, Jumping, Falling, Attacking, Stunned, Dead
}

public enum CharacterInput
{
    Move, Run, Jump, Attack, Land, TakeDamage, Die, Recover, Stop
}

public class CharacterController
{
    private readonly StateMachine<CharacterState, CharacterInput> _machine;
    private readonly IAnimator _animator;
    private readonly IPhysics _physics;

    public CharacterState State => _machine.CurrentState;
    public float Health { get; private set; }
    public bool IsGrounded => _physics.IsGrounded;
    public bool IsAlive => Health > 0;

    public CharacterController(IAnimator animator, IPhysics physics)
    {
        _animator = animator;
        _physics = physics;
        Health = 100;

        _machine = StateMachine<CharacterState, CharacterInput>.Create(CharacterState.Idle)
            .State(CharacterState.Idle)
                .OnEntry(() => _animator.Play("Idle"))
                .On(CharacterInput.Move).TransitionTo(CharacterState.Walking)
                .On(CharacterInput.Run).TransitionTo(CharacterState.Running)
                .On(CharacterInput.Jump)
                    .When(() => IsGrounded)
                    .Execute(() => _physics.ApplyJumpForce())
                    .TransitionTo(CharacterState.Jumping)
                .On(CharacterInput.Attack).TransitionTo(CharacterState.Attacking)
                .On(CharacterInput.TakeDamage)
                    .Execute(() => ApplyDamage())
                    .TransitionTo(CharacterState.Stunned)
                .On(CharacterInput.Die).TransitionTo(CharacterState.Dead)

            .State(CharacterState.Walking)
                .OnEntry(() => _animator.Play("Walk"))
                .On(CharacterInput.Stop).TransitionTo(CharacterState.Idle)
                .On(CharacterInput.Run).TransitionTo(CharacterState.Running)
                .On(CharacterInput.Jump)
                    .When(() => IsGrounded)
                    .Execute(() => _physics.ApplyJumpForce())
                    .TransitionTo(CharacterState.Jumping)
                .On(CharacterInput.Attack).TransitionTo(CharacterState.Attacking)

            .State(CharacterState.Running)
                .OnEntry(() => _animator.Play("Run"))
                .On(CharacterInput.Stop).TransitionTo(CharacterState.Idle)
                .On(CharacterInput.Move).TransitionTo(CharacterState.Walking)
                .On(CharacterInput.Jump)
                    .When(() => IsGrounded)
                    .Execute(() => _physics.ApplyJumpForce(1.2f)) // Higher jump when running
                    .TransitionTo(CharacterState.Jumping)

            .State(CharacterState.Jumping)
                .OnEntry(() => _animator.Play("Jump"))
                .On(CharacterInput.Land)
                    .When(() => IsGrounded)
                    .TransitionTo(CharacterState.Idle)
                .On(CharacterInput.Attack)
                    .Execute(() => PerformAirAttack())
                    .TransitionTo(CharacterState.Falling)

            .State(CharacterState.Falling)
                .OnEntry(() => _animator.Play("Fall"))
                .On(CharacterInput.Land)
                    .When(() => IsGrounded)
                    .TransitionTo(CharacterState.Idle)

            .State(CharacterState.Attacking)
                .OnEntry(() =>
                {
                    _animator.Play("Attack");
                    PerformAttack();
                })
                .On(CharacterInput.Stop)
                    .When(() => _animator.IsComplete("Attack"))
                    .TransitionTo(CharacterState.Idle)

            .State(CharacterState.Stunned)
                .OnEntry(() => _animator.Play("Stunned"))
                .On(CharacterInput.Recover)
                    .When(() => IsAlive)
                    .TransitionTo(CharacterState.Idle)
                .On(CharacterInput.Die)
                    .When(() => !IsAlive)
                    .TransitionTo(CharacterState.Dead)

            .State(CharacterState.Dead)
                .OnEntry(() =>
                {
                    _animator.Play("Death");
                    DisableCollision();
                })

            .Build();
    }

    public void Update(float deltaTime)
    {
        // Physics-based state updates
        if (!IsGrounded && State == CharacterState.Jumping)
        {
            if (_physics.Velocity.Y < 0)
                _machine.TryFire(CharacterInput.Land);
        }
    }
}
```

### Why This Pattern

- **Animation sync**: Entry actions trigger correct animations
- **Physics guards**: Jump only when grounded
- **Interruptible states**: Damage can interrupt any state

---

## Example 4: Hotel Room Booking System

### The Problem

A hotel reservation system needs to manage room states from available through booking, check-in, stay, and checkout with proper handling of cancellations and no-shows.

### The Solution

Use State Machine to enforce booking lifecycle rules.

### The Code

```csharp
public enum RoomState
{
    Available, Reserved, CheckedIn, Occupied, CheckedOut, Cleaning, Maintenance
}

public enum RoomEvent
{
    Reserve, ConfirmReservation, CancelReservation, NoShow,
    CheckIn, CheckOut, StartCleaning, FinishCleaning,
    ReportIssue, CompleteMaintenance
}

public class HotelRoom
{
    private readonly StateMachine<RoomState, RoomEvent> _machine;
    private readonly INotificationService _notifications;

    public string RoomNumber { get; }
    public RoomState State => _machine.CurrentState;
    public Reservation? CurrentReservation { get; private set; }

    public HotelRoom(string roomNumber, INotificationService notifications)
    {
        RoomNumber = roomNumber;
        _notifications = notifications;

        _machine = StateMachine<RoomState, RoomEvent>.Create(RoomState.Available)
            .State(RoomState.Available)
                .On(RoomEvent.Reserve)
                    .Execute(() => SendConfirmation())
                    .TransitionTo(RoomState.Reserved)
                .On(RoomEvent.ReportIssue).TransitionTo(RoomState.Maintenance)

            .State(RoomState.Reserved)
                .OnEntry(() => BlockCalendar())
                .On(RoomEvent.CheckIn)
                    .When(() => IsCheckInTime())
                    .Execute(() => ActivateKeyCard())
                    .TransitionTo(RoomState.CheckedIn)
                .On(RoomEvent.CancelReservation)
                    .Execute(() => ProcessRefund())
                    .TransitionTo(RoomState.Available)
                .On(RoomEvent.NoShow)
                    .When(() => IsNoShowTime())
                    .Execute(() => ChargeNoShowFee())
                    .TransitionTo(RoomState.Available)

            .State(RoomState.CheckedIn)
                .OnEntry(() =>
                {
                    _notifications.Send($"Welcome to room {RoomNumber}");
                    RecordCheckIn();
                })
                .On(RoomEvent.CheckOut)
                    .When(() => IsMinimumStayComplete())
                    .TransitionTo(RoomState.Occupied)

            .State(RoomState.Occupied)
                .On(RoomEvent.CheckOut)
                    .Execute(() =>
                    {
                        ProcessPayment();
                        DeactivateKeyCard();
                    })
                    .TransitionTo(RoomState.CheckedOut)
                .On(RoomEvent.ReportIssue)
                    .Execute(() => CreateMaintenanceTicket())
                    .TransitionTo(RoomState.Maintenance)

            .State(RoomState.CheckedOut)
                .OnEntry(() =>
                {
                    RecordCheckOut();
                    _notifications.Send("Thank you for staying!");
                })
                .On(RoomEvent.StartCleaning).TransitionTo(RoomState.Cleaning)

            .State(RoomState.Cleaning)
                .OnEntry(() => AssignHousekeeping())
                .On(RoomEvent.FinishCleaning)
                    .Execute(() => InspectRoom())
                    .TransitionTo(RoomState.Available)
                .On(RoomEvent.ReportIssue).TransitionTo(RoomState.Maintenance)

            .State(RoomState.Maintenance)
                .OnEntry(() =>
                {
                    BlockCalendar();
                    NotifyMaintenance();
                })
                .On(RoomEvent.CompleteMaintenance)
                    .Execute(() => ClearMaintenanceTickets())
                    .TransitionTo(RoomState.Cleaning)

            .Build();
    }

    private bool IsCheckInTime() =>
        CurrentReservation != null &&
        DateTime.Now >= CurrentReservation.CheckInTime.AddHours(-1);

    private bool IsNoShowTime() =>
        CurrentReservation != null &&
        DateTime.Now > CurrentReservation.CheckInTime.AddHours(6);

    private bool IsMinimumStayComplete() =>
        CurrentReservation != null &&
        DateTime.Now >= CurrentReservation.CheckInTime.AddHours(4);
}
```

### Why This Pattern

- **Lifecycle enforcement**: Room must go through proper states
- **Time-based guards**: Check-in only at appropriate times
- **Automatic transitions**: No-show handling, cleaning queues

---

## Key Takeaways

1. **Model real-world states**: Use business terminology for state names
2. **Guard complex transitions**: Use predicates for conditional logic
3. **Entry/Exit for side effects**: Notifications, logging, cleanup
4. **Query permitted triggers**: Drive UI based on available actions
5. **Test edge cases**: Verify guards and unexpected events

---

## See Also

- [Overview](index.md)
- [Comprehensive Guide](guide.md)
- [API Reference](api-reference.md)
