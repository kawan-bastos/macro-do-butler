using System;
using System.Linq;

internal static class PresetEditingService
{
    internal static Preset Copy(Preset p)
    {
        return new Preset { PrimaryTrajectory=Trajectory.Normalize(p.PrimaryTrajectory), SecondaryTrajectory=Trajectory.Normalize(p.SecondaryTrajectory), GameName=p.GameName, Name=p.Name, CharacterName=p.CharacterName, WeaponName=p.WeaponName,
            WeaponSlot=p.WeaponSlot, PrimaryVertical=p.PrimaryVertical, PrimaryHorizontal=p.PrimaryHorizontal,
            SecondaryVertical=p.SecondaryVertical, SecondaryHorizontal=p.SecondaryHorizontal,
            IntervalMilliseconds=p.IntervalMilliseconds, SecondaryIntervalMilliseconds=p.SecondaryIntervalMilliseconds,
            RequireRightButton=p.RequireRightButton, CreatedAt=p.CreatedAt, UpdatedAt=p.UpdatedAt };
    }
    internal static bool SameContent(Preset a, Preset b)
    {
        return SameTrajectory(a.PrimaryTrajectory,b.PrimaryTrajectory) && SameTrajectory(a.SecondaryTrajectory,b.SecondaryTrajectory)
            && a.Name==b.Name && a.GameName==b.GameName && a.PrimaryVertical==b.PrimaryVertical && a.PrimaryHorizontal==b.PrimaryHorizontal
            && a.SecondaryVertical==b.SecondaryVertical && a.SecondaryHorizontal==b.SecondaryHorizontal
            && a.IntervalMilliseconds==b.IntervalMilliseconds && a.SecondaryIntervalMilliseconds==b.SecondaryIntervalMilliseconds
            && a.RequireRightButton==b.RequireRightButton && a.WeaponSlot==b.WeaponSlot
            && a.CharacterName==b.CharacterName && a.WeaponName==b.WeaponName && a.UpdatedAt==b.UpdatedAt;
    }
    internal static bool SameTrajectory(Trajectory a, Trajectory b)
    {
        if(a==null||b==null)return a==b;
        return a.Enabled==b.Enabled && a.Step==b.Step && a.Smooth==b.Smooth && a.HoldLast==b.HoldLast && a.Points.Count==b.Points.Count
            && a.Points.Zip(b.Points,(x,y)=>x.TimeMs==y.TimeMs && x.Horizontal==y.Horizontal && x.Vertical==y.Vertical).All(same=>same);
    }
    // One atomic repository write also handles renames, keeping the original identity until commit.
    internal static void Save(PresetService repository, Preset original, Preset candidate, Action backup)
    {
        if (candidate.IntervalMilliseconds < 2 || (candidate.SecondaryIntervalMilliseconds ?? candidate.IntervalMilliseconds) < 2)
            throw new ArgumentException("Defina os intervalos das duas armas entre 2 e 50 ms antes de salvar.");
        if (!PresetTransferService.IsValidPreset(candidate)) throw new ArgumentException("Verifique o nome e os limites dos valores do preset.");
        var all = repository.LoadAll().ToList();
        int index = original == null ? -1 : all.FindIndex(p => PresetService.IdentityEquals(p, original));
        if (original != null && (index < 0 || !SameContent(all[index], original)))
            throw new InvalidOperationException("Este preset foi alterado ou removido enquanto era editado. Cancele e abra novamente para carregar os dados atuais.");
        if (all.Where((p, i) => i != index).Any(p => PresetService.IdentityEquals(p, candidate)))
            throw new ArgumentException("Já existe um preset com esse nome neste jogo. Escolha outro nome.");
        if (!repository.LoadGames().Any(g => string.Equals(g, candidate.GameName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("O jogo associado não está mais disponível. Cancele a edição e selecione um jogo.");
        var saved = Copy(candidate);
        saved.CreatedAt = original == null ? DateTime.Now : all[index].CreatedAt;
        saved.UpdatedAt = DateTime.Now;
        if (index >= 0) all[index] = saved; else all.Add(saved);
        backup();
        repository.ReplaceAll(all);
    }
}
