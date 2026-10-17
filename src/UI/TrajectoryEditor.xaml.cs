using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MacroDoButler
{
    public partial class TrajectoryEditor : UserControl
    {
        internal sealed class ProfileRow { public Preset Preset { get; set; } public string Label { get { return Preset.Name; } } public override string ToString() { return Label; } }
        private PresetService repository;
        private Action<Preset> saved;
        private Action backup;
        private Preset original;
        private Trajectory liveTrajectory;
        private long liveRevision;
        internal Action<bool> TestWeaponSelected;
        private List<ProfileRow> allProfiles=new List<ProfileRow>();
        private string previousGame;
        private int PreviewDuration { get { return draft.Duration+1000; } }
        internal bool IsDrawingOpen { get { return DrawingSection.IsExpanded; } }
        private Trajectory draft=Trajectory.Create(), baseline=Trajectory.Create();
        private readonly Stack<Trajectory> undo=new Stack<Trajectory>();
        private bool updating, initialized, playing, dirty, invalid;
        private int selected, previousSlot;
        private readonly Stopwatch watch=new Stopwatch();
        private readonly DispatcherTimer timer=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(33) };
        private double time, startTime;
        public TrajectoryEditor()
        {
            InitializeComponent(); initialized=true;
            Overview.ShowComponents=true;
            Drawing.Smooth=()=>draft.Smooth;
            Drawing.Step=()=>draft.Step;
            Drawing.Apply=ApplyDrawing;
            Timeline.SelectPoint=SelectPoint;
            Timeline.MovePoint=MovePoint;
            Plot.SelectPoint=SelectPoint;
            timer.Tick+=delegate { AdvancePreview(startTime+watch.Elapsed.TotalMilliseconds); };
            IsVisibleChanged+=delegate { if(!IsVisible)Pause(); };
            Unloaded+=delegate { Pause(); };
            Refresh();
        }
        internal void Configure(PresetService service, Action backupAction, Action<Preset> onSaved, Preset preferred)
        {
            repository=service; backup=backupAction; saved=onSaved;
            updating=true;
            allProfiles=service.LoadAll().Select(p=>new ProfileRow { Preset=p }).ToList();
            var games=service.LoadGames().OrderBy(g=>g,StringComparer.CurrentCultureIgnoreCase).ToList();
            Games.ItemsSource=games;
            Games.SelectedItem=games.FirstOrDefault(g=>preferred!=null&&string.Equals(g,preferred.GameName,StringComparison.OrdinalIgnoreCase))??games.FirstOrDefault();
            PopulateProfiles(preferred);
            updating=false; LoadProfile();
        }
        private void PopulateProfiles(Preset preferred)
        {
            previousGame=Games.SelectedItem as string;
            var rows=allProfiles.Where(r=>string.Equals(r.Preset.GameName,previousGame,StringComparison.OrdinalIgnoreCase))
                .OrderBy(r=>r.Preset.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            Profiles.ItemsSource=rows; Profiles.IsEnabled=rows.Count>0;
            Profiles.SelectedItem=rows.FirstOrDefault(r=>preferred!=null&&PresetService.IdentityEquals(r.Preset,preferred))??rows.FirstOrDefault();
        }
        private void GameChanged(object sender,SelectionChangedEventArgs e)
        {
            if(updating||!initialized)return;
            if(!ConfirmDiscard()) { updating=true; Games.SelectedItem=previousGame; updating=false; return; }
            updating=true; PopulateProfiles(null); updating=false; LoadProfile();
        }
        internal bool ConfirmDiscard()
        {
            return (!dirty&&!invalid&&!Drawing.HasChanges)||MessageBox.Show(Window.GetWindow(this),"Descartar as alterações não salvas da trajetória ou do desenho?","Descartar edição",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;
        }
        private void LoadProfile()
        {
            Pause(); Drawing.Reset(); HorizontalAdjustment.Text=VerticalAdjustment.Text="1"; DrawingSection.IsExpanded=false; original=(Profiles.SelectedItem as ProfileRow)?.Preset;
            var value=original==null?null:(Slot.SelectedIndex==1?original.SecondaryTrajectory:original.PrimaryTrajectory);
            liveRevision++;
            draft=Trajectory.Normalize(value)??Trajectory.Create(); baseline=draft.Copy(); undo.Clear(); selected=0; dirty=invalid=false; time=0; previousSlot=Slot.SelectedIndex; Refresh();
            TestWeaponSelected?.Invoke(Slot.SelectedIndex==1);
            Status.Text=original==null?"Este jogo não tem presets. Crie um preset na seção Presets para associar uma trajetória.":"Teste temporário ativo: alterações válidas já valem no macro. Use Desfazer para comparar e Salvar no preset para manter.";
        }
        private void ProfileChanged(object sender,SelectionChangedEventArgs e)
        {
            if(updating||!initialized)return;
            if(!ConfirmDiscard()) { updating=true; Profiles.SelectedItem=Profiles.Items.Cast<ProfileRow>().FirstOrDefault(r=>r.Preset==original); updating=false; return; }
            LoadProfile();
        }
        private void SlotChanged(object sender,SelectionChangedEventArgs e)
        {
            if(updating||!initialized)return;
            if(!ConfirmDiscard()) { updating=true; Slot.SelectedIndex=previousSlot; updating=false; return; }
            LoadProfile();
        }
        private void Remember() { undo.Push(draft.Copy()); if(undo.Count>100) { var recent=undo.Take(100).Reverse().ToArray(); undo.Clear(); foreach(var v in recent)undo.Push(v); } dirty=true; }
        private void SelectPoint(int index) { selected=index; RefreshFields(); Redraw(); }
        private void MovePoint(int index,int ms,bool first)
        {
            if(index==0)return;
            int lower=draft.Points[index-1].TimeMs+1, upper=index+1<draft.Points.Count?draft.Points[index+1].TimeMs-1:Trajectory.MaxTime;
            ms=Math.Max(lower,Math.Min(upper,ms)); if(first)Remember();
            if(ms==draft.Points[index].TimeMs)return; draft.Points[index].TimeMs=ms; dirty=true; selected=index; Refresh();
        }
        private void RefreshFields()
        {
            updating=true; var p=draft.Points[selected];
            PointTime.Text=p.TimeMs.ToString(); PointTime.IsReadOnly=selected==0;
            PointX.Text=p.Horizontal.ToString("0.###",CultureInfo.CurrentCulture); PointY.Text=p.Vertical.ToString("0.###",CultureInfo.CurrentCulture);
            Remove.IsEnabled=draft.Points.Count>2&&selected>0;
            updating=false; invalid=false;
        }
        private void Refresh()
        {
            updating=true; UseTrajectory.IsChecked=draft.Enabled; Interpolation.SelectedIndex=draft.Step?2:draft.Smooth?1:0; Ending.SelectedIndex=draft.HoldLast?1:0; updating=false;
            RefreshFields(); Redraw(); UpdateCursor();
        }
        private void Redraw()
        {
            if(!PresetEditingService.SameTrajectory(liveTrajectory,draft))
            {
                liveTrajectory=draft.Copy(); liveRevision++;
            }
            Plot.Value=Timeline.Value=Overview.Value=draft; Plot.Selected=Timeline.Selected=Overview.Selected=selected;
            Plot.Rebuild(); Timeline.InvalidateVisual(); Overview.InvalidateVisual();
        }
        internal void SelectTestWeapon(bool secondary) { Slot.SelectedIndex=secondary?1:0; }
        internal MovementSettings GetLiveSettings(bool macroEnabled,bool secondary)
        {
            if(original==null||liveTrajectory==null) { LiveTestStatus.Text="Selecione um preset para testar alterações temporárias."; return null; }
            bool editedSlot=secondary==(Slot.SelectedIndex==1);
            var trajectory=editedSlot?liveTrajectory:(secondary?original.SecondaryTrajectory:original.PrimaryTrajectory);
            string state=!macroEnabled?"macro desligado":IsDrawingOpen?"pausado para desenhar":invalid?"pausado: corrija os campos inválidos":editedSlot?"rascunho em uso, sem salvar":"configuração salva desta arma em uso";
            LiveTestStatus.Text="Teste temporário • "+original.Name+" • "+(secondary?"secundária":"primária")+" • "+state;
            return new MovementSettings
            {
                Enabled=macroEnabled&&!IsDrawingOpen&&!invalid,
                Revision=liveRevision,
                Trajectory=trajectory,
                RequireRightButton=original.RequireRightButton,
                HorizontalPixelsPerSecond=secondary?original.SecondaryHorizontal:original.PrimaryHorizontal,
                VerticalPixelsPerSecond=secondary?original.SecondaryVertical:original.PrimaryVertical,
                IntervalMilliseconds=Math.Max(2,Math.Min(50,secondary?(original.SecondaryIntervalMilliseconds??original.IntervalMilliseconds):original.IntervalMilliseconds))
            };
        }
        private void PointTextChanged(object sender,TextChangedEventArgs e)
        {
            if(updating||!initialized)return;
            int ms; double x,y;
            bool valid=int.TryParse(PointTime.Text,out ms)&&double.TryParse(PointX.Text,out x)&&double.TryParse(PointY.Text,out y);
            // Parse separately so all variables are assigned on every compiler path.
            double.TryParse(PointX.Text,out x); double.TryParse(PointY.Text,out y);
            int lower=selected==0?0:draft.Points[selected-1].TimeMs+1;
            int upper=selected==0?0:(selected+1<draft.Points.Count?draft.Points[selected+1].TimeMs-1:Trajectory.MaxTime);
            invalid=!valid||ms<lower||ms>upper||double.IsNaN(x)||double.IsInfinity(x)||double.IsNaN(y)||double.IsInfinity(y)||Math.Abs(x)>5000||Math.Abs(y)>10000;
            if(invalid) { Status.Text="Tempo permitido: "+lower+"–"+upper+" ms. Horizontal: ±5000; vertical: ±10000 px/s. Tempos devem ser únicos."; return; }
            var p=draft.Points[selected]; if(p.TimeMs==ms&&p.Horizontal==x&&p.Vertical==y)return;
            Remember(); p.TimeMs=ms; p.Horizontal=x; p.Vertical=y; Status.Text="Alterações não salvas."; Redraw(); UpdateCursor();
        }
        private void OptionsChanged(object sender,RoutedEventArgs e)
        {
            if(updating||!initialized)return;
            Remember(); draft.Enabled=UseTrajectory.IsChecked==true; draft.Smooth=Interpolation.SelectedIndex==1; draft.Step=Interpolation.SelectedIndex==2; draft.HoldLast=Ending.SelectedIndex==1; Redraw(); UpdateCursor(); Status.Text="Alterações não salvas.";
        }
        private void AddClick(object sender,RoutedEventArgs e)
        {
            if(draft.Points.Count>=Trajectory.MaxPoints) { Status.Text="Limite de 128 pontos atingido."; return; }
            int at=draft.Points[selected].TimeMs;
            int end=selected+1<draft.Points.Count?draft.Points[selected+1].TimeMs:Math.Min(Trajectory.MaxTime,at+1000);
            int ms=at+(end-at)/2;
            if(ms<=at) { Status.Text="Não há espaço neste trecho. Selecione outro ponto ou aumente o tempo seguinte."; return; }
            var value=draft.Evaluate(ms); value.TimeMs=ms; Remember(); draft.Points.Insert(selected+1,value); selected++; Refresh();
        }
        private void RemoveClick(object sender,RoutedEventArgs e) { if(selected==0||draft.Points.Count<=2)return; Remember(); draft.Points.RemoveAt(selected); selected=Math.Min(selected,draft.Points.Count-1); Refresh(); }
        private void UndoClick(object sender,RoutedEventArgs e) { if(undo.Count==0)return; draft=undo.Pop(); selected=Math.Min(selected,draft.Points.Count-1); dirty=true; Refresh(); }
        private void RestoreClick(object sender,RoutedEventArgs e) { Pause(); Drawing.Reset(); draft=baseline.Copy(); selected=0; dirty=invalid=false; undo.Clear(); time=0; Refresh(); Status.Text="Configuração salva restaurada."; }
        private void ApplyDrawing(Trajectory converted)
        {
            if(invalid)throw new ArgumentException("Corrija o ponto com valores inválidos antes de aplicar o desenho.");
            Pause(); Remember(); draft=converted; selected=0; time=0; Refresh(); DrawingSection.IsExpanded=false;
            Status.Text="Desenho aplicado temporariamente ao macro. Teste antes de salvar; Desfazer recupera a trajetória anterior.";
        }
        private void SmoothPassagesClick(object sender,RoutedEventArgs e)
        {
            if(invalid) { Status.Text="Corrija os campos antes de suavizar o desenho."; return; }
            try
            {
                var continuous=DrawingTrajectoryConverter.SmoothLegacy(draft);
                Remember(); draft=continuous; selected=0; Refresh();
                Status.Text="Paradas intermediárias do desenho antigo removidas. Posições e duração preservadas; já vale no teste temporário. Use Desfazer para comparar.";
            }
            catch(ArgumentException ex) { Status.Text=ex.Message; }
        }
        private void HorizontalAdjustmentClick(object sender,RoutedEventArgs e) { AdjustAxis(true); }
        private void VerticalAdjustmentClick(object sender,RoutedEventArgs e) { AdjustAxis(false); }
        private void AdjustAxis(bool horizontal)
        {
            if(invalid) { Status.Text="Corrija os valores do ponto antes de ajustar o eixo."; return; }
            string text=horizontal?HorizontalAdjustment.Text:VerticalAdjustment.Text;
            double factor;
            if(!double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out factor) && !double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out factor)) { Status.Text="Informe um fator entre 0,1 e 10."; return; }
            try
            {
                var adjusted=draft.ScaleAxes(horizontal?factor:1,horizontal?1:factor);
                if(factor==1) { Status.Text="Fator 1 mantém os valores. Nenhum ponto foi alterado."; return; }
                Remember(); draft=adjusted; Refresh();
                Status.Text=(horizontal?"Horizontal ajustada; vertical preservada.":"Vertical ajustada; horizontal preservada.")+" Já vale no teste temporário. Use Desfazer para comparar; salve para manter.";
            }
            catch(ArgumentException ex) { Status.Text=ex.Message; }
        }
        private void SaveClick(object sender,RoutedEventArgs e)
        {
            if(Drawing.HasChanges) { Status.Text="Há um desenho ainda não aplicado. Use Aplicar desenho ao rascunho ou Limpar pontos antes de salvar."; return; }
            if(original==null) { Status.Text="Selecione um preset existente."; return; }
            if(invalid) { Status.Text="Corrija os valores do ponto antes de salvar."; return; }
            try
            {
                var candidate=PresetEditingService.Copy(original);
                if(Slot.SelectedIndex==1)candidate.SecondaryTrajectory=draft.Copy(); else candidate.PrimaryTrajectory=draft.Copy();
                PresetEditingService.Save(repository,original,candidate,backup);
                original=repository.LoadAll().First(p=>PresetService.IdentityEquals(p,candidate));
                ((ProfileRow)Profiles.SelectedItem).Preset=original;
                baseline=draft.Copy(); dirty=false; undo.Clear(); saved(original); Status.Text="Trajetória salva no preset.";
            }
            catch(Exception ex) { Status.Text="Não foi possível salvar: "+ex.Message; }
        }
        private void PlayClick(object sender,RoutedEventArgs e)
        {
            if(playing) { Pause(); return; } if(time>=PreviewDuration)time=0;
            startTime=time; watch.Restart(); playing=true; Play.Content="Ⅱ Pausar"; timer.Start();
        }
        private void Pause() { timer.Stop(); watch.Stop(); playing=false; if(Play!=null)Play.Content="▶ Iniciar"; }
        private void RestartClick(object sender,RoutedEventArgs e) { Pause(); time=0; UpdateCursor(); }
        private void ScrubChanged(object sender,RoutedPropertyChangedEventArgs<double> e) { if(updating||!initialized)return; time=Scrub.Value; startTime=time; watch.Restart(); UpdateCursor(); }
        private void AdvancePreview(double elapsedMs)
        {
            time=Math.Min(PreviewDuration,elapsedMs); UpdateCursor();
            if(time>=PreviewDuration)Pause();
        }
        private void UpdateCursor()
        {
            time=Math.Min(time,PreviewDuration); updating=true; Scrub.Maximum=PreviewDuration; Scrub.Value=time; updating=false;
            Plot.Time=Timeline.Time=Overview.Time=time; Plot.InvalidateVisual(); Timeline.InvalidateVisual(); Overview.InvalidateVisual(); Clock.Text=time>=draft.Duration?((int)(time-draft.Duration))+" ms após o fim • fase final":((int)time)+" / "+draft.Duration+" ms";
            var velocity=draft.Evaluate(time);
            Velocity.Text=string.Format(CultureInfo.CurrentCulture,"Movimento agora: H {0:0.##} • V {1:0.##} px/s",velocity.Horizontal,velocity.Vertical);
            Drawing.UpdateTimingHint(draft.Step);
            InterpolationHelp.Text=draft.Step?"Em degraus: mantém o valor do ponto atual e troca exatamente no próximo. Exemplo: 350 → 250, sem rampa. Para desenhar sem pausas, escolha este modo antes de aplicar o desenho.":draft.Smooth?"Suave: velocidade e aceleração contínuas nas junções. Desenhos novos não zeram em cada ponto; para desenhos antigos, use Suavizar passagens. Zeros explicitamente configurados continuam valendo.":"Linear: a velocidade muda de maneira uniforme entre os pontos. Pontos com valores iguais não mudam.";
            var displacement=draft.Integrate(0,draft.Duration);
            TrajectoryMetrics.Text=string.Format(CultureInfo.CurrentCulture,"Deslocamento total: H {0:0.##} / V {1:0.##} px • pico: H {2:0.##} / V {3:0.##} px/s",displacement.Horizontal,displacement.Vertical,draft.Points.Max(p=>Math.Abs(p.Horizontal)),draft.Points.Max(p=>Math.Abs(p.Vertical)));
            var last=draft.Points[draft.Points.Count-1];
            EndingHelp.Text=!draft.HoldLast?"Após o último ponto, horizontal e vertical ficam em zero.":
                (last.Horizontal==0&&last.Vertical==0?"O último ponto está em zero: manter e parar produzem o mesmo resultado.":string.Format(CultureInfo.CurrentCulture,"Após o último ponto, mantém H {0:0.##} e V {1:0.##} px/s enquanto o disparo continuar.",last.Horizontal,last.Vertical));
        }
    }

    public sealed class TrajectoryPlot : FrameworkElement
    {
        internal Trajectory Value; internal int Selected; internal double Time; internal Action<int> SelectPoint;
        internal bool StaticPreview;
        private Point[] path=new Point[0], keys=new Point[0];
        private double minX,minY,scale;
        private StreamGeometry pathGeometry;
        private static readonly Brush Gold=new SolidColorBrush(Color.FromRgb(232,185,79));
        private Point Map(TrajectoryPoint p) { return new Point(24+(p.Horizontal-minX)*scale,24+(p.Vertical-minY)*scale); }
        internal void Rebuild()
        {
            if(Value==null||ActualWidth<50||ActualHeight<50)return;
            var values=Enumerable.Range(0,301).Select(i=>Value.Integrate(0,(Value.Duration+1000)*i/300.0)).ToArray();
            double loX=values.Min(p=>p.Horizontal),hiX=values.Max(p=>p.Horizontal),loY=values.Min(p=>p.Vertical),hiY=values.Max(p=>p.Vertical);
            scale=Math.Min((ActualWidth-48)/Math.Max(1,hiX-loX),(ActualHeight-48)/Math.Max(1,hiY-loY));
            minX=loX-((ActualWidth-48)/scale-(hiX-loX))/2; minY=loY-((ActualHeight-48)/scale-(hiY-loY))/2;
            path=values.Select(Map).ToArray();
            pathGeometry=new StreamGeometry(); using(var c=pathGeometry.Open()) { c.BeginFigure(path[0],false,false); c.PolyLineTo(path.Skip(1).ToArray(),true,false); } pathGeometry.Freeze();
            keys=Value.Points.Select(p=>Map(Value.Integrate(0,p.TimeMs))).ToArray(); InvalidateVisual();
        }
        protected override void OnRenderSizeChanged(SizeChangedInfo info) { base.OnRenderSizeChanged(info); Rebuild(); }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(12,16,18)),new Pen(new SolidColorBrush(Color.FromRgb(41,50,55)),1),new Rect(RenderSize),8,8);
            var grid=new Pen(new SolidColorBrush(Color.FromRgb(25,34,38)),1);
            for(double x=24;x<ActualWidth;x+=32)dc.DrawLine(grid,new Point(x,0),new Point(x,ActualHeight));
            for(double y=24;y<ActualHeight;y+=32)dc.DrawLine(grid,new Point(0,y),new Point(ActualWidth,y));
            if(Value==null||path.Length==0)return;
            dc.DrawGeometry(null,new Pen(Gold,1.6) { DashStyle=DashStyles.Dash },pathGeometry);
            for(int i=0;i<keys.Length;i++)dc.DrawEllipse(i==Selected?Brushes.White:Gold,null,keys[i],i==Selected?5:3,i==Selected?5:3);
            if(!StaticPreview)dc.DrawEllipse(Gold,new Pen(Brushes.White,1.5),Map(Value.Integrate(0,Time)),7,7);
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { base.OnMouseLeftButtonDown(e); var pos=e.GetPosition(this); for(int i=0;i<keys.Length;i++)if((keys[i]-pos).Length<12) { SelectPoint?.Invoke(i); e.Handled=true; return; } }
    }
    public sealed class TrajectoryTimeline : FrameworkElement
    {
        internal Trajectory Value; internal int Selected; internal double Time; internal bool ShowComponents; internal Action<int> SelectPoint; internal Action<int,int,bool> MovePoint;
        private int dragging=-1, dragDuration; private bool first;
        private double X(double ms) { return 16+Math.Min(ms,Value.Duration)/Math.Max(1,Value.Duration)*Math.Max(1,ActualWidth-32); }
        protected override void OnRender(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize)); if(Value==null)return;
            var gold=new SolidColorBrush(Color.FromRgb(232,185,79)); double y=ActualHeight*0.65;
            dc.DrawLine(new Pen(Brushes.SlateGray,1),new Point(16,y),new Point(ActualWidth-16,y));
            for(int i=0;i<=4;i++) { double ms=Value.Duration*i/4.0,x=X(ms); dc.DrawLine(new Pen(Brushes.DimGray,1),new Point(x,y-10),new Point(x,y+10)); var text=new FormattedText(((int)ms)+" ms",CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),10,Brushes.LightGray,VisualTreeHelper.GetDpi(this).PixelsPerDip); dc.DrawText(text,new Point(Math.Max(0,Math.Min(ActualWidth-text.Width,x-10)),0)); }
            if(ShowComponents)
            {
                double limit=Math.Max(1,Value.Points.Max(p=>Math.Max(Math.Abs(p.Horizontal),Math.Abs(p.Vertical))));
                var xPen=new Pen(gold,1.3); var yPen=new Pen(Brushes.SlateGray,1.3);
                if(Value.Step)
                {
                    for(int i=1;i<Value.Points.Count;i++)
                    {
                        var a=Value.Points[i-1]; var b=Value.Points[i];
                        var fromX=new Point(X(a.TimeMs),y-a.Horizontal/limit*17); var toX=new Point(X(b.TimeMs),fromX.Y);
                        var fromY=new Point(X(a.TimeMs),y-a.Vertical/limit*17); var toY=new Point(X(b.TimeMs),fromY.Y);
                        dc.DrawLine(xPen,fromX,toX); dc.DrawLine(xPen,toX,new Point(toX.X,y-b.Horizontal/limit*17));
                        dc.DrawLine(yPen,fromY,toY); dc.DrawLine(yPen,toY,new Point(toY.X,y-b.Vertical/limit*17));
                    }
                }
                else
                {
                Point? prevX=null,prevY=null;
                for(int j=0;j<=200;j++)
                {
                    double ms=Value.Duration*j/200.0;
                    // Show the final control value, even when the post-duration behavior is zero.
                    var value=j==200?Value.Points[Value.Points.Count-1]:Value.Evaluate(ms);
                    var px=new Point(X(ms),y-value.Horizontal/limit*17); var py=new Point(X(ms),y-value.Vertical/limit*17);
                    if(prevX.HasValue) { dc.DrawLine(xPen,prevX.Value,px); dc.DrawLine(yPen,prevY.Value,py); }
                    prevX=px; prevY=py;
                }
                }
            }
            for(int i=0;i<Value.Points.Count;i++) { var p=Value.Points[i]; double x=X(p.TimeMs); dc.DrawEllipse(i==Selected?gold:Brushes.SlateGray,new Pen(Brushes.White,1),new Point(x,y),i==Selected?6:4,i==Selected?6:4); }
            double cursor=X(Time); dc.DrawLine(new Pen(gold,1),new Point(cursor,20),new Point(cursor,ActualHeight));
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e); if(Value==null)return; double x=e.GetPosition(this).X;
            int nearest=Enumerable.Range(0,Value.Points.Count).OrderBy(i=>Math.Abs(X(Value.Points[i].TimeMs)-x)).First();
            SelectPoint?.Invoke(nearest); dragging=nearest; dragDuration=Value.Duration; first=true; CaptureMouse(); e.Handled=true;
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e); if(dragging<0||Value==null||e.LeftButton!=MouseButtonState.Pressed)return;
            int ms=(int)Math.Round((e.GetPosition(this).X-16)/Math.Max(1,ActualWidth-32)*dragDuration);
            MovePoint?.Invoke(dragging,ms,first); first=false;
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { base.OnMouseLeftButtonUp(e); dragging=-1; ReleaseMouseCapture(); }
    }
}
