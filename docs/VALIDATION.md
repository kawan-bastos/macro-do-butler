# Validação do instalador Velqora 3.2.0

Executado em Windows, com Inno Setup 6.7.3 verificado por assinatura Authenticode e SHA-256. Os testes automatizados usam arquivos sintéticos e uma identidade de instalador separada da instalação Velqora real. Nenhum preset real foi alterado.

| Verificação | Resultado |
|---|---|
| Build Release com `-warnaserror` no projeto e em cópia limpa | Passou: 0 avisos, 0 erros |
| Instalação do Inno Setup a partir de fonte oficial | Assinatura válida, editor Pyrsys B.V.; hash fixado no script |
| Compilação do Setup a partir do build normal e limpo | Passou |
| Migração, conflitos, retry após arquivo bloqueado e idempotência | 30 verificações passaram; arquivos sintéticos |
| Instalação em caminho com espaços, registro no Windows e dois atalhos | Passou na variante isolada |
| Seleção de arquivo de presets no Setup | Passou na variante isolada: `.cfg` escolhido foi copiado para `pending-imports` com SHA-256 idêntico |
| Segunda execução do Setup | Passou na variante isolada: execução silenciosa comum recusada; resposta Sim de teste desinstalou e encerrou sem reinstalar, preservando dados; modo de atualização reinstalou |
| Reinstalação da mesma versão e transição de `Macro do Butler.exe` | Passou; dados sintéticos e cópia anterior verificadas |
| Desinstalação padrão | Passou; presets, configurações e backups sintéticos preservados; atalhos e registro removidos |
| Remoção explícita de dados | Passou na variante isolada; pasta vizinha preservada |
| Atualizador portátil, hashes, cancelamento e rollback | 24 verificações passaram no harness local existente, adaptado à exigência de SHA-256 |
| Propriedades de versão do executável | Velqora, versão 3.2.0.0 / produto 3.2.0 |
| Assinatura dos artefatos desta entrega | `Get-AuthenticodeSignature`: `NotSigned` para executável e Setup |
| Conteúdo do pacote | `Velqora.exe`, `Velqora.exe.config` e o aviso legal `LICENSE.txt` |

A seleção do pacote Setup na consulta de releases e a execução completa da atualização instalada ainda exigem uma release de teste controlada. O parser de `SHA256SUMS.txt` e a detecção do canal instalado passaram com dados sintéticos. Ainda exigem teste interativo em uma VM Windows com .NET Framework 4.8: navegação visual das perguntas Sim/Não e do seletor de arquivo, importação na primeira abertura com dados reais de versões anteriores, interface de desinstalação e confirmação da remoção, bloqueio por aplicativo aberto, rejeição e aceitação do UAC para controlar outro processo elevado, F1/F2/F12, movimento do mouse, downgrade com confirmação, recuperação manual da versão anterior e aparência final em DPI/alto contraste. Esses resultados não são inferidos dos testes sintéticos.

Assinatura e publicação dependem de um certificado legítimo e dos secrets configurados pelo responsável. O pipeline falha se a assinatura for solicitada e a verificação falhar. Nenhum build assinado ou release pública foi produzido nesta tarefa.

