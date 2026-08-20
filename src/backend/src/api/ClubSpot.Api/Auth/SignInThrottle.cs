using Microsoft.Extensions.Caching.Memory;

namespace ClubSpot.Api.Auth;

// Brute force protection for the only anonymous endpoint that hands out a token. Counts *failures*
// and nothing else: a club whose whole staff signs in at nine in the morning shares one address and
// must not lock itself out, while a script guessing passwords produces nothing but failures.
//
// Two counters, either of which blocks. The one keyed by account stops a single password being
// guessed; the one keyed by caller stops the same guess being sprayed across many accounts, which
// a per-account counter alone never sees.
public sealed class SignInThrottle(IMemoryCache cache)
{
    public const int MaxFailuresPerAccount = 10;
    public const int MaxFailuresPerCaller = 30;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public bool IsBlocked(string email, string caller) =>
        Failures(AccountKey(email)) >= MaxFailuresPerAccount
        || Failures(CallerKey(caller)) >= MaxFailuresPerCaller;

    public void RecordFailure(string email, string caller)
    {
        Increment(AccountKey(email));
        Increment(CallerKey(caller));
    }

    // A password that works clears the account's count: the legitimate owner fumbling a few times
    // should not stay blocked once they get it right.
    public void RecordSuccess(string email) => cache.Remove(AccountKey(email));

    private int Failures(string key) =>
        cache.TryGetValue<Counter>(key, out var counter) ? Volatile.Read(ref counter!.Value) : 0;

    // Read-then-write would lose counts under a burst, which is exactly when the count matters:
    // the entry holds a mutable counter so the increment itself is atomic.
    private void Increment(string key)
    {
        var counter = cache.GetOrCreate(key, entry =>
        {
            // Absolute, not sliding: a sliding window lets a slow drip keep the count alive forever.
            entry.AbsoluteExpirationRelativeToNow = Window;
            return new Counter();
        })!;
        Interlocked.Increment(ref counter.Value);
    }

    private sealed class Counter
    {
        public int Value;
    }

    private static string AccountKey(string email) =>
        $"signin-failures:account:{email.Trim().ToLowerInvariant()}";

    private static string CallerKey(string caller) => $"signin-failures:caller:{caller}";
}
