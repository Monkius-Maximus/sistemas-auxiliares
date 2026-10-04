# Arte e fluxo do jogo

Este documento cobre duas coisas:

1. **Arte:** o que é desenhado por código, que imagens o jogo procura e como colocá-las.
2. **Fluxo:** como o Godot faz o jogo andar, do menu até o simulador, e o que cada clique faz.

A planilha [`arte-e-fluxo.xlsx`](arte-e-fluxo.xlsx) tem as mesmas listas em formato de tabela,
com filtro, e com o **status de cada imagem** (feito / falta). Ela é gerada a partir do conteúdo
do jogo e de `Art/`:

```
pip install openpyxl
python3 Tools/gerar_planilha_arte.py
```

---

## 1. Por que trocar a "imagem do bacon" não funcionava

Nenhum item do jogo tinha imagem. O campo `Icon` existia em todos os `.tres`, mas estava vazio.
O que aparecia na tela era a amostra de cor (`TintColor`) pintada por
`PanelPrimitives.Swatch`. Então o bacon era um quadrado vermelho por definição. Além disso:

- Vários itens tinham cores quase iguais (bacon e tomate vermelhos; brócolis, ervas e cebolinha
  verdes; sal e açúcar brancos). Por isso pareciam "repetidos".
- Quando havia ícone, ele era desenhado **por cima** da cor. Um PNG com fundo transparente
  continuava aparecendo dentro de um quadrado vermelho.
- Não havia uma pasta nem uma regra de nome. O único jeito de ligar uma imagem era abrir cada
  `.tres` no Inspetor e arrastar o arquivo.
- Pratos nem tinham onde receber arte. A prévia mostrava o ícone do recipiente.

O que mudou:

| Antes | Agora |
|---|---|
| Arte só pelo campo `Icon` do `.tres` | Arte achada **pelo nome do arquivo**, em `Art/<Categoria>/<id>.png`. O campo `Icon` continua valendo e tem prioridade |
| Cor por baixo da arte | Com arte, o fundo some e só o desenho aparece |
| Sem arte, quadrado de cor lisa | Sem arte, a cor do item com **duas iniciais** (BC = bacon, BT = batata, TM = tomate) |
| Prato usava o ícone do recipiente | Prato tem arte própria: uma por receita e uma genérica por recipiente |

O código é `Content/ArtLibrary.cs`, usado por `ContentLibrary` ao carregar o conteúdo.

---

## 2. O que já é feito por código (não precisa de arte)

Tudo o que é **estrutura de tela** é desenhado pelo Godot a partir das cores da pele
(`addons/context_panel/PanelSkin.cs`):

- fundos escuros, molduras, divisórias e colunas do painel;
- células, ladrilhos, botões e os estados de hover e pressionado;
- o anel de foco laranja (teclado e controle);
- as barras de qualidade, de fome e de sede;
- etiquetas, chips e pontos de estado;
- o fundo escurecido atrás de um painel aberto e do menu de pausa;
- o marcador de item sem arte (cor + iniciais);
- as teclas do rodapé (Enter, Tab, Esc), que hoje são texto.

Para mudar a cara disso tudo, você troca **cores** em `PanelSkin.Dark` / `PanelSkin.Light`. Não
existe imagem a substituir. A aba "Já feito em código" da planilha diz onde fica cada elemento.

---

## 3. A arte que o jogo procura

Todas as imagens vivem em `Art/`, na raiz do projeto. São **39** no total. A planilha lista
cada uma com o caminho exato.

| Pasta | Nome do arquivo | Tamanho | Onde aparece | Prioridade |
|---|---|---|---|---|
| `Art/Ingredients/` | `<Id>.png` (ex.: `bacon.png`), 13 arquivos | 128×128 | Despensa no painel manual, prateleira e carrinho da loja, porções do menu rápido | A |
| `Art/Dishes/` | `<receita>.png` (ex.: `omelete-de-queijo.png`), 3 arquivos | 256×256 | Menu rápido, prato previsto, geladeira | A |
| `Art/Dishes/` | `generico-<recipiente>.png` (ex.: `generico-panela.png`), 3 arquivos | 256×256 | Prato improvisado ("Sopa de…") no painel e na geladeira | A |
| `Art/House/` | `geladeira`, `fogao`, `pia`, `porta` | 360×360 | Os objetos clicáveis da casa | A |
| `Art/Vessels/` | `frigideira`, `panela`, `tigela` | 128×128 | Escolha do recipiente; prévia com recipiente vazio | B |
| `Art/Vendors/` | `mercado`, `conveniencia` | 256×256 | Loja: escolha e cartão do vendedor | B |
| `Art/Hud/` | `fome`, `sede`, `humor` | 64×64 | HUD e geladeira | B |
| `Art/Hud/` | `retrato-sim` | 128×128 | HUD, ao lado do nome | B |
| `Art/Hud/` | `pausa`, `velocidade-1`, `velocidade-2`, `velocidade-3` | 64×64 | Botões de velocidade (no lugar de II > >> >>>) | C |
| `Art/House/` | `fundo-cozinha` | 1920×1080, opaco | Fundo da casa | C |
| `Art/Menu/` | `logo` 760×320 · `fundo` 1920×1080 | — | Menu principal | C |

O que significa cada prioridade:

- **A:** com isso, o jogo passa a parecer um jogo.
- **B:** acabamento das telas.
- **C:** cosmético.

As regras de nome:

- O `<Id>` é o campo `Id` do `.tres` (abra o arquivo em `Content/` e confira).
- A receita não tem `Id`, então vale o **nome do arquivo**: `Content/Anchors/salada-caprese.tres`
  procura a imagem `Art/Dishes/salada-caprese.png`.
- Use letras minúsculas, sem acento e sem espaço: `fogao.png`, não `Fogão.png`.

Sobre os tamanhos: o arquivo tem o dobro ou mais do tamanho exibido. O Godot reduz sem perder
nitidez, e a mesma arte continua servindo em telas maiores.

### Como a tela escolhe a imagem de um prato

1. Se o prato é uma receita conhecida (casou uma âncora), usa a arte da receita. Um prato
   "(Estragado)" continua com o desenho da omelete, porque o estado aparece no nome.
2. Senão, usa a arte genérica daquele recipiente (`generico-panela.png`).
3. Senão, mostra o marcador: a cor do recipiente com as iniciais do prato.

---

## 4. Como inserir uma imagem

1. Escolha a linha na planilha. A coluna **Caminho exato** é o arquivo que o jogo procura.
2. Desenhe ou gere a imagem no tamanho indicado:
   - PNG com **fundo transparente** (os fundos de tela são a exceção);
   - objeto centralizado, sem muita margem;
   - mesma luz e mesmo ângulo em toda a série.
3. Salve com o nome exato. Exemplo: `Art/Ingredients/bacon.png`.
4. Abra (ou volte para) o **editor do Godot**. Ele importa o arquivo sozinho e cria o
   `bacon.png.import` ao lado. **Sem esse passo o jogo não vê a imagem.** Se for rodar pela
   linha de comando, importe antes com `godot --headless --path . --import`.
5. Rode o jogo (F5). O marcador some e a arte aparece em **todas** as telas que usam o item.
6. Se quiser atualizar a planilha, rode `python3 Tools/gerar_planilha_arte.py`. A linha passa
   para "feito".

Outros casos:

- **Arquivo com outro nome ou em outra pasta.** Abra o `.tres` no Inspetor e arraste o PNG
  para o campo `Icon`. No recipiente, o campo do prato improvisado é `DishIcon`. O campo
  preenchido tem prioridade sobre a convenção.
- **Item novo.** Crie o `.tres` em `Content/<pasta>/` com um `Id` novo e salve a arte com o
  mesmo `Id`. Nada mais precisa ser registrado.
- **Pixel art.** Vá em *Projeto › Configurações do Projeto › Renderização › Texturas* e mude
  o *Filtro de textura padrão* para **Nearest**. Senão o Godot borra os pixels.

### Gerando com IA

Peça a série inteira de uma vez, com o mesmo texto base, para manter a consistência. Por exemplo:

> Ícone de jogo de simulação de vida, **{item}**, vista 3/4 de cima, contorno suave, sombreado
> leve, paleta quente, objeto único centralizado, **fundo transparente**, sem texto, 512×512.

Gere em 512×512 e reduza para o tamanho da planilha. Confira se o fundo saiu transparente de
verdade: muitos geradores devolvem um xadrez cinza desenhado na imagem.

### Testar sem arte final

Qualquer PNG serve de teste. Salve um rabisco como `Art/Ingredients/bacon.png`, abra o editor
e rode. O bacon muda na despensa, na loja e nas porções ao mesmo tempo.

---

## 5. Como o Godot faz o jogo andar

### O básico

- O jogo é uma **árvore de nós**. Uma **cena** (`.tscn`) é uma árvore salva em arquivo. As
  cenas deste jogo têm um nó só, um `Control`, com um script C#. O script monta o resto da
  tela por código, no `_Ready`.
- O Godot chama métodos do script em momentos fixos:

| Método | Quando roda | Usado aqui para |
|---|---|---|
| `_Ready()` | uma vez, quando o nó entra na tela | montar HUD, objetos e menus |
| `_Process(delta)` | todo quadro (~60 por segundo) | fazer o relógio do jogo andar |
| `_Input(event)` | toda tecla, clique e botão, antes da interface | trocar de região do painel (Tab, LB/RB) |
| `_UnhandledInput(event)` | toda entrada que nenhum botão consumiu | Esc, pausa, velocidades, confirmar |

- Botões avisam por **sinais**: `button.Pressed += () => ...` liga o clique a uma função.
- As teclas não são lidas direto. O jogo pergunta por **ações** (`game_menu`, `panel_commit`…),
  definidas em *Projeto › Configurações › Mapa de Entrada*. Assim teclado e controle são o
  mesmo caminho, e trocar uma tecla não exige mexer no código.
- Para trocar de tela, o jogo chama `GetTree().ChangeSceneToFile(...)`. Os caminhos ficam em
  `UI/Game/GameScenes.cs`.

### As telas

```
 (abrir o jogo)
      │  run/main_scene em project.godot
      ▼
 ┌──────────────┐  Novo jogo   ┌───────────────────────────────┐
 │ Menu         │─────────────▶│ Casa (GameScreen)             │
 │ principal    │◀─────────────│  relógio correndo             │
 └──────────────┘ Menu princ.  │  ┌─────────────────────────┐  │
      │  Bancada de testes     │  │ clique num objeto       │  │
      ▼                        │  ▼                         │  │
 ┌──────────────┐              │ Painel aberto  ── ✕/Esc ───┘  │
 │ Bancada      │              │  (tempo parado)               │
 │ (CookingDemo)│              │                               │
 └──────────────┘              │ Esc ─▶ Pausa ─ Continuar ─▶   │
                               └───────────────────────────────┘
```

| Cena | Script | Papel |
|---|---|---|
| `Scenes/MainMenu.tscn` | `UI/Game/MainMenu.cs` | Primeira tela: Novo jogo · Bancada de testes · Sair |
| `Scenes/Game.tscn` | `UI/Game/GameScreen.cs` | A casa: HUD, objetos clicáveis, diário, painel e pausa |
| `Scenes/CookingDemo.tscn` | `UI/CookingDemo.cs` | Ferramenta de dev: o fogão em tela cheia, pele e relógio na mão |

### O loop da casa

A cada quadro, o `GameScreen._Process` faz o seguinte:

1. Se há um painel ou o menu de pausa aberto, não faz nada: o tempo está parado.
2. Senão, pergunta ao `GameClock` quantas horas inteiras passaram, conforme a velocidade:
   - `>` (normal): 1 hora de jogo a cada 4 s, ou seja, um dia em 96 s;
   - `>>`: 3 vezes mais rápido;
   - `>>>`: 10 vezes mais rápido.
3. Para cada hora, chama `Household.AdvanceHours`. Essa chamada:
   - envelhece a despensa e as sobras;
   - cansa a Ana (fome e sede caem);
   - paga o salário às 9h;
   - deixa a autonomia agir: com o livre-arbítrio ligado, ela bebe, come e cozinha sozinha.
4. A casa dispara `Changed`, e o HUD, os objetos e o diário são redesenhados.

### O que cada clique faz

Nenhuma regra mora na tela. O clique chama o modelo, e o modelo avisa que mudou:

```
clique no Fogão
  → HouseInteractions.OpenStove()        (qual tela abrir: estado de tela)
    → QuickMealPlanner.Plan(...)          (o que dá para fazer: modelo)
    → evento Opened
      → GameScreen.ShowPanel()            (cria o ContextPanel por cima da casa)

clique em "Preparar"
  → QuickMealPlanner.Prepare(...)         (gasta a despensa, cozinha)
  → Household.Store(prato)                (vira refeição na geladeira)
  → Household.Changed → HUD e objetos redesenham

Esc no painel
  → ContextPanel consome o Esc (é o nó mais fundo, recebe primeiro)
  → HouseInteractions.Close() → evento Closed
    → GameScreen.HidePanel()              (tira o painel, o tempo volta)
```

A tabela completa (tela, elemento, mouse, teclado, controle, efeito e código) está na aba
**Cliques e teclas** da planilha. Os atalhos principais:

| Onde | Mouse | Teclado | Controle |
|---|---|---|---|
| Casa: usar objeto | clique | setas + Enter | direcional + A |
| Casa: pausa / velocidade | II > >> >>> | P · 1 · 2 · 3 | Select |
| Casa: menu de pausa | Menu (Esc) | Esc | Start |
| Painel: trocar de região | — | Tab / Shift+Tab | RB / LB |
| Painel: somar / tirar | + / − | = / − | A / X |
| Painel: confirmar | botão laranja | Enter | Y |
| Painel: fechar | ✕ | Esc | B |

### Onde mexer para…

| Quero… | Arquivo |
|---|---|
| um objeto novo na casa (ex.: cama) | `UI/Game/GameScreen.cs`, lista `_objects`, mais a arte em `Art/House/` |
| uma tela nova (ex.: inventário) | uma definição de contexto em `UI/` mais um `Open…` em `HouseInteractions` |
| mudar a velocidade do tempo | `UI/Game/GameClock.cs` (`SecondsPerHour`, `Multipliers`) |
| mudar uma tecla | *Projeto › Configurações › Mapa de Entrada* (ações `game_*` e `panel_*`) |
| mudar cores | `addons/context_panel/PanelSkin.cs` |
| um ingrediente novo | um `.tres` em `Content/Ingredients/` mais a arte em `Art/Ingredients/` |

---

## 6. Testar

1. Abra a pasta `CookingSystem/` no Godot 4.7 (.NET).
2. Na primeira vez, clique em **Build** (o martelo, no canto superior direito).
3. Aperte F5. O jogo abre no menu principal.
4. Em **Novo jogo**: clique no fogão, prepare algo, feche com Esc, coma na geladeira e
   acelere o tempo com `3`.
