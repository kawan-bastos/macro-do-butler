using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;

internal sealed class UpdateInstaller
{
    internal void Prepare(string downloadedFile,string currentExecutable,bool restart,int processId,bool keepPrevious,int previousVersionsToKeep)
    {
        if(!File.Exists(downloadedFile)||!File.Exists(currentExecutable))throw new UpdateException("O executável atual ou a atualização não foi encontrado.");
        string helper=Path.Combine(Path.GetTempPath(),"MacroDoButler_Installer_"+Guid.NewGuid().ToString("N")+".ps1");
        string log=Path.Combine(Path.GetTempPath(),"MacroDoButler_Installer_"+Guid.NewGuid().ToString("N")+".log");
        File.WriteAllText(helper,BuildScript(downloadedFile,currentExecutable,restart,log,processId,keepPrevious,previousVersionsToKeep),new UTF8Encoding(false));
        try{bool needsElevation=NeedsElevation(Path.GetDirectoryName(currentExecutable));var start=new ProcessStartInfo{FileName="powershell.exe",Arguments="-NoProfile -ExecutionPolicy Bypass -File \""+helper+"\"",WorkingDirectory=Path.GetDirectoryName(currentExecutable),UseShellExecute=true,Verb=IsAdministrator()||!needsElevation?"open":"runas",WindowStyle=ProcessWindowStyle.Hidden};if(Process.Start(start)==null)throw new UpdateException("Não foi possível iniciar o instalador auxiliar.");}
        catch(Exception ex){try{File.Delete(helper);}catch{}if(ex is UpdateException)throw;throw new UpdateException("A instalação precisa de permissão para substituir o executável. A versão atual foi mantida. "+ex.Message,ex);}
    }
    private static bool IsAdministrator(){try{return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}catch{return false;}}
    private static bool NeedsElevation(string directory){string probe=Path.Combine(directory,".MacroDoButlerWriteTest_"+Guid.NewGuid().ToString("N")+".tmp");try{using(File.Create(probe)){}File.Delete(probe);return false;}catch(UnauthorizedAccessException){try{if(File.Exists(probe))File.Delete(probe);}catch{}return true;}catch(IOException){try{if(File.Exists(probe))File.Delete(probe);}catch{}return true;}}
    private static string Ps(string value){return "'"+value.Replace("'","''")+"'";}
    private static string BuildScript(string source,string target,bool restart,string log,int processId,bool keepPrevious,int previousVersionsToKeep)
    {
        string previous=target+".old";var s=new StringBuilder();s.AppendLine("$ErrorActionPreference = 'Stop'");s.AppendLine("$source = "+Ps(source));s.AppendLine("$target = "+Ps(target));s.AppendLine("$previous = "+Ps(previous));s.AppendLine("$log = "+Ps(log));s.AppendLine("$processId = "+processId);s.AppendLine("try {");
        s.AppendLine("  for ($i=0; $i -lt 120; $i++) { if (-not (Get-Process -Id $processId -ErrorAction SilentlyContinue)) { break }; Start-Sleep -Milliseconds 500 }");s.AppendLine("  if (Get-Process -Id $processId -ErrorAction SilentlyContinue) { throw 'Application did not exit.' }");
        s.AppendLine("  $versionDirectory = Join-Path (Split-Path -Parent $target) 'versions'; if (-not (Test-Path -LiteralPath $versionDirectory)) { New-Item -ItemType Directory -Path $versionDirectory | Out-Null }");
        s.AppendLine("  if (Test-Path -LiteralPath $previous) { $staleArchive = Join-Path $versionDirectory ('Macro do Butler_previous_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '.exe'); Move-Item -LiteralPath $previous -Destination $staleArchive -Force }");
        s.AppendLine("  $archive = Join-Path $versionDirectory ('Macro do Butler_' + [DateTime]::UtcNow.ToString('yyyyMMdd_HHmmss') + '.exe')");s.AppendLine("  Move-Item -LiteralPath $target -Destination $previous -Force");s.AppendLine("  try { Copy-Item -LiteralPath $source -Destination $target -Force; if ((Get-Item -LiteralPath $target).Length -lt 524288) { throw 'Downloaded executable is incomplete.' }");
        if(restart)s.AppendLine("    $new = Start-Process -FilePath $target -PassThru; Start-Sleep -Seconds 3; if ($new.HasExited -and $new.ExitCode -ne 0) { throw 'Updated application failed to start.' }");
        s.AppendLine("    Remove-Item -LiteralPath $source -Force");if(keepPrevious)s.AppendLine("    Move-Item -LiteralPath $previous -Destination $archive -Force; $archives = Get-ChildItem -LiteralPath $versionDirectory -Filter 'Macro do Butler_*.exe' -File | Sort-Object LastWriteTimeUtc -Descending | Select-Object -Skip "+Math.Max(1,previousVersionsToKeep)+"; foreach ($oldArchive in $archives) { Remove-Item -LiteralPath $oldArchive.FullName -Force }");else s.AppendLine("    Remove-Item -LiteralPath $previous -Force");
        s.AppendLine("  } catch { if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }; if (Test-Path -LiteralPath $previous) { Move-Item -LiteralPath $previous -Destination $target -Force }; if (Test-Path -LiteralPath $target) { Start-Process -FilePath $target }; throw }");
        s.AppendLine("} catch { [IO.File]::AppendAllText($log, [DateTime]::UtcNow.ToString('o') + ' ' + $_.Exception.Message + [Environment]::NewLine); exit 1 }");s.AppendLine("Remove-Item -LiteralPath $PSCommandPath -Force");return s.ToString();
    }
}
