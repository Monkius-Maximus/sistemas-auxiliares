# CLAUDE.md — Sistema de Cozinha Emergente

Godot 4.7 / C# (.NET). Sistema de preparo de comida **sem receitas fixas**: o jogador escolhe um
recipiente, adiciona ingredientes em quantidades variáveis, e o jogo deriva nome, nutrição e
qualidade do resultado. Destinado a um life sim estilo The Sims / Project Zomboid.

## Mapa

```
Data/      Resources do Godot (.tres-ready). Autoria de conteúdo.
           IngredientDef · BaseItemDef · RecipeAnchor · VendorDef · DishModifier ·
           FlavorProfile · Nutrition · CookingEnums

Cooking/   Modelo. Nenhuma chamada de API do Godot.
           CookingSession (estado mutável) · Pantry (lotes com idade) · Freshness (a régua) ·
           DishEvaluator + DishNamer (puros) · ModifierStack · CookedDish (imutável) ·
           IngredientStack · QuickMealPlanner + QuickMealOption (menu rápido, puros)

Household/ A casa: Household (relógio do mundo, diário, dinheiro) · Meal (prato pronto) ·
           Sim (fome, sede, humor por moodlets) · Autonomy (o que o Sim decide sozinho, puro) ·
           ShoppingTrip (o carrinho de uma ida às compras).
           Nenhuma chamada de API do Godot.

Content/   O conteúdo, em .tres: Ingredients/ · Bases/ · Anchors/ · Vendors/.
           ContentLibrary lê as pastas — ingrediente novo é arquivo novo, não linha de C#.

addons/context_panel/
           O shell reutilizável, sem nada de cozinha dentro. Namespace ContextUi,
           sem dependência deste projeto: é o mesmo painel que vai para os outros jogos.
           PanelContext (a definição: 7 regiões) · PanelPrimitives (desenho) ·
           ContextPanel (o painel) · PanelSkin (cores por papel) ·
           PanelFocus + PanelCell (grafo de foco e célula focável) · PromptBar.
           Contrato próprio em addons/context_panel/README.md.

UI/        As definições de contexto deste sistema: CookingContext (painel manual) ·
           QuickMealContext (menu rápido) · FridgeContext (geladeira: comer) ·
           ShopContext (mercearia: comprar) · DishReadout ·
           PantryText · CookingDemo. Zero estado próprio.

Tools/     BalanceCheck — roda o avaliador real sobre casos de referência.
Scenes/    CookingDemo.tscn — cena principal.
```

## Fluxo pretendido

O **menu rápido é a porta de entrada padrão** — clicar no fogão abre a lista estilo The Sims
(nome, custo, qualidade prevista), um clique resolve. O painel manual fica atrás da última
linha, "Preparar manualmente". A profundidade é opcional de propósito: a maioria dos
jogadores vai preferir o caminho automatizado, e o sistema profundo existe para dar peso a
essa escolha.

As linhas do menu rápido **são** as `RecipeAnchor`. Não existe lista de pratos escrita
à parte: `QuickMealPlanner` deriva tudo das âncoras que o Sim conhece, e custo e qualidade
prevista saem do mesmo `DishEvaluator` que o painel usa.

Os dois são o **mesmo painel** com definições diferentes — ver `docs/context-panel.md`.
Cozinhar, comprar e construir devem ser definições de contexto, não janelas novas.

## Invariantes — não quebrar sem discutir

1. **`DishEvaluator.Evaluate` é pura.** Sem estado, sem efeito colateral, sem `GD.Print`.
   O preview ao vivo e o ato de cozinhar chamam essa mesma função — é isso que garante
   que o número mostrado é o número recebido. Nunca criar uma "versão rápida para o preview".

2. **A UI não guarda estado do modelo.** O `ContextPanel` escuta `CookingSession.Changed` e
   reconstrói a definição inteira. Se aparecer um campo no painel que espelha algo da sessão,
   é bug — o estado de tela que não tem dono no modelo (qual contexto está aberto, qual linha
   está marcada no menu) fica no host que abriu a interação, nunca no painel.

   A exceção é o **foco**: `PanelFocus` guarda `(região, índice)` dentro do painel. Não é
   estado do modelo nem do host — é onde o cursor do jogador está *neste* painel, e morre com
   ele. Guardá-lo no host obrigaria todo host a saber o que é uma região. Continua valendo a
   regra de fundo: nada em `PanelFocus` espelha o modelo, e o painel nunca lê de lá para
   decidir o que desenhar.

3. **`CookingSession` é a única que move unidades entre despensa e prato.** As duas
   quantidades mudam na mesma operação. A despensa virou classe própria (`Pantry`, em lotes)
   porque envelhecer e escolher lote são responsabilidade dela — mas as operações que tiram e
   devolvem são `internal` e só a sessão as chama. O tempo também passa pela sessão
   (`AdvanceTime`), porque passar o tempo muda o que o painel mostra e precisa disparar `Changed`.

4. **`Cooking/` e `Household/` não importam `Godot`.** É o que mantém o modelo testável fora do engine.
   Se precisar de `GD.Print` para debugar, o print vai na UI ou em `Tools/`.

5. **Nenhuma lista de pratos autorada à mão.** Nome, custo e qualidade são sempre derivados —
   das âncoras, dos preços por unidade e do avaliador. Se aparecer um prato com preço ou
   qualidade escritos direto, é regressão.

6. **Pré-condição violada lança exceção.** A UI desabilita o que não pode ser feito;
   exceção subindo da sessão é bug de UI, não input do jogador. Não engolir com try/catch.

## Tuning

As constantes de balanceamento estão no topo de `Cooking/DishEvaluator.cs`, num bloco
marcado. **Mexer nelas é decisão de design, não bugfix.** Não ajustar constantes para fazer
um caso específico passar sem rodar o `BalanceCheck` inteiro depois.

Qualidade é o **produto** de cinco fatores 0..1 (tempero, equilíbrio, harmonia, variedade,
frescor), limitado pelo teto de perícia `0.50 + 0.05 × nível`. Multiplicativo de propósito:
um erro grave em qualquer eixo afunda o prato inteiro. Não trocar por soma ponderada.

Distribuição-alvo: lixo ~0.05 · preguiçoso ~0.45 · sólido ~0.80 · excelente ~0.95.

Rodar o verificador:
```
godot --headless --path . --script res://Tools/BalanceCheck.cs
```

## Convenções de autoria de conteúdo

- Vetor de sabor de ingrediente: soma L1 entre `0.2` (batata, insosso) e `1.0` (bacon, assertivo).
- Tempero: vetor unitário num eixo + `Potency` 3–4, `WeightPerUnit` 0.001.
- Ingrediente: `WeightPerUnit` 0.010. O peso define a fração de massa na penalidade de frescor.
- `RecipeAnchor` são poucas de propósito — atalhos premiados, não o conteúdo principal.

## Pendências

| O quê | Onde | Estado |
|---|---|---|
| Morte por fome ou sede | `Sim.PassHours` | hoje zerar desmaia (recuperável); morte é decisão de tom do jogo |
| Autonomia além de comer e beber | `Household.ActOnNeeds` | só fome e sede; dormir, higiene e diversão entram aqui |
| `UnhappinessRelief` / `BoredomRelief` | `CookedDish` | fórmulas antigas sem consumidor; o humor de comer usa os moodlets do `Sim` |
| Mais de um Sim na casa | `Household` | um Sim só; refeição em grupo e porções divididas entram aqui |
| Armazenamento (geladeira × bancada × geladeira quebrada) | `Pantry.Age` | tudo envelhece como em geladeira; entra como multiplicador do tempo |
| Reação por traço de personalidade | não existe | usar `Group` + `DominantAxis` |
| Ícones de verdade | `IngredientDef.Icon` | campo pronto no `.tres`, sem arte — desenha o `TintColor` |
| Primitivos `grid.dual` e `text` | `addons/context_panel/PanelPrimitives.cs` | no mock, sem sistema que os use |
| Bulk +5 no stepper | — | dispensado: segurar A/+ já repete acelerando, e dois jeitos de fazer a mesma coisa violam o estilo |
| Emprego | `Household.DailyIncome` | salário fixo às 9h é um substituto declarado; sem renda toda casa morre de fome |
| Autonomia de compras | `Household.ActOnNeeds` | o Sim não compra sozinho, como no The Sims 4; é papel do jogador |
| Entrega e deslocamento | `Household.Buy` | a compra chega na hora; ir à loja não gasta tempo |

NPCs devem cozinhar com este mesmo código: perícia baixa produz prato ruim por ter menos
slots e teto menor, não por uma tabela separada de "pratos de NPC".

## Conteúdo

Ingredientes, recipientes e âncoras vivem em `Content/**/*.tres`. **Adicionar conteúdo é
adicionar arquivo** — `ContentLibrary` varre as pastas, e não existe lista para registrar o
arquivo novo. É o mesmo trato que sustenta uma década de DLC em jogos data-driven: o motor
define os substantivos, o conteúdo é combinação deles.

A biblioteca não interpreta nada — só carrega e ordena por nome exibido. Toda regra continua
em `Cooking/`, em C#, testável pelo `BalanceCheck`. Lógica em arquivo de dados é o passo que
esses jogos pagam caro (depuração sem tipos, avaliação de script no late-game) e que este
sistema não precisa dar: são poucos avaliadores e profundos, não milhares de combinações.

## Perecíveis

A despensa guarda **lotes** — ingrediente, idade em dias, unidades — e não contadores. Dois ovos
da mesma definição comprados em dias diferentes não são a mesma coisa, e é essa diferença que dá
sentido a escolher qual usar.

- **Curva por ingrediente**, no `.tres`: fresco até `FreshDays`, cai em linha reta até `RotDays`.
  `FreshDays = 0` não perece (sal, açúcar, vinagre, pimenta). Valores pensados para geladeira.
- **Régua única** (`Freshness`): passado abaixo de 0.65, estragado abaixo de 0.30. Nome do prato,
  ladrilho, risco e descarte leem os mesmos cortes.
- **Escolha do jogador**: por padrão saem os lotes mais velhos (o que um cozinheiro faz); o verbo
  "usar os mais frescos primeiro" troca nota agora por desperdício depois. Trocar reescolhe o que
  já está no prato — o preview não pode mentir sobre o prato na tela.
- **Intoxicação** vem só de massa estragada: `fração × 2.5`, até 95%, escalada pela alavanca
  `PoisoningRisk` (frigideira ×0.6, panela ×0.4, tigela cru ×1.0). É um número, não um sorteio:
  quem sorteia é quem come, para o preview poder mostrá-lo.
- **Estragado cancela o bônus de âncora** e o nome ganha "(Estragado)" — receita reconhecida não
  salva ingrediente podre. Passado não cancela. Uma unidade podre já faz o prato "estragado",
  porque é a mesma massa que gera o risco.

O `BalanceCheck` calibra com despensa fresca (`ContentLibrary.FreshPantry`) e tem uma seção
própria de frescor. A cozinha do demo (`StartingPantry`) começa com alguns lotes velhos de
propósito, para o sistema aparecer na primeira tela.

## Comer

O preparo termina numa **refeição** (`Meal`) que vai para a geladeira da `Household`:

- **Porções derivadas da massa** (`peso / 0.10 kg`, de 1 a 8) — nunca autoradas. A omelete rende 3,
  o caldo 4.
- **Sobra estraga** com curva própria: fresca 2 dias, estragada no 5º. Passada perde 20% da nota;
  estragada fica com 35% e risco de intoxicação de pelo menos 75% — cozinhar não salva o que
  apodreceu depois de pronto.
- **O Sim** tem fome e sede de 0 a 100, caindo por hora, e humor pela soma dos moodlets. Uma porção
  soma `fome do prato / porções × 3.2` (omelete ≈ +46). O moodlet sai da qualidade com os mesmos
  cortes do painel e **substitui** o de comida anterior em vez de empilhar.
- **Comer é o único sorteio do sistema** (`Household.Eat`). Intoxicado, o Sim fica "Enjoado"
  (−35 por 6 h) e devolve metade do que comeu. Tudo antes disso é determinístico, por isso a
  geladeira mostra fome, sede e humor *depois* de comer antes do clique.
- **`Household.AdvanceHours` é o relógio do mundo**: envelhece despensa e sobras e cansa o Sim na
  mesma chamada. O relógio do jogo real chama isto; no demo são os botões +4 h / +1 dia.

Cada recipiente gera um perfil de refeição que o Sim sente: frito enche e não mata sede, sopa faz
as duas coisas, salada mata a sede. A água do caldo é uma **soma** no stack (`sede +16`), não um
multiplicador — multiplicar a sede quase nula de batata e cogumelo não hidratava nada.

## Necessidades e autonomia

- **Necessidade baixa pesa no humor** com moodlets calculados, não guardados: abaixo de 30 "Com
  fome"/"Com sede" (−10), abaixo de 15 "Faminto"/"Desidratado" (−25). Comer tira na hora — ninguém
  precisa lembrar de remover. A tela usa esses mesmos dois cortes nas notas.
- **Zerar desmaia** ("Desmaiou de fome/sede", −40 por 8 h), uma vez por esvaziamento: só desmaia de
  novo depois de a necessidade subir acima de 15. Desmaio e não morte, por enquanto.
- **Água da pia** é de graça e sem limite (+45 de sede). Sem ela, um Sim com só omelete na
  geladeira morria de sede por desenho.
- **Autonomia** (`Household.Autonomous`, o livre-arbítrio): a cada hora que passa, com sede o Sim
  bebe água; com fome come a melhor refeição da geladeira (`Autonomy.ChooseMeal`) e, se nenhuma
  serve, cozinha sozinho pelo menu rápido (`Autonomy.ChooseRecipe`) e come. Só aceita risco de
  intoxicação acima de 10% quando já está faminto, nunca cozinha sozinho comida com risco, e não
  come se a porção não render pelo menos +10 de fome.
- **Escolher é puro, agir é da casa.** `Autonomy` só decide; `Household` executa e registra no
  **diário** (`Household.Log`), que no jogo é a parede de notificações.

Essa última regra de +10 saiu do teste de três dias do `BalanceCheck`: a primeira versão tratava
sede como motivo para comer, e Ana comia omelete de hora em hora, cozinhava outra, e desmaiava de
sede com seis porções no estômago. O teste roda a casa do demo por 72 h com semente fixa, com e
sem livre-arbítrio: com, ela se mantém sozinha; sem, desmaia no primeiro dia.

## Comprar

A despensa só esvaziava. A mercearia (`ShopContext`) é o terceiro contexto do painel — o layout de
loja do mock original, agora com um sistema por trás.

- **Dinheiro** mora na casa (`Household.Funds`, começa com $150). Só a compra tira; só o salário
  diário de $120 às 9h põe. O salário é substituto declarado do emprego.
- **Vendedores são conteúdo** (`VendorDef`, em `Content/Vendors/`), e cada um é uma troca que usa os
  sistemas que já existem: o **mercado** tem preço de tabela e produto do dia, mas fecha às 20h; a
  **conveniência** abre 24h, cobra +40% e vende produto com 2 dias de prateleira — que estraga antes.
  Preço por unidade é `ceil(preço base × markup)`.
- **O carrinho é do modelo** (`ShoppingTrip`), não da tela: estado da interação, como o prato em
  preparo. Fechar a loja descarta; nada sai do saldo nem entra na despensa antes de `Household.Buy`,
  que recusa exatamente o que a tela recusa (fechado, vazio, dinheiro curto).
- **"Completar receita"**: um verbo por receita conhecida que põe no carrinho exatamente o que falta,
  descontando a despensa e o que já está no carrinho. A loja conta só o que **presta** — ingrediente
  estragado não é estoque na hora de decidir o que comprar.

O `BalanceCheck` imprime quanto custa cada receita em cada vendedor contra o salário: no mercado,
comer custa $41–78 por dia; só de conveniência, a omelete custa ~$108 — dói sem quebrar a casa. Foi
essa tabela que mostrou que a conveniência não completava receita nenhuma (sem cebolinha, cogumelo
nem ervas); ganhou cebolinha, para a omelete da madrugada existir.

## Modificadores

O que mexe no prato além dos ingredientes entra como `DishModifier` (`Data/`), resolvido por
`ModifierStack` (`Cooking/`): `(base + Σ somas) × Π multiplicadores`, sem prioridade nem
sobrescrita. As alavancas são um conjunto fechado (`DishStat`): intensidade de sabor, penalidade
de choque, penalidade de estrago, sede, gordura, risco de intoxicação. Entrada nova vai sempre
no fim do enum — o `.tres` grava o número. Conteúdo combina alavancas; não inventa novas.

Hoje quem contribui é o **recipiente** (`BaseItemDef.Modifiers`, nos `.tres` de `Content/Bases/`):

| Recipiente | Domínio | Efeitos |
|---|---|---|
| Frigideira | salva o insosso, engorda | intensidade ×1.08 · gordura ×1.25 · sede ×0.7 |
| Panela | salva o que briga, hidrata | choques ×0.75 · intensidade ×0.85 · sede +16 |
| Tigela | exige precisão, mantém a água | choques ×1.3 · estrago ×1.5 · sede ×1.2 |

A regra de balanceamento: **nenhum recipiente vence todos os casos** da tabela "mesmo conteúdo,
recipientes diferentes" do `BalanceCheck`. Recipiente que domina vira escolha falsa. A
intensidade da frigideira foi baixada de 1.15 para 1.08 porque punia bacon com ovo — o prato
frito mais clássico não pode piorar no próprio recipiente.

Traço do Sim, perícia ou fogão entram somando suas listas em `CookingSession.Modifiers`, sem
tocar no avaliador. Cada efeito tem uma `Description` que o painel mostra ao escolher a fonte:
efeito que o jogador não lê antes de escolher é efeito que ele não usa.

## Navegação do painel

Teclado e controle são caminhos iguais ao mouse — ver `docs/context-panel.md`. As ações vivem
no `project.godot` com o prefixo `panel_`: `panel_region_next/prev` (Tab · LB/RB),
`panel_increment/decrement` (`=`/`-` · A/X), `panel_commit` (Enter · Y), `panel_close` (Esc · B).

A travessia é **por região**, não por controle. Confirmar e fechar ficam fora do grafo de foco:
são as duas coisas que o jogador precisa alcançar de qualquer lugar do painel.

## Estilo

- Mudanças cirúrgicas e mínimas; corrigir causa raiz, não sintoma.
- Um jeito de fazer cada coisa; sem caminhos alternativos nem fallbacks.
- Falhar rápido quando pré-condições não são atendidas.
- Cada função com uma responsabilidade.
- Comentário explica *por que*, não *o que*.

## Primeira compilação

O `.csproj` aponta para `Godot.NET.Sdk/4.7.0` e `net8.0`. Se sua engine ou SDK for outra
versão, ajuste essas duas linhas — é o único ponto de acoplamento de versão.
