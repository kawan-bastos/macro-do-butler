using System;
using System.Threading;
using System.Windows;

namespace MacroDoButler
{
    public partial class App : Application
    {
        private Mutex instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            bool firstInstance;
            instanceMutex = new Mutex(true, "MacroDoButler_UnicaInstancia_6B5E", out firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show("O programa já está aberto.", "Velqora", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
            try { new MainWindow().Show(); }
            catch (Exception exception)
            {
                MessageBox.Show("O Velqora não conseguiu iniciar: " + exception.Message, "Falha ao iniciar", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }
    }
}
