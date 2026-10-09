# GameTracker

Aplicativo leve para Windows que fica na bandeja, detecta automaticamente quando um jogo é aberto e registra cada sessão de jogo. Mostra uma biblioteca com capas, detalhes e relatórios de tempo jogado, filtráveis por mês e por ano.

## Funcionalidades

- **Rastreamento automático em segundo plano**: inicia com o Windows (minimizado), sem precisar de administrador.
- **Detecção de jogos**: bibliotecas Steam, Epic, GOG e Ubisoft, pastas padrão (Xbox, Riot, EA, Rockstar, Amazon), pastas do usuário e executáveis adicionados manualmente.
- **Sugestão automática em qualquer pasta**: um programa em tela cheia que se comporta como jogo vira a pergunta "Isso é um jogo?", feita na biblioteca e numa notificação. O tempo já é registrado enquanto a resposta não vem.
- **Sessões confiáveis**: heartbeat a cada minuto (no máximo 1 min perdido em caso de crash ou queda de energia), descarte de aberturas acidentais e união de sessões quando o launcher reinicia o jogo.
- **Metadados**: capa, arte de fundo, descrição, gêneros, desenvolvedora, publicadora e data de lançamento.
- **Relatórios**: tempo total, sessões, média por sessão, sessão mais longa, gráfico dos últimos 12 meses e histórico de sessões. Uma sessão que atravessa a virada do mês tem o tempo dividido entre os dois meses.
- **Controle**: renomear, "não é um jogo" (pode ser desfeito nas Configurações), remover e atualizar dados.

## Arquitetura

```
src/
  GameTracker.Core            net10.0, sem dependências: domínio e regras (portável para um futuro servidor)
    Domain/                   Game, GameSession, GameKeys (identidade estável do jogo)
    Detection/                GameDetector (processo → jogo), contratos de scanners/processos
    Tracking/                 SessionTracker (snapshots → sessões), contratos de persistência
    Metadata/                 GameMetadata (merge entre provedores), IMetadataProvider, IMetadataQueue
    Reporting/                ReportPeriod, PlaytimeCalculator (recorte exato por período), modelos de relatório
    Library/ Settings/        Contratos de gerenciamento da biblioteca e das configurações
  GameTracker.Infrastructure  net10.0-windows: implementações
    Persistence/              EF Core + SQLite (migrations, WAL), ReportService, GameLibrary
    Detection/                Win32ProcessSnapshotProvider (P/Invoke), TrackingWorker, scanners das lojas
    Metadata/                 Steam Store, IGDB, ImageStore, MetadataEnrichmentService (fila em background)
    Settings/                 JSON + DPAPI para segredos, registro de inicialização com o Windows
  GameTracker.App             WPF + MVVM (CommunityToolkit.Mvvm), Generic Host/DI, ícone na bandeja
tests/
  GameTracker.Core.Tests      xUnit: detector, tracker de sessões e cálculo de tempo
```

**Como um jogo é reconhecido** (`GameDetector`), em ordem de precedência:

1. executável adicionado manualmente;
2. auxiliares (crash reporter, anti-cheat, instaladores…) nunca contam;
3. o processo roda dentro da pasta de instalação de um jogo de launcher;
4. o processo roda em `…\<pasta padrão>\<Jogo>\…` ou numa pasta configurada pelo usuário;
5. processos protegidos (sem acesso ao caminho) são reconhecidos pelo nome de um executável já visto;
6. **por comportamento** (`BehaviorGameDetector`): a janela em primeiro plano cobre o monitor inteiro **e** o processo carregou uma API 3D (Direct3D, Vulkan ou OpenGL) **e** uma biblioteca de controle (XInput, DirectInput, GameInput) ou um runtime de jogo (Unity, SDK Steam/Epic/GOG, FMOD, SDL, Bink).

Uma API 3D sozinha não basta, porque o Explorer, o Bloco de Notas, o Spotify e o Terminal também carregam Direct3D. Bibliotecas de controle e de engine, por outro lado, não aparecem em apps comuns.

Navegadores, players, launchers, overlays e ferramentas criativas nunca são sugeridos. Só o processo em primeiro plano é inspecionado, no máximo uma vez a cada 30 s.

Depois que o usuário confirma, o executável passa a ser reconhecido diretamente pela regra 1. Se ele responder "Não é", o programa fica ignorado (dá para desfazer nas Configurações).

**Leveza**:

- A cada ciclo (5 s por padrão) é feita uma única enumeração de processos. O caminho e o resultado da detecção ficam em cache por PID, então um ciclo sem mudanças custa quase nada (~0,15% de CPU medido).
- Ao fechar a janela, ela é descartada de verdade (escopo de DI por janela), o processo entra no *modo eficiência* do Windows e a memória é coletada.
- A renderização é feita por software: o driver da GPU não é carregado. Medições: ~40 MB privados na bandeja e ~57 MB com a janela aberta.

**Dados** em `%LocalAppData%\GameTracker\`:

- `gametracker.db`: SQLite com horários em UTC. Como o acesso passa pelo EF Core, migrar para um servidor (SQL Server/PostgreSQL) é basicamente trocar o provider.
- `settings.json`: o client secret do IGDB fica criptografado com DPAPI.
- `images/<id>/`: capas e artes de fundo.
- `logs/`: logs dos últimos 7 dias.

## APIs de metadados

| API | Chave | Uso |
|-----|-------|-----|
| **Steam Store** (`store.steampowered.com/api`) | Não precisa | Fonte principal para jogos da Steam (textos em pt-BR). Também busca por nome para jogos de outras lojas. |
| **IGDB** (Twitch) | Client ID + Secret | Cobertura ampla para qualquer loja. Configure em ⚙ › Metadados. |

A Steam Web API key não é necessária: os endpoints da loja são públicos. Uma possível evolução é o **SteamGridDB** (chave gratuita), para capas alternativas em jogos sem arte oficial.

## Build e execução

Requer o .NET 10 SDK.

```bash
dotnet build
dotnet test
dotnet run --project src/GameTracker.App
```

Para publicar um executável único:

```bash
dotnet publish src/GameTracker.App -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Para criar uma nova migration após alterar o modelo:

```bash
dotnet ef migrations add <Nome> --project src/GameTracker.Infrastructure --output-dir Persistence/Migrations
```

## Abas

- **Jogos**: biblioteca, filtros e página de cada jogo.
- **Cápsula do tempo**: linha do tempo agrupada por ano. Cada mês mostra tempo total, sessões, jogos, dias ativos, a variação em relação ao mês anterior, um selo de recorde e os 5 jogos mais jogados. O botão "Ver {mês} completo" carrega o ranking inteiro daquele mês sob demanda, numa lista virtualizada.

### Novas abas

O shell (`MainViewModel.Tabs`) recebe `NavigationItem(título, viewModel)`. Para adicionar uma aba:

1. crie o ViewModel e a View;
2. mapeie os dois em `Themes/ViewTemplates.xaml`;
3. registre o ViewModel como *scoped* em `App.xaml.cs`;
4. adicione a aba na lista `Tabs`.
