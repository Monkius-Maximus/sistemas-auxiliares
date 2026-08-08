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

Content/   SampleContent — conteúdo de teste em código, com ícones placeholder.
UI/        QuickMealMenu · CookingPanel · IngredientSlotView · CookingDemo. Zero estado próprio.
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

## Invariantes — não quebrar sem discutir

1. **`DishEvaluator.Evaluate` é pura.** Sem estado, sem efeito colateral, sem `GD.Print`.
   O preview ao vivo e o ato de cozinhar chamam essa mesma função — é isso que garante
   que o número mostrado é o número recebido. Nunca criar uma "versão rápida para o preview".

2. **A UI não guarda estado.** `CookingPanel` escuta `CookingSession.Changed` e redesenha.
   Se aparecer um campo no painel que espelha algo da sessão, é bug.

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
| Ícones de verdade | `IngredientDef.Icon` | placeholders coloridos |
| Conteúdo em `.tres` | `Content/SampleContent.cs` | tudo em código |

NPCs devem cozinhar com este mesmo código: perícia baixa produz prato ruim por ter menos
slots e teto menor, não por uma tabela separada de "pratos de NPC".

## Estilo

- Mudanças cirúrgicas e mínimas; corrigir causa raiz, não sintoma.
- Um jeito de fazer cada coisa; sem caminhos alternativos nem fallbacks.
- Falhar rápido quando pré-condições não são atendidas.
- Cada função com uma responsabilidade.
- Comentário explica *por que*, não *o que*.

## Primeira compilação

O `.csproj` aponta para `Godot.NET.Sdk/4.7.0` e `net8.0`. Se sua engine ou SDK for outra
versão, ajuste essas duas linhas — é o único ponto de acoplamento de versão.
