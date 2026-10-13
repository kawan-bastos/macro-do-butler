using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.IO;
using Microsoft.Win32;
using System.Linq;

namespace MacroDoButler
{
    public partial class MainWindow : Window
    {
        private readonly PresetService presetService;
        private readonly InputService inputService;
        private readonly PresetBackupService backupService;
        private readonly PresetTransferService transferService;
        private readonly PresetOrganizationService organizationService = new PresetOrganizationService();
        private Preset selectedPreset;
        private bool updatingSelectors;
        private readonly string[] selectorEmptyText = { "Nenhum jogo cadastrado", "Nenhum preset neste jogo" };
        private Storyboard backgroundAnimation;
        private volatile bool enabled;
        private volatile bool secondaryActive;

        public MainWindow()
        {
            InitializeComponent();
            presetService = new PresetService(AppDomain.CurrentDomain.BaseDirectory);
            backupService = new PresetBackupService(presetService.FilePath);
            transferService = new PresetTransferService(backupService);
            inputService = new InputService(GetMovementSettings);
            inputService.ToggleRequested += delegate { Dispatcher.BeginInvoke(new Action(Toggle)); };
            inputService.WeaponToggleRequested += delegate { Dispatcher.BeginInvoke(new Action(ToggleWeapon)); };
            inputService.ExitRequested += delegate { Dispatcher.BeginInvoke(new Action(Close)); };

            IList<Preset> initialPresets = presetService.LoadAll();
            RefreshOrganization(initialPresets.FirstOrDefault());
            if (initialPresets.Count == 0) { PrimaryVerticalSlider.Value=350; PrimaryHorizontalSlider.Value=0; SecondaryVerticalSlider.Value=0; SecondaryHorizontalSlider.Value=0; IntervalSlider.Value=7; RequireRight.IsChecked=true; }
            UpdateStatus();
            UpdateWeaponStatus();
            inputService.Start();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            backgroundAnimation = (Storyboard)FindResource("BackgroundAnimation");
            backgroundAnimation.Begin(this, true);
        }

        private MovementSettings GetMovementSettings()
        {
            if (!Dispatcher.CheckAccess()) return Dispatcher.Invoke(new Func<MovementSettings>(GetMovementSettings));
            return new MovementSettings
            {
                Enabled = enabled,
                RequireRightButton = RequireRight.IsChecked == true,
                HorizontalPixelsPerSecond = (int)(secondaryActive ? SecondaryHorizontalSlider.Value : PrimaryHorizontalSlider.Value),
                VerticalPixelsPerSecond = (int)(secondaryActive ? SecondaryVerticalSlider.Value : PrimaryVerticalSlider.Value),
                IntervalMilliseconds = Math.Max(2, (int)IntervalSlider.Value)
            };
        }

        private void SavePreset_Click(object sender, RoutedEventArgs e)
        {
            string name = PresetName.Text.Trim();
            if (name.Length == 0) { ShowFeedback("Digite um nome para o preset.", false); return; }
            try
            {
                string game=GameSelector.SelectedItem as string;
                if(string.IsNullOrWhiteSpace(game)){ShowFeedback("Crie ou selecione um jogo antes de salvar.",false);return;}
                Preset existing=presetService.LoadAll().FirstOrDefault(p=>p.GameName.Equals(game,StringComparison.OrdinalIgnoreCase)&&p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
                bool samePreset=selectedPreset!=null&&PresetService.IdentityEquals(selectedPreset,new Preset{GameName=game,Name=name});
                if(existing!=null&&!samePreset){ShowFeedback("Já existe um preset com esse nome neste jogo. Escolha outro nome ou selecione o existente.",false);return;}
                Preset preset = selectedPreset == null ? new Preset { GameName=game, CharacterName="Preset", WeaponName="Arma não definida" } : ClonePreset(selectedPreset);
                preset.GameName=game; preset.CharacterName="Preset"; preset.WeaponName="Arma não definida"; preset.Name = name;
                preset.PrimaryVertical = (int)PrimaryVerticalSlider.Value;
                preset.PrimaryHorizontal = (int)PrimaryHorizontalSlider.Value;
                preset.SecondaryVertical = (int)SecondaryVerticalSlider.Value;
                preset.SecondaryHorizontal = (int)SecondaryHorizontalSlider.Value;
                preset.IntervalMilliseconds = (int)IntervalSlider.Value;
                preset.RequireRightButton = RequireRight.IsChecked == true;
                backupService.CreateBackup();
                organizationService.Save(presetService, preset);
                selectedPreset = preset;
                RefreshOrganization(preset);
                ShowFeedback("Preset salvo com sucesso.", true);
            }
            catch (Exception exception) { ShowFeedback("Erro ao salvar: " + exception.Message, false); }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            Preset target = selectedPreset;
            if (target == null) { ShowFeedback("Selecione um preset para excluir.", false); return; }
            string fullName = target.GameName + " • " + target.Name;
            if (System.Windows.MessageBox.Show(this, "Deseja excluir o preset '" + fullName + "'?", "Confirmar exclusão", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                backupService.CreateBackup();
                presetService.Delete(target);
                bool deleteEmptyGame=false;
                bool gameHasPresets=presetService.LoadAll().Any(p=>p.GameName.Equals(target.GameName,StringComparison.OrdinalIgnoreCase));
                if(!gameHasPresets&&!PresetService.IsUncategorized(target.GameName))
                    deleteEmptyGame=System.Windows.MessageBox.Show(this,"Este era o último preset do jogo “"+target.GameName+"”. Deseja excluir o jogo também?","Jogo sem presets",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
                if(deleteEmptyGame)presetService.DeleteGame(target.GameName);
                RefreshOrganization(presetService.LoadAll().FirstOrDefault(p=>p.GameName.Equals(target.GameName,StringComparison.OrdinalIgnoreCase)),target.GameName);
                ShowFeedback(deleteEmptyGame?"Preset e jogo excluídos. Backup criado.":"Preset excluído. Backup criado.", true);
            }
            catch (Exception exception) { ShowFeedback("Erro ao excluir: " + exception.Message, false); }
        }

        private void ExportPresets_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = "Exportar presets JSON", Filter = "Arquivo de presets JSON (*.json)|*.json", DefaultExt = ".json", AddExtension = true, FileName = "presets.json" };
            if (dialog.ShowDialog(this) != true) return;
            try { transferService.Export(dialog.FileName, presetService.LoadAll()); ShowTransferFeedback("Presets exportados com sucesso.", true); }
            catch (Exception exception) { ShowTransferFeedback("Erro ao exportar: " + exception.Message, false); }
        }

        private void ImportPresets_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Importar presets", Filter = "JSON ou CFG (*.json;*.cfg)|*.json;*.cfg|Todos os arquivos (*.*)|*.*", CheckFileExists = true };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                transferService.ReadAndValidate(dialog.FileName);
                var choice = new DuplicateChoiceWindow(this);
                if (choice.ShowDialog() != true) return;
                backupService.CreateBackup();
                ImportResult result = transferService.Import(dialog.FileName, presetService, selectedPreset == null ? null : selectedPreset.Name, choice.Mode);
                RefreshOrganization(selectedPreset == null ? presetService.LoadAll().FirstOrDefault() : presetService.LoadAll().FirstOrDefault(p => PresetService.IdentityEquals(p, selectedPreset)));
                ShowTransferFeedback("Importados: " + result.Imported + " · Substituídos: " + result.Replaced + " · Ignorados: " + result.Ignored, true);
            }
            catch (Exception exception) { ShowTransferFeedback("Importação cancelada: " + exception.Message, false); }
        }

        private void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try { string backup = backupService.CreateBackup(); ShowTransferFeedback(backup == null ? "O JSON ainda não foi criado." : "Backup criado: " + backup, true); }
            catch (Exception exception) { ShowTransferFeedback("Erro ao criar backup: " + exception.Message, false); }
        }

        private void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            IList<PresetBackupInfo> backups;
            try { backups = backupService.ListBackups(); }
            catch (Exception exception) { ShowTransferFeedback("Erro ao listar backups: " + exception.Message, false); return; }
            if (backups.Count == 0) { ShowTransferFeedback("Nenhum backup foi encontrado.", false); return; }
            var chooser = new BackupChooserWindow(backups, this);
            if (chooser.ShowDialog() != true || chooser.SelectedBackup == null) return;
            if (System.Windows.MessageBox.Show(this, "Restaurar o backup de " + chooser.SelectedBackup.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss") + "? Um backup do estado atual será criado antes.", "Confirmar restauração", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { backupService.Restore(chooser.SelectedBackup.FilePath, presetService); RefreshOrganization(presetService.LoadAll().FirstOrDefault()); ShowTransferFeedback("Backup " + Path.GetFileName(chooser.SelectedBackup.FilePath) + " restaurado com sucesso.", true); }
            catch (Exception exception) { ShowTransferFeedback("Erro ao restaurar backup: " + exception.Message, false); }
        }

        private void RefreshOrganization(Preset selected, string selectedGame = null)
        {
            updatingSelectors = true;
            IList<string> knownGames=presetService.LoadGames();
            string game = selected == null ? selectedGame : selected.GameName;
            GameSelector.ItemsSource = knownGames;
            GameSelector.ToolTip = GameSelector.Items.Count == 0 ? selectorEmptyText[0] : "Jogo ativo";
            GameEmptyText.Visibility = GameSelector.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            GameSelector.SelectedItem = game;
            if(GameSelector.SelectedItem==null&&knownGames.Count>0){GameSelector.SelectedIndex=0;game=GameSelector.SelectedItem as string;}
            RefreshPresetsForGame(game, selected == null ? null : selected.Name);
            Preset current=selected ?? selectedPreset;
            updatingSelectors = false;
            selectedPreset = current;
            if(current!=null) LoadPreset(current); else PresetName.Text="";
        }
        private void RefreshPresetsForGame(string game,string selectedName)
        {
            IList<Preset> presets=presetService.LoadAll().Where(p=>game!=null&&p.GameName.Equals(game,StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Name).ToList();
            PresetSelector.ItemsSource=presets.Select(p=>p.Name).ToList();
            PresetSelector.ToolTip=presets.Count==0?selectorEmptyText[1]:"Preset";
            PresetEmptyText.Visibility=presets.Count==0?Visibility.Visible:Visibility.Collapsed;
            PresetSelector.SelectedItem=selectedName;
            if(selectedName==null&&presets.Count>0)PresetSelector.SelectedIndex=0;
            if(PresetSelector.SelectedItem!=null){Preset p=presets.FirstOrDefault(x=>x.Name.Equals(PresetSelector.SelectedItem.ToString(),StringComparison.OrdinalIgnoreCase));if(p!=null)selectedPreset=p;}
            else selectedPreset=null;
        }
        private void LoadPreset(Preset preset)
        {
            selectedPreset = preset;
            updatingSelectors = true;
            GameSelector.SelectedItem = preset.GameName;
            RefreshPresetsForGame(preset.GameName,preset.Name);
            updatingSelectors = false;
            PrimaryVerticalSlider.Value = Clamp(preset.PrimaryVertical, 0, 10000); PrimaryHorizontalSlider.Value = Clamp(preset.PrimaryHorizontal, -5000, 5000);
            SecondaryVerticalSlider.Value = Clamp(preset.SecondaryVertical, 0, 10000); SecondaryHorizontalSlider.Value = Clamp(preset.SecondaryHorizontal, -5000, 5000);
            IntervalSlider.Value = Clamp(preset.IntervalMilliseconds, 2, 50); RequireRight.IsChecked = preset.RequireRightButton;
            PresetName.Text = preset.Name; secondaryActive = preset.WeaponSlot == WeaponSlot.Secondary; UpdateWeaponStatus(); ShowFeedback("Preset carregado no macro.", true);
        }
        private void Organization_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (updatingSelectors || presetService == null) return;
            string game=GameSelector.SelectedItem as string;
            if (sender==GameSelector) { RefreshPresetsForGame(game,null); if(selectedPreset!=null)LoadPreset(selectedPreset);else PresetName.Text=""; }
        }

        private void PresetSelector_SelectionChanged(object sender,SelectionChangedEventArgs e)
        {
            if(updatingSelectors||PresetSelector.SelectedItem==null)return;
            string game=GameSelector.SelectedItem as string,name=PresetSelector.SelectedItem.ToString();
            Preset preset=presetService.LoadAll().FirstOrDefault(p=>game!=null&&p.GameName.Equals(game,StringComparison.OrdinalIgnoreCase)&&p.Name.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(preset!=null)LoadPreset(preset);
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            var dialog=new GamePresetOrganizerWindow(this,presetService.LoadGames(),presetService.LoadAll(),GameSelector.SelectedItem as string);
            if(dialog.ShowDialog()!=true)return;
            try
            {
                backupService.CreateBackup();
                if(dialog.DeleteGameRequested)
                {
                    presetService.DeleteGame(dialog.GameName);
                    Preset remaining=presetService.LoadAll().FirstOrDefault();
                    RefreshOrganization(remaining);
                    ShowFeedback("Jogo excluído. Backup criado.",true);
                    return;
                }
                if(dialog.RenameGameRequested)presetService.RenameGame(dialog.RenameGameFrom,dialog.RenameGameTo);
                presetService.SaveGame(dialog.GameName);
                foreach(string discoveredGame in dialog.GamesToAdd)presetService.SaveGame(discoveredGame);
                int moved=0, skipped=0, transferred=0;
                foreach(Preset item in dialog.SelectedPresets)
                {
                    Preset duplicate=presetService.LoadAll().FirstOrDefault(p=>p.GameName.Equals(dialog.GameName,StringComparison.OrdinalIgnoreCase)&&p.Name.Equals(item.Name,StringComparison.OrdinalIgnoreCase));
                    if(duplicate!=null&&!PresetService.IdentityEquals(duplicate,item)){skipped++;continue;}
                    presetService.DeleteOne(item);
                    item.GameName=dialog.GameName; item.CharacterName="Preset"; item.WeaponName="Arma não definida";
                    organizationService.Save(presetService,item); moved++;
                }
                if(dialog.TransferPresetsRequested)transferred=presetService.TransferPresets(dialog.TransferSourceGame,dialog.TransferTargetGame);
                Preset selected=presetService.LoadAll().FirstOrDefault(p=>p.GameName.Equals(dialog.GameName,StringComparison.OrdinalIgnoreCase));
                if(selected==null&&selectedPreset!=null&&selectedPreset.GameName.Equals(dialog.GameName,StringComparison.OrdinalIgnoreCase))selected=selectedPreset;
                RefreshOrganization(selected,dialog.GameName);
                ShowFeedback((dialog.RenameGameRequested?"Jogo renomeado e salvo. ":"Jogo salvo. ")+"Presets adicionados: "+moved+(transferred>0?" · Transferidos: "+transferred:string.Empty)+(skipped>0?" · Ignorados por nome repetido: "+skipped:string.Empty),true);
            }
            catch(Exception ex){ShowFeedback("Erro: "+ex.Message,false);}
        }
        private void InnerScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Deixe o ComboBox aberto receber o wheel: mover o ScrollViewer principal
            // enquanto o Popup está aberto faz a lista parecer que se soltou do campo.
            if (GameSelector.IsDropDownOpen || PresetSelector.IsDropDownOpen) return;
            ScrollViewer pane=sender as ScrollViewer;
            if(pane!=null){pane.ScrollToVerticalOffset(pane.VerticalOffset-e.Delta);e.Handled=true;}
        }
        private static Preset ClonePreset(Preset p){return new Preset{GameName=p.GameName,CharacterName=p.CharacterName,WeaponName=p.WeaponName,WeaponSlot=p.WeaponSlot,Name=p.Name,PrimaryVertical=p.PrimaryVertical,PrimaryHorizontal=p.PrimaryHorizontal,SecondaryVertical=p.SecondaryVertical,SecondaryHorizontal=p.SecondaryHorizontal,IntervalMilliseconds=p.IntervalMilliseconds,RequireRightButton=p.RequireRightButton,CreatedAt=p.CreatedAt,UpdatedAt=p.UpdatedAt};}

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Slider slider = sender as Slider;
            if (slider == null) return;
            TextBox target = null;
            switch (slider.Tag as string)
            {
                case "PrimaryVertical": target = PrimaryVerticalText; break;
                case "PrimaryHorizontal": target = PrimaryHorizontalText; break;
                case "SecondaryVertical": target = SecondaryVerticalText; break;
                case "SecondaryHorizontal": target = SecondaryHorizontalText; break;
                case "Interval": target = IntervalText; break;
            }
            if (target != null) target.Text = ((int)slider.Value).ToString();
        }

        private void NumericText_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            int value;
            if (textBox == null || !int.TryParse(textBox.Text, out value)) { SyncAllText(); return; }
            if (textBox == PrimaryVerticalText) PrimaryVerticalSlider.Value = Clamp(value, 0, 10000);
            else if (textBox == PrimaryHorizontalText) PrimaryHorizontalSlider.Value = Clamp(value, -5000, 5000);
            else if (textBox == SecondaryVerticalText) SecondaryVerticalSlider.Value = Clamp(value, 0, 10000);
            else if (textBox == SecondaryHorizontalText) SecondaryHorizontalSlider.Value = Clamp(value, -5000, 5000);
            else if (textBox == IntervalText) IntervalSlider.Value = Clamp(value, 2, 50);
            SyncAllText();
        }

        private void NumericText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsInitialized) return;
            TextBox textBox = sender as TextBox;
            int value;
            if (textBox == null || !int.TryParse(textBox.Text, out value)) return;
            if (textBox == PrimaryVerticalText) PrimaryVerticalSlider.Value = Clamp(value, 0, 10000);
            else if (textBox == PrimaryHorizontalText) PrimaryHorizontalSlider.Value = Clamp(value, -5000, 5000);
            else if (textBox == SecondaryVerticalText) SecondaryVerticalSlider.Value = Clamp(value, 0, 10000);
            else if (textBox == SecondaryHorizontalText) SecondaryHorizontalSlider.Value = Clamp(value, -5000, 5000);
            else if (textBox == IntervalText) IntervalSlider.Value = Clamp(value, 2, 50);
        }

        private void SyncAllText()
        {
            PrimaryVerticalText.Text = ((int)PrimaryVerticalSlider.Value).ToString();
            PrimaryHorizontalText.Text = ((int)PrimaryHorizontalSlider.Value).ToString();
            SecondaryVerticalText.Text = ((int)SecondaryVerticalSlider.Value).ToString();
            SecondaryHorizontalText.Text = ((int)SecondaryHorizontalSlider.Value).ToString();
            IntervalText.Text = ((int)IntervalSlider.Value).ToString();
        }

        private void Toggle_Click(object sender, RoutedEventArgs e) { Toggle(); }
        private void ToggleWeapon_Click(object sender, RoutedEventArgs e) { ToggleWeapon(); }
        private void Toggle() { enabled = !enabled; UpdateStatus(); }
        private void ToggleWeapon() { secondaryActive = !secondaryActive; UpdateWeaponStatus(); }

        private void UpdateStatus()
        {
            StatusText.Text = enabled ? "LIGADO" : "DESLIGADO";
            StatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(enabled ? "#7EE2A8" : "#9199A6"));
            StatusPill.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(enabled ? "#263C302F" : "#342029"));
            ToggleButton.Content = enabled ? "DESLIGAR · F1" : "LIGAR · F1";
        }

        private void UpdateWeaponStatus()
        {
            WeaponStatusText.Text = secondaryActive ? "Arma ativa: Secundária" : "Arma ativa: Primária";
            WeaponButton.Content = secondaryActive ? "USAR PRIMÁRIA · F2" : "USAR SECUNDÁRIA · F2";
            PrimaryCard.Opacity = secondaryActive ? 0.70 : 1;
            SecondaryCard.Opacity = secondaryActive ? 1 : 0.70;
            PrimaryActiveBadge.Background = BrushOf(secondaryActive ? "#252B34" : "#5AE52B3D");
            SecondaryActiveBadge.Background = BrushOf(secondaryActive ? "#5AE52B3D" : "#252B34");
            ((TextBlock)PrimaryActiveBadge.Child).Text = secondaryActive ? "INATIVA" : "ATIVA";
            ((TextBlock)SecondaryActiveBadge.Child).Text = secondaryActive ? "ATIVA" : "INATIVA";
        }

        private void ShowFeedback(string message, bool success)
        {
            ShowFeedbackIn(FeedbackText, message, success);
        }

        private void ShowTransferFeedback(string message, bool success) { ShowFeedbackIn(TransferFeedbackText, message, success); }

        private void ShowFeedbackIn(TextBlock target, string message, bool success)
        {
            target.Text = message;
            target.Foreground = BrushOf(success ? "#7EE2A8" : "#FF7B8A");
            target.Opacity = 1;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(2.2) };
            target.BeginAnimation(OpacityProperty, fade);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
        private void Minimize_Click(object sender, RoutedEventArgs e) { WindowState = WindowState.Minimized; }
        private void Close_Click(object sender, RoutedEventArgs e) { Close(); }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (backgroundAnimation == null) return;
            if (WindowState == WindowState.Minimized) backgroundAnimation.Pause(this);
            else backgroundAnimation.Resume(this);
        }
        private void Window_Closed(object sender, EventArgs e) { inputService.Dispose(); }

        private static int Clamp(int value, int minimum, int maximum) { return Math.Max(minimum, Math.Min(maximum, value)); }
        private static SolidColorBrush BrushOf(string color) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
    }
}
