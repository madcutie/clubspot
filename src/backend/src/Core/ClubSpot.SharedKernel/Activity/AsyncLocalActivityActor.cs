namespace ClubSpot.SharedKernel.Activity;

// Current throws when nobody opened a scope, same rule as ITenantContext: a fact recorded with a
// made-up actor is worse than no record at all, so the caller has to say who is acting.
public interface IActivityActor
{
    bool HasActor { get; }

    ActivityActor Current { get; }
}

// HTTP: the middleware opens one scope per request from the token, and the anonymous groups open
// their own. Background: the job or webhook opens it explicitly.
public interface IActivityActorScopeFactory
{
    IDisposable BeginScope(ActivityActor actor);
}

public sealed class AsyncLocalActivityActor : IActivityActor, IActivityActorScopeFactory
{
    // Static on purpose: the scope belongs to the async flow, not to the instance.
    private static readonly AsyncLocal<ActivityActor?> Ambient = new();

    public bool HasActor => Ambient.Value is not null;

    public ActivityActor Current =>
        Ambient.Value ?? throw new MissingActivityActorException("IActivityActor.Current");

    public IDisposable BeginScope(ActivityActor actor)
    {
        var previous = Ambient.Value;
        Ambient.Value = actor;
        return new Scope(previous);
    }

    private sealed class Scope(ActivityActor? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Ambient.Value = previous;
        }
    }
}

public sealed class MissingActivityActorException(string operation)
    : InvalidOperationException(
        $"No activity actor scope is set for operation '{operation}'. " +
        "Whoever records activity must say who is acting: a user, a webhook, a job or the system.");
