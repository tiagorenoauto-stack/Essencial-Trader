# Status da sessão — onde paramos

Documento de continuidade. Sempre que uma sessão terminar, atualize este
arquivo para que a próxima sessão saiba em poucos segundos onde
estamos. Última atualização: **2026-05-07**, commit `f3971d8`.

## Estado em uma frase

A casca visual do painel antigo foi adotada como `EssencialChartGuardPanel`
no projeto Essencial. Compila, abre num chart, mostra dados observados
nos campos certos, e está com tudo desabilitado. Falta só apagar
referências antigas (sub-passo 2c) e seguir para conexões reais (Compra
/ Venda primeiro, com cuidado).

## O que está rodando hoje

- **Painel lateral injetado no chart**, redimensionável horizontalmente
  via Thumb (esquerda do painel).
- **Layout visual** copiado do painel antigo: header com brand gold +
  dot de status + ⚙ engrenagem (disabled), card no topo com Modo /
  Unidade / Conta / Instrumento / Estratégia (todos disabled), seção
  ENTRADA com Tipo / Qtd / Sizing / Compra / Venda / Pânico (todos
  disabled), seção POSIÇÃO ATIVA com 12 linhas de leitura observada,
  chips de Takes / Stop, Trail combo, botões Travar 1R/2R/3R + BE
  (todos disabled), seção RISCO com Saldo / Quebra em / % comprometido
  + barra de progresso + Limite diário / Status / Block, seção SESSÃO
  com Trades / PnL / Tempo, e card OBSERVAÇÃO com dots de Posição
  inicial e Eventos. Toast overlay no topo (visual pronto, sem
  disparo).
- **Snapshot inicial** de posição da conta (lê `Account.Positions` uma
  vez no attach via `NinjaTraderPositionSnapshotReader`).
- **EventBridge read-only** (`NinjaTraderAccountEventBridge`) escuta
  `Account.OrderUpdate` / `Account.ExecutionUpdate` e roteia via
  `OrderEventRouter` para `ObservedAccountState` no Safe Core.
- **Linhas read-only no chart** (`ChartGuardReadOnlyLineRenderer`)
  desenham `ECG Entry / ECG Last / ECG Stop Draft / ECG T1 Draft /
  ECG T2 Draft` quando `EnableDraftPreview=true` e há referência de
  preço segura. Default `false`: nenhuma linha.
- **Draft preview** `Preview Scalper` aplicado no painel quando o
  parâmetro do indicador está ligado.
- **Logs estáveis** com prefixos `[EssencialUI]`, `[EssencialOrder]`,
  `[EssencialCommand]`, `[EssencialProtect]`, `[EssencialRisk]`.
- **Labels do painel em pt-BR** (Direção / Quantidade / Preço médio /
  Última execução / Ordens pendentes / Takes / Proteção / Limite
  diário / Tempo / Posição inicial / Eventos / Observação). PnL,
  Trades, Stop e Status mantidos como estão.

## Contrato de segurança que continua válido

Validado por grep no repo inteiro:

- **Zero** `Account.Submit` / `Account.CreateOrder` / `AtmStrategyCreate`
  / `EnableForControlledTest` chamados fora do
  `NinjaTraderAccountAdapter` (que continua disabled-by-default e
  gated por acknowledgement).
- **Zero** handlers `Click` / `SelectionChanged` / `TextChanged` /
  `MouseDown` / `MouseUp` / `MouseMove` / `PreviewMouse*` / `KeyDown` /
  `KeyUp` / `KeyBinding` / `InputBindings` / `ContextMenu` no painel.
  Único handler do painel é o `DispatcherTimer.Tick` do toast (auto-
  dismiss visual).
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
| **Adoção da casca visual antiga** | Trazer tema rico + painel completo do projeto antigo, mantendo Safe Core embaixo | 🔧 **2 de 3 sub-passos concluídos.** Sub-passo 2c (limpeza de resíduos) pendente. |
| Fase 4 | Comandos dry-run | ⏳ Próxima fase prevista |
| Fase 5 | Primeira entrada protegida em Replay/Sim (Compra/Venda) | ⏳ Combinada como **primeira coisa real** depois da casca |
| Fase 6+ | Stops/targets/proteções, settings, gráfico avançado | ⏳ Futuro |

## Próximo passo combinado

1. **Sub-passo 2c — apagar referências antigas** do painel anterior
   que ainda estão como nomes vagos no código (campos não usados,
   etc.). Trabalho de limpeza, sem mudança visual. Um commit só. Você
   compila uma vez para confirmar que nada quebrou.
2. **Fase 4 / Compra-Venda primeiro** — habilitar a primeira rota real
   pelo `TradeCommandService`. Conforme acordado: começamos por
   **Compra/Venda**, com você testando antes de qualquer fluxo
   delicado, sem implementar várias coisas ao mesmo tempo, sem
   atropelo. Toda alteração que envolver risco passa por sua revisão
   antes de eu rodar.

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
- Commit no topo (no momento desta atualização): `f3971d8`
- Working tree: limpo (`git status --short` vazio).

## Histórico recente (últimos commits desta sessão)

```
f3971d8 chore(panel): rename row label Alvos to Takes for consistency
fdb763b chore(panel): translate row labels to Portuguese
b9e0533 fix(panel): remove duplicate summary chip line above active position
36c38aa fix(panel): qualify Panel control type in DisableChildren
3e6a87a feat(panel): adopt legacy visual shell, keep host API
6dd5c9e fix(panel): qualify Border control type in theme styles
ba919c2 chore(panel): bring rich visual theme, keep legacy aliases
2269f4e docs: specify phase 3.2 chart-lines visual polish
964727c fix(chart): add NinjaTrader.Gui using for DashStyleHelper
a114cec fix(chart): improve read-only line visibility
d0fa1ca chore(chart): keep read-only line renderer linter cleanup
e0d2cdb feat(chart): add read-only draft line renderer
a1047f8 docs: specify phase 3 read-only chart lines
ad23bad docs: mark phase 2.3 draft preview validation
1eb7163 fix(panel): clear entry average when flat
d8eff59 feat(panel): add draft preview validation mode
27967b1 docs: mark phase 2.2 panel validation
93ab44b docs: add project working rules
e4f4417 chore: add read-only chartguard baseline
```

## Como começar a próxima sessão

1. `git status --short` para garantir working tree limpo.
2. `git pull` (se trabalhou em outra máquina).
3. Abrir este arquivo.
4. Decidir: sub-passo 2c (limpeza) ou pular direto para a Fase 4
   (Compra/Venda dry-run).
5. Combinar comigo o escopo da próxima tarefa antes de eu mexer em
   código.
