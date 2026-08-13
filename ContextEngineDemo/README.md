# Context Engine — painel de contexto para Godot .NET

Um painel de interação, muitos contextos. Cozinhar, comprar, montar na bancada, transferir itens,
fazer prova, arrombar fechadura e consertar veículo são **o mesmo painel** preenchido por uma
definição de contexto diferente — não sete telas.

Implementação em C# (Godot.NET.Sdk **4.7.1**, **net10.0**) da especificação em
`../project/Context Engine Design Doc.dc.html` e do protótipo `../project/Context Panel.dc.html`.

---

## Rodando a demo

```bash
godot --path ContextEngineDemo            # abre no editor
godot --path ContextEngineDemo demo/Main.tscn   # roda direto
```

A barra de dev no topo troca de contexto, alterna as duas skins (escura in-game / clara de
editor), liga o overlay com o nome de cada região e abre o dump JSON da definição do contexto
atual. Nada disso é chrome de jogo — é o *dev context switcher* que o design doc recomenda
manter atrás de uma flag de debug.

**Testado**: compila e roda em Godot 4.7.1 mono. As capturas de verificação foram feitas nos
sete contextos, nas duas skins, com overlay de regiões e com o dump de definição aberto.

---

## Como isso entra no SoccerDreamGame

Copie **`addons/context_engine/`** para a raiz do seu projeto. Não há dependência da pasta
`demo/` — ela é só o harness. O addon não depende de nenhum `.tscn`: toda a UI é construída em
C#, então não há cena para conflitar com as suas.

O `.csproj` do seu jogo já tem `Godot.NET.Sdk/4.7.1` e `net10.0`; os arquivos são incluídos
automaticamente pelo glob padrão do SDK. As únicas coisas que você precisa adicionar são as
**ações de input** (veja abaixo).

### Abrindo o painel no jogo

```csharp
using ContextEngine;

var controller = new ContextController();
controller.CommitRequested += module => ApplyToWorld(module);   // escrever no mundo aqui
controller.CloseRequested  += ClosePanel;

var panel = new ContextPanel();
AddChild(panel);
panel.Attach(controller);

controller.Load(new CookModule());          // seu módulo de contexto
panel.FocusEntry(controller.Module.Definition.FocusEntry);

if (controller.Module.Definition.PausesWorld) GetTree().Paused = true;
```

Fechar o painel descarta a sessão: nada do que o jogador fez toca o mundo antes do commit.

### Ações de input a registrar (Project → Project Settings → Input Map)

Já estão em `project.godot` desta demo; copie para o seu jogo.

| Ação | Teclado | Gamepad |
|---|---|---|
| `ce_region_next` | Tab | LB |
| `ce_region_prev` | — (Shift+Tab via `ui_focus_prev`) | RB |
| `ce_increment` | roda do mouse / clique em `+` | A |
| `ce_decrement` | roda do mouse / clique em `−` | X |
| `ce_bulk` | Shift | RT |
| `ce_commit` | Enter | Y / Start |
| `ce_close` | Esc | B |

A região — não o controle — é a unidade de travessia: LB/RB pulam de região em região e
reentrar numa região devolve o foco para a célula de onde você saiu. O anel de foco é desenhado
2 px **por fora** do tile, para sobreviver sobre um preenchimento tintado.

---

## Arquitetura

Fluxo de dados de mão única, e o único lugar onde isso é imposto é o `ContextController`:

```
ContextController  →  ContextViewModel  →  ContextPanel.Bind
        ↑                                        ↓
     Intent            ←  primitivas emitem intents (nunca leem estado de jogo)
```

```
addons/context_engine/
├── Core/
│   ├── Skin.cs                 tokens de cor (dark/light) + rampa de qualidade
│   ├── ViewModels.cs           um VM por primitiva, imutável, sem cores
│   ├── Intent.cs               o que a primitiva diz que aconteceu
│   ├── ContextDefinition.cs    Resource .tres — o arquivo que o designer edita
│   └── ContextController.cs    IContextModule, IEvaluator, o loop de rebind
├── Primitives/                 as 13 primitivas do catálogo
└── Shell/
    ├── ContextPanel.cs         três trilhos, grafo de foco, prompt bar
    ├── RegionHost.cs           header + troca de primitiva por região
    ├── Ui.cs                   fontes, caixas, réguas, medidores
    ├── Pressable.cs            a única superfície interativa (+ hold-to-repeat)
    └── Decor.cs                grid auto-fill e padrões de fundo

demo/
├── Data/GameData.cs            o mundo de exemplo
├── Evaluators/                 sete funções puras estado → julgamento
├── Modules/                    sete módulos de contexto
└── DemoRoot.cs                 o dev switcher
```

### As sete regiões

`subject` · `actions` (trilho esquerdo) — `primary` · `secondary` (centro) —
`preview` · `readout` · `commit` (direita). Um contexto preenche o que precisa; o resto some.
O resultado fica imediatamente acima do botão que o causa — a decisão de layout mais importante
do sistema.

### O catálogo de primitivas

`picker` · `verbs` · `grid.qty` · `grid.select` · `grid.dual` · `checklist` · `text` ·
`question` · `sequence` · `hotspot.map` · `timing.bar` · `preview` · `readout`
(com `timer` e `forecast` opcionais) · `commit`.

Duas regras que impedem o catálogo de apodrecer: uma primitiva precisa se justificar em pelo
menos **dois** contextos, e **primitivas nunca falam com sistemas de jogo** — recebem um
view-model e emitem um intent.

### Escrevendo um contexto novo

1. `class MinhaCoisaModule : IContextModule`
2. Preencha `Definition` (id, título, largura, uma `RegionSpec` por região, o commit).
3. `Enter()` zera a sessão · `Apply(intent)` muda estado · `Build()` devolve o view-model.
4. Escreva o avaliador como `IEvaluator<TState, TJudgement>` — puro, sem efeito colateral e sem
   aleatoriedade, para poder ser chamado a cada tecla e testado contra uma tabela de casos.

Se precisar de uma primitiva nova, é uma classe em `Primitives/` e um `case` em
`PrimitiveFactory`. **A shell não muda.**

### Definições como `.tres`

`ContextDefinition` e `RegionSpec` são `[GlobalClass] Resource`, então dá para criar `.tres` pelo
inspetor, versionar e recarregar a quente. Nesta demo os módulos constroem a definição em código
(mais fácil de ler junto do módulo); trocar para `[Export] ContextDefinition` carregado de `.tres`
é uma linha por módulo.

---

## Decisões que valem saber

- **Multiplicativo vs aditivo.** Qualidade do prato e confiabilidade do veículo são **produtos**
  (`pior × média`): um fator catastrófico arruína o resultado, a regra do parafuso emperrado.
  Cesta de compras e nota de prova são **somas** — contribuições genuinamente independentes.
- **Verbos travados são conteúdo.** "Pedir fiado — precisa de reputação 8" ensina que reputação
  existe, é por vendedor e destrava algo, sem tutorial. Todo contexto tem pelo menos um.
- **`timing.bar` é a única primitiva em tempo real.** A fase da varredura vive dentro do widget e
  sai como uma posição no momento do golpe, então o avaliador continua puro. O verbo de
  acessibilidade converte a barra em uma rolagem com o mesmo valor esperado — a barra nunca é o
  único caminho para o objetivo.
- **`PausesWorld` é uma flag por contexto** (a pergunta em aberto §12 do design doc): a loja
  pausa o mundo, o conserto sob pressão não. O host lê a flag antes de abrir o painel.
- **Tudo de cor mora em `Skin`.** As duas skins são o mesmo layout com um conjunto de tokens
  trocado; um modo de alto contraste é uma terceira entrada e nada mais.
- **Fontes**: Archivo (variável, eixo `wght`) e IBM Plex Mono, em `assets/fonts/`, ambas SIL OFL.
  Números são tabulares por feature OpenType (`tnum`) — um número que treme enquanto você segura
  o stepper é ilegível.

## Limitações conhecidas

- **Piso de texto de console.** O design doc pede 24 px a 1080p para tudo que o jogador precisa
  ler; a demo reproduz a densidade do protótipo (rótulos de 9–10 px), que é luxo de desktop.
  Para console, escale o painel inteiro como uma unidade (`panel.Scale`) em vez de refluir o
  layout — refluir quebra a posição memorizada de todo controle.
- **Alvos de toque.** Botões principais respeitam os 44 px (`Pressable.MinHit`); os steppers ±
  ficam em 22 px, como no protótipo, porque no gamepad o alvo é o tile inteiro (A / X).
- **Sem arte.** O slot de preview desenha o contrato ("sprite 64²"); passe uma `Texture2D` em
  `PreviewVm.Art` para trocar.
- **Sem testes automatizados.** Os avaliadores foram escritos para receber uma tabela de casos em
  CI, como o design doc recomenda, mas essa tabela ainda não existe.
