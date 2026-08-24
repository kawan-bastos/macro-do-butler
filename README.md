# Macro do Butler

Aplicativo Windows para configurar movimentos relativos do mouse com perfis persistentes para duas configurações (primária e secundária).

## Recursos

- Interface escura em vermelho e preto.
- Presets salvos localmente.
- Valores separados para configuração primária e secundária.
- Ajuste vertical de 0 a 10.000 pixels por segundo.
- Ajuste lateral de -5.000 a 5.000 pixels por segundo.
- Ícone e marca visual incorporados ao executável.
- Aplicativo independente de softwares de fabricantes de mouse.

## Atalhos

| Tecla | Ação |
|---|---|
| `F1` | Ligar ou desligar |
| `F2` | Alternar entre configuração primária e secundária |
| `F12` | Encerrar o aplicativo |

O movimento configurado é aplicado enquanto o botão esquerdo estiver pressionado. Opcionalmente, o programa pode exigir que o botão direito também esteja pressionado.

## Presets

Os presets são armazenados em `presets.cfg`, ao lado do executável. Esse arquivo está ignorado pelo Git para evitar publicar configurações pessoais.

Presets antigos com uma única configuração continuam compatíveis. Ao serem salvos novamente, passam a armazenar os valores da configuração secundária.

## Compilar no Windows

Requisitos:

- Windows 10 ou 11.
- Visual Studio Build Tools com suporte ao .NET Framework 4.8, ou Visual Studio 2022.

No PowerShell, execute:

```powershell
.\build.ps1
```

O resultado será criado em `dist\Macro do Butler.exe`.

## Estrutura do código

```text
src/
├── Program.cs
├── Forms/
│   └── MainForm.cs
├── Models/
│   ├── MovementSettings.cs
│   └── Preset.cs
├── Native/
│   └── NativeMethods.cs
├── Services/
│   ├── InputService.cs
│   └── PresetService.cs
└── UI/
    └── Theme.cs
```

- `Forms`: interface, eventos e apresentação dos dados.
- `Models`: estruturas de dados sem dependência da interface.
- `Services`: persistência dos presets e processamento de entrada.
- `Native`: chamadas isoladas à API do Windows.
- `UI`: paleta, criação de controles e carregamento dos recursos visuais.

## GitHub Actions

O workflow em `.github/workflows/build.yml` compila o projeto automaticamente. O executável fica disponível como artefato chamado `macro-do-butler-windows` em cada execução do workflow.

## Uso responsável

Use automações somente em aplicativos e ambientes que permitam entradas simuladas. Alguns programas podem ignorar esse tipo de entrada. Este projeto não inclui mecanismos para contornar proteções, sistemas antitrapaça ou restrições de terceiros.

## Licença

Distribuído sob a licença MIT. Consulte [LICENSE](LICENSE).
