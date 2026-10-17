using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MacroDoButler
{
    public partial class PresetMovementSummary : UserControl
    {
        public PresetMovementSummary()
        {
            InitializeComponent();
            MiniPlot.StaticPreview=true;
            MiniPlot.Selected=-1;
        }
        internal void ShowSettings(Preset preset, bool secondary, bool macroEnabled, int horizontal, int vertical)
        {
            Visibility=preset==null?Visibility.Collapsed:Visibility.Visible;
            if(preset==null) { MiniPlot.Value=null; return; }
            var trajectory=secondary?preset.SecondaryTrajectory:preset.PrimaryTrajectory;
            bool advanced=trajectory!=null&&trajectory.Enabled;
            string weapon=secondary?"SECUNDÁRIA":"PRIMÁRIA";
            ModeText.Text=(advanced?"AVANÇADA":"BÁSICA")+" • "+weapon;
            ModeBadge.BorderBrush=(System.Windows.Media.Brush)FindResource(advanced?"AccentBrush":"BorderBrush");
            ModeText.Foreground=(System.Windows.Media.Brush)FindResource(advanced?"AccentBrightBrush":"MutedBrush");
            MacroState.Text=macroEnabled?"Macro ligado • esta é a configuração em uso":"Macro desligado • configuração selecionada";
            AdvancedDetails.Visibility=advanced?Visibility.Visible:Visibility.Collapsed;
            VerticalLabel.Text=advanced?"Vertical • faixa da trajetória":"Força vertical";
            HorizontalLabel.Text=advanced?"Horizontal • faixa da trajetória":"Correção lateral";
            VerticalValue.Text=advanced?Range(trajectory,false):Format(vertical)+" px/s";
            HorizontalValue.Text=advanced?Range(trajectory,true):Format(horizontal)+" px/s";
            if(advanced)
            {
                TrajectoryInfo.Text=trajectory.Points.Count+" pontos • "+trajectory.Duration+" ms • interpolação "+trajectory.InterpolationName;
                var last=trajectory.Points[trajectory.Points.Count-1];
                TrajectoryEnding.Text=trajectory.HoldLast?"Ao terminar: mantém H "+Format(last.Horizontal)+" / V "+Format(last.Vertical)+" px/s":"Ao terminar: zero movimento";
            }
            var displayed=advanced?trajectory:null;
            if(!ReferenceEquals(MiniPlot.Value,displayed)) { MiniPlot.Value=displayed; MiniPlot.Rebuild(); }
        }
        private static string Range(Trajectory trajectory,bool horizontal)
        {
            var values=trajectory.Points.Select(p=>horizontal?p.Horizontal:p.Vertical);
            double min=values.Min(), max=values.Max();
            return (min==max?Format(min):Format(min)+" a "+Format(max))+" px/s";
        }
        private static string Format(double value) { return value.ToString("0.##",CultureInfo.CurrentCulture); }
    }
}
