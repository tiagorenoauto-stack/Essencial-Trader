# Plano Integrado do Essencial ChartGuard

Este documento organiza o caminho do produto unindo três fontes:

- **CunhaTrader Gold**: referência principal de layout, ergonomia, botões,
  selects, seções e fluxo visual.
- **Essencial ChartGuard atual**: base nova já validada para painel lateral,
  resize, observação de conta/posição/eventos e arquitetura segura.
- **NTB_TradeSafe**: referência madura de informações, proteção, leitura de
  risco e comportamento útil observado em uso normal.

O objetivo não é escolher um dos três. O objetivo é montar um produto único:
bonito como o CunhaTrader Gold, mais informativo como ferramentas maduras, e
seguro/consistente por causa do Safe Core.

## Princípio Central

Preservar a intenção visual e operacional do CunhaTrader Gold.

Substituir 100% da lógica por uma arquitetura nova, testável e segura.

Tudo que aparece no painel deve ter uma lógica clara por trás. Se ainda não
existe lógica confiável, a função pode aparecer apenas como preview
desabilitado, ou deve ficar escondida.

## O Que Cada Fonte Contribui

### CunhaTrader Gold

Contribui com:

- layout lateral;
- cores e estilo visual;
- organização das seções;
- botões grandes de comprar/vender;
- botão pânico;
- seleção de modo/estratégia;
- seleção de conta/instrumento;
- tipo de entrada;
- quantidade/sizing;
- área de takes;
- área de stop;
- lock 1R/2R/3R;
- breakeven;
- risco da conta;
- sessão;
- ideia de linhas no gráfico com legendas;
- formas práticas de selecionar e adicionar funções.

Não contribui com:

- lógica antiga de ordem;
- rotas antigas;
- risco antigo sem validação;
- click handlers antigos;
- hotkeys antigas;
- qualquer trecho que envie/cancele/modifique ordem.

### Essencial ChartGuard Atual

Contribui com:

- painel lateral direito funcional;
- resize horizontal funcional;
- leitura de conta/instrumento;
- snapshot inicial de posição;
- EventBridge read-only;
- estado observado de posição/ordens;
- logs padronizados;
- separação SafeCore / Bridge / Panel / Indicator;
- regra de não enviar ordem pela UI;
- base para DTOs e mutators seguros do painel.

Essas coisas devem ser preservadas e incorporadas ao layout inspirado no
CunhaTrader Gold.

### NTB_TradeSafe

Contribui como referência observável:

- informações importantes de posição;
- formas maduras de exibir proteção;
- leitura de risco mais completa;
- comportamento de painel dimensionável;
- clareza em status de operação;
- possíveis alertas e bloqueios configuráveis;
- experiência de linhas parecidas com o nativo.

Não será copiado código nem implementação interna. Apenas ideias úteis serão
traduzidas para o nosso Safe Core.

## Modelo Do Painel Final

O painel final deve parecer uma evolução do CunhaTrader Gold, não um painel
genérico novo.

### Cabeçalho

Base visual: CunhaTrader Gold.

Melhorias:

- usar informações diretas do ChartGuard atual;
- mostrar conta/instrumento;
- posição + qty;
- entrada/preço médio;
- PnL quando disponível;
- estado: Observer / Playback / Sim / Live / Blocked / Warning;
- engrenagem de configurações.

### Modo / Estratégia

Base visual: CunhaTrader Gold.

Melhorias necessárias:

- selecionar estratégia;
- criar estratégia;
- editar estratégia;
- excluir estratégia;
- copiar/duplicar estratégia;
- definir padrões de stop/target/sizing por estratégia;
- estratégia deve ser configuração, não lógica solta dentro da UI.

Inicialmente:

- preview desabilitado ou read-only;
- depois persistência segura;
- depois ligação com comandos.

### Entrada

Base visual: CunhaTrader Gold.

Controles:

- Tipo: Mercado, Limite, Stop Market, Stop Limit.
- Quantidade.
- Sizing.
- Unidade: ticks, pontos, preço.
- Comprar.
- Vender.
- Pânico / cancelar tudo / fechar tudo.

Melhorias:

- stop pode ser obrigatório por estratégia, mas não uma regra universal.
- o usuário/estratégia pode escolher trabalhar com ou sem proteção obrigatória.
- quando risco estiver configurado como bloqueador, entrada pode ser bloqueada.
- quando risco estiver configurado como alerta, entrada apenas alerta.

### Posição Ativa

Base visual: CunhaTrader Gold.

Melhorias vindas do ChartGuard/NTB:

- direção;
- qty;
- entrada/preço médio;
- último fill;
- PnL em ticks;
- PnL em pontos;
- PnL em porcentagem;
- PnL em dinheiro;
- working orders;
- stop ativo;
- targets ativos;
- estado de proteção.

### Takes / Targets

Base visual: CunhaTrader Gold.

Controles:

- adicionar alvo;
- múltiplos alvos;
- remover alvo;
- editar alvo;
- visualizar no painel;
- visualizar no gráfico.

Lógica futura:

- sempre por comando;
- idempotência;
- validação por conta/instrumento;
- Replay antes de qualquer uso real.

### Stop / Proteção

Base visual: CunhaTrader Gold.

Controles:

- adicionar stop;
- editar stop;
- breakeven;
- lock 1R;
- lock 2R;
- lock 3R;
- trail/stop móvel.

Melhorias:

- lock R usa risco inicial;
- stop pode ser arrastado no gráfico no futuro;
- mudança de stop deve passar por `ProtectionService`.

### Risco

Base visual: CunhaTrader Gold + maturidade observada no NTB.

Importante:

- risco não deve ser sempre imposição.
- o usuário deve poder escolher entre:
  - modo alerta;
  - modo bloqueio;
  - desativado.

Informações:

- limite diário;
- perda máxima;
- drawdown;
- espaço restante;
- status de risco;
- novas entradas permitidas/bloqueadas;
- regras de conta quando aplicável.

Regra:

- mesmo em bloqueio, flatten e ações protetivas continuam disponíveis.

### Sessão

Base visual: CunhaTrader Gold.

Informações:

- PnL da sessão;
- trades;
- tempo;
- aberto/realizado;
- estado de monitoramento.

### Configurações

Essencial para o produto.

Configurações futuras:

- mostrar/ocultar seções;
- modo compacto/completo;
- unidades de PnL;
- estratégias;
- risco em alerta/bloqueio/desativado;
- defaults por estratégia;
- linhas no gráfico;
- comportamento de clique/arrasto;
- informações do cabeçalho;
- conta/instrumento preferidos.

## Linhas No Gráfico

As linhas são parte central do produto.

Objetivo visual:

- parecerem tão naturais quanto as linhas nativas do NinjaTrader;
- se possível, seguir comportamento semelhante ao nativo;
- usar legenda própria do ChartGuard;
- usar cores coerentes com painel.

Linhas desejadas:

- entrada;
- ordem limite;
- stop;
- target;
- drawdown;
- risco;
- posição ativa.

Interação futura:

- arrastar linha de stop altera stop;
- arrastar linha de target altera alvo;
- arrastar linha de ordem altera ordem;
- Ctrl + botão direito cria intenção de ordem no gráfico;
- menu/seleção própria do ChartGuard, independente do menu nativo se necessário.

Regra:

- linha nunca modifica ordem direto.
- arrasto/clique cria comando;
- comando passa pelo Safe Core;
- resultado aparece no log e no painel.

## Fases Reorganizadas

### Fase 0 — Base Técnica Validada

Status: em andamento.

- painel lateral;
- resize;
- snapshot;
- EventBridge;
- estado observado;
- sem envio de ordem.

### Fase 1 — Shell Visual CunhaTrader Gold + Dados Atuais

Objetivo:

- reorganizar o painel para seguir o layout do CunhaTrader Gold;
- preservar resize atual;
- preservar dados atuais de posição;
- adicionar placeholders das funções antigas no mesmo espírito visual;
- botões/controles ainda desabilitados;
- incluir informações mais úteis já observadas no NTB quando forem apenas
  exibição.

Resultado esperado:

- visual familiar;
- painel mais próximo do produto final;
- nenhuma ordem enviada.

#### Estado atual da Fase 1 (shell visual implementada)

O `EssencialChartGuardPanel` já entrega a casca visual final, com tudo
desabilitado por contrato. Hoje o painel mostra, de cima para baixo:

1. **Cabeçalho** — marca, dot de conexão, modo (`Observer` /
   `Observer / Playback` / `Sim` / `non-sim`), engrenagem (preview/disabled),
   linha `<conta> / <instrumento>` e linha-resumo
   `Position · qty · avg · wo`.
2. **Strategy** — combobox de estratégia + botões `+ / ✎ / ❏ / ✕`
   (preview/disabled).
3. **Entry** — selects/inputs `Type / Qty / Sizing / Unit / Stop / Target` e
   botões grandes `BUY` / `SELL` / `PANIC` (todos preview/disabled).
4. **Active position** — leitura observada hoje
   (`Direction / Qty / Entry/Avg / Last fill / Working orders`), com linhas
   reservadas para `PnL ticks/points/%/$ / Stop / Targets / Protection` que
   permanecem `-` até existir fonte real.
5. **Takes** — lista vazia + botões `+ Add target / Edit / Remove`
   (preview/disabled).
6. **Stop** — `Current` + botão `Edit stop` (preview/disabled).
7. **Protection** — `BE / Lock 1R / Lock 2R / Lock 3R / Trail`
   (preview/disabled).
8. **Risk** — `Daily limit / Status / Block` + select `Mode` (`Alert / Block /
   Off`, preview/disabled).
9. **Session** — `Trades / PnL / Time`.
10. **Observation** — dots de `Snapshot` e `Event bridge`.

Cada botão, combobox, textbox e ícone tem `ToolTip` explicando a função e
deixando claro que está em preview/disabled. Nenhum desses controles tem
handler `Click`, `MouseDown`, `MouseUp`, `PreviewMouse*`, `ContextMenu`,
`SelectionChanged`, `TextChanged` ou hotkey. O contrato é "se aparece com
ação, está `IsEnabled=false`".

Tudo o que a Fase 0 já validava continua intacto:

- `ChartGuardPanelHost` (injeção lateral + resize por `Thumb` + ciclo de vida
  no `State.Terminated`);
- `NinjaTraderPositionSnapshotReader` para snapshot inicial;
- `NinjaTraderAccountEventBridge` em modo somente leitura;
- `ObservedAccountState` + `ObservedAccountSnapshotDto`;
- SafeCore sem `using NinjaTrader.Cbi` / `NinjaTrader.Data`;
- `AddOns/Panel` sem `using NinjaTrader.Cbi` / `NinjaTrader.Data`;
- nenhuma rota de envio/cancelamento/modificação/flatten de ordem.

### Fase 2 — Estratégias E Configurações Read-Only/Preview

Objetivo:

- desenhar seleção/criação/edição/exclusão de estratégia;
- ainda sem aplicar estratégia em ordens reais;
- definir modelo de dados para estratégia;
- definir opções de risco: alerta, bloqueio, off;
- definir defaults de stop/target/sizing.

#### Estado atual da Fase 2 (modelo/draft preview)

A Fase 2 agora tem uma camada de modelos puros em
`src/EssencialChartGuard/AddOns/Panel/Models/`. Esses tipos representam a
intenção visual/configurável do painel, mas continuam fora de qualquer rota de
trading:

- `StrategyDraft` — nome/descrição da estratégia e defaults de entrada, stop,
  alvos, proteção e risco.
- `EntryPlanDraft` — `EntryType`, `Quantity`, `SizingMode`, `Unit`, `Stop` e
  `Target`.
- `TakeTargetDraft` — linha de alvo planejado (`Label`, `Quantity`, `Value`,
  `Unit`).
- `StopDraft` — stop planejado/current preview e unidade.
- `ProtectionDraft` — flags de preview para `BE`, `Lock 1R`, `Lock 2R`,
  `Lock 3R`, `Trail` e resumo textual.
- `RiskModeDraft` — modo `Alert`/`Block`/`Off` em preview, limite/status/bloco.

O `EssencialChartGuardPanel` recebeu mutators seguros:

- `SetStrategyDraft(...)`
- `SetEntryPlanDraft(...)`
- `SetTakeTargetsDraft(...)`
- `SetStopDraft(...)`
- `SetProtectionDraft(...)`
- `SetRiskModeDraft(...)`

Esses métodos apenas renderizam texto em controles já desabilitados ou labels
read-only. Eles não registram handlers, não criam comandos, não chamam serviços
de ordem e não alteram o fluxo preservado da Fase 0/1:
`ApplyPositionSnapshot -> BuildSnapshotDto -> SetObservedState`.

Persistência, edição real dos controles e comandos continuam fora desta fase.

### Fase 3 — Linhas Read-Only No Gráfico

Objetivo:

- desenhar linhas observadas;
- labels próprias;
- sem arrastar;
- sem clique;
- comparar visual com linhas nativas.

A especificação técnica desta fase (linhas suportadas, fontes de dados,
arquitetura sugerida `ChartGuardLineState` + `ChartGuardReadOnlyLineRenderer`,
contrato de segurança, regras visuais e checklist da Fase 3.1) está em
`docs/chart-lines-readonly-phase3.md`. A implementação só começa depois que
esse documento estiver revisado e o checklist da Fase 3.1 estiver
referenciado a partir de `docs/manual-test-checklist.md`.

### Fase 4 — Comandos Dry-Run

Objetivo:

- botões criam comandos;
- linhas criam comandos simulados;
- tudo passa pelo Safe Core;
- logs mostram validação e decisão;
- nenhuma ordem real.

### Fase 5 — Primeira Entrada Em Replay/Sim

Objetivo:

- habilitar rota mínima;
- com ou sem stop conforme estratégia/configuração;
- começar pelo caso mais seguro validável;
- checklist manual obrigatório;
- nada em conta live.

### Fase 6 — Stops, Targets E Proteções Em Replay/Sim

Objetivo:

- stop real;
- target real;
- múltiplos targets;
- BE;
- lock R;
- trail;
- arrasto de linhas;
- tudo com idempotência e checklist.

### Fase 7 — Operação Pelo Gráfico

Objetivo:

- Ctrl + botão direito;
- menu inteligente próprio;
- alterar ordens por linhas;
- manter comportamento seguro e previsível.

### Fase 8 — Produto Personalizável

Objetivo:

- persistência de configurações;
- perfis/estratégias;
- layout personalizável;
- preferências por usuário.

## Matriz De Decisão

| Item | Layout base | Informação/função melhorada por | Lógica futura |
| --- | --- | --- | --- |
| Painel lateral | CunhaTrader Gold | ChartGuard atual | Host seguro |
| Resize | Novo ChartGuard | NTB/Chart Trader | Thumb seguro |
| Header | CunhaTrader Gold | ChartGuard + NTB | Read-only state |
| Estratégias | CunhaTrader Gold | NinjaTrader nativo | Config model |
| Comprar/Vender | CunhaTrader Gold | SafeCore | ProtectedEntryCommand |
| Pânico | CunhaTrader Gold | SafeCore | Flatten/Cancel scoped |
| Stops | CunhaTrader Gold | NTB + SafeCore | ProtectionService |
| Alvos | CunhaTrader Gold | NTB + SafeCore | Command route |
| Lock R | CunhaTrader Gold | SafeCore | ProtectionService |
| Risco | CunhaTrader Gold | NTB | RiskGuard configurável |
| Sessão | CunhaTrader Gold | NTB | Session state |
| Linhas | CunhaTrader Gold | NinjaTrader/NTB | Read-only first, command later |
| Configurações | CunhaTrader Gold | Necessidade atual | Persistência futura |

## Garantias

- Layout pode ser parecido com o CunhaTrader Gold.
- Botões/selects podem seguir a mesma ideia do CunhaTrader Gold.
- Nenhuma lógica antiga será reaproveitada.
- O que já funcionou no ChartGuard atual não deve ser perdido.
- Informações úteis do NTB devem ser incorporadas quando fizerem sentido.
- O usuário deve poder escolher entre alerta, bloqueio ou desligado em regras de
  risco.
- Estratégias devem poder ser criadas, editadas, excluídas e selecionadas.
- Linhas no gráfico devem se tornar parte operacional do produto.
- Nada fica ativo antes de ter lógica confiável e checklist.
