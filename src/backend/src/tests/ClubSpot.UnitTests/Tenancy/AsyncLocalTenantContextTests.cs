using ClubSpot.SharedKernel.Tenancy;

namespace ClubSpot.UnitTests.Tenancy;

public class AsyncLocalTenantContextTests
{
    private static readonly TenantId ClubA = TenantId.From(Guid.NewGuid());
    private static readonly TenantId ClubB = TenantId.From(Guid.NewGuid());

    [Fact]
    public void Current_throws_outside_a_scope()
    {
        var context = new AsyncLocalTenantContext();

        Assert.False(context.HasTenant);
        Assert.Throws<MissingTenantException>(() => context.Current);
    }

    [Fact]
    public void Inside_a_scope_returns_the_open_tenant()
    {
        var context = new AsyncLocalTenantContext();

        using (context.BeginScope(ClubA))
        {
            Assert.True(context.HasTenant);
            Assert.Equal(ClubA, context.Current);
        }

        Assert.False(context.HasTenant);
    }

    [Fact]
    public void Closing_a_nested_scope_restores_the_previous_one()
    {
        var context = new AsyncLocalTenantContext();

        using (context.BeginScope(ClubA))
        {
            using (context.BeginScope(ClubB))
                Assert.Equal(ClubB, context.Current);

            Assert.Equal(ClubA, context.Current);
        }
    }

    [Fact]
    public void Disposing_a_scope_twice_does_not_clobber_a_later_scope()
    {
        var context = new AsyncLocalTenantContext();

        var first = context.BeginScope(ClubA);
        first.Dispose();

        using (context.BeginScope(ClubB))
        {
            first.Dispose();
            Assert.Equal(ClubB, context.Current);
        }
    }

    [Fact]
    public async Task The_scope_does_not_leak_across_parallel_async_flows()
    {
        var context = new AsyncLocalTenantContext();

        var flowA = Task.Run(async () =>
        {
            using var scope = context.BeginScope(ClubA);
            await Task.Delay(50);
            return context.Current;
        });

        var flowB = Task.Run(async () =>
        {
            using var scope = context.BeginScope(ClubB);
            await Task.Delay(50);
            return context.Current;
        });

        Assert.Equal(ClubA, await flowA);
        Assert.Equal(ClubB, await flowB);
        Assert.False(context.HasTenant);
    }
}
