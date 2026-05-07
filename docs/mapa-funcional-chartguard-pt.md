# Mapa Funcional do Essencial ChartGuard

Este documento é a versão em português do destino funcional do Essencial
ChartGuard. Ele existe para orientar a visão do produto: o que vamos preservar
do antigo CunhaTrader Gold, o que vamos aprender observando o NTB_TradeSafe, o
que vamos reconstruir com segurança, e em qual fase cada coisa deve entrar.

Os documentos técnicos em inglês continuam sendo usados para orientar
implementação e validação. Este aqui é o mapa para acompanhamento humano do
produto.

## Objetivo

Criar um painel lateral para NinjaTrader 8 com a aparência, ergonomia e
organização visual que eram boas no CunhaTrader Gold, mas com uma arquitetura
nova, segura, testável e validada.

O painel final deve ser prático para operação real:

- mostrar posição e risco com clareza;
- permitir entrada protegida;
- permitir stops e alvos flexíveis;
- mostrar linhas no gráfico;
- oferecer personalização;
- evitar qualquer comportamento escondido ou perigoso.

## Regra Principal

Podemos preservar a ideia visual, disposição dos blocos, formas de seleção e
experiência de uso do CunhaTrader Gold.

Não vamos preservar a lógica antiga de trading.

Tudo que no futuro enviar, cancelar, modificar, proteger ou fechar ordem deve
passar pelo Safe Core. Nenhum botão, indicador, clique no gráfico ou hotkey pode
chamar diretamente as APIs de ordem do NinjaTrader.

## O Que Vamos Preservar Do CunhaTrader Gold

- Painel lateral embutido no chart.
- Visual escuro, compacto e profissional.
- Destaques amarelo/dourado nas seções.
- Organização em blocos.
- Botões grandes de compra e venda.
- Área de entrada.
- Área de posição ativa.
- Área de stops e takes.
- Área de risco da conta.
- Área de sessão.
- Linhas desenhadas no gráfico com legendas.
- Formas práticas de adicionar e selecionar stops/alvos.
- Ideia de operação rápida diretamente no chart.

O objetivo é manter a sensação de uso do painel antigo, porque ele tinha um
desenho muito bom. A diferença é que agora cada função precisa ser confiável
antes de ficar ativa.

## O Que Não Vamos Preservar Do CunhaTrader Gold

- Código antigo de envio de ordem.
- Rotas paralelas de ordem.
- Panic/flatten antigo.
- Hotkeys antigas.
- Right-click antigo se ele enviar ordem direto.
- Bracket antigo sem validação.
- Risk engine antigo sem testes.
- Qualquer função que parecia existir visualmente, mas não era confiável.
- Qualquer mistura de responsabilidade onde UI, risco, envio e proteção ficam
  todos no mesmo arquivo.

O antigo serve como referência de desenho e fluxo, não como base segura de
execução.

## O Que Vamos Observar No NTB_TradeSafe

O NTB_TradeSafe deve ser usado como referência de uso, não como fonte de código.

Vamos observar:

- quais informações ele mostra;
- quais funções parecem úteis;
- como ele organiza risco e proteção;
- como ele lida com conta, instrumento e posição;
- como ele apresenta controles de segurança;
- como o painel redimensiona;
- quais funções faltavam no CunhaTrader Gold.

Não vamos copiar código, decompilar ou tratar o comportamento dele como
automaticamente seguro. Tudo que for aproveitado como ideia será refeito dentro
do nosso Safe Core.

## Informações Que O Painel Deve Mostrar

Informações principais:

- Conta.
- Instrumento.
- Direção da posição: Flat, Long, Short ou Unknown.
- Quantidade da posição.
- Preço de entrada ou preço médio.
- Último preço de execução, quando útil.
- Lucro/prejuízo aberto em ticks.
- Lucro/prejuízo aberto em pontos.
- Lucro/prejuízo aberto em porcentagem.
- Lucro/prejuízo aberto em valor financeiro.
- Ordens pendentes.
- Stop ativo.
- Alvos ativos.
- Estado de risco.
- Estado da sessão.
- Estado de bloqueio ou permissão para novas entradas.

O cabeçalho deve usar o espaço nobre do painel com informações diretas para a
operação. Evitaremos duplicar informação sem necessidade.

## Áreas Do Painel Final

### Cabeçalho

Função: leitura rápida durante a operação.

Deve mostrar:

- Essencial ChartGuard.
- Conta e instrumento.
- Posição e quantidade.
- Preço médio/entrada.
- PnL aberto quando disponível.
- Estado geral: observando, sim/playback, live, bloqueado, alerta.

### Posição Ativa

Função: detalhar a operação atual.

Deve mostrar:

- Direção.
- Quantidade.
- Entrada/preço médio.
- Última execução.
- PnL em ticks.
- PnL em pontos.
- PnL em porcentagem.
- PnL em dinheiro.
- Stops ativos.
- Alvos ativos.
- Ordens pendentes.

### Entrada

Função: preparar e executar entradas protegidas.

Deve ter:

- Tipo de entrada: mercado, limite, stop market, stop limit.
- Quantidade.
- Sizing.
- Stop obrigatório ou configurável.
- Target opcional.
- Um ou vários alvos.
- Botão Comprar.
- Botão Vender.

Regra: enquanto a rota segura não existir, esses controles podem aparecer apenas
desabilitados ou como placeholders. Quando ficarem ativos, devem funcionar de
verdade.

### Stops E Alvos

Função: adicionar, visualizar e ajustar proteção da operação.

Deve permitir futuramente:

- Adicionar stop.
- Editar stop.
- Adicionar um alvo.
- Adicionar vários alvos.
- Remover alvo.
- Visualizar alvos no painel.
- Visualizar alvos no gráfico.
- Trabalhar com ticks, pontos ou preço.

### Proteção

Função: proteger posição aberta.

Deve incluir:

- Breakeven.
- Travar 1R.
- Travar 2R.
- Travar 3R.
- Stop móvel/trailing.
- Proteção baseada no risco inicial.

Regra: Lock R deve sempre usar o risco inicial da operação, não o stop já
movido.

### Risco Da Conta

Função: impedir que o trader ultrapasse limites importantes.

Deve mostrar:

- Limite diário.
- Perda máxima.
- Drawdown.
- Espaço restante até bloqueio.
- Status de risco.
- Se novas entradas estão permitidas ou bloqueadas.
- Regras de conta/prop firm quando aplicável.

Regra: risco pode bloquear novas entradas, mas não deve bloquear flatten ou
ações protetivas.

### Sessão

Função: mostrar contexto do dia/sessão.

Deve mostrar:

- PnL da sessão.
- Trades da sessão.
- Tempo de sessão.
- Posição aberta.
- Resultado realizado.
- Resultado aberto.
- Estado histórico/monitoramento quando existir.

### Configurações

Função: tornar o painel personalizável.

Deve permitir futuramente:

- Mostrar ou ocultar seções.
- Escolher modo compacto ou completo.
- Escolher unidade de PnL: ticks, pontos, %, dinheiro.
- Definir stop padrão.
- Definir target padrão.
- Definir sizing padrão.
- Definir comportamento de linhas no gráfico.
- Escolher informações visíveis no cabeçalho.
- Ajustar preferências de conta/instrumento.

Quanto mais personalizável, melhor, desde que não esconda alertas críticos de
segurança sem intenção clara do usuário.

## Funções No Gráfico

O ChartGuard deve ser uma ferramenta de chart, não apenas um painel.

Funções desejadas:

- Linhas de entrada.
- Linhas de stop.
- Linhas de alvo.
- Linhas de ordens limite.
- Linhas de drawdown.
- Legendas nas linhas.
- Cores coerentes com o painel.
- Informações iguais no painel e no gráfico.

Interação futura:

- Adicionar ordens com Ctrl + botão direito.
- Escolher compra/venda/stop/limite/mercado de forma organizada.
- Evitar interferir no menu nativo do NinjaTrader sem necessidade.

Regra: clique no gráfico nunca deve enviar ordem direto. Ele deve criar uma
intenção/comando e passar pelo Safe Core.

## Funções Planejadas

| Área | Função | Origem da ideia | Manter layout antigo? | Status |
| --- | --- | --- | --- | --- |
| Painel | Painel lateral embutido | CunhaTrader Gold | Sim | Validado parcialmente |
| Painel | Redimensionar horizontalmente | NTB/Chart Trader | Sim | Validado |
| Posição | Direção e quantidade | Cunha/NTB | Sim | Validado |
| Posição | Entrada/preço médio | Cunha/NTB | Sim | Validado parcialmente |
| Posição | PnL ticks/pontos/%/$ | Usuário/NTB | Sim | Planejado |
| Entrada | Comprar/Vender mercado | Cunha | Sim | Futuro |
| Entrada | Limite/stop/stop limit | Cunha/necessidade | Sim | Futuro |
| Entrada | Stop obrigatório/configurável | SafeCore | Sim | Futuro |
| Entrada | Um ou vários alvos | Cunha/necessidade | Sim | Futuro |
| Proteção | Breakeven | Cunha/NTB | Sim | Futuro |
| Proteção | Lock 1R/2R/3R | Cunha | Sim | Futuro |
| Proteção | Stop móvel/trail | Cunha/NTB | Sim | Futuro |
| Risco | Limite diário/drawdown | Cunha/NTB | Sim | Futuro |
| Sessão | PnL/trades/tempo | Cunha | Sim | Futuro |
| Chart | Linhas de entrada/stop/alvo | Cunha | Sim | Futuro |
| Chart | Ctrl + botão direito | Cunha | Refeito com segurança | Futuro |
| Config | Mostrar/ocultar blocos | Necessidade atual | Novo | Futuro |
| Config | Preferências de unidades | Necessidade atual | Novo | Futuro |

## Fases De Construção

### Fase 0 — Painel De Observação

Status: em andamento e já validado em parte.

Já temos:

- painel à direita;
- redimensionamento horizontal;
- leitura de conta/instrumento;
- snapshot inicial da posição;
- atualização por eventos de ordem/execução;
- nenhuma ordem enviada pelo ChartGuard.

### Fase 1 — Layout Operacional Read-Only

Objetivo:

- transformar o painel atual no desenho operacional final;
- organizar cabeçalho, posição, entrada, proteção, risco, sessão e settings;
- manter botões e controles desabilitados;
- melhorar nomes dos campos;
- reduzir duplicidade visual;
- preservar o visual inspirado no CunhaTrader Gold.

### Fase 2 — Linhas Read-Only No Gráfico

Objetivo:

- desenhar linhas observadas de posição, ordens, stop e alvo;
- adicionar legendas;
- não permitir clique/interação ainda.

### Fase 3 — Comandos Dry-Run

Objetivo:

- botões criam comandos;
- comandos passam pelo Safe Core;
- submitter ainda simula;
- logs comprovam validação;
- nenhuma ordem real é enviada.

### Fase 4 — Primeira Entrada Protegida Em Replay

Objetivo:

- habilitar uma rota estreita e validada;
- começar por entrada a mercado com stop obrigatório;
- somente Replay/Sim;
- checklist manual obrigatório.

### Fase 5 — Proteções

Objetivo:

- Breakeven;
- Lock 1R;
- depois Lock 2R/3R;
- depois trail;
- tudo validado em Replay.

### Fase 6 — Alvos, Stops E Bracket Completo

Objetivo:

- múltiplos alvos;
- edição de stop;
- proteção consistente;
- idempotência contra eventos duplicados.

### Fase 7 — Configurações E Personalização

Objetivo:

- mostrar/ocultar áreas;
- salvar preferências;
- modo compacto/completo;
- unidades de PnL;
- preferências de linhas.

### Fase 8 — Interação Pelo Gráfico

Objetivo:

- Ctrl + botão direito;
- menu inteligente;
- criar ordens e proteções pelo gráfico;
- sempre passando pelo Safe Core.

## Garantias Do Projeto

- O layout antigo pode ser preservado como experiência visual.
- A lógica antiga não será preservada.
- Nenhuma função fica ativa antes de ser segura.
- Nenhum botão ativo pode ser “de mentira”.
- Nenhuma rota paralela de ordem será aceita.
- Toda função crítica precisa passar por checklist em Replay/Sim.
- O painel precisa ser bonito, mas principalmente confiável.

