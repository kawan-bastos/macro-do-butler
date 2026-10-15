# Macro do Butler

Aplicativo desktop para Windows, feito em WPF com .NET Framework 4.8. O macro aplica movimentos relativos do mouse usando perfis configuráveis e atalhos globais.

## Começar

Na janela principal, escolha um jogo e um preset. Os controles exibem os valores do perfil ativo. Clique em **Salvar** para criar ou atualizar o preset; **Excluir** remove o perfil selecionado após confirmação.

Os perfis mantêm valores separados para arma primária e secundária. Use o botão de alternância ou `F2` para escolher qual conjunto está ativo. Os ícones `?` ao lado dos controles mostram explicações ao passar o mouse.

## Configurações

Abra a engrenagem no topo da janela. As opções são organizadas em três abas:

- **Atalhos:** altera os atalhos globais e restaura os valores padrão.
- **Macro:** configura o intervalo de atualização e se o botão direito também precisa estar pressionado.
- **Dados e backup:** importa, exporta e restaura presets.

### Intervalo global

O intervalo é compartilhado por todos os presets e permanece selecionado ao trocar de perfil. O valor permitido é de 2 a 50 ms; o padrão é 7 ms. A preferência é salva em `%LOCALAPPDATA%\MacroDoButler\macro-settings.json`.

### Atalhos globais

| Padrão | Ação |
|---|---|
| `F1` | Ligar ou desligar o macro |
| `F2` | Alternar entre arma primária e secundária |
| `F12` | Fechar o aplicativo |
| Desativado | Mostrar ou minimizar a janela |

Os atalhos continuam ativos quando a janela está minimizada ou outro aplicativo está em primeiro plano; não dependem do Logitech G HUB. Em **Atalhos**, clique em **Alterar**, pressione a combinação desejada e confirme com **Enter**. `Esc` cancela a captura. `Backspace` ou `Delete` desativa o atalho opcional de mostrar a janela. Os três atalhos principais são obrigatórios. Se uma combinação já estiver em uso por outra ação, o aplicativo oferece substituir o atalho anterior; também avisa quando o Windows não consegue registrar uma combinação.

As preferências ficam em `%LOCALAPPDATA%\MacroDoButler\hotkeys.json`. Se o Windows impedir a gravação nesse local, o aplicativo tenta salvar o arquivo ao lado do executável. Se um arquivo existente estiver inválido, os padrões são carregados e o arquivo é preservado.

## Ajustes do macro

- **Força vertical:** velocidade do movimento vertical, de 0 a 10.000 px/s.
- **Correção lateral:** movimento horizontal, de -5.000 a 5.000 px/s; valores negativos movem para a esquerda e positivos para a direita.
- **Exigir botão direito pressionado:** quando ativado, o macro só atua enquanto os botões esquerdo e direito estiverem pressionados.

Os valores das armas e a opção do botão direito pertencem ao preset. O intervalo, por outro lado, é geral e compartilhado por todos eles.

## Presets, importação e backups

O arquivo ativo é `presets.json`, ao lado do executável. Ele guarda jogos, presets, valores e datas; a gravação usa um arquivo temporário antes de substituir o original. A pasta `backups\` mantém até 10 cópias recentes.

Na primeira inicialização sem `presets.json`, o programa pode migrar `presets.cfg` nos formatos legados de cinco ou sete campos. Os registros migram para o jogo **Não categorizado**, e uma cópia chamada `presets.cfg.migration.bak` é criada antes da conversão. O CFG original é preservado.

**Exportar presets** salva uma cópia completa em JSON. **Importar presets** aceita JSON e CFG legado, valida os registros e permite substituir ou ignorar itens duplicados por jogo e nome. A importação cria um backup antes de aplicar as alterações. **Restaurar backup** valida a cópia selecionada e também salva o estado atual antes de restaurar.

## Compilar no Windows

Requisitos: Windows e Visual Studio Build Tools com suporte ao .NET Framework 4.8, Visual Studio 2022 ou o SDK do .NET com suporte ao projeto.

No PowerShell, na pasta do projeto, execute:

```powershell
.\build.ps1
```

O executável e o arquivo de configuração são copiados para `dist\`. Também é possível compilar diretamente:

```powershell
dotnet build MacroDoButler.csproj -c Release
```

## Atualizações

Abra **Configurações → ATUALIZAÇÕES** e escolha **VERIFICAR ATUALIZAÇÕES**. Com a verificação automática ativada (padrão), o aplicativo consulta uma vez o GitHub Releases ao iniciar, em segundo plano, sem bloquear o macro ou baixar arquivos. Uma versão encontrada aparece como indicador nessa seção. Use **BAIXAR ATUALIZAÇÃO** e, após a validação do executável e do SHA-256 publicado, **INSTALAR AGORA**. O download pode ser cancelado enquanto está em andamento; uma falha de rede mantém a instalação atual.

Antes de instalar, o atualizador cria um diretório `%LOCALAPPDATA%\MacroDoButler\backups\update_...` com `backup-manifest.json` e cópias verificadas dos presets, atalhos, configurações e backups locais. Os dados originais não são movidos. A nova versão é instalada por um auxiliar após o fechamento do aplicativo. Se a substituição ou a inicialização falhar, o auxiliar restaura a cópia anterior. Em `Program Files`, o Windows solicita consentimento UAC quando necessário. Se a permissão for negada, a instalação é cancelada e o executável atual continua no lugar.

Os presets ativos ficam em `presets.json` junto do executável; formatos antigos como `presets.cfg` são mantidos, e os backups ficam em `backups\`. Atalhos e intervalo ficam em `%LOCALAPPDATA%\MacroDoButler\hotkeys.json` e `macro-settings.json`. O log opcional de diagnóstico do atualizador fica em `%LOCALAPPDATA%\MacroDoButler\update.log`. Para restaurar presets, use **Configurações → DADOS E BACKUP → RESTAURAR BACKUP** ou copie os arquivos do diretório de backup após fechar o aplicativo. Os dados pessoais nunca são empacotados como assets de release.

A versão aparece em **Configurações → ATUALIZAÇÕES**. Releases estáveis usam tags `vMAJOR.MINOR.PATCH` e assets `Macro.do.Butler.exe` e `Macro.do.Butler.sha256`; o workflow publica esses assets quando uma tag é criada. A interface não exibe o endereço do repositório ou da release. O atualizador não depende do Logitech G HUB.

## Estrutura do projeto

- `MainWindow.xaml` e `MainWindow.xaml.cs`: interface, presets e integração do macro.
- `Themes/Controls.xaml`: estilos e controles visuais do tema.
- `src/Models/Preset.cs`: modelo dos presets.
- `src/Services/PresetService.cs`: leitura, migração e gravação dos presets.
- `src/Services/PresetOrganizationService.cs`: organização e validação dos presets.
- `src/Services/PresetBackupService.cs` e `PresetTransferService.cs`: backups e importação/exportação.
- `src/Services/HotkeyService.cs`, `HotkeyParser.cs` e `HotkeyStorageService.cs`: registro, validação e persistência dos atalhos globais.
- `src/Services/MacroSettingsStorageService.cs`: persistência do intervalo geral.
- `src/Services/UpdateService.cs`, `UpdateDownloader.cs`, `UpdateInstaller.cs` e `UpdateBackupService.cs`: consulta, download validado, substituição com rollback e cópia de segurança.
