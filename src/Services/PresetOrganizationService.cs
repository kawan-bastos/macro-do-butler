using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class PresetOrganizationService
{
    internal IList<Preset> All(PresetService repository) { return repository.LoadAll(); }
    internal void Save(PresetService repository, Preset preset)
    {
        if (preset == null || string.IsNullOrWhiteSpace(preset.GameName) || string.IsNullOrWhiteSpace(preset.Name)) throw new ArgumentException("Jogo e nome do preset são obrigatórios.");
        if (!PresetTransferService.IsValidPreset(preset)) throw new ArgumentException("Os valores devem estar dentro dos limites permitidos.");
        repository.SaveByIdentity(preset);
    }
    internal bool HasDuplicate(PresetService repository, Preset preset) { return repository.LoadAll().Any(p => PresetService.IdentityEquals(p,preset)); }
}

internal sealed class PresetValidationService
{
    internal void Validate(Preset p) { if(!PresetTransferService.IsValidPreset(p)) throw new ArgumentException("Verifique os campos obrigatórios e os limites numéricos do preset."); }
}

internal sealed class PresetMigrationService
{
    private readonly PresetService repository;
    internal PresetMigrationService(PresetService repository) { this.repository=repository; }
    internal IList<Preset> Migrate() { return repository.LoadAll(); }
}

internal sealed class PresetImportExportService
{
    private readonly PresetTransferService transfer;
    internal PresetImportExportService(PresetTransferService transfer) { this.transfer=transfer; }
    internal void Export(string path,IList<Preset> presets) { transfer.Export(path,presets); }
    internal IList<Preset> Import(string path) { return transfer.ReadAndValidate(path); }
}
