# Trajetórias avançadas

Presets novos começam com movimento zero e intervalos de 2 ms nas duas armas. O editor aceita intervalos entre 2 e 50 ms, permitindo salvar um preset vazio de movimento sem ajustar os intervalos.

Abra **Avançada** na barra lateral, escolha primeiro o jogo, depois um dos presets desse jogo e a arma (primária ou secundária). Cada arma tem sua própria configuração. Marque **Usar trajetória avançada** para substituir os valores fixos dessa arma no teste temporário e, depois de salvar, no perfil persistido. Desmarcar restaura o comportamento básico sem apagar a trajetória.

Selecione um marcador na linha do tempo ou no gráfico. Edite tempo em milissegundos e componentes em pixels por segundo. Horizontal positivo move para a direita; vertical positivo move para baixo. O primeiro ponto fica em 0 ms. **Adicionar ponto** insere um ponto entre o selecionado e o próximo; após o último, estende a duração. Arraste marcadores na linha do tempo para mudar os tempos. A edição numérica rejeita tempos duplicados; o arraste limita o ponto ao espaço entre seus vizinhos. São permitidos de 2 a 128 pontos, duração até 60 segundos, horizontal de −5000 a +5000 e vertical de −10000 a +10000 px/s.

**Linear** muda os componentes uniformemente entre pontos. **Suave** usa uma transição gradual sem ultrapassar os valores dos pontos. **Em degraus** mantém os valores do ponto atual até o instante exato do seguinte: 350 permanece 350 e troca diretamente para 250. O modo é salvo em um campo opcional, sem alterar o comportamento de trajetórias antigas. Para reduzir a intensidade, use componentes menores; para uma fase só vertical, use horizontal zero. Dois pontos consecutivos com ambos os componentes em zero criam uma fase sem movimento. **Ao terminar → Zero** interrompe todo movimento após o último ponto; **Manter valores do último ponto** mantém sua velocidade enquanto a condição de ativação continuar satisfeita.

A prévia mostra o deslocamento acumulado, com escala ajustada automaticamente, e os componentes no resumo inferior. Iniciar/pausar, reiniciar e o controle de tempo operam somente na visualização. A prévia demonstra mais um segundo após o último ponto: em Zero o marcador fica parado; em Manter ele continua com os últimos valores. Se o último ponto já tiver ambos os componentes em zero, as duas opções são equivalentes e a interface informa isso. Os valores instantâneos de horizontal e vertical ajudam a comparar a interpolação linear com a suave; componentes iguais entre dois pontos produzem o mesmo movimento em ambas. A animação não move o cursor do sistema. **Desfazer** reverte edições; **Restaurar salvo** descarta o rascunho. Trocar de preset, arma ou seção e fechar a janela pedem descarte quando há mudanças não salvas. **Salvar no preset** cria o backup existente e grava atomicamente; o preset salvo passa a ser o perfil ativo, seguindo o fluxo atual do aplicativo.

Na aba **Presets**, o card de detalhes acompanha a arma selecionada e identifica o modo **Básica** ou **Avançada**, distinguindo macro ligado de configuração apenas selecionada. Para trajetórias habilitadas, mostra as faixas horizontal/vertical, quantidade de pontos, duração, interpolação, comportamento final e uma miniatura estática feita pelo mesmo gráfico do editor. Os valores fixos básicos reaparecem quando a arma selecionada não usa uma trajetória habilitada. A miniatura não possui animação nem edição.

## Desenho experimental

Abra **Avançada → Desenhar trajetória • experimental**. Clique para adicionar até 64 pontos em ordem, arraste para ajustar e use o botão direito para remover. O primeiro ponto define a origem relativa. **Imagem de referência** carrega PNG, JPEG ou BMP como fundo; não há reconhecimento automático. Os arquivos são liberados após carregar.

A referência tem zoom de 100% a 600% (slider ou roda do mouse), giro de 180° e espelhamento vertical/horizontal. Reposicione com Shift + arrastar ou botão do meio. **Ajustar imagem à área** restaura zoom, posição e orientação. Estes controles afetam somente a imagem de fundo e preservam os pontos já desenhados. Os rótulos INÍCIO/FIM e as setas mostram a ordem da trajetória. Girar a imagem não é o mesmo que inverter o movimento: trace o recuo de baixo para cima com a opção de inversão marcada para obter compensação para baixo.

Configure duração (100–60000 ms, no mínimo 2 ms por trecho) e escala (0,01–10 px por unidade da área de 600 × 360). **Desenho representa o recuo — inverter movimento** vem marcado e inverte os dois eixos em relação ao primeiro ponto. Para desenhar diretamente o movimento a executar, desmarque. O primeiro ponto permanece o início temporal, mesmo quando está embaixo. O tempo é distribuído proporcionalmente ao comprimento dos trechos. **Aplicar desenho ao rascunho** gera os keyframes, habilita a trajetória e fecha o painel para conferir a prévia; **Salvar no preset** mantém o fluxo existente de backup e persistência. O desfazer principal restaura a trajetória anterior; o desfazer dentro do painel opera sobre os pontos desenhados.

Nos modos linear e suave, a conversão conecta as velocidades nas junções, em vez de gerar um pulso separado com zero em cada ponto. Os trechos passam exatamente pelos pontos desenhados nos tempos calculados. Entre eles a curva pode ficar arredondada. No modo suave, velocidade e aceleração são contínuas nas junções dos keyframes gerados. No modo Em degraus, o desenho gera velocidade constante por trecho e troca diretamente nas junções, sem pausas intermediárias. Trocar o modo de uma trajetória já convertida preserva seus keyframes, incluindo quaisquer zeros existentes. Para desenhos de versões anteriores, use **Suavizar passagens de desenho antigo**, abaixo da interpolação: a ação reconhece o padrão alternado zero/pico/zero, reconstrói as posições e recalcula as velocidades sem as paradas artificiais. Posições dos vértices, seus tempos, duração e deslocamento total são preservados; os valores de velocidade intermediários são recalculados. A alteração vale no teste temporário, pode ser desfeita e só é persistida ao salvar. Um S é aproximado com vários trechos; não há desenho à mão livre ou curva espacial automática. O resultado termina em zero. Uma reversão completa de direção pode ter velocidade zero instantânea; trechos explicitamente estacionários continuam parados. A interpolação selecionada no editor controla a transição de velocidade em cada trecho. Velocidades fora dos limites são rejeitadas com orientação para aumentar a duração ou reduzir a escala. Enquanto o painel de desenho estiver aberto na aba Avançada, a aplicação local do movimento fica pausada, permitindo clicar e arrastar.

Apenas a trajetória convertida é salva no preset. Os pontos originais do desenho e a imagem são temporários, e o desenho não é reconstruído automaticamente ao reabrir um preset. Alterações numéricas posteriores continuam disponíveis. Os presets mantêm o formato atual.

Para compilar o protótipo em pasta separada:

```powershell
dotnet build MacroDoButler.csproj -c Release -warnaserror -p:UseSharedCompilation=false -p:OutputPath=build-check/continuous-motion-test/
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File scripts/Test-TrajectoryEditor.ps1 -AssemblyPath "$PWD/build-check/continuous-motion-test/Velqora.exe"
```

## Ajustar intensidade

O multiplicador geral foi substituído por **Ajustar horizontal e vertical separadamente**. Cada eixo tem um campo e um botão próprio: **Aplicar só horizontal** altera apenas os componentes horizontais de todos os pontos; **Aplicar só vertical** altera apenas os verticais. O campo do outro eixo é ignorado, mesmo que contenha um valor diferente de 1. Os tempos e o modo de interpolação são preservados.

Fator 2 dobra o movimento daquele eixo, 0,5 reduz à metade e 1 mantém sem gerar alteração. Cada aplicação atua sobre os valores atuais e pode ser desfeita. Valores fora dos limites são rejeitados antes de alterar o rascunho; não há corte silencioso. O resultado é salvo nos próprios keyframes. A escala do desenho continua definindo a conversão inicial das unidades desenhadas em pixels. Reaplicar um desenho substitui os keyframes e seus ajustes por eixo; ajuste novamente os eixos se necessário.

A prévia ajusta sua escala visual automaticamente, portanto um aumento de força pode parecer o mesmo caminho na tela. Observe o deslocamento total em px e os picos de velocidade exibidos abaixo do gráfico. A forma desenhada não determina automaticamente a intensidade necessária num jogo; a imagem não fornece uma calibração entre suas coordenadas e o movimento local aplicado. A duração, a força e os instantes dos pontos continuam sendo ajustes manuais. Não foi adicionada captura ou calibração automática de jogos.

## Testar antes de salvar

Enquanto a seção **Avançada** estiver aberta, o serviço de movimento usa temporariamente o perfil em edição e suas alterações válidas. Ajustar um eixo, editar pontos, mudar interpolação/fase final, aplicar um desenho, desfazer e restaurar atualizam esse teste antes de salvar. O indicador no topo informa o preset e a arma efetivamente em teste. O macro precisa estar ligado e os botões de ativação do perfil continuam valendo.

Selecionar a arma do editor também seleciona essa arma para o teste. O atalho de alternância continua funcionando: a outra arma usa sua configuração salva, sem receber o rascunho da arma editada. Campos numéricos inválidos e o painel de desenho aberto pausam a aplicação local do movimento. A animação do gráfico continua sendo apenas visual.

O teste usa uma cópia imutável do último rascunho válido e só troca essa cópia quando o conteúdo muda, reiniciando a sessão temporal. Selecionar um ponto ou consultar as configurações não reinicia a sequência. Nenhuma alteração temporária é escrita em `presets.json`. **Desfazer** permite comparar antes de salvar; **Restaurar salvo** recupera a configuração persistida. Ao sair da aba e confirmar descarte, o serviço volta ao preset salvo que estava selecionado na página Presets. **Salvar no preset** confirma a configuração e encerra o histórico atual, seguindo o comportamento existente.

## Arquitetura

- `src/Models/DrawingTrajectoryConverter.cs`: converte pontos espaciais em velocidades conectadas, com correção de integral exata nos pontos, validando duração, escala, contagem e limites. `TrajectoryDrawingEditor` implementa a área de desenho e a referência visual, sem alterar a entrada nativa.
- `src/Models/Trajectory.cs`: contrato opcional, normalização, interpolação e integração analítica dos componentes. A mesma integral alimenta a prévia e a aplicação local. `MovementSession` mantém tempo e frações de pixels, separado da leitura nativa de botões.
- `src/Models/Preset.cs`: campos opcionais `primaryTrajectory` e `secondaryTrajectory`. `presets.json` mantém `formatVersion: 2` e todos os campos básicos; não há migração destrutiva. Presets sem esses campos usam o modo básico. Exportação, importação, cópia e edição básica preservam as duas trajetórias.
- `src/Services/InputService.cs`: usa os botões existentes e o relógio monotônico. A sessão inicia na primeira observação da condição de ativação, avança com o tempo decorrido e zera ao soltá-la. Desativar, trocar perfil/arma/configuração ou sair do rascunho também invalida a sessão. A precisão da detecção dos botões é limitada pelo intervalo já configurado (2–50 ms).
- `src/UI/TrajectoryEditor.xaml` e `.xaml.cs`: rascunho isolado, histórico de até 100 alterações, linha do tempo e desenho WPF. Animação a aproximadamente 30 quadros/s; o timer para ao pausar, terminar ou ocultar a seção. Nenhum controle da prévia chama APIs de movimento.
- `MainWindow.xaml`, `MainWindow.xaml.cs` e `src/UI/PresetEditor.cs`: navegação, seleção do perfil salvo e geração de revisão para reinício temporal.

Dados avançados ausentes ou nulos mantêm o modo básico; listas vazias desabilitam a trajetória. Na leitura, tempos são limitados e ordenados, duplicados conservam o último valor, componentes não finitos viram zero e valores fora dos limites são restringidos. Erros estruturais no JSON completo continuam usando a proteção existente do repositório, que recusa sobrescrever arquivos corrompidos.

## Validação

Na raiz do projeto, usando PowerShell:

```powershell
dotnet build MacroDoButler.csproj -c Release -warnaserror -p:UseSharedCompilation=false
dotnet build tests/TrajectoryTests.csproj -c Release -warnaserror -p:UseSharedCompilation=false
& tests/bin/Release/net48/TrajectoryTests.exe build-check/trajectory-fixtures
dotnet build tests/PresetEditingTests.csproj -c Release -warnaserror -p:UseSharedCompilation=false
& tests/bin/Release/net48/PresetEditingTests.exe build-check/preset-fixtures
dotnet build tests/HotkeyRegistrationTests.csproj -c Release -warnaserror -p:UseSharedCompilation=false
& tests/bin/Release/net48/HotkeyRegistrationTests.exe
dotnet build tests/MigrationTests.csproj -c Release -warnaserror -p:UseSharedCompilation=false
& tests/bin/Release/net48/MigrationTests.exe
powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -File scripts/Test-TrajectoryEditor.ps1
```

Os testes usam dados sintéticos em `build-check`, sem carregar presets pessoais nem enviar movimento ao sistema. O teste WPF também verifica o resumo de detalhes nos modos básico e avançado, a troca de arma, macro desligado, trajetória desabilitada e ausência de seleção. Verifica edição, rejeição de valores não finitos, arraste, desfazer, adição/remoção, reprodução/reinício e renderiza duas larguras para inspeção visual.

Para conferir manualmente, configure uma curva com mudanças de sinal, avance a prévia e altere valores durante a reprodução. Salve, reabra o aplicativo e confira ambas as armas. Em um ambiente local controlado, verifique a ativação com esquerdo e com esquerdo+direito: soltar a condição, desligar o macro ou trocar a arma deve reiniciar o padrão. A aplicação física do movimento não faz parte dos testes automatizados. Não foi adicionada integração com processos de jogos ou captura de padrões.
