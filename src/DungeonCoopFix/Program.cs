namespace DungeonCoopFix;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // A second instance must never allocate a second dummy.
        using var singleInstance = new Mutex(true, @"Local\DungeonCoopFix.V1", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("DungeonCoopFix is already open. Use its existing window.",
                "DungeonCoopFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        using var service = new ControllerService(new VirtualControllerBackend(), new XInputControllers());
        Application.ThreadException += (_, e) =>
        {
            service.Disable();
            MessageBox.Show($"The fix was disabled after an error.\n\n{e.Exception.Message}",
                "DungeonCoopFix", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        using var window = new MainForm(service);
        // Finally also runs if the message loop exits without FormClosing.
        try { Application.Run(window); }
        finally { service.Disable(); }
    }
}
