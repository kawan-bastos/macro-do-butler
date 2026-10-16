using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal abstract class ButlerDialog : Window
{
    protected ButlerDialog(string title, Window owner)
    {
        Title = title; Owner = owner; Width = 440; SizeToContent = SizeToContent.Height; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize;
        Resources[typeof(Button)] = CreateDialogButtonStyle();
        var shell = new Border { CornerRadius = new CornerRadius(16), BorderBrush = Brush("#413B2E"), BorderThickness = new Thickness(1), Background = Brush("#FF171719"), Padding = new Thickness(22) };
        Content = shell; Root = new StackPanel(); shell.Child = Root;
    }
    protected StackPanel Root { get; private set; }
    protected void Heading(string title, string subtitle)
    {
        Root.Children.Add(new TextBlock { Text = title, Foreground = Brush("#DFC184"), FontSize = 14, FontWeight = FontWeights.SemiBold });
        Root.Children.Add(new TextBlock { Text = subtitle, Foreground = Brush("#B5B1A8"), Margin = new Thickness(0, 6, 0, 14), TextWrapping = TextWrapping.Wrap });
    }
    protected static SolidColorBrush Brush(string value) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)); }
    protected Button AddButton(string label, Action action)
    {
        var button = new Button { Content = label, Height = 48, Margin = new Thickness(0, 6, 0, 0), Background = Brush("#55452A"), Foreground = Brushes.White, BorderThickness = new Thickness(1), FontWeight = FontWeights.SemiBold, Cursor = System.Windows.Input.Cursors.Hand };
        button.Click += (s, e) => action(); Root.Children.Add(button); return button;
    }
    private static Style CreateDialogButtonStyle()
    {
        return new Style(typeof(Button), (Style)Application.Current.FindResource("SecondaryButtonStyle"));
    }

}

internal sealed class GamePresetOrganizerWindow : ButlerDialog
{
    private readonly System.Windows.Controls.ComboBox gameSelector;
    private readonly System.Windows.Controls.TextBox newGameName;
    private readonly System.Windows.Controls.TextBox renameGameName;
    private readonly Button renameGameButton;
    private readonly Button deleteGameButton;
    private readonly Button transferPresetsButton;
    private readonly System.Windows.Controls.ListBox availablePresets;
    private readonly CheckBox selectionToggleButton;
    private readonly System.Windows.Controls.ListBox steamGamesList;
    private readonly TextBlock steamStatus;
    private readonly Dictionary<string, System.Windows.Controls.ListBox> launcherGamesLists = new Dictionary<string, System.Windows.Controls.ListBox>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TextBlock> launcherStatuses = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, bool> launcherSearchDone = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
    private readonly ExternalLauncherGameDiscoveryService externalLauncherDiscovery = new ExternalLauncherGameDiscoveryService();
    private readonly IList<Preset> allPresets;
    private readonly HashSet<string> gamesToAdd = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool steamSearchDone;
    internal string GameName { get; private set; }
    internal IList<Preset> SelectedPresets { get; private set; }
    internal IList<string> GamesToAdd { get { return gamesToAdd.ToList(); } }
    internal bool DeleteGameRequested { get; private set; }
    internal bool RenameGameRequested { get { return !string.IsNullOrWhiteSpace(RenameGameFrom) && !string.IsNullOrWhiteSpace(RenameGameTo); } }
    internal string RenameGameFrom { get; private set; }
    internal string RenameGameTo { get; private set; }
    internal bool TransferPresetsRequested { get; private set; }
    internal string TransferSourceGame { get; private set; }
    internal string TransferTargetGame { get; private set; }

    internal GamePresetOrganizerWindow(Window owner, IList<string> games, IList<Preset> presets, string selectedGame) : base("Editar jogos e presets", owner)
    {
        allPresets=presets;
        Heading("JOGO", "Selecione um jogo existente ou crie um novo. Presets já vinculados a outro jogo não aparecem para movimentação.");
        var tabs=new TabControl{Height=360,Margin=new Thickness(0,0,0,8),Style=(Style)owner.FindResource("DarkTabControlStyle")};
        var editTab=new TabItem{Header="Editar jogos e presets",Style=(Style)owner.FindResource("DarkTabItemStyle")};
        var editorPanel=new StackPanel{Margin=new Thickness(8)};
        gameSelector=new System.Windows.Controls.ComboBox{Style=(Style)owner.FindResource("ComboStyle"),ItemsSource=games};
        editorPanel.Children.Add(gameSelector);
        editorPanel.Children.Add(new TextBlock{Text="Editar nome do jogo selecionado",Foreground=Brush("#B5B1A8"),Margin=new Thickness(0,10,0,5)});
        var renameRow=new Grid();renameRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());renameRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=new GridLength(8)});renameRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=new GridLength(176)});
        renameGameName=new System.Windows.Controls.TextBox{Height=38,Padding=new Thickness(12,0,12,0),Background=Brush("#FF202022"),Foreground=Brush("#F2F0EB"),BorderBrush=Brush("#333335"),CaretBrush=Brush("#DFC184"),VerticalContentAlignment=VerticalAlignment.Center};
        renameRow.Children.Add(renameGameName);
        renameGameButton=new Button{Content="Renomear",Height=48,Background=Brush("#343330"),Foreground=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(1),FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand};Grid.SetColumn(renameGameButton,2);renameRow.Children.Add(renameGameButton);editorPanel.Children.Add(renameRow);
        editorPanel.Children.Add(new TextBlock{Text="Criar novo jogo",Foreground=Brush("#B5B1A8"),Margin=new Thickness(0,14,0,5)});
        var createRow=new Grid();createRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());createRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=new GridLength(8)});createRow.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=new GridLength(176)});
        newGameName=new System.Windows.Controls.TextBox{Height=38,Padding=new Thickness(12,0,12,0),Background=Brush("#FF202022"),Foreground=Brush("#F2F0EB"),BorderBrush=Brush("#333335"),CaretBrush=Brush("#DFC184"),VerticalContentAlignment=VerticalAlignment.Center};
        createRow.Children.Add(newGameName);
        var createButton=new Button{Content="Criar jogo",Height=48,Background=Brush("#55452A"),Foreground=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(1),FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand};Grid.SetColumn(createButton,2);createRow.Children.Add(createButton);editorPanel.Children.Add(createRow);
        var presetHeader=new Grid{Margin=new Thickness(0,14,0,5)};
        presetHeader.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
        presetHeader.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=GridLength.Auto});
        presetHeader.Children.Add(new TextBlock{Text="Presets sem jogo",Foreground=Brush("#B5B1A8"),VerticalAlignment=VerticalAlignment.Center});
        selectionToggleButton=new CheckBox{Content="Selecionar tudo",Style=(Style)owner.FindResource("ModernCheckStyle"),Foreground=Brush("#DFC184"),FontSize=12,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0)};
        Grid.SetColumn(selectionToggleButton,1);presetHeader.Children.Add(selectionToggleButton);editorPanel.Children.Add(presetHeader);
        availablePresets=new System.Windows.Controls.ListBox{Background=Brush("#FF202022"),Foreground=System.Windows.Media.Brushes.White,BorderBrush=Brush("#333335"),MinHeight=45,MaxHeight=125};System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(availablePresets,System.Windows.Controls.ScrollBarVisibility.Auto);editorPanel.Children.Add(availablePresets);
        editTab.Content=editorPanel;

        var recognitionTab=new TabItem{Header="Reconhecer jogos automaticamente",Style=(Style)owner.FindResource("DarkTabItemStyle")};
        var recognitionPanel=new StackPanel{Margin=new Thickness(0)};
        var launcherTabs=new TabControl{Style=(Style)owner.FindResource("DarkTabControlStyle")};
        var steamTab=new TabItem{Header="Steam",Tag="steam",Style=(Style)owner.FindResource("DarkTabItemStyle")};
        var steamPanel=new StackPanel{Margin=new Thickness(8)};
        steamPanel.Children.Add(new TextBlock{Text="Jogos instalados pela Steam",Foreground=Brush("#B5B1A8"),Margin=new Thickness(0,0,0,6)});
        steamStatus=new TextBlock{Text="Abra esta aba para procurar jogos instalados nas bibliotecas Steam deste computador.",Foreground=Brush("#A5A29B"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
        steamPanel.Children.Add(steamStatus);
        steamGamesList=new System.Windows.Controls.ListBox{Background=Brush("#FF202022"),Foreground=System.Windows.Media.Brushes.White,BorderBrush=Brush("#333335"),MinHeight=80,MaxHeight=230};
        System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(steamGamesList,System.Windows.Controls.ScrollBarVisibility.Auto);
        steamPanel.Children.Add(steamGamesList);
        steamPanel.Children.Add(new TextBlock{Text="Marque os jogos que deseja adicionar e clique em APLICAR.",Foreground=Brush("#A5A29B"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
        steamTab.Content=steamPanel;
        launcherTabs.Items.Add(steamTab);
        AddRecognitionLauncherTab(launcherTabs,owner,"rockstar","Rockstar","Jogos instalados pelo Rockstar Games Launcher.");
        AddRecognitionLauncherTab(launcherTabs,owner,"ubisoft","Ubisoft","Jogos instalados pelo Ubisoft Connect.");
        AddRecognitionLauncherTab(launcherTabs,owner,"epic","Epic Games","Jogos instalados pela Epic Games Store.");
        recognitionPanel.Children.Add(launcherTabs);
        recognitionPanel.Children.Add(new TextBlock{Text="Marque os jogos que deseja adicionar e clique em APLICAR.",Foreground=Brush("#A5A29B"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8,0,8,6)});
        recognitionTab.Content=recognitionPanel;
        tabs.Items.Add(editTab);tabs.Items.Add(recognitionTab);Root.Children.Add(tabs);
        createButton.Click+=(s,e)=>CreateGame();
        renameGameButton.Click+=(s,e)=>StageGameRename();
        selectionToggleButton.Click+=(s,e)=>TogglePresetSelection();
        gameSelector.SelectionChanged+=(s,e)=>{RefreshAvailable();if(string.IsNullOrWhiteSpace(RenameGameFrom))renameGameName.Text=gameSelector.SelectedItem as string??string.Empty;};
        launcherTabs.SelectionChanged+=(s,e)=>
        {
            if(!ReferenceEquals(e.OriginalSource,launcherTabs))return;
            var selected=launcherTabs.SelectedItem as TabItem;
            string launcher=selected==null?null:selected.Tag as string;
            if(string.Equals(launcher,"steam",StringComparison.OrdinalIgnoreCase))SearchSteamGames();
            else if(!string.IsNullOrWhiteSpace(launcher))SearchExternalLauncherGames(launcher);
        };
        tabs.SelectionChanged+=(s,e)=>{if(ReferenceEquals(tabs.SelectedItem,recognitionTab)){if(ReferenceEquals(launcherTabs.SelectedItem,steamTab))SearchSteamGames();else{var selected=launcherTabs.SelectedItem as TabItem;if(selected!=null)SearchExternalLauncherGames(selected.Tag as string);}}};
        gameSelector.SelectedItem=games.Contains(selectedGame,StringComparer.OrdinalIgnoreCase)?selectedGame:(games.Count>0?games[0]:null);
        RefreshAvailable();
        var gameActions=new Grid{Margin=new Thickness(0,6,0,0)};
        gameActions.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());gameActions.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition{Width=new GridLength(8)});gameActions.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());
        transferPresetsButton=new Button{Content="Transferir presets",Height=48,Background=Brush("#343330"),Foreground=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(1),FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand};
        transferPresetsButton.Click+=(s,e)=>ChoosePresetTransfer();gameActions.Children.Add(transferPresetsButton);
        deleteGameButton=new Button{Content="Excluir jogo",Height=48,Background=Brush("#55452A"),Foreground=System.Windows.Media.Brushes.White,BorderThickness=new Thickness(1),FontWeight=FontWeights.SemiBold,Cursor=System.Windows.Input.Cursors.Hand};
        deleteGameButton.Click+=(s,e)=>DeleteSelectedGame();Grid.SetColumn(deleteGameButton,2);gameActions.Children.Add(deleteGameButton);Root.Children.Add(gameActions);
        AddButton("Aplicar",Save);AddButton("Cancelar",()=>{DialogResult=false;});
    }

    private void AddRecognitionLauncherTab(TabControl tabs,Window owner,string key,string header,string description)
    {
        var tab=new TabItem{Header=header,Tag=key,Style=(Style)owner.FindResource("DarkTabItemStyle")};
        var panel=new StackPanel{Margin=new Thickness(8)};
        panel.Children.Add(new TextBlock{Text=description,Foreground=Brush("#B5B1A8"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,6)});
        var status=new TextBlock{Text="Abra esta aba para procurar jogos instalados.",Foreground=Brush("#A5A29B"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)};
        var list=new System.Windows.Controls.ListBox{Background=Brush("#FF202022"),Foreground=System.Windows.Media.Brushes.White,BorderBrush=Brush("#333335"),MinHeight=80,MaxHeight=215};
        System.Windows.Controls.ScrollViewer.SetVerticalScrollBarVisibility(list,System.Windows.Controls.ScrollBarVisibility.Auto);
        panel.Children.Add(status);panel.Children.Add(list);tab.Content=panel;tabs.Items.Add(tab);
        launcherStatuses[key]=status;launcherGamesLists[key]=list;launcherSearchDone[key]=false;
    }

    private void CreateGame()
    {
        if(RenameGameRequested){MessageBox.Show(this,"Aplique ou cancele a alteração do nome antes de criar outro jogo.","Edição em andamento",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(TransferPresetsRequested){MessageBox.Show(this,"Aplique ou cancele a transferência antes de criar outro jogo.","Transferência em andamento",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        string value=newGameName.Text.Trim();
        if(value.Length==0){MessageBox.Show(this,"Digite o nome do jogo.","Jogo obrigatório",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(PresetService.IsUncategorized(value)){MessageBox.Show(this,"“Não categorizado” é reservado para presets importados sem jogo.","Nome reservado",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        var games=new List<string>();foreach(object item in gameSelector.Items)games.Add(item.ToString());
        if(!games.Any(g=>g.Equals(value,StringComparison.OrdinalIgnoreCase)))games.Add(value);
        gameSelector.ItemsSource=games;gameSelector.SelectedItem=value;newGameName.Clear();
    }
    private void StageGameRename()
    {
        if(TransferPresetsRequested){MessageBox.Show(this,"Aplique ou cancele a transferência antes de renomear um jogo.","Transferência em andamento",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        string oldName=gameSelector.SelectedItem as string;
        string newName=renameGameName.Text.Trim();
        if(string.IsNullOrWhiteSpace(oldName)){MessageBox.Show(this,"Selecione um jogo existente para editar.","Jogo obrigatório",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(string.IsNullOrWhiteSpace(newName)){MessageBox.Show(this,"Digite o novo nome do jogo.","Nome obrigatório",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(PresetService.IsUncategorized(oldName)||PresetService.IsUncategorized(newName)){MessageBox.Show(this,"“Não categorizado” não pode ser renomeado.","Nome reservado",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(oldName.Equals(newName,StringComparison.OrdinalIgnoreCase))return;
        if(gameSelector.Items.Cast<object>().Any(item=>item.ToString().Equals(newName,StringComparison.OrdinalIgnoreCase))){MessageBox.Show(this,"Já existe um jogo com esse nome.","Nome duplicado",MessageBoxButton.OK,MessageBoxImage.Warning);return;}

        RenameGameFrom=oldName;RenameGameTo=newName;
        List<string> games=gameSelector.Items.Cast<object>().Select(item=>item.ToString()).Select(name=>name.Equals(oldName,StringComparison.OrdinalIgnoreCase)?newName:name).ToList();
        gameSelector.ItemsSource=games;gameSelector.SelectedItem=newName;renameGameName.Text=newName;
        gameSelector.IsEnabled=false;renameGameName.IsEnabled=false;renameGameButton.IsEnabled=false;renameGameButton.Content="Nome alterado";deleteGameButton.IsEnabled=false;
    }
    private void ChoosePresetTransfer()
    {
        if(RenameGameRequested){MessageBox.Show(this,"Aplique ou cancele a alteração de nome antes de transferir presets.","Edição em andamento",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        IList<string> games=gameSelector.Items.Cast<object>().Select(item=>item.ToString()).Where(name=>!PresetService.IsUncategorized(name)).ToList();
        // This dialog is launched while this editor is already modal to the main
        // window. Keep it ownerless to avoid a conflicting modal-owner chain,
        // but position it over the main window instead of at screen coordinate 0,0.
        var transfer=new PresetTransferWindow(null,Owner,games,allPresets,gameSelector.SelectedItem as string);
        transfer.WindowStartupLocation=WindowStartupLocation.Manual;
        double dialogHeight=360;
        double workLeft=SystemParameters.VirtualScreenLeft,workTop=SystemParameters.VirtualScreenTop;
        double workRight=workLeft+SystemParameters.VirtualScreenWidth,workBottom=workTop+SystemParameters.VirtualScreenHeight;
        double desiredLeft=Owner.Left+(Owner.ActualWidth-transfer.Width)/2;
        double desiredTop=Owner.Top+(Owner.ActualHeight-dialogHeight)/2;
        transfer.Left=Math.Max(workLeft,Math.Min(desiredLeft,workRight-transfer.Width));
        transfer.Top=Math.Max(workTop,Math.Min(desiredTop,workBottom-dialogHeight));
        bool accepted=false;
        WindowState previousState=WindowState;
        WindowState=WindowState.Minimized;
        try{accepted=transfer.ShowDialog()==true;}
        finally{WindowState=previousState;Activate();}
        if(!accepted)return;
        TransferSourceGame=transfer.SourceGame;TransferTargetGame=transfer.TargetGame;TransferPresetsRequested=true;
        gameSelector.SelectedItem=TransferTargetGame;
        transferPresetsButton.Content="Transferido";transferPresetsButton.IsEnabled=false;
        deleteGameButton.IsEnabled=false;
    }
    private async void SearchSteamGames()
    {
        if(steamSearchDone)return;
        steamSearchDone=true;steamStatus.Text="Procurando jogos instalados pela Steam...";System.Windows.Input.Mouse.OverrideCursor=System.Windows.Input.Cursors.Wait;
        try
        {
            SteamGameDiscoveryResult result=await System.Threading.Tasks.Task.Run(()=>new SteamGameDiscoveryService().FindInstalledGames());
            IList<SteamInstalledGame> games=result.Games;
            steamGamesList.Items.Clear();
            foreach(SteamInstalledGame game in games)
            {
                bool alreadyAdded=IsGameAlreadyAdded(game.Name);
                var check=new CheckBox{Content=game.Name+(alreadyAdded?" (já adicionado)":string.Empty),Tag=game,Style=(Style)Owner.FindResource("ModernCheckStyle"),Margin=new Thickness(5,4,5,4),ToolTip=alreadyAdded?"Este jogo já está cadastrado nos presets.":game.InstallDirectory,IsChecked=alreadyAdded,IsEnabled=!alreadyAdded};
                steamGamesList.Items.Add(check);
            }
            steamStatus.Text=games.Count==0
                ?(result.SomeAppsCouldNotBeClassified?"Não foi possível confirmar os jogos pelos metadados locais da Steam.":"Nenhum jogo instalado da Steam foi encontrado.")
                :games.Count+" jogo(s) encontrado(s)."+(result.SomeAppsCouldNotBeClassified?" Alguns itens não puderam ser confirmados.":" Selecione os que quer adicionar aos jogos do macro.");
            if(games.Count==0)steamGamesList.Items.Add(new ListBoxItem{Content="Nenhum jogo encontrado",Foreground=Brush("#A5A29B"),Background=System.Windows.Media.Brushes.Transparent});
        }
        catch(Exception ex){steamSearchDone=false;steamStatus.Text="Não foi possível pesquisar os jogos da Steam: "+ex.Message;}
        finally{System.Windows.Input.Mouse.OverrideCursor=null;}
    }
    private IList<string> StageSelectedSteamGames()
    {
        List<string> selected=steamGamesList.Items.OfType<CheckBox>().Where(c=>c.IsChecked==true).Select(c=>(c.Tag as SteamInstalledGame)?.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(selected.Count==0)return selected;
        var games=gameSelector.Items.Cast<object>().Select(item=>item.ToString()).ToList();
        foreach(string name in selected){gamesToAdd.Add(name);if(!games.Any(g=>g.Equals(name,StringComparison.OrdinalIgnoreCase)))games.Add(name);}
        string current=gameSelector.SelectedItem as string;
        gameSelector.ItemsSource=games;
        gameSelector.SelectedItem=string.IsNullOrWhiteSpace(current)?selected[0]:current;
        return selected;
    }
    private async void SearchExternalLauncherGames(string launcher)
    {
        System.Windows.Controls.ListBox list;
        TextBlock status;
        if(string.IsNullOrWhiteSpace(launcher)||!launcherGamesLists.TryGetValue(launcher,out list)||!launcherStatuses.TryGetValue(launcher,out status))return;
        if(launcherSearchDone[launcher])return;
        launcherSearchDone[launcher]=true;status.Text="Procurando jogos instalados...";System.Windows.Input.Mouse.OverrideCursor=System.Windows.Input.Cursors.Wait;
        try
        {
            LauncherGameDiscoveryResult result=await System.Threading.Tasks.Task.Run(()=>externalLauncherDiscovery.FindGames(launcher));
            list.Items.Clear();
            foreach(DetectedLauncherGame game in result.Games)
            {
                bool alreadyAdded=IsGameAlreadyAdded(game.Name);
                var check=new CheckBox{Content=game.Name+(alreadyAdded?" (já adicionado)":string.Empty),Tag=game,Style=(Style)Owner.FindResource("ModernCheckStyle"),Margin=new Thickness(5,4,5,4),ToolTip=alreadyAdded?"Este jogo já está cadastrado nos presets.":game.InstallDirectory,IsChecked=alreadyAdded,IsEnabled=!alreadyAdded};
                list.Items.Add(check);
            }
            if(result.Games.Count==0)
            {
                status.Text=!result.LauncherDataFound
                    ?"Não foi possível localizar os dados locais desse launcher neste computador."
                    :"Nenhum jogo instalado foi confirmado nesse launcher."+(result.EntriesSkipped>0?" Alguns registros não puderam ser lidos.":string.Empty);
                list.Items.Add(new ListBoxItem{Content="Nenhum jogo encontrado",Foreground=Brush("#A5A29B"),Background=System.Windows.Media.Brushes.Transparent});
            }
            else status.Text=result.Games.Count+" jogo(s) encontrado(s)."+(result.EntriesSkipped>0?" Alguns registros não puderam ser lidos.":" Marque os que deseja adicionar.");
        }
        catch(Exception ex){launcherSearchDone[launcher]=false;status.Text="Não foi possível pesquisar os jogos: "+ex.Message;}
        finally{System.Windows.Input.Mouse.OverrideCursor=null;}
    }
    private bool IsGameAlreadyAdded(string gameName)
    {
        return !string.IsNullOrWhiteSpace(gameName)&&gameSelector.Items.Cast<object>().Any(item=>string.Equals(item.ToString(),gameName,StringComparison.OrdinalIgnoreCase));
    }
    private IList<string> StageSelectedExternalGames(string launcher)
    {
        System.Windows.Controls.ListBox list;
        if(!launcherGamesLists.TryGetValue(launcher,out list))return new List<string>();
        List<string> selected=list.Items.OfType<CheckBox>().Where(c=>c.IsChecked==true).Select(c=>(c.Tag as DetectedLauncherGame)?.Name).Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if(selected.Count==0)return selected;
        var games=gameSelector.Items.Cast<object>().Select(item=>item.ToString()).ToList();
        foreach(string name in selected){gamesToAdd.Add(name);if(!games.Any(g=>g.Equals(name,StringComparison.OrdinalIgnoreCase)))games.Add(name);}
        string current=gameSelector.SelectedItem as string;
        gameSelector.ItemsSource=games;
        gameSelector.SelectedItem=string.IsNullOrWhiteSpace(current)?selected[0]:current;
        return selected;
    }
    private void RefreshAvailable()
    {
        availablePresets.Items.Clear();
        foreach(Preset preset in allPresets.Where(p=>p.GameName.Equals("Não categorizado",StringComparison.OrdinalIgnoreCase)))
        {
            var check=new System.Windows.Controls.CheckBox{Content=preset.Name,Tag=preset,Style=(Style)Owner.FindResource("ModernCheckStyle"),Margin=new Thickness(5,4,5,4),ToolTip="Preset antigo sem jogo atribuído"};
            check.Checked+=(s,e)=>UpdateSelectionToggleText();check.Unchecked+=(s,e)=>UpdateSelectionToggleText();availablePresets.Items.Add(check);
        }
        if(availablePresets.Items.Count==0)availablePresets.Items.Add(new System.Windows.Controls.ListBoxItem{Content="Nenhum preset sem jogo para adicionar",Foreground=Brush("#A5A29B"),Background=System.Windows.Media.Brushes.Transparent});
        selectionToggleButton.IsEnabled=availablePresets.Items.OfType<System.Windows.Controls.CheckBox>().Any();
        UpdateSelectionToggleText();
    }
    private void TogglePresetSelection()
    {
        var checks=availablePresets.Items.OfType<System.Windows.Controls.CheckBox>().ToList();
        bool selectAll=selectionToggleButton.IsChecked==true;
        foreach(var check in checks)check.IsChecked=selectAll;
        UpdateSelectionToggleText();
    }
    private void UpdateSelectionToggleText()
    {
        if(selectionToggleButton==null)return;
        var checks=availablePresets.Items.OfType<System.Windows.Controls.CheckBox>().ToList();
        bool allSelected=checks.Count>0&&checks.All(c=>c.IsChecked==true);
        selectionToggleButton.Content=allSelected?"Remover seleção":"Selecionar tudo";
        selectionToggleButton.IsChecked=allSelected;
    }
    private void Save()
    {
        IList<string> selectedSteamGames=StageSelectedSteamGames();
        selectedSteamGames=selectedSteamGames.Concat(StageSelectedExternalGames("rockstar"))
            .Concat(StageSelectedExternalGames("ubisoft"))
            .Concat(StageSelectedExternalGames("epic")).ToList();
        GameName=gameSelector.SelectedItem as string;
        if(string.IsNullOrWhiteSpace(GameName)&&selectedSteamGames.Count>0)GameName=selectedSteamGames[0];
        if(string.IsNullOrWhiteSpace(GameName)){MessageBox.Show(this,"Selecione ou crie um jogo.","Jogo obrigatório",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        SelectedPresets=availablePresets.Items.OfType<System.Windows.Controls.CheckBox>().Where(c=>c.IsChecked==true).Select(c=>c.Tag as Preset).Where(p=>p!=null).ToList();
        DialogResult=true;
    }
    private void DeleteSelectedGame()
    {
        if(TransferPresetsRequested){MessageBox.Show(this,"Aplique ou cancele a transferência antes de excluir um jogo.","Transferência em andamento",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        string selected=gameSelector.SelectedItem as string;
        if(string.IsNullOrWhiteSpace(selected)){MessageBox.Show(this,"Selecione um jogo para excluir.","Jogo obrigatório",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(PresetService.IsUncategorized(selected)){MessageBox.Show(this,"“Não categorizado” desaparece automaticamente quando todos os presets importados forem vinculados ou excluídos.","Categoria automática",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        if(allPresets.Any(p=>p.GameName.Equals(selected,StringComparison.OrdinalIgnoreCase))){MessageBox.Show(this,"Este jogo ainda tem presets. Exclua os presets vinculados antes de excluir o jogo.","Jogo com presets",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(MessageBox.Show(this,"Excluir o jogo “"+selected+"”?","Excluir jogo",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        GameName=selected;DeleteGameRequested=true;DialogResult=true;
    }
}

internal sealed class PresetTransferWindow : ButlerDialog
{
    private readonly System.Windows.Controls.ComboBox sourceSelector;
    private readonly System.Windows.Controls.ComboBox targetSelector;
    private readonly TextBlock preview;
    private readonly IList<Preset> presets;
    private readonly IList<string> games;
    internal string SourceGame { get; private set; }
    internal string TargetGame { get; private set; }

    internal PresetTransferWindow(Window windowOwner,Window resourceOwner,IList<string> availableGames,IList<Preset> allPresets,string selectedGame) : base("Transferir presets",windowOwner)
    {
        games=availableGames.Where(game=>!string.IsNullOrWhiteSpace(game)&&!PresetService.IsUncategorized(game)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        presets=allPresets;
        Heading("TRANSFERIR PRESETS","Mova todos os presets de um jogo para outro. O jogo de origem continuará cadastrado.");
        Root.Children.Add(new TextBlock{Text="Jogo de origem",Foreground=Brush("#B5B1A8"),Margin=new Thickness(0,0,0,5)});
        sourceSelector=new System.Windows.Controls.ComboBox{Style=(Style)resourceOwner.FindResource("ComboStyle"),ItemsSource=games};Root.Children.Add(sourceSelector);
        Root.Children.Add(new TextBlock{Text="Jogo de destino",Foreground=Brush("#B5B1A8"),Margin=new Thickness(0,12,0,5)});
        targetSelector=new System.Windows.Controls.ComboBox{Style=(Style)resourceOwner.FindResource("ComboStyle"),ItemsSource=games};Root.Children.Add(targetSelector);
        preview=new TextBlock{Foreground=Brush("#A5A29B"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,6)};Root.Children.Add(preview);
        sourceSelector.SelectionChanged+=(s,e)=>{EnsureDifferentTarget();RefreshPreview();};
        targetSelector.SelectionChanged+=(s,e)=>RefreshPreview();
        sourceSelector.SelectedItem=games.Contains(selectedGame,StringComparer.OrdinalIgnoreCase)?selectedGame:games.FirstOrDefault();
        EnsureDifferentTarget();RefreshPreview();
        AddButton("Transferir presets",ConfirmTransfer);AddButton("Cancelar",()=>{DialogResult=false;});
    }

    private void EnsureDifferentTarget()
    {
        string source=sourceSelector.SelectedItem as string;
        string target=targetSelector.SelectedItem as string;
        if(string.IsNullOrWhiteSpace(target)||string.Equals(source,target,StringComparison.OrdinalIgnoreCase))
            targetSelector.SelectedItem=games.FirstOrDefault(game=>!game.Equals(source,StringComparison.OrdinalIgnoreCase));
    }

    private IList<Preset> SourcePresets()
    {
        string source=sourceSelector.SelectedItem as string;
        return presets.Where(p=>source!=null&&p.GameName.Equals(source,StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private IList<string> ConflictingNames()
    {
        string target=targetSelector.SelectedItem as string;
        HashSet<string> targetNames=new HashSet<string>(presets.Where(p=>target!=null&&p.GameName.Equals(target,StringComparison.OrdinalIgnoreCase)).Select(p=>p.Name),StringComparer.OrdinalIgnoreCase);
        return SourcePresets().Where(p=>targetNames.Contains(p.Name)).Select(p=>p.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private void RefreshPreview()
    {
        if(preview==null)return;
        int count=SourcePresets().Count;
        IList<string> conflicts=ConflictingNames();
        preview.Text=count==0?"O jogo de origem não tem presets para transferir.":count+" preset(s) serão movidos."+(conflicts.Count>0?" Há "+conflicts.Count+" nome(s) repetido(s) no destino; renomeie-os antes da transferência.":string.Empty);
    }

    private void ConfirmTransfer()
    {
        SourceGame=sourceSelector.SelectedItem as string;TargetGame=targetSelector.SelectedItem as string;
        if(string.IsNullOrWhiteSpace(SourceGame)||string.IsNullOrWhiteSpace(TargetGame)){MessageBox.Show(this,"Selecione os jogos de origem e destino.","Jogos obrigatórios",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(SourceGame.Equals(TargetGame,StringComparison.OrdinalIgnoreCase)){MessageBox.Show(this,"Escolha jogos diferentes para origem e destino.","Destino inválido",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        if(SourcePresets().Count==0){MessageBox.Show(this,"O jogo de origem não possui presets para transferir.","Nenhum preset",MessageBoxButton.OK,MessageBoxImage.Information);return;}
        IList<string> conflicts=ConflictingNames();
        if(conflicts.Count>0){MessageBox.Show(this,"O destino já possui preset(s) com estes nomes: "+string.Join(", ",conflicts)+". Renomeie-os e tente novamente.","Nomes repetidos",MessageBoxButton.OK,MessageBoxImage.Warning);return;}
        DialogResult=true;
    }
}

internal sealed class DuplicateChoiceWindow : ButlerDialog
{
    internal DuplicatePresetMode Mode { get; private set; }
    internal DuplicateChoiceWindow(Window owner) : base("Importar presets", owner)
    {
        Heading("PRESETS EXISTENTES", "O arquivo pode conter categorias que já existem. Escolha como tratar esses itens.");
        AddButton("Substituir com o arquivo", () => { Mode = DuplicatePresetMode.Replace; DialogResult = true; });
        AddButton("Manter os atuais", () => { Mode = DuplicatePresetMode.Ignore; DialogResult = true; });
        AddButton("Cancelar", () => { DialogResult = false; });
    }
}

internal sealed class BackupChooserWindow : ButlerDialog
{
    internal PresetBackupInfo SelectedBackup { get; private set; }
    internal BackupChooserWindow(IList<PresetBackupInfo> backups, Window owner) : base("Histórico de backups", owner)
    {
        Heading("HISTÓRICO DE BACKUPS", "Selecione um estado salvo para restaurar.");
        var list = new ListBox { Background = Brush("#FF202022"), Foreground = Brushes.White, BorderBrush = Brush("#333335"), MaxHeight = 270, MinHeight = 76, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        foreach (PresetBackupInfo backup in backups)
        {
            var item = new ListBoxItem { Content = new TextBlock { Text = backup.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture) + "   ·   " + Path.GetFileName(backup.FilePath), Foreground = Brushes.White, Padding = new Thickness(7, 3, 7, 3), TextWrapping = TextWrapping.Wrap }, Tag = backup, Padding = new Thickness(4, 6, 4, 6), Background = Brushes.Transparent };
            list.Items.Add(item);
        }
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        Root.Children.Add(list);
        AddButton("Selecionar backup", () => { ListBoxItem selected = list.SelectedItem as ListBoxItem; SelectedBackup = selected == null ? null : selected.Tag as PresetBackupInfo; if (SelectedBackup != null) DialogResult = true; });
        AddButton("Cancelar", () => { DialogResult = false; });
    }
}
