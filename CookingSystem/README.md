# Sistema de Cozinha Emergente — Godot 4.7 / C#

Sistema de preparo sem receitas fixas, inspirado na interface do mod *Project Cook* (Project Zomboid B42).
O jogador escolhe um recipiente, joga ingredientes dentro em quantidades variáveis, e o jogo **deriva**
nome, nutrição e qualidade do resultado.

## Como rodar

Projeto Godot completo — abra a pasta no Godot 4.7 e dê play. A cena principal já é
`Scenes/CookingDemo.tscn`. O nível de culinária é exportado no inspetor (`CookingLevel`, padrão 6).

Não precisa de arte: sem ícone, o painel desenha o `TintColor` do item.

Verificar o balanceamento sem abrir o painel:

```
godot --headless --path . --script res://Tools/BalanceCheck.cs
```

O `.csproj` aponta para `Godot.NET.Sdk/4.7.0` e `net8.0`. Se sua engine for outra versão,
ajuste essas duas linhas — é o único acoplamento de versão do projeto.

## Camadas

```
Data/      IngredientDef, BaseItemDef, RecipeAnchor, FlavorProfile, Nutrition
           → Resource do Godot: as classes que dão forma ao conteúdo

Content/   Ingredients/ · Bases/ · Anchors/ em .tres, lidos por ContentLibrary
           → ingrediente novo é arquivo novo; não há lista para registrá-lo

Cooking/   CookingSession (estado)  ·  DishEvaluator + DishNamer (funções puras)
           → nenhuma chamada de API do Godot; testável fora do engine

addons/context_panel/
           → o shell reutilizável, sem dependência deste projeto: um painel para toda
             interação, e o mesmo painel para os outros jogos. Contrato no README de lá.

UI/        CookingContext, QuickMealContext, CookingDemo
           → zero estado próprio; escuta CookingSession.Changed e reconstrói a definição
```

## Um painel, vários contextos

O painel manual e o menu rápido são o **mesmo** `ContextPanel` lendo definições diferentes.
Sete regiões nomeadas — subject, actions, primary, secondary, preview, readout, commit — e um
punhado de primitivos que se recombinam. Loja, bancada e baú entram como definições novas, não
como janelas novas. O contrato está em [`docs/context-panel.md`](docs/context-panel.md).

Teclado, controle e mouse são caminhos iguais: a travessia é por **região** (Tab · LB/RB), o
ladrilho inteiro é o alvo no controle (A/X somam e tiram, segurando para repetir), e o rodapé
mostra o que a região focada aceita. Confirmar e fechar ficam fora do grafo de foco.

A regra que sustenta tudo: **o preview ao vivo e o ato de cozinhar chamam a mesma função.**
`DishEvaluator.Evaluate` é pura, então o número que o jogador vê enquanto mexe é literalmente
o número que ele recebe ao confirmar. Não há duas implementações para divergir.

## Modelo de qualidade

Qualidade é o **produto** de cinco fatores 0..1, limitado por um teto de perícia.
Multiplicativo e não somado: um erro grave em qualquer eixo afunda o prato inteiro,
que é como cozinhar funciona de verdade.

| Fator | Do que vem | Falha quando |
|---|---|---|
| `SeasoningScore` | intensidade total de sabor por unidade, contra uma janela ideal | insosso ou temperado demais |
| `BalanceScore` | fração do eixo dominante vs. 42% ideal | um sabor domina tudo, ou nenhum lidera |
| `HarmonyScore` | pares de eixos que brigam (doce×umami, doce×salgado, amargo×ácido) | dois eixos conflitantes fortes juntos |
| `VarietyScore` | número de grupos alimentares distintos, ótimo em 3 | ingrediente único, ou tudo jogado dentro |
| `FreshnessScore` | penalidade proporcional à **massa** estragada | metade do prato é ingrediente podre |

O teto de perícia é `0.50 + 0.05 × nível`. Nível 0 nunca passa de 50% mesmo acertando tudo;
o painel avisa quando é a perícia que está segurando a nota, em vez de deixar o jogador
otimizar às cegas.

### Distribuição calibrada

As constantes em `DishEvaluator` foram ajustadas para este espalhamento, verificável
a qualquer momento por `Tools/BalanceCheck.cs`:

```
ovo puro, sem sal          0.06     lixo
sopa de batata sem tempero 0.05
bacon + açúcar (choque)    0.04
sal demais                 0.07
─────────────────────────────────
só brócolis + sal          0.46     preguiçoso
tudo junto (bagunça)       0.42
salada tomate/queijo       0.59
─────────────────────────────────
omelete queijo + sal       0.81     sólido
bacon + ovo + sal          0.82
─────────────────────────────────
omelete completa           0.96     excelente
sopa de batata equilibrada 0.95
```

Mexer nessas constantes é decisão de design, não bugfix. Elas estão isoladas no topo de
`DishEvaluator` com comentário explícito.

## Convenções de autoria de conteúdo

- **Vetor de sabor de ingrediente**: soma L1 entre `0.2` (batata, insosso) e `1.0` (bacon, assertivo).
  Sair muito dessa faixa quebra a janela de intensidade.
- **Tempero**: vetor unitário num eixo + `Potency` entre 3 e 4. Entra em poucas unidades mas
  precisa mover o vetor do prato inteiro.
- **Peso por unidade**: `0.010 kg` para ingredientes, `0.001 kg` para temperos. É o peso que
  define a fração de massa usada na penalidade de frescor.

## Âncoras

`RecipeAnchor` é o anteparo contra "tudo emergente vira sopa sem graça". Não trava nada:
o jogador continua livre, mas se acertar o conjunto de uma âncora ganha bônus de qualidade
e o prato passa a ter nome próprio ("Omelete de Queijo" em vez de "Fritada de ovo com queijo").

Poucas de propósito — são atalhos premiados, não o conteúdo principal.

## Pontos de integração pendentes

| O quê | Onde | Estado |
|---|---|---|
| Frescor real dos itens da despensa | `CookingSession.FreshnessOf` | fixo em `1.0` |
| Moodlet e motivos do Sim ao comer | `CookedDish.UnhappinessRelief` / `BoredomRelief` / `MoodletMinutes` | fórmulas prontas, sem consumidor |
| Prato como item persistente do mundo | `CookingSession.Cook` retorna e descarta | precisa criar o item |
| Reação individual por traço de personalidade | ainda não existe | usar `IngredientDef.Group` + `DominantAxis` |
| Ícones de verdade | `IngredientDef.Icon` | placeholders coloridos |

**NPCs cozinham com este mesmo código.** Um NPC com perícia baixa produz pratos ruins porque
tem menos slots e teto menor — não porque exista uma tabela de "pratos ruins de NPC".

## Estado da verificação

Compila e roda em **Godot 4.7.1 mono** com o SDK .NET 8: menu rápido e painel manual, nas duas
peles, com as etiquetas de região ligadas. Navegação por região, foco sobrevivendo ao redesenho,
repetição ao segurar o stepper (parando sozinha no teto da sessão), confirmar por Enter e fechar
por Esc — tudo conferido em execução, por captura headless.

O `.csproj` aponta para `Godot.NET.Sdk/4.7.0`; a verificação rodou com a engine 4.7.1, que
aceita o SDK 4.7.0 sem ajuste.
