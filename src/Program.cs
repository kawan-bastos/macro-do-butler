using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Macro Do Butler")]
[assembly: AssemblyProduct("Macro Do Butler")]
[assembly: AssemblyDescription("Utilitário configurável de controle de movimento")]

internal static class Program
{
    private static Mutex instanceMutex;

    [STAThread]
    private static void Main()
    {
        bool firstInstance;
        instanceMutex = new Mutex(true, "MacroDoButler_UnicaInstancia_6B5E", out firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("O programa já está aberto perto do relógio.", "Macro Do Butler", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}
