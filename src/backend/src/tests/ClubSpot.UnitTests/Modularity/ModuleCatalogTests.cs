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
        new BookingsModule(),
        new PadelModule(),
        new FootballModule()
    ]);

    [Fact]
    public void El_catalogo_del_producto_es_valido()
    {
        // Construir el catálogo valida el grafo: si alguien declara una dependencia
        // inexistente o introduce un ciclo, esto falla acá y no en producción.
        var catalog = Catalog();

        Assert.Equal(6, catalog.All.Count);
    }

    [Fact]
    public void Core_es_el_unico_modulo_que_no_se_puede_apagar()
    {
        var core = Catalog().CoreModules;

        Assert.Equal([ModuleId.Core], core);
    }

    [Fact]
    public void Contratar_padel_arrastra_todo_lo_que_necesita()
    {
        var enabled = Catalog().Resolve([ModuleId.Padel]);

        Assert.Equal(
            [ModuleId.Core, ModuleId.Finance, ModuleId.Bookings, ModuleId.Padel],
            enabled.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());

        Assert.DoesNotContain(ModuleId.Members, enabled);
        Assert.DoesNotContain(ModuleId.Football, enabled);
    }

    [Fact]
    public void Un_club_sin_nada_contratado_igual_tiene_el_core()
    {
        var enabled = Catalog().Resolve([]);

        Assert.Equal([ModuleId.Core], enabled);
    }

    [Fact]
    public void Members_no_se_puede_contratar_sin_finance()
    {
        // La cuota es la razón de ser del padrón societario: sin cuenta corriente no hay deuda,
        // sin deuda no hay habilitación, y la condición de socio no decide nada.
        var enabled = Catalog().Resolve([ModuleId.Members]);

        Assert.Contains(ModuleId.Finance, enabled);
    }

    [Fact]
    public void Apagar_bookings_se_rechaza_si_hay_deportes_contratados()
    {
        var catalog = Catalog();
        var enabled = catalog.Resolve([ModuleId.Padel, ModuleId.Football]);

        var dependents = catalog.DependentsOf(ModuleId.Bookings, enabled);

        Assert.Equal(
            [ModuleId.Padel, ModuleId.Football],
            dependents.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());
    }

    [Fact]
    public void Un_modulo_con_dependencia_inexistente_no_deja_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CoreModule(), new BrokenModule()]));

        Assert.Contains("no está declarado", ex.Message);
    }

    [Fact]
    public void Un_ciclo_entre_modulos_no_deja_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CyclicModuleA(), new CyclicModuleB()]));

        Assert.Contains("circular", ex.Message);
    }

    [Fact]
    public void Dos_modulos_con_el_mismo_id_no_dejan_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new CoreModule(), new CoreModule()]));

        Assert.Contains("más de una vez", ex.Message);
    }

    private sealed class BrokenModule : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("roto");
        public override string DisplayName => "Roto";
        public override string Description => "Depende de algo que no existe.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("fantasma")];
    }

    private sealed class CyclicModuleA : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("ciclo-a");
        public override string DisplayName => "A";
        public override string Description => "A depende de B.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("ciclo-b")];
    }

    private sealed class CyclicModuleB : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("ciclo-b");
        public override string DisplayName => "B";
        public override string Description => "B depende de A.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("ciclo-a")];
    }
}
