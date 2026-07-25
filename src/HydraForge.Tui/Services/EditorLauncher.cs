using System.Diagnostics;

namespace HydraForge.Tui.Services;

public class EditorLauncher
{
    public async Task<string?> EditAsync(string initialContent)
    {
        var tempFile = Path.GetTempFileName() + ".md";
        try
        {
            await File.WriteAllTextAsync(tempFile, initialContent);

            var editor = Environment.GetEnvironmentVariable("EDITOR")
                         ?? Environment.GetEnvironmentVariable("VISUAL")
                         ?? (OperatingSystem.IsWindows() ? "notepad.exe" : "vi");

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = editor,
                    Arguments = tempFile,
                    UseShellExecute = true,
                }
            };

            process.Start();
            await process.WaitForExitAsync();

            return await File.ReadAllTextAsync(tempFile);
        }
        catch
        {
            return null; // Caller falls back to inline prompt
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }
}