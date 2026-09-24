using PermaLocke.GameLink;
using Xunit;

namespace PermaLocke.GameLink.Tests;

/// <summary>
/// Lo que la aplicación debe a la run cuando el jugador recarga la partida.
/// </summary>
/// <remarks>
/// Las balls se quitan de la mochila VIVA. Quien recarga antes de guardar las recupera él solo, y la aplicación,
/// al ver la mochila llena en una ruta gastada, se las volvía a quitar y a SUMAR a lo que ya debía. El 2026-09-21
/// eso dejó una deuda de 20 Poké Ball y 20 Super Ball con un jugador que nunca tuvo más de diez de cada, y la
/// partida guardada las tenía todas.
/// </remarks>
public sealed class WithheldReloadTests
{
    private static readonly DateTimeOffset Guardado = new(2026, 9, 21, 2, 50, 44, TimeSpan.Zero);
    private static readonly DateTimeOffset Retirada = new(2026, 9, 21, 2, 50, 49, TimeSpan.Zero);

    /// <summary>El caso medido: se guardó a las 02:50:44 y se retiró a las 02:50:49, con las diez en la partida.</summary>
    [Fact]
    public void A_reload_before_saving_undoes_the_debt()
    {
        Assert.True(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: Guardado, carried: 10, inTheSave: 10));
    }

    /// <summary>Si se guardó DESPUÉS de retirarlas, la partida ya no las tiene: se deben de verdad.</summary>
    [Fact]
    public void A_save_after_the_withholding_leaves_a_real_debt()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: Retirada.AddSeconds(30), carried: 10, inTheSave: 0));
    }

    /// <summary>
    /// La mochila vacía justo después de retirar, con balls que se recogieron después del último guardado: cero y cero
    /// «coinciden» y no demuestran ninguna recarga. Así se perdieron diez Poké Balls en la carpeta de prueba el
    /// 2026-09-21: se retiraron en una ruta gastada y, al llegar a la siguiente, la deuda se perdonó en vez de devolverse.
    /// </summary>
    [Fact]
    public void An_empty_bag_matching_an_empty_save_is_given_back_not_forgiven()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: Guardado, carried: 0, inTheSave: 0));
    }

    /// <summary>
    /// El que hace falta comprobar y el que impide el arreglo fácil: sin recargar, el jugador encuentra más balls.
    /// La partida también es más vieja que la deuda, así que mirar solo las fechas le borraría el primer lote.
    /// </summary>
    [Fact]
    public void Finding_more_balls_in_the_same_session_is_not_a_reload()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: Guardado, carried: 5, inTheSave: 10));
    }

    [Fact]
    public void Nothing_owed_is_nothing_to_undo()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 0, debtWrittenAt: Retirada, saveWrittenAt: Guardado, carried: 10, inTheSave: 10));
    }

    /// <summary>Sin partida legible no se decide nada, que es como se comportaba antes.</summary>
    [Fact]
    public void An_unreadable_save_answers_no()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: null, carried: 10, inTheSave: null));

        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: Retirada, saveWrittenAt: Guardado, carried: 10, inTheSave: null));
    }

    /// <summary>Un registro sin fecha —los de antes de esta línea— tampoco decide.</summary>
    [Fact]
    public void A_ledger_with_no_stamp_answers_no()
    {
        Assert.False(BagService.DebtWasUndoneByAReload(
            owed: 10, debtWrittenAt: null, saveWrittenAt: Guardado, carried: 10, inTheSave: 10));
    }

    /// <summary>El registro apunta cuándo, y solo se lo dice a su propia run.</summary>
    [Fact]
    public void The_ledger_stamps_when_it_wrote_and_only_for_its_own_run()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;

        try
        {
            var path = Path.Combine(folder, "objetos-retirados.txt");
            var ledger = new WithheldLedger(path, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);
            var run = Guid.NewGuid();

            Assert.Null(ledger.WrittenAt(run));

            var before = DateTimeOffset.UtcNow.AddSeconds(-1);
            ledger.Remember(run, 4, 10);

            var stamp = ledger.WrittenAt(run);

            Assert.NotNull(stamp);
            Assert.InRange(stamp.Value, before, DateTimeOffset.UtcNow.AddSeconds(1));
            Assert.Equal(10, ledger.Owed(run, 4));

            Assert.Null(ledger.WrittenAt(Guid.NewGuid()));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
