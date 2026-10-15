using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

[DataContract]
internal sealed class UpdateManifest
{
    [DataMember(Name="application")] public string Application { get; set; }
    [DataMember(Name="latestVersion")] public string LatestVersion { get; set; }
    [DataMember(Name="releaseUrl")] public string ReleaseUrl { get; set; }
    [DataMember(Name="downloadUrl")] public string DownloadUrl { get; set; }
    [DataMember(Name="sha256", EmitDefaultValue=false)] public string Sha256 { get; set; }
    [DataMember(Name="fileSize", EmitDefaultValue=false)] public long? FileSize { get; set; }
    [DataMember(Name="releaseNotes")] public List<string> ReleaseNotes { get; set; }
    [DataMember(Name="minimumSupportedVersion", EmitDefaultValue=false)] public string MinimumSupportedVersion { get; set; }
}

internal sealed class UpdateInfo
{
    internal string Version { get; set; }
    internal string Name { get; set; }
    internal string DownloadUrl { get; set; }
    internal string Sha256 { get; set; }
    internal long? FileSize { get; set; }
    internal DateTime? PublishedAt { get; set; }
    internal IList<string> ReleaseNotes { get; set; }
    internal string MinimumSupportedVersion { get; set; }
}

internal enum UpdateResult { Current, Available, Downloaded, Installed, Cancelled, Failed }

internal class UpdateException : Exception
{
    internal UpdateException(string message) : base(message) { }
    internal UpdateException(string message, Exception inner) : base(message, inner) { }
}

internal sealed class UpdateRateLimitException : UpdateException
{
    internal DateTime RetryAfterUtc { get; private set; }
    internal UpdateRateLimitException(DateTime retryAfterUtc) : base("O limite de consultas do GitHub foi atingido.") { RetryAfterUtc=retryAfterUtc; }
}

[DataContract]
internal sealed class UpdatePreferences
{
    [DataMember(Order=1)] public bool CheckAutomatically { get; set; }
    [DataMember(Order=2)] public bool AskBeforeDownload { get; set; }
    [DataMember(Order=3)] public bool AskBeforeInstall { get; set; }
    [DataMember(Order=4)] public bool ReopenAfterUpdate { get; set; }
    [DataMember(Order=5)] public bool KeepPreviousVersion { get; set; }
    [DataMember(Order=6)] public int PreviousVersionsToKeep { get; set; }
    [DataMember(Order=7,EmitDefaultValue=false)] public DateTime? NextUpdateCheckUtc { get; set; }
    internal static UpdatePreferences Defaults() { return new UpdatePreferences { CheckAutomatically=true, AskBeforeDownload=true, AskBeforeInstall=true, ReopenAfterUpdate=true, KeepPreviousVersion=true, PreviousVersionsToKeep=1 }; }
}

[DataContract]
internal sealed class UpdateBackupManifest
{
    [DataMember(Name="application")] public string Application { get; set; }
    [DataMember(Name="version")] public string Version { get; set; }
    [DataMember(Name="createdAt")] public string CreatedAt { get; set; }
    [DataMember(Name="files")] public List<UpdateBackupFile> Files { get; set; }
}

[DataContract]
internal sealed class UpdateBackupFile
{
    [DataMember(Name="name")] public string Name { get; set; }
    [DataMember(Name="sha256")] public string Sha256 { get; set; }
    [DataMember(Name="size")] public long Size { get; set; }
    [DataMember(Name="source")] public string Source { get; set; }
}
