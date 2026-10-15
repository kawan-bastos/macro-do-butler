using System;
using System.Text.RegularExpressions;

internal static class UpdateVersionComparer
{
    private static readonly Regex Pattern = new Regex("^v?(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-([0-9A-Za-z.-]+))?(?:\\+[0-9A-Za-z.-]+)?$", RegexOptions.Compiled);
    internal static int Compare(string left, string right)
    {
        Match a=Pattern.Match((left??"").Trim()), b=Pattern.Match((right??"").Trim());
        if(!a.Success||!b.Success)throw new UpdateException("A versão não está em formato Semantic Versioning válido.");
        for(int i=1;i<=3;i++){int c=ulong.Parse(a.Groups[i].Value).CompareTo(ulong.Parse(b.Groups[i].Value));if(c!=0)return c;}
        string ap=a.Groups[4].Value,bp=b.Groups[4].Value;
        if(ap.Length==0||bp.Length==0)return ap.Length==bp.Length?0:(ap.Length==0?1:-1);
        string[] ai=ap.Split('.'),bi=bp.Split('.');
        for(int i=0;i<Math.Max(ai.Length,bi.Length);i++){if(i>=ai.Length)return -1;if(i>=bi.Length)return 1;ulong an,bn;bool anum=ulong.TryParse(ai[i],out an),bnum=ulong.TryParse(bi[i],out bn);int c=anum&&bnum?an.CompareTo(bn):anum?-1:bnum?1:string.CompareOrdinal(ai[i],bi[i]);if(c!=0)return c;}
        return 0;
    }
}
