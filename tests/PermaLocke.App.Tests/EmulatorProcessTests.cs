using PermaLocke.App.Services;

namespace PermaLocke.App.Tests;

/// <summary>
/// La decisión de «¿está el emulador abierto?», que es lo que se equivocaba: contar los procesos
/// que llevan su nombre daba por abierto un Azahar ya terminado que seguía en la tabla.
/// </summary>
public sealed class EmulatorProcessTests
{
    [Fact]
    public void No_candidates_is_closed()
    {
        Assert.False(EmulatorProcess.Decide([]));
    }

    [Fact]
    public void A_ghost_in_the_table_is_not_a_running_emulator()
    {
        // La regresión: antes esto era «hay un proceso con ese nombre», o sea true.
        Assert.False(EmulatorProcess.Decide([false]));
    }

    [Fact]
    public void One_live_process_settles_it()
    {
        Assert.True(EmulatorProcess.Decide([true]));
    }

    [Fact]
    public void A_live_one_wins_over_any_number_of_ghosts()
    {
        Assert.True(EmulatorProcess.Decide([false, false, true, false]));
    }

    [Fact]
    public void A_candidate_that_will_not_say_leaves_the_answer_unknown()
    {
        Assert.Null(EmulatorProcess.Decide([null]));
        Assert.Null(EmulatorProcess.Decide([false, null]));
    }

    [Fact]
    public void Unknown_never_beats_a_live_one()
    {
        Assert.True(EmulatorProcess.Decide([null, true]));
    }

    [Fact]
    public void Asking_the_real_machine_does_not_throw()
    {
        // Recorre de verdad la tabla de procesos y libera lo que abre. No se afirma el resultado:
        // depende de si hay un emulador abierto mientras corre la prueba.
        Assert.Null(Record.Exception(() => EmulatorProcess.IsRunning()));
    }
}
