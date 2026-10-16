using System.Media;

internal static class AudioCueService
{
    private static readonly SoundPlayer MacroToggle = Load("Velqora.Sounds.MacroToggle");
    private static readonly SoundPlayer WeaponSwitch = Load("Velqora.Sounds.WeaponSwitch");

    private static SoundPlayer Load(string resource)
    {
        try
        {
            var stream = typeof(AudioCueService).Assembly.GetManifestResourceStream(resource);
            if (stream == null) return null;
            var player = new SoundPlayer(stream);
            player.Load();
            return player;
        }
        catch { return null; }
    }
    internal static void PlayMacroToggle() { Play(MacroToggle); }
    internal static void PlayWeaponSwitch() { Play(WeaponSwitch); }
    private static void Play(SoundPlayer player)
    {
        // Playback is asynchronous and never prevents a shortcut from completing.
        try { if (player != null) player.Play(); }
        catch { }
    }
}
