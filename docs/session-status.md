# Status da sessão — onde paramos

Documento de continuidade. Sempre que uma sessão terminar, atualize este
arquivo para que a próxima sessão saiba em poucos segundos onde
estamos. Última atualização: **2026-05-08**, commit `5aa251f`.

## Estado em uma frase

Painel limpo de resíduos do Cunha, reorganizado em seções alinhadas
(header com Conta + Instrumento em molduras separadas, card Proteção
próprio entre Entrada e Posição ativa, alinhamento por colunas em
Takes/Stop/Trail/Lock R), e a checkbox "Visível" do indicador agora
mostra/esconde o painel inteiro (sem deixar gutter no chart). Pronto
para a Fase 4 (Compra/Venda dry-run).

## O que está rodando hoje

- **Painel lateral injetado no chart**, redimensionável horizontalmente
  via Thumb (esquerda do painel), com show/hide refletindo o checkbox
  **Visível** do indicador (poll de 500ms no `OnRefreshTick` do host).
  A largura redimensionada pelo usuário é preservada entre toggles.
- **Header em uma linha:** dot · `Essencial ChartGuard` (FontSizeBrand) ·
  `[Conta ▾]` · `[Instrumento ▾]` · ⚠ · ⚙. Conta e instrumento são dois
  TextBlocks dentro de molduras "select-like" (Border dourado + caret
  decorativo, sem dropdown). modeLine vem para o tooltip do dot.
- **Card top:** Modo / Unidade na primeira linha, Estratégia em linha
  cheia abaixo. (Os combos de Conta e Instrumento que estavam aqui foram
  removidos — eram placeholders nunca alimentados, redundantes com o
  header.)
- **Seção Entrada:** Tipo / Qtd / Sizing / Compra / Venda / Pânico
  (todos disabled).
- **Seção Proteção** (novo card próprio entre Entrada e Posição ativa):
  Takes (label · input · `+` · chips) e Stop (label · input · `+` ·
  chips) compartilhando a mesma grade de colunas. "Stop móvel" + combo
  vivem só na linha do Stop (linha do Takes mantém a largura cheia para
  vários chips). Travar 1R/2R/3R/→BE em quatro colunas iguais. Tudo
  disabled.
- **Seção Posição ativa:** só leitura — header de posição, PnL inline e
  12 LabelValueRows (Direção / Quantidade / Preço médio / Última
  execução / PnL ticks/points/%/$ / Ordens pendentes / Stop / Takes /
  Proteção). Os controles de proteção saíram daqui na refatoração.
- **Risco / Sessão / Observação** inalterados.
- **Snapshot inicial** de posição da conta (`Account.Positions` lido
  uma vez no attach via `NinjaTraderPositionSnapshotReader`).
- **EventBridge read-only** (`NinjaTraderAccountEventBridge`) escuta
  `Account.OrderUpdate` / `Account.ExecutionUpdate` e roteia via
  `OrderEventRouter` para `ObservedAccountState` no Safe Core. Daí o
  host empurra `SetAccountAndInstrument`, `SetObservedState`,
  `SetSnapshotStatus`, `SetBridgeStatus` no painel.
- **Linhas read-only no chart** (`ChartGuardReadOnlyLineRenderer`)
  desenham `ECG Entry / ECG Last / ECG Stop Draft / ECG T1 Draft /
  ECG T2 Draft` quando `EnableDraftPreview=true` e há referência de
  preço segura. Default `false`: nenhuma linha.
- **Draft preview** `Preview Scalper` aplicado no painel quando o
  parâmetro do indicador está ligado.
- **Logs estáveis** com prefixos `[EssencialUI]`, `[EssencialOrder]`,
  `[EssencialCommand]`, `[EssencialProtect]`, `[EssencialRisk]`.

## Contrato de segurança que continua válido

Validado por grep no repo inteiro:

- **Zero** `Account.Submit` / `Account.CreateOrder` / `AtmStrategyCreate`
  / `EnableForControlledTest` chamados fora do
  `NinjaTraderAccountAdapter` (que continua disabled-by-default e
  gated por acknowledgement).
- **Zero** handlers `Click` / `SelectionChanged` / `TextChanged` /
  `MouseDown` / `MouseUp` / `MouseMove` / `PreviewMouse*` / `KeyDown` /
  `KeyUp` / `KeyBinding` / `InputBindings` / `ContextMenu` em
  `AddOns/Panel/`. Único handler do painel é o `DispatcherTimer.Tick`
  do toast (auto-dismiss visual).
- **Zero** `using NinjaTrader.Cbi` / `NinjaTrader.Data` em
  `AddOns/SafeCore/`, `AddOns/Panel/`, `AddOns/Panel/ChartLines/` ou
  `AddOns/Panel/Models/`.
- **Zero** persistência: nenhum `File.*`, `XmlSerializer`,
  `JsonSerializer` ou storage de settings.
- **Zero** ordens criadas pelo ChartGuard durante toda a sessão de
  validação.

## Onde estamos no plano oficial

Referência: `docs/plano-integrado-chartguard-pt.md`

| Fase | O que é | Status |
|---|---|---|
| Fase 0 | Base técnica (painel lateral, resize, snapshot, EventBridge, ObservedAccountState, sem ordem) | ✅ Validada |
| Fase 1 / 2 | Shell visual + drafts + chart lines read-only | ✅ Validada |
| Fase 2.3 | Validação dos draft mutators no NT | ✅ Validada (2026-05-07) |
| Fase 3.1 | Implementação das chart lines read-only | ✅ Validada |
| Fase 3.2 | Polimento visual das chart lines (price marker, badge VENDIDO/COMPRADO, customização) | 📄 Especificada em `docs/chart-lines-readonly-phase3.2-visual.md`; **não implementada** |
| **Adoção da casca visual antiga** | Trazer tema rico + painel completo do projeto antigo, mantendo Safe Core embaixo | ✅ Concluída (2026-05-08): limpeza dos resíduos do Cunha + reorganização visual + sync visibilidade. |
| Fase 4 | Comandos dry-run | ⏳ **Próxima fase** |
| Fase 5 | Primeira entrada protegida em Replay/Sim (Compra/Venda) | ⏳ Combinada como **primeira coisa real** depois da casca |
| Fase 6+ | Stops/targets/proteções, settings, gráfico avançado | ⏳ Futuro |

## Próximo passo combinado

**Fase 4 / Compra-Venda primeiro** — habilitar a primeira rota real
pelo `TradeCommandService`. Conforme acordado: começamos por
**Compra/Venda**, com você testando antes de qualquer fluxo delicado,
sem implementar várias coisas ao mesmo tempo, sem atropelo. Toda
alteração que envolver risco passa por sua revisão antes de eu rodar.

Antes de iniciar a Fase 4 vale combinar:

- escopo mínimo do dry-run (Compra a mercado em qty=1 num chart Sim/
  Replay, sem stop nem target ainda?);
- onde fica o gate (`EnableForControlledTest` continua off; precisamos
  decidir se esta fase já o aciona ou se o dry-run primeiro só simula a
  rota de comando até o ponto antes do submit real).

## Princípios de trabalho que valem para qualquer sessão

Ler antes de começar: `docs/project-working-rules.md`. O que importa
no dia-a-dia:

- Uma intenção por commit (chore / feat / fix / docs / refactor / test).
- Compactar o relatório nas tarefas pequenas. Linguagem de usuário no
  chat; precisão técnica fica na mensagem do commit.
- Eu releio o `.cs` editado antes de te entregar, conferindo `using`s
  e tipos contra os indicadores built-in do NT8. Você não deve abrir
  o NinjaTrader várias vezes para o mesmo erro de compilação.
- Spec doc separado só para mudança grande (novo contrato /
  arquitetura). Polimento visual e ajustes pequenos vão direto no
  commit.
- Ações com risco real (mexer em rota de ordem, hotkey, click no
  chart) sempre passam por confirmação sua antes.

## Repositório

- GitHub: <https://github.com/tiagorenoauto-stack/Essencial-Trader>
- Branch: `main` (com upstream `origin/main`)
- Commit no topo (no momento desta atualização): `5aa251f`
- Working tree: limpo (`git status --short` vazio).

## Histórico recente (últimos commits desta sessão)

```
5aa251f fix(host): make panel show/hide idempotent to avoid orphan columns
f7aa2d4 fix(host): remove and re-add panel columns on hide/show
a04d75f fix(host): poll IsVisible from refresh tick instead of OnRender
84dfde8 fix(host): mirror indicator Visible toggle onto the side panel
992ec17 fix(panel): pin Stop móvel to the Stop row to free the Takes row
8abea7c fix(panel): align header fields and Proteção rows, breathe under section underlines
b1a79f6 feat(panel): split protection into its own card after entrada
daf3cb7 feat(panel): consolidate account/instrument into header frame
fc5a5a3 chore(panel): drop cunha-era aliases and dead theme builders
48a130a docs: record session status and next-step plan
```

## Como começar a próxima sessão

1. `git status --short` para garantir working tree limpo.
2. `git pull` (se trabalhou em outra máquina).
3. Abrir este arquivo.
4. Combinar comigo o escopo da Fase 4 (dry-run de Compra/Venda) —
   especialmente: até onde a rota chega no primeiro commit, e se
   `EnableForControlledTest` é acionado já nesta fase ou só em Fase 5.
5. Só então começar a mexer em código.
