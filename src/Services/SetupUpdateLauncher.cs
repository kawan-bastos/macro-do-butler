using System;
using System.Diagnostics;
using System.IO;
using System.Text;

// Starts the validated Setup only after this process has released its files.
internal sealed class SetupUpdateLauncher
{
    internal void Prepare(string setupPath, int processId)
    {
        if (!File.Exists(setupPath) || !string.Equals(Path.GetExtension(setupPath), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new UpdateException("O instalador baixado não está disponível.");
        string helper = Path.Combine(Path.GetTempPath(), "Velqora_Setup_" + Guid.NewGuid().ToString("N") + ".ps1");
        string log = Path.Combine(Path.GetTempPath(), "Velqora_Setup_" + Guid.NewGuid().ToString("N") + ".log");
        string script = "$ErrorActionPreference = 'Stop'" + Environment.NewLine
            + "$setup = " + Quote(setupPath) + Environment.NewLine
            + "$log = " + Quote(log) + Environment.NewLine
            + "try {" + Environment.NewLine
            + "  for ($i = 0; $i -lt 120; $i++) { if (-not (Get-Process -Id " + processId + " -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }" + Environment.NewLine
            + "  if (Get-Process -Id " + processId + " -ErrorAction SilentlyContinue) { throw 'O aplicativo não foi fechado.' }" + Environment.NewLine
            + "  $result = Start-Process -FilePath $setup -ArgumentList '/UPDATE' -PassThru -Wait" + Environment.NewLine
            + "  if ($result.ExitCode -ne 0) { throw ('O instalador terminou com código ' + $result.ExitCode) }" + Environment.NewLine
            + "  Remove-Item -LiteralPath $setup -Force" + Environment.NewLine
            + "  Remove-Item -LiteralPath $PSCommandPath -Force" + Environment.NewLine
            + "} catch { [IO.File]::AppendAllText($log, [DateTime]::UtcNow.ToString('o') + ' ' + $_.Exception.Message + [Environment]::NewLine); exit 1 }";
        File.WriteAllText(helper, script, new UTF8Encoding(false));
        try
        {
            var start = new ProcessStartInfo("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File \"" + helper + "\"")
            { UseShellExecute = true, WorkingDirectory = Path.GetTempPath(), WindowStyle = ProcessWindowStyle.Hidden };
            if (Process.Start(start) == null) throw new UpdateException("Não foi possível iniciar a atualização.");
        }
        catch (Exception ex)
        {
            try { File.Delete(helper); } catch { }
            throw new UpdateException("Não foi possível abrir o instalador. O aplicativo atual foi mantido. " + ex.Message, ex);
        }
    }

    private static string Quote(string value) { return "'" + value.Replace("'", "''") + "'"; }
}
