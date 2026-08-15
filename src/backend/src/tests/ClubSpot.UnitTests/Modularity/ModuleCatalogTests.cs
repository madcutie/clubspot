using ClubSpot.Modules.Clubes;
using ClubSpot.Modules.Finanzas;
using ClubSpot.Modules.Futbol;
using ClubSpot.Modules.Padel;
using ClubSpot.Modules.Reservas;
using ClubSpot.SharedKernel.Modularity;

namespace ClubSpot.UnitTests.Modularity;

public class ModuleCatalogTests
{
    private static ModuleCatalog Catalogo() => new(
    [
        new NucleoModule(),
        new SociosModule(),
        new FinanzasModule(),
        new ReservasModule(),
        new PadelModule(),
        new FutbolModule()
    ]);

    [Fact]
    public void El_catalogo_del_producto_es_valido()
    {
        // Construir el catálogo valida el grafo: si alguien declara una dependencia
        // inexistente o introduce un ciclo, esto falla acá y no en producción.
        var catalogo = Catalogo();

        Assert.Equal(6, catalogo.All.Count);
    }

    [Fact]
    public void Nucleo_es_el_unico_modulo_que_no_se_puede_apagar()
    {
        var core = Catalogo().CoreModules;

        Assert.Equal([ModuleId.Nucleo], core);
    }

    [Fact]
    public void Contratar_padel_arrastra_todo_lo_que_necesita()
    {
        var habilitados = Catalogo().Resolve([ModuleId.Padel]);

        Assert.Equal(
            [ModuleId.Nucleo, ModuleId.Finanzas, ModuleId.Reservas, ModuleId.Padel],
            habilitados.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());

        Assert.DoesNotContain(ModuleId.Socios, habilitados);
        Assert.DoesNotContain(ModuleId.Futbol, habilitados);
    }

    [Fact]
    public void Un_club_sin_nada_contratado_igual_tiene_el_nucleo()
    {
        var habilitados = Catalogo().Resolve([]);

        Assert.Equal([ModuleId.Nucleo], habilitados);
    }

    [Fact]
    public void Socios_no_se_puede_contratar_sin_finanzas()
    {
        // La cuota es la razón de ser del padrón societario: sin cuenta corriente no hay deuda,
        // sin deuda no hay habilitación, y la condición de socio no decide nada.
        var habilitados = Catalogo().Resolve([ModuleId.Socios]);

        Assert.Contains(ModuleId.Finanzas, habilitados);
    }

    [Fact]
    public void Apagar_reservas_se_rechaza_si_hay_deportes_contratados()
    {
        var catalogo = Catalogo();
        var habilitados = catalogo.Resolve([ModuleId.Padel, ModuleId.Futbol]);

        var dependientes = catalogo.DependentsOf(ModuleId.Reservas, habilitados);

        Assert.Equal(
            [ModuleId.Padel, ModuleId.Futbol],
            dependientes.OrderBy(m => m.Value).ToHashSet(),
            HashSet<ModuleId>.CreateSetComparer());
    }

    [Fact]
    public void Un_modulo_con_dependencia_inexistente_no_deja_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new NucleoModule(), new ModuloRoto()]));

        Assert.Contains("no está declarado", ex.Message);
    }

    [Fact]
    public void Un_ciclo_entre_modulos_no_deja_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new ModuloCiclicoA(), new ModuloCiclicoB()]));

        Assert.Contains("circular", ex.Message);
    }

    [Fact]
    public void Dos_modulos_con_el_mismo_id_no_dejan_arrancar()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ModuleCatalog([new NucleoModule(), new NucleoModule()]));

        Assert.Contains("más de una vez", ex.Message);
    }

    private sealed class ModuloRoto : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("roto");
        public override string DisplayName => "Roto";
        public override string Description => "Depende de algo que no existe.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("fantasma")];
    }

    private sealed class ModuloCiclicoA : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("ciclo-a");
        public override string DisplayName => "A";
        public override string Description => "A depende de B.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("ciclo-b")];
    }

    private sealed class ModuloCiclicoB : ClubModuleBase
    {
        public override ModuleId Id => ModuleId.From("ciclo-b");
        public override string DisplayName => "B";
        public override string Description => "B depende de A.";
        public override IReadOnlyCollection<ModuleId> DependsOn => [ModuleId.From("ciclo-a")];
    }
}
