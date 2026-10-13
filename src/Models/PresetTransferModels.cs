using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
internal sealed class PresetExportFile
{
    [DataMember(Name="formatVersion")]
    public int FormatVersion { get; set; }
    [DataMember(Name="application")]
    public string Application { get; set; }
    [DataMember(Name="exportedAt")]
    public DateTime ExportedAt { get; set; }
    [DataMember(Name="presets")]
    public List<Preset> Presets { get; set; }
}

internal sealed class PresetBackupInfo
{
    public string FilePath { get; set; }
    public DateTime CreatedAt { get; set; }
}

internal sealed class ImportResult
{
    public int Imported { get; set; }
    public int Ignored { get; set; }
    public int Replaced { get; set; }
    public string SelectedPreset { get; set; }
}

internal enum DuplicatePresetMode { Replace, Ignore }

internal sealed class PresetImportException : Exception
{
    internal PresetImportException(string message) : base(message) { }
    internal PresetImportException(string message, Exception inner) : base(message, inner) { }
}
