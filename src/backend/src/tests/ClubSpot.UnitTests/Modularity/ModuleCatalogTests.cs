using ClubSpot.Application.Modularity;
using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.UnitTests.Modularity;

public class ModuleCatalogTests
{
    private static ModuleCatalog Catalog() => new(
    [
        new CoreModule(),
        new MembersModule(),
        new FinanceModule(),
        new BookingsModule()
    ]);

    [Fact]
    public void The_product_catalog_is_valid()
    {
        var catalog = Catalog();

        Assert.Equal(4, catalog.All.Count);
    }

    [Fact]
    public void Core_is_the_only_module_that_cannot_be_disabled()
    {
        var core = Catalog().CoreModules;

        Assert.Equal([ModuleId.Core], core);
    }

    [Fact]
    public void Contracting_members_pulls_in_everything_it_needs()
    {
        var enabled = Catalog().Resolve([ModuleId.Members]);

        Assert.Equal(
            [ModuleId.Core, ModuleId.Finance, ModuleId.Members],
            enabled.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());

        Assert.DoesNotContain(ModuleId.Bookings, enabled);
    }

    [Fact]
    public void Contracting_bookings_pulls_in_everything_it_needs()
    {
        var enabled = Catalog().Resolve([ModuleId.Bookings]);

        Assert.Equal(
            [ModuleId.Core, ModuleId.Finance, ModuleId.Bookings],
            enabled.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());

        Assert.DoesNotContain(ModuleId.Members, enabled);
    }

    [Fact]
    public void A_club_with_nothing_contracted_still_has_core()
    {
        var enabled = Catalog().Resolve([]);

        Assert.Equal([ModuleId.Core], enabled);
    }

    [Fact]
    public void Members_cannot_be_contracted_without_finance()
    {
        var enabled = Catalog().Resolve([ModuleId.Members]);

        Assert.Contains(ModuleId.Finance, enabled);
    }

    [Fact]
    public void Disabling_finance_is_rejected_while_its_dependents_are_contracted()
    {
        var catalog = Catalog();
        var enabled = catalog.Resolve([ModuleId.Members, ModuleId.Bookings]);

        var dependents = catalog.DependentsOf(ModuleId.Finance, enabled);

        Assert.Equal(
            [ModuleId.Members, ModuleId.Bookings],
            dependents.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());
    }

    [Fact]
    public void A_missing_dependency_prevents_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CoreModule(), new BrokenModule()]));

        Assert.Contains("not declared", ex.Message);
    }

    [Fact]
    public void A_dependency_cycle_prevents_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CyclicModuleA(), new CyclicModuleB()]));

        Assert.Contains("Circular", ex.Message);
    }

    [Fact]
    public void Two_modules_with_the_same_id_prevent_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CoreModule(), new CoreModule()]));

        Assert.Contains("more than once", ex.Message);
    }

    private sealed class BrokenModule : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("broken");
        public override string DisplayName => "Broken";
        public override string Description => "Depends on something that does not exist.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("ghost")];
    }

    private sealed class CyclicModuleA : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("cycle-a");
        public override string DisplayName => "A";
        public override string Description => "A depends on B.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("cycle-b")];
    }

    private sealed class CyclicModuleB : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("cycle-b");
        public override string DisplayName => "B";
        public override string Description => "B depends on A.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("cycle-a")];
    }
}
