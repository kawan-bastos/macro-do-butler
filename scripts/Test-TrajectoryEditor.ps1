param([string]$AssemblyPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase
$root=Split-Path -Parent (Split-Path -Parent $PSCommandPath)
if(!$AssemblyPath) { $AssemblyPath=Join-Path $root 'bin\Release\net48\Velqora.exe' }
$assembly=[Reflection.Assembly]::LoadFrom($AssemblyPath)
[Windows.Application]::ResourceAssembly=$assembly
$app=New-Object MacroDoButler.App
$app.InitializeComponent()
$editor=New-Object MacroDoButler.TrajectoryEditor
$flags=[Reflection.BindingFlags]'Instance,NonPublic,Public'
function Field($obj,$name) { $obj.GetType().GetField($name,$flags).GetValue($obj) }
function Put($obj,$name,$value) { $obj.GetType().GetField($name,$flags).SetValue($obj,$value) }
function Invoke($obj,$name,$values) { $obj.GetType().GetMethod($name,$flags).Invoke($obj,$values) }
function Check($value,$message) { if(!$value){throw $message}; $script:checks++ }
$script:checks=0
$trajectoryType=$assembly.GetType('Trajectory')
$pointType=$assembly.GetType('TrajectoryPoint')
$draft=Field $editor 'draft'
$draft.Points.Clear()
foreach($spec in @(@(0,-150,220),@(250,50,270),@(500,200,100),@(750,-160,-140),@(1000,30,0),@(1400,0,0))) {
 $point=[Activator]::CreateInstance($pointType,$true); $point.TimeMs=$spec[0]; $point.Horizontal=$spec[1]; $point.Vertical=$spec[2]; $draft.Points.Add($point)
}
$draft.Enabled=$true
$draft.Smooth=$true
Invoke $editor 'Refresh' @() | Out-Null
$profiles=Field $editor 'Profiles'
Put $editor 'updating' $true
$preset=[Activator]::CreateInstance($assembly.GetType('Preset'),$true)
$preset.GameName='Exemplo'; $preset.Name='Trajetória em S'
$row=[Activator]::CreateInstance($assembly.GetType('MacroDoButler.TrajectoryEditor+ProfileRow'),$true)
$row.Preset=$preset
$profiles.Items.Add($row) | Out-Null
$profiles.SelectedIndex=0
Put $editor 'updating' $false
# Populate a display label only; no user profiles or native input are loaded.
Invoke $editor 'SelectPoint' @(2) | Out-Null
$x=Field $editor 'PointX'
$x.Text='250'
Check ($draft.Points[2].Horizontal -eq 250) 'Live point edit failed'
$x.Text='NaN'
Check (Field $editor 'invalid') 'Nonfinite input accepted'
Invoke $editor 'Refresh' @() | Out-Null
Invoke $editor 'MovePoint' @(2,250,$true) | Out-Null
Check ($draft.Points[2].TimeMs -eq 251) 'Duplicate drag not clamped'
Invoke $editor 'UndoClick' @($null,$null) | Out-Null
$draft=Field $editor 'draft'
Check ($draft.Points[2].TimeMs -eq 500) 'Drag undo failed'
$count=$draft.Points.Count
Invoke $editor 'AddClick' @($null,$null) | Out-Null
$draft=Field $editor 'draft'
Check ($draft.Points.Count -eq ($count+1)) 'Add failed'
Invoke $editor 'RemoveClick' @($null,$null) | Out-Null
Check ($draft.Points.Count -eq $count) 'Remove failed'
Invoke $editor 'SelectPoint' @(2) | Out-Null
Invoke $editor 'PlayClick' @($null,$null) | Out-Null
Check (Field $editor 'playing') 'Playback failed'
Invoke $editor 'RestartClick' @($null,$null) | Out-Null
Check (!(Field $editor 'playing')) 'Restart did not pause'
Check ((Field $editor 'time') -eq 0) 'Restart did not seek'
# Exercise the real ComboBox selection events, including preview after the last point.
$interpolation=Field $editor 'Interpolation'
$scrub=Field $editor 'Scrub'
$scrub.Value=62
$interpolation.SelectedIndex=0
$linear=(Invoke $draft 'Evaluate' @(62.0)).Horizontal
$interpolation.SelectedIndex=1
$smooth=(Invoke $draft 'Evaluate' @(62.0)).Horizontal
Check ([Math]::Abs($linear-$smooth) -gt 1) 'Interpolation choices do not change the evaluated movement'
Check ($draft.Smooth) 'Interpolation selection did not update draft'
Invoke $editor 'SelectPoint' @(($draft.Points.Count-1)) | Out-Null
(Field $editor 'PointX').Text='100'
$duration=$draft.GetType().GetProperty('Duration',$flags).GetValue($draft,$null)
$ending=Field $editor 'Ending'
$ending.SelectedIndex=0
Invoke $editor 'AdvancePreview' @(($duration+500.0)) | Out-Null
Check ((Field $editor 'time') -gt $duration) 'Preview does not demonstrate final phase'
Check ((Invoke $draft 'Evaluate' @(($duration+500.0))).Horizontal -eq 0) 'Zero ending does not stop'
$ending.SelectedIndex=1
Check ($draft.HoldLast) 'Ending selection did not update draft'
Check ((Invoke $draft 'Evaluate' @(($duration+500.0))).Horizontal -eq 100) 'Hold ending does not continue'
Check ((Field $editor 'Velocity').Text -match '100') 'Final velocity display did not update live'
Invoke $editor 'PlayClick' @($null,$null) | Out-Null
Invoke $editor 'AdvancePreview' @(($duration+500.0)) | Out-Null
Check (Field $editor 'playing') 'Playback stops before demonstrating final phase'
Invoke $editor 'AdvancePreview' @(($duration+1000.0)) | Out-Null
Check (!(Field $editor 'playing')) 'Playback does not stop after final demonstration'
(Field $editor 'PointX').Text='0'
Check ((Field $editor 'EndingHelp').Text -match 'mesmo resultado') 'Zero last point equivalence is unexplained'
Invoke $editor 'RestartClick' @($null,$null) | Out-Null
# Test game filtering with 500 synthetic presets, including identical names across games.
$fixtureDirectory=Join-Path $root ('build-check\trajectory-game-filter-'+[Guid]::NewGuid().ToString('N'))
$constructor=$assembly.GetType('PresetService').GetConstructor($flags,$null,[Type[]]@([string]),$null)
$service=$constructor.Invoke([object[]]@([string]$fixtureDirectory))
foreach($game in @('Jogo A','Jogo B','Jogo vazio')) { Invoke $service 'SaveGame' @($game) | Out-Null }
$presets=[Array]::CreateInstance($assembly.GetType('Preset'),500)
for($i=0;$i -lt 500;$i++) {
 $item=[Activator]::CreateInstance($assembly.GetType('Preset'),$true)
 $item.Name='Preset '+($i % 250).ToString('000'); $item.GameName=if($i -lt 250){'Jogo A'}else{'Jogo B'}
 $item.IntervalMilliseconds=7
 $presets.SetValue($item,$i)
}
Invoke $service 'ReplaceAll' (, $presets) | Out-Null
$filterEditor=New-Object MacroDoButler.TrajectoryEditor
Invoke $filterEditor 'Configure' @($service,$null,$null,$presets[321]) | Out-Null
$games=Field $filterEditor 'Games'; $filtered=Field $filterEditor 'Profiles'
Check ($games.SelectedItem -eq 'Jogo B') 'Preferred game not selected'
Check ($filtered.Items.Count -eq 250) 'Presets not filtered by game'
Check (($filtered.SelectedItem).Preset.Name -eq 'Preset 071') 'Preferred preset lost'
Check (@($filtered.Items | Where-Object {$_.Preset.GameName -ne 'Jogo B'}).Count -eq 0) 'Other games leaked into preset list'
$games.SelectedItem='Jogo A'
Check ($filtered.Items.Count -eq 250) 'Changing game did not populate presets'
Check (@($filtered.Items | Where-Object {$_.Preset.GameName -ne 'Jogo A'}).Count -eq 0) 'Changing game left other games in list'
$games.SelectedItem='Jogo vazio'
Check ($filtered.Items.Count -eq 0 -and !$filtered.IsEnabled) 'Empty game shows stale presets'
$games.SelectedItem='Jogo B'
Check ($filtered.Items.Count -eq 250 -and $filtered.IsEnabled) 'Returning from empty game failed'
# Use the actual filtered selectors in the layout render while retaining the sample curve.
Put $editor 'updating' $true
(Field $editor 'Games').Items.Add('Exemplo') | Out-Null
(Field $editor 'Games').SelectedIndex=0
Put $editor 'updating' $false
$background=New-Object Windows.Controls.Border
$background.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#0C1012')
$background.Child=$editor
foreach($size in @(@(1100,920),@(885,820))) {
 $width=$size[0];$height=$size[1]
 $background.Width=$width; $background.Height=$height
 $background.Measure([Windows.Size]::new($width,$height));$background.Arrange([Windows.Rect]::new(0,0,$width,$height));$background.UpdateLayout()
 Invoke $editor 'Refresh' @() | Out-Null
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $bitmap.Render($background)
 $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $file=[IO.File]::Create((Join-Path $root ('build-check\trajectory-'+$width+'.png')));$encoder.Save($file);$file.Dispose()
}
# Verify the details card without constructing MainWindow or starting native input.
$summary=New-Object MacroDoButler.PresetMovementSummary
$summaryPreset=[Activator]::CreateInstance($assembly.GetType('Preset'),$true)
$summaryPreset.PrimaryTrajectory=$draft
$draft.Enabled=$true
Invoke $summary 'ShowSettings' @($summaryPreset,$false,$true,999,888) | Out-Null
Check ((Field $summary 'ModeText').Text -match 'AVANÇADA.*PRIMÁRIA') 'Details omit primary advanced mode'
Check ((Field $summary 'AdvancedDetails').Visibility -eq 'Visible') 'Advanced details are hidden'
Check ((Field $summary 'HorizontalValue').Text -notmatch '999') 'Details display unused basic horizontal value'
Check ((Field $summary 'VerticalValue').Text -notmatch '888') 'Details display unused basic vertical value'
Check ((Field $summary 'TrajectoryInfo').Text -match '1400 ms') 'Details omit duration'
Check ((Field $summary 'MiniPlot').GetType().GetField('Value',$flags).GetValue((Field $summary 'MiniPlot')) -eq $draft) 'Mini plot does not use saved trajectory'
Check (Field (Field $summary 'MiniPlot') 'StaticPreview') 'Summary plot shows a playback marker'
Invoke $summary 'ShowSettings' @($summaryPreset,$true,$true,25,40) | Out-Null
Check ((Field $summary 'ModeText').Text -match 'BÁSICA.*SECUNDÁRIA') 'Weapon switch retains primary mode'
Check ((Field $summary 'AdvancedDetails').Visibility -eq 'Collapsed') 'Basic weapon retains stale graph'
Check ((Field $summary 'HorizontalValue').Text -eq '25 px/s') 'Secondary basic horizontal is incorrect'
Check ((Field $summary 'VerticalValue').Text -eq '40 px/s') 'Secondary basic vertical is incorrect'
$summaryPreset.SecondaryTrajectory=$draft
Invoke $summary 'ShowSettings' @($summaryPreset,$true,$false,25,40) | Out-Null
Check ((Field $summary 'ModeText').Text -match 'AVANÇADA.*SECUNDÁRIA') 'Secondary advanced mode missing'
Check ((Field $summary 'MacroState').Text -match 'desligado') 'Details falsely claim macro is active'
$draft.Enabled=$false
Invoke $summary 'ShowSettings' @($summaryPreset,$false,$true,12,34) | Out-Null
Check ((Field $summary 'ModeText').Text -match 'BÁSICA') 'Disabled trajectory still shown as advanced'
Check ((Field $summary 'AdvancedDetails').Visibility -eq 'Collapsed') 'Disabled trajectory graph remains visible'
$draft.Enabled=$true
Invoke $summary 'ShowSettings' @($null,$false,$false,0,0) | Out-Null
Check ($summary.Visibility -eq 'Collapsed') 'Empty selection retains old summary'
Invoke $summary 'ShowSettings' @($summaryPreset,$false,$true,12,34) | Out-Null
$summaryBorder=New-Object Windows.Controls.Border
$summaryBorder.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#111719')
$summaryBorder.Padding=[Windows.Thickness]::new(18)
$summaryBorder.Child=$summary
$summaryBorder.Measure([Windows.Size]::new(680,600))
$summaryBorder.Arrange([Windows.Rect]::new(0,0,680,$summaryBorder.DesiredSize.Height))
$summaryBorder.UpdateLayout()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(680,[int][Math]::Ceiling($summaryBorder.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($summaryBorder)
$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$file=[IO.File]::Create((Join-Path $root 'build-check\preset-details-advanced.png'));$encoder.Save($file);$file.Dispose()
# Experimental point drawing: edit, undo, reference image, conversion and parent undo.
$drawingEditor=Field $editor 'Drawing'
$surface=Field $drawingEditor 'Surface'
(Field $editor 'DrawingSection').IsExpanded=$true
Check ($editor.GetType().GetProperty('IsDrawingOpen',$flags).GetValue($editor,$null)) 'Drawing pause state not exposed'
(Field $editor 'DrawingSection').IsExpanded=$false
Check (!$editor.GetType().GetProperty('IsDrawingOpen',$flags).GetValue($editor,$null)) 'Drawing pause state remains after closing'
Invoke $surface 'AddAt' @(200.0,20.0) | Out-Null
Invoke $surface 'AddAt' @(100.0,100.0) | Out-Null
Invoke $surface 'AddAt' @(300.0,200.0) | Out-Null
Invoke $surface 'AddAt' @(200.0,300.0) | Out-Null
Check ((Field $surface 'Points').Count -eq 4) 'Drawing click points not added'
Check ($drawingEditor.HasChanges -or $drawingEditor.GetType().GetProperty('HasChanges',$flags).GetValue($drawingEditor,$null)) 'Drawing does not track unsaved edits'
Invoke $surface 'MoveTo' @(1,150.0,100.0) | Out-Null
Check ((Field $surface 'Points')[1].X -eq 150) 'Drawing drag did not change point'
Invoke $surface 'RemoveAt' @(1) | Out-Null
Check ((Field $surface 'Points').Count -eq 3) 'Drawing removal failed'
Invoke $drawingEditor 'UndoClick' @($null,$null) | Out-Null
Check ((Field $surface 'Points').Count -eq 4) 'Drawing undo failed'
$referencePath=Join-Path $root 'build-check\drawing-reference-test.png'
[IO.File]::Copy((Join-Path $root 'assets\Velqora.png'),$referencePath,$true)
Invoke $drawingEditor 'LoadReferenceImage' @([string]$referencePath) | Out-Null
Check ((Field $surface 'ReferenceImage') -ne $null) 'Reference image did not load'
# OnLoad decoding must release the file immediately.
$file=[IO.File]::Open($referencePath,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$file.Dispose()
Check $true 'Reference file remains locked'
# View operations transform only the background, preserving all traced positions.
$pointSnapshot=@((Field $surface 'Points') | ForEach-Object { "$($_.X),$($_.Y)" }) -join ';'
(Field $drawingEditor 'ImageZoom').Value=2
Check ((Field $surface 'ReferenceZoom') -eq 2) 'Zoom slider did not update image'
Check ((Field $drawingEditor 'ImageZoomText').Text -eq '200%') 'Zoom percentage is wrong'
(Field $drawingEditor 'ImageFlipVertical').IsChecked=$true
$matrix=$surface.GetType().GetProperty('ReferenceTransform',$flags).GetValue($surface,$null)
Check ($matrix.M11 -eq 2 -and $matrix.M22 -eq -2) 'Vertical image reflection is wrong'
(Field $drawingEditor 'ImageFlipHorizontal').IsChecked=$true
$matrix=$surface.GetType().GetProperty('ReferenceTransform',$flags).GetValue($surface,$null)
Check ($matrix.M11 -eq -2 -and $matrix.M22 -eq -2) 'Horizontal image reflection is wrong'
Invoke $drawingEditor 'ResetImageViewClick' @($null,$null) | Out-Null
Invoke $drawingEditor 'RotateImageClick' @($null,$null) | Out-Null
$matrix=$surface.GetType().GetProperty('ReferenceTransform',$flags).GetValue($surface,$null)
Check ($matrix.M11 -eq -1 -and $matrix.M22 -eq -1) '180 degree image rotation is wrong'
Invoke $surface 'PanReference' @(50.0,-25.0) | Out-Null
Check ((Field $surface 'ReferenceOffsetX') -eq 50 -and (Field $surface 'ReferenceOffsetY') -eq -25) 'Image pan did not update'
Invoke $surface 'SetReferenceZoom' @(20.0) | Out-Null
Check ((Field $surface 'ReferenceZoom') -eq 6) 'Image zoom exceeds safe view range'
Check ((Field $drawingEditor 'ImageZoom').Value -eq 6) 'Wheel zoom did not synchronize slider'
$afterViewSnapshot=@((Field $surface 'Points') | ForEach-Object { "$($_.X),$($_.Y)" }) -join ';'
Check ($pointSnapshot -eq $afterViewSnapshot) 'Image transform moved drawn points'
Invoke $drawingEditor 'ResetImageViewClick' @($null,$null) | Out-Null
Check ((Field $surface 'ReferenceZoom') -eq 1 -and (Field $surface 'ReferenceOffsetX') -eq 0 -and (Field $surface 'ReferenceOffsetY') -eq 0) 'Fit image did not reset zoom and pan'
Check (!(Field $surface 'ReferenceRotated') -and !(Field $surface 'ReferenceFlipVertical') -and !(Field $surface 'ReferenceFlipHorizontal')) 'Fit image did not reset reflections'
Check ((Field $drawingEditor 'Invert').IsChecked) 'Recoil tracing does not default to opposite movement'
Invoke $drawingEditor 'RemoveImageClick' @($null,$null) | Out-Null
Check ((Field $surface 'ReferenceImage') -eq $null) 'Reference image not removed'
$beforeDrawing=Field $editor 'draft'
Invoke $editor 'SaveClick' @($null,$null) | Out-Null
Check ((Field $editor 'Status').Text -match 'ainda não aplicado') 'Saving silently ignores unapplied drawing'
(Field $drawingEditor 'DurationInput').Text='2000'
(Field $drawingEditor 'ScaleInput').Text='1'
Invoke $drawingEditor 'ApplyClick' @($null,$null) | Out-Null
$afterDrawing=Field $editor 'draft'
Check (![object]::ReferenceEquals($beforeDrawing,$afterDrawing)) 'Drawing not applied to parent draft'
Check ($afterDrawing.Points.Count -eq 7 -and $afterDrawing.Enabled) 'Drawing did not generate enabled keyframes'
$drawnEnd=Invoke $afterDrawing 'Integrate' @(0.0,2000.0)
Check ($drawnEnd.Vertical -lt 0) 'Compensation does not oppose traced vertical movement'
Check ((Field $editor 'time') -eq 0) 'Applying drawing did not reset preview'
Check (!$drawingEditor.GetType().GetProperty('HasChanges',$flags).GetValue($drawingEditor,$null)) 'Applied drawing remains pending'
Invoke $editor 'UndoClick' @($null,$null) | Out-Null
Check ((Field $editor 'draft').Points.Count -eq $beforeDrawing.Points.Count) 'Parent undo did not restore previous trajectory'
# Invalid conversion must leave the trajectory untouched.
$beforeInvalid=Field $editor 'draft'
(Field $drawingEditor 'DurationInput').Text='bad'
Invoke $drawingEditor 'ApplyClick' @($null,$null) | Out-Null
Check ([object]::ReferenceEquals($beforeInvalid,(Field $editor 'draft'))) 'Invalid drawing replaced trajectory'
(Field $drawingEditor 'DurationInput').Text='2000'
Invoke $drawingEditor 'ApplyClick' @($null,$null) | Out-Null
# Render the actual drawing surface and its controls as a standalone WPF artifact.
$drawContainer=New-Object Windows.Controls.Border
$drawContainer.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#111719')
$drawContainer.Padding=[Windows.Thickness]::new(18)
# Reparent only in this isolated test process.
$drawParent=[Windows.Media.VisualTreeHelper]::GetParent($drawingEditor)
if($drawParent -is [Windows.Controls.ContentControl]) { $drawParent.Content=$null }
elseif($drawParent -is [Windows.Controls.Decorator]) { $drawParent.Child=$null }
else { (Field $editor 'DrawingSection').Content=$null }
$drawContainer.Child=$drawingEditor
$drawContainer.Measure([Windows.Size]::new(1000,1000))
$drawContainer.Arrange([Windows.Rect]::new(0,0,1000,$drawContainer.DesiredSize.Height));$drawContainer.UpdateLayout()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(1000,[int][Math]::Ceiling($drawContainer.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($drawContainer)
$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$file=[IO.File]::Create((Join-Path $root 'build-check\trajectory-drawing.png'));$encoder.Save($file);$file.Dispose()
# Step interpolation is wired through the UI, drawing conversion and intensity adjustment.
(Field $editor 'Interpolation').SelectedIndex=2
$stepDraft=Field $editor 'draft'
Check ($stepDraft.Step -and !$stepDraft.Smooth) 'Step interpolation selector does not update draft'
Check ((Field $editor 'InterpolationHelp').Text -match 'sem rampa') 'Step explanation missing'
Invoke $drawingEditor 'ApplyClick' @($null,$null) | Out-Null
$stepDraft=Field $editor 'draft'
Check ($stepDraft.Step -and $stepDraft.Points.Count -eq (Field $surface 'Points').Count) 'Drawing ignores selected step mode'
$originalVelocity=$stepDraft.Points[0].Vertical
$originalHorizontal=$stepDraft.Points[0].Horizontal
$verticalSnapshot=@($stepDraft.Points | ForEach-Object {$_.Vertical})
(Field $editor 'HorizontalAdjustment').Text='2'
# A stale value in the other field must not be applied by this button.
(Field $editor 'VerticalAdjustment').Text='3'
Invoke $editor 'HorizontalAdjustmentClick' @($null,$null) | Out-Null
$horizontalDraft=Field $editor 'draft'
Check ([Math]::Abs($horizontalDraft.Points[0].Horizontal-2*$originalHorizontal) -lt 0.00001) 'Horizontal UI does not scale horizontal'
for($i=0;$i -lt $verticalSnapshot.Count;$i++) { Check ($horizontalDraft.Points[$i].Vertical -eq $verticalSnapshot[$i]) 'Horizontal UI changes vertical' }
Check ($horizontalDraft.Step) 'Axis adjustment loses interpolation mode'
Check ((Field $editor 'TrajectoryMetrics').Text -match 'Deslocamento total') 'Displacement metrics missing'
Invoke $editor 'UndoClick' @($null,$null) | Out-Null
Check ([Math]::Abs((Field $editor 'draft').Points[0].Horizontal-$originalHorizontal) -lt 0.00001) 'Horizontal undo fails'
foreach($halfFactor in @('0.5','0,5')) {
 (Field $editor 'VerticalAdjustment').Text=$halfFactor
 Invoke $editor 'VerticalAdjustmentClick' @($null,$null) | Out-Null
 Check ([Math]::Abs((Field $editor 'draft').Points[0].Vertical-0.5*$originalVelocity) -lt 0.00001) 'Decimal vertical factor is misread'
 Check ((Field $editor 'draft').Points[0].Horizontal -eq $originalHorizontal) 'Vertical UI changes horizontal'
 Invoke $editor 'UndoClick' @($null,$null) | Out-Null
}
$beforeBadStrength=Field $editor 'draft'
(Field $editor 'HorizontalAdjustment').Text='NaN'
Invoke $editor 'HorizontalAdjustmentClick' @($null,$null) | Out-Null
Check ([object]::ReferenceEquals($beforeBadStrength,(Field $editor 'draft'))) 'Invalid axis factor changes draft'
(Field $editor 'HorizontalAdjustment').Text='1'
Invoke $editor 'HorizontalAdjustmentClick' @($null,$null) | Out-Null
Check ([object]::ReferenceEquals($beforeBadStrength,(Field $editor 'draft'))) 'Factor one needlessly changes draft'
# The explicit repair updates the live runtime snapshot and remains undoable.
$legacyDraft=[Activator]::CreateInstance($trajectoryType,$true)
$legacyDraft.Enabled=$true; $legacyDraft.Smooth=$true
$pointList=[Activator]::CreateInstance([System.Collections.Generic.List[object]].GetGenericTypeDefinition().MakeGenericType($pointType))
foreach($spec in @(@(0,0,0),@(250,100,300),@(500,0,0),@(750,-100,300),@(1000,0,0))) {
 $point=[Activator]::CreateInstance($pointType,$true);$point.TimeMs=$spec[0];$point.Horizontal=$spec[1];$point.Vertical=$spec[2];$pointList.Add($point)
}
$legacyDraft.Points=$pointList
Put $filterEditor 'draft' $legacyDraft
Invoke $filterEditor 'Refresh' @() | Out-Null
$legacyLive=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Invoke $filterEditor 'SmoothPassagesClick' @($null,$null) | Out-Null
$flowingLive=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check ((Invoke $flowingLive.Trajectory 'Evaluate' @(500.0)).Vertical -gt 0) 'Repair does not remove live vertex stop'
Check ((Invoke $legacyLive.Trajectory 'Evaluate' @(500.0)).Vertical -eq 0) 'Repair mutates old runtime snapshot'
Check ($flowingLive.Revision -gt $legacyLive.Revision) 'Repair does not refresh live temporal state'
Invoke $filterEditor 'UndoClick' @($null,$null) | Out-Null
Check ((Invoke (Field $filterEditor 'draft') 'Evaluate' @(500.0)).Vertical -eq 0) 'Undo repair fails'
Invoke $filterEditor 'RestoreClick' @($null,$null) | Out-Null
# Adjustments are reset when another profile/game is loaded.
(Field $filterEditor 'HorizontalAdjustment').Text='3'
(Field $filterEditor 'VerticalAdjustment').Text='2'
(Field $filterEditor 'Games').SelectedItem='Jogo A'
Check ((Field $filterEditor 'HorizontalAdjustment').Text -eq '1') 'Horizontal adjustment carries over between profiles'
Check ((Field $filterEditor 'VerticalAdjustment').Text -eq '1') 'Vertical adjustment carries over between profiles'
# The provider-facing snapshot changes before Save and remains immutable between polls.
$livePreset=Field $filterEditor 'original'
$storePath=$service.GetType().GetProperty('FilePath',$flags).GetValue($service,$null)
$beforeLiveFile=[IO.File]::ReadAllText($storePath)
(Field $filterEditor 'UseTrajectory').IsChecked=$true
(Field $filterEditor 'PointX').Text='20'
(Field $filterEditor 'PointY').Text='40'
$liveBefore=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check ($liveBefore.Enabled -and $liveBefore.Trajectory.Enabled) 'Live draft is not enabled for execution'
Check ($liveBefore.Trajectory.Points[0].Horizontal -eq 20) 'Live provider ignores unsaved edits'
$liveRepeated=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check ([object]::ReferenceEquals($liveBefore.Trajectory,$liveRepeated.Trajectory)) 'Polling recreates trajectory and restarts time'
Check ($liveBefore.Revision -eq $liveRepeated.Revision) 'Polling changes temporal revision'
(Field $filterEditor 'HorizontalAdjustment').Text='2'
Invoke $filterEditor 'HorizontalAdjustmentClick' @($null,$null) | Out-Null
$liveAdjusted=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check ($liveAdjusted.Trajectory.Points[0].Horizontal -eq 40) 'Axis adjustment is not live before Save'
Check ($liveAdjusted.Trajectory.Points[0].Vertical -eq 40) 'Live horizontal adjustment changes vertical'
Check ($liveAdjusted.Revision -gt $liveBefore.Revision) 'Live edit does not reset temporal session'
Check ($liveBefore.Trajectory.Points[0].Horizontal -eq 20) 'Live edit mutates in-flight snapshot'
Check ($livePreset.PrimaryTrajectory -eq $null) 'Live edit mutates saved preset object'
Check ([IO.File]::ReadAllText($storePath) -eq $beforeLiveFile) 'Live testing writes to presets file'
Invoke $filterEditor 'UndoClick' @($null,$null) | Out-Null
$liveUndone=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check ($liveUndone.Trajectory.Points[0].Horizontal -eq 20) 'Undo does not update live execution'
Check ($liveUndone.Revision -gt $liveAdjusted.Revision) 'Undo does not reset temporal session'
$otherWeapon=Invoke $filterEditor 'GetLiveSettings' @($true,$true)
Check ($otherWeapon.Trajectory -eq $null) 'Editing primary leaks into secondary trajectory'
Check ($otherWeapon.VerticalPixelsPerSecond -eq $livePreset.SecondaryVertical) 'Other weapon ignores saved basic values'
$disabledLive=Invoke $filterEditor 'GetLiveSettings' @($false,$false)
Check (!$disabledLive.Enabled) 'Live preview bypasses macro disabled state'
(Field $filterEditor 'DrawingSection').IsExpanded=$true
Check (!(Invoke $filterEditor 'GetLiveSettings' @($true,$false)).Enabled) 'Live execution moves while drawing'
(Field $filterEditor 'DrawingSection').IsExpanded=$false
(Field $filterEditor 'PointX').Text='invalid'
Check (!(Invoke $filterEditor 'GetLiveSettings' @($true,$false)).Enabled) 'Invalid field does not pause live execution'
(Field $filterEditor 'PointX').Text='20'
Check ((Invoke $filterEditor 'GetLiveSettings' @($true,$false)).Enabled) 'Correcting field does not resume live execution'
Invoke $filterEditor 'RestoreClick' @($null,$null) | Out-Null
$restoredLive=Invoke $filterEditor 'GetLiveSettings' @($true,$false)
Check (!$restoredLive.Trajectory.Enabled) 'Restore does not restore saved basic mode'
Check ([IO.File]::ReadAllText($storePath) -eq $beforeLiveFile) 'Restore changes saved presets'
# Selecting points redraws the graph but must not restart the firing timeline.
$revisionBeforeSelection=$restoredLive.Revision
Invoke $filterEditor 'SelectPoint' @(1) | Out-Null
Check ((Invoke $filterEditor 'GetLiveSettings' @($true,$false)).Revision -eq $revisionBeforeSelection) 'Point selection restarts execution'
# Render the adjustment card by itself for visual review.
(Field $editor 'HorizontalAdjustment').Text='1'
(Field $editor 'VerticalAdjustment').Text='1'
$axisCard=(Field $editor 'HorizontalAdjustment').Parent.Parent.Parent
$axisCard.Parent.Children.Remove($axisCard)
$axisContainer=New-Object Windows.Controls.Border
$axisContainer.Background=[Windows.Media.BrushConverter]::new().ConvertFromString('#0C1012')
$axisContainer.Padding=[Windows.Thickness]::new(18)
$axisContainer.Child=$axisCard
$axisContainer.Measure([Windows.Size]::new(800,900))
$axisContainer.Arrange([Windows.Rect]::new(0,0,800,$axisContainer.DesiredSize.Height));$axisContainer.UpdateLayout()
$bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new(800,[int][Math]::Ceiling($axisContainer.ActualHeight),96,96,[Windows.Media.PixelFormats]::Pbgra32)
$bitmap.Render($axisContainer)
$encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
$file=[IO.File]::Create((Join-Path $root 'build-check\axis-adjustment.png'));$encoder.Save($file);$file.Dispose()
Write-Output "WPF editor: $script:checks checks passed; rendered 1100px and 885px layouts."
