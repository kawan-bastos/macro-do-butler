using System;
using System.Collections.Generic;
using System.IO;

internal sealed class PresetService
{
    private readonly string filePath;
    internal PresetService(string directory) { filePath = Path.Combine(directory, "presets.cfg"); }

    internal IList<Preset> LoadAll()
    {
        var result = new List<Preset>();
        if (!File.Exists(filePath)) return result;
        foreach (string line in File.ReadAllLines(filePath)) { Preset preset; if (TryParse(line, out preset)) result.Add(preset); }
        return result;
    }

    internal void Save(Preset preset)
    {
        var presets = new List<Preset>(LoadAll());
        int index = presets.FindIndex(item => item.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) presets[index] = preset; else presets.Add(preset);
        WriteAll(presets);
    }

    internal void Delete(string name)
    {
        var presets = new List<Preset>(LoadAll());
        presets.RemoveAll(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        WriteAll(presets);
    }

    private void WriteAll(IEnumerable<Preset> presets)
    {
        var lines = new List<string>();
        foreach (Preset p in presets) lines.Add(Uri.EscapeDataString(p.Name) + "|" + p.PrimaryVertical + "|" + p.PrimaryHorizontal + "|" + p.SecondaryVertical + "|" + p.SecondaryHorizontal + "|" + p.IntervalMilliseconds + "|" + (p.RequireRightButton ? "1" : "0"));
        string temporary = filePath + ".tmp";
        File.WriteAllLines(temporary, lines.ToArray());
        if (File.Exists(filePath)) File.Replace(temporary, filePath, filePath + ".bak"); else File.Move(temporary, filePath);
    }

    private static bool TryParse(string line, out Preset preset)
    {
        preset = null;
        string[] p = line.Split('|');
        bool legacy = p.Length == 5, current = p.Length == 7;
        if (!legacy && !current) return false;
        int pv, ph, sv = 0, sh = 0, interval;
        int intervalIndex = legacy ? 3 : 5, rightIndex = legacy ? 4 : 6;
        if (!int.TryParse(p[1], out pv) || !int.TryParse(p[2], out ph) || !int.TryParse(p[intervalIndex], out interval) || (current && (!int.TryParse(p[3], out sv) || !int.TryParse(p[4], out sh)))) return false;
        string name;
        try { name = Uri.UnescapeDataString(p[0]); } catch { return false; }
        if (string.IsNullOrWhiteSpace(name)) return false;
        preset = new Preset { Name = name, PrimaryVertical = pv, PrimaryHorizontal = ph, SecondaryVertical = sv, SecondaryHorizontal = sh, IntervalMilliseconds = interval, RequireRightButton = p[rightIndex] == "1" };
        return true;
    }
}
