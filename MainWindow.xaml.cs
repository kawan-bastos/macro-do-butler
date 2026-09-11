using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MacroDoButler
{
    public partial class MainWindow : Window
    {
        private readonly PresetService presetService;
        private readonly InputService inputService;
        private Storyboard backgroundAnimation;
        private volatile bool enabled;
        private volatile bool secondaryActive;

        public MainWindow()
        {
            InitializeComponent();
            presetService = new PresetService(AppDomain.CurrentDomain.BaseDirectory);
            inputService = new InputService(GetMovementSettings);
            inputService.ToggleRequested += delegate { Dispatcher.BeginInvoke(new Action(Toggle)); };
            inputService.WeaponToggleRequested += delegate { Dispatcher.BeginInvoke(new Action(ToggleWeapon)); };
            inputService.ExitRequested += delegate { Dispatcher.BeginInvoke(new Action(Close)); };

            RefreshPresets(null);
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
                presetService.Save(new Preset
                {
                    Name = name,
                    PrimaryVertical = (int)PrimaryVerticalSlider.Value,
                    PrimaryHorizontal = (int)PrimaryHorizontalSlider.Value,
                    SecondaryVertical = (int)SecondaryVerticalSlider.Value,
                    SecondaryHorizontal = (int)SecondaryHorizontalSlider.Value,
                    IntervalMilliseconds = (int)IntervalSlider.Value,
                    RequireRightButton = RequireRight.IsChecked == true
                });
                RefreshPresets(name);
                ShowFeedback("Preset salvo com sucesso.", true);
            }
            catch (Exception exception) { ShowFeedback("Erro ao salvar: " + exception.Message, false); }
        }

        private void DeletePreset_Click(object sender, RoutedEventArgs e)
        {
            string name = PresetList.SelectedItem == null ? PresetName.Text.Trim() : PresetList.SelectedItem.ToString();
            if (name.Length == 0) { ShowFeedback("Selecione um preset para excluir.", false); return; }
            try { presetService.Delete(name); RefreshPresets(null); PresetName.Text = ""; ShowFeedback("Preset excluído.", true); }
            catch (Exception exception) { ShowFeedback("Erro ao excluir: " + exception.Message, false); }
        }

        private void PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PresetList.SelectedItem == null || presetService == null) return;
            string selected = PresetList.SelectedItem.ToString();
            foreach (Preset preset in presetService.LoadAll())
            {
                if (!preset.Name.Equals(selected, StringComparison.OrdinalIgnoreCase)) continue;
                PrimaryVerticalSlider.Value = Clamp(preset.PrimaryVertical, 0, 10000);
                PrimaryHorizontalSlider.Value = Clamp(preset.PrimaryHorizontal, -5000, 5000);
                SecondaryVerticalSlider.Value = Clamp(preset.SecondaryVertical, 0, 10000);
                SecondaryHorizontalSlider.Value = Clamp(preset.SecondaryHorizontal, -5000, 5000);
                IntervalSlider.Value = Clamp(preset.IntervalMilliseconds, 2, 50);
                RequireRight.IsChecked = preset.RequireRightButton;
                PresetName.Text = preset.Name;
                ShowFeedback("Preset carregado.", true);
                break;
            }
        }

        private void RefreshPresets(string selected)
        {
            PresetList.Items.Clear();
            foreach (Preset preset in presetService.LoadAll()) PresetList.Items.Add(preset.Name);
            if (selected == null) { PresetList.SelectedIndex = -1; return; }
            for (int i = 0; i < PresetList.Items.Count; i++)
                if (PresetList.Items[i].ToString().Equals(selected, StringComparison.OrdinalIgnoreCase)) { PresetList.SelectedIndex = i; break; }
            PresetName.Text = selected;
        }

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
            FeedbackText.Text = message;
            FeedbackText.Foreground = BrushOf(success ? "#7EE2A8" : "#FF7B8A");
            FeedbackText.Opacity = 1;
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5)) { BeginTime = TimeSpan.FromSeconds(2.2) };
            FeedbackText.BeginAnimation(OpacityProperty, fade);
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
