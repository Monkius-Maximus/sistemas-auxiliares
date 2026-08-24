# CLAUDE.md — Sistema de Cozinha Emergente

Godot 4.7 / C# (.NET). Sistema de preparo de comida **sem receitas fixas**: o jogador escolhe um
recipiente, adiciona ingredientes em quantidades variáveis, e o jogo deriva nome, nutrição e
qualidade do resultado. Destinado a um life sim estilo The Sims / Project Zomboid.

## Mapa

```
Data/      Resources do Godot (.tres-ready). Autoria de conteúdo.
           IngredientDef · BaseItemDef · RecipeAnchor · FlavorProfile · Nutrition · CookingEnums

Cooking/   Modelo. Nenhuma chamada de API do Godot.
           CookingSession (estado mutável) · DishEvaluator + DishNamer (puros) ·
           CookedDish (resultado imutável) · IngredientStack ·
           QuickMealPlanner + QuickMealOption (menu rápido, puros)

Content/   O conteúdo, em .tres: Ingredients/ · Bases/ · Anchors/.
           ContentLibrary lê as pastas — ingrediente novo é arquivo novo, não linha de C#.

addons/context_panel/
           O shell reutilizável, sem nada de cozinha dentro. Namespace ContextUi,
           sem dependência deste projeto: é o mesmo painel que vai para os outros jogos.
           PanelContext (a definição: 7 regiões) · PanelPrimitives (desenho) ·
           ContextPanel (o painel) · PanelSkin (cores por papel) ·
           PanelFocus + PanelCell (grafo de foco e célula focável) · PromptBar.
           Contrato próprio em addons/context_panel/README.md.

UI/        As definições de contexto deste sistema: CookingContext (painel manual) ·
           QuickMealContext (menu rápido) · DishReadout · CookingDemo. Zero estado próprio.

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

3. **`CookingSession` é dona da despensa E do prato.** As duas quantidades mudam na mesma
   operação. Não mover o inventário para outra classe.

4. **`Cooking/` não importa `Godot`.** É o que mantém o modelo testável fora do engine.
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
| Frescor real da despensa | `CookingSession.FreshnessOf` | fixo em `1.0` |
| Prato como item persistente | `CookingSession.Cook` retorna e descarta | falta criar o item |
| Moodlet / motivos ao comer | `CookedDish.UnhappinessRelief` etc. | fórmulas prontas, sem consumidor |
| Reação por traço de personalidade | não existe | usar `Group` + `DominantAxis` |
| Ícones de verdade | `IngredientDef.Icon` | campo pronto no `.tres`, sem arte — desenha o `TintColor` |
| Primitivos `grid.dual` e `text` | `addons/context_panel/PanelPrimitives.cs` | no mock, sem sistema que os use |
| Despensa que diminui ao cozinhar | `CookingDemo` | cada preparo abre uma sessão nova |
| Bulk +5 no stepper | `CookingSession` | falta operação de lote que saiba parar no teto |
| Segundo contexto real (loja/bancada) | não existe | o shell aguenta e já é addon; falta o sistema por trás |
| Stack de modificadores | não existe | traço/perícia mexendo no avaliador sem ele saber que existem |

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

Quando um traço de personalidade ou uma perícia precisar mexer no resultado, o caminho é um
**stack de modificadores** que o conteúdo alimenta e o avaliador soma — não script no `.tres`.

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
