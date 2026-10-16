using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MacroDoButler
{
    public partial class MainWindow
    {
        private PresetEditDraft editDraft;
        private Preset editOriginal;

        private void OpenPresetEditor(Preset preset, string newGame = null)
        {
            bool sameDraft = editDraft != null && (preset == null
                ? editOriginal == null && string.Equals(editDraft.GameName, newGame, StringComparison.OrdinalIgnoreCase)
                : editOriginal != null && PresetService.IdentityEquals(editOriginal, preset));
            if (!sameDraft)
            {
                if (!ConfirmDiscardDraft()) return;
                editOriginal = preset == null ? null : PresetEditingService.Copy(preset);
                var initial = preset ?? new Preset
                {
                    GameName = newGame, Name = string.Empty, CharacterName = "Preset", WeaponName = "Arma não definida",
                    PrimaryVertical = 0, PrimaryHorizontal = 0,
                    SecondaryVertical = 0, SecondaryHorizontal = 0,
                    IntervalMilliseconds = 0, SecondaryIntervalMilliseconds = 0,
                    RequireRightButton = true
                };
                editDraft = new PresetEditDraft(initial, preset == null);
                EditorPage.DataContext = editDraft;
                EditorFeedback.Text = string.Empty;
            }
            EditorPageTitle.Text = editOriginal == null ? "Novo Preset" : "Editar preset";
            EditorPageSubtitle.Text = editOriginal == null ? "Defina os valores do novo perfil" : "Ajuste os valores do perfil selecionado";
            MainPage.Visibility = Visibility.Collapsed;
            SettingsPage.Visibility = Visibility.Collapsed;
            EditorPage.Visibility = Visibility.Visible;
            PresetsNavigationButton.IsChecked = true;
            SettingsNavigationButton.IsChecked = false;
            if (capturingAction != null) CancelCapture();
            EditorName.Focus();
        }

        private bool ConfirmDiscardDraft()
        {
            if (editDraft == null || (!editDraft.IsDirty && !HasEditorErrors(EditorPage))) return true;
            return MessageBox.Show(this, "Descartar as alterações não salvas deste preset?", "Descartar edição",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }
        private void EditorBack_Click(object sender, RoutedEventArgs e) { SetSettingsPage(false); }
        private void EditorCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!ConfirmDiscardDraft()) return;
            ClearPresetDraft();
            SetSettingsPage(false);
        }
        private void ClearPresetDraft()
        {
            editDraft = null;
            editOriginal = null;
            EditorPage.DataContext = null;
            EditorFeedback.Text = string.Empty;
        }
        private void EditorSave_Click(object sender, RoutedEventArgs e)
        {
            if (editDraft == null) return;
            // Moving focus commits the current textbox before validation.
            ((Button)sender).Focus();
            if (HasEditorErrors(EditorPage)) { EditorFeedback.Text = "Corrija os campos numéricos destacados antes de salvar."; return; }
            try
            {
                var saved = editDraft.ToPreset();
                PresetEditingService.Save(presetService, editOriginal, saved, () => backupService.CreateBackup());
                ClearPresetDraft();
                RefreshOrganization(saved);
                SetSettingsPage(false);
                ShowFeedback("Preset salvo com sucesso.", true);
            }
            catch (Exception exception) { EditorFeedback.Text = "Não foi possível salvar: " + exception.Message; }
        }
        private static bool HasEditorErrors(DependencyObject node)
        {
            if (Validation.GetHasError(node)) return true;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                if (HasEditorErrors(VisualTreeHelper.GetChild(node, i))) return true;
            return false;
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!ConfirmDiscardDraft()) e.Cancel = true;
        }
    }
}
