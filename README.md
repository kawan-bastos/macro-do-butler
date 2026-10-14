# Macro do Butler

Aplicativo Windows em WPF para configurar movimentos relativos do mouse com presets organizados por jogo e nome. Requer .NET Framework 4.8.

## Organização de presets

Na área **Presets**, escolha um jogo e um preset. O campo **Nome do preset** é texto livre, não uma lista. Ao trocar o nome e clicar em **Salvar**, um novo preset é criado no jogo selecionado com os valores atuais; ao manter o nome do preset carregado, seus valores são atualizados.

- **Editar jogo / presets** permite criar jogos e selecionar vários presets legados ainda sem jogo para adicioná-los juntos ao jogo escolhido.
- Presets atribuídos a outro jogo não aparecem na lista de presets disponíveis para movimentação.
- **Excluir** pede confirmação com jogo e nome do preset e cria backup antes da remoção.
- O nome do preset é universal: pode ser um operador, uma arma ou qualquer outro perfil que faça sentido para o jogo.

O macro continua mantendo valores para as configurações primária e secundária. F2 alterna qual conjunto de valores está ativo.

## Dados, compatibilidade e backups

O arquivo ativo é `presets.json`, ao lado do executável. O JSON versionado (formato 2) guarda jogos, nome do preset, valores, requisito do botão direito e datas. A gravação usa arquivo temporário e substituição atômica. A pasta `backups\` guarda até 10 cópias recentes do arquivo ativo.

Na primeira inicialização sem JSON, o programa lê `presets.cfg`, interpreta os formatos CFG de cinco e sete campos e converte cada registro para o jogo **Não categorizado**, usando o nome legado como nome do preset. Antes de criar o JSON, copia o CFG original para `presets.cfg.migration.bak`. O arquivo CFG original permanece intacto. Os presets antigos aparecem como não atribuídos e podem ser adicionados em grupo ao jogo escolhido.

Use **Exportar presets** para salvar um JSON completo. **Importar presets** aceita JSON e CFG antigo, valida os registros e oferece substituir ou ignorar combinações jogo/nome já existentes; cria backup antes de aplicar. **Restaurar backup** valida o arquivo escolhido e salva o estado atual antes da restauração.

## Atalhos

| Tecla | Ação |
|---|---|
| `F1` | Ligar ou desligar o macro |
| `F2` | Alternar entre primária e secundária |
| `F12` | Encerrar o aplicativo |

Os atalhos são globais e continuam ativos com a janela minimizada ou outro aplicativo em primeiro plano. Não dependem do Logitech G HUB. Clique na engrenagem para abrir **Configurações** e, em **Atalhos**, clique em **Alterar**, pressione a tecla ou combinação e confirme com **Enter**. Use **Esc** para cancelar; **Backspace** ou **Delete** desativa o atalho opcional de mostrar a janela. Os atalhos principais são obrigatórios. Combinações repetidas são bloqueadas e o aplicativo informa quando o Windows não consegue registrar uma combinação. O atalho opcional **Mostrar janela** alterna entre minimizar e restaurar a interface. **Restaurar padrões** retorna F1/F2/F12 e desativa esse atalho opcional.

As preferências ficam em `%LOCALAPPDATA%\MacroDoButler\hotkeys.json`, separadas dos presets. Se o arquivo estiver inválido, os padrões são carregados e o arquivo é mantido para recuperação.

O movimento é aplicado enquanto o botão esquerdo está pressionado. É possível exigir também o botão direito.

## Limites

- Força vertical: 0 a 10.000 px/s.
- Correção lateral: -5.000 a 5.000 px/s.
- Intervalo geral: 2 a 50 ms, compartilhado por todos os presets.
- Jogo e nome do preset são obrigatórios.

## Compilar no Windows

Com Visual Studio Build Tools com suporte a .NET Framework 4.8 ou Visual Studio 2022, execute `.\build.ps1` no PowerShell. O executável é criado em `dist\Macro do Butler.exe`.

## Estrutura

- `MainWindow.xaml` e `MainWindow.xaml.cs`: interface, seletores e integração do macro.
- `src/Models/Preset.cs`: modelo hierárquico e metadados.
- `src/Services/PresetService.cs`: leitura, migração e persistência JSON/CFG.
- `src/Services/PresetOrganizationService.cs`: operações de organização, pesquisa e validação.
- `src/Services/PresetBackupService.cs`: cópia, listagem e restauração de backups.
- `src/Services/PresetTransferService.cs`: importação e exportação.
- `src/Services/HotkeyService.cs`, `HotkeyParser.cs` e `HotkeyStorageService.cs`: registro global por `RegisterHotKey`, conversão, validação e persistência dos atalhos.
- `src/Services/PresetDialogs.cs`: diálogos de categorias e duplicidades.

Serviços específicos de migração e validação de domínio ficam em `PresetOrganizationService.cs`; a leitura legada é coordenada por `PresetService` antes de habilitar gravações no JSON.

Use automações somente em aplicativos e ambientes que permitam entradas simuladas. O projeto não implementa mecanismos para contornar proteções ou restrições de terceiros.
