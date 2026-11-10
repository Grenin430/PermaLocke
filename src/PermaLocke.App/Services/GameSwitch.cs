using System.Diagnostics;
using System.Windows;

namespace PermaLocke.App.Services;

/// <summary>
/// Back to the launcher's menu from inside a game (2026-11-10). One game per process, so going back is opening PermaLocke again: the new
/// one asks which game, this one closes. Not while the emulator is open, the same as closing PermaLocke (§192).
/// </summary>
public static class GameSwitch
{
    public static void BackToMenu(bool gameOpen)
    {
        if (gameOpen)
        {
            Views.PixelDialog.Show("Cambiar de juego", "Cierra el juego antes de volver al menú de juegos.", Views.PixelDialogKind.Warning,
                owner: Application.Current?.MainWindow);
            return;
        }

        if (!Views.PixelDialog.Show("Cambiar de juego", "PermaLocke se cerrará y volverá a abrirse en el menú de juegos. ¿Seguir?",
                ask: true, owner: Application.Current?.MainWindow))
        {
            return;
        }

        // la nueva espera a que esta suelte el candado de «solo una abierta» (AnotherIsRunning espera hasta 15 s)
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false });
        BackgroundMode.Leaving = true;
        Application.Current?.Shutdown();
    }
}
