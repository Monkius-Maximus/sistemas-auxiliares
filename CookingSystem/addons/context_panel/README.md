# Painel por contexto

Um painel para todas as interações de um jogo. O shell lê uma **definição de contexto** —
sete regiões nomeadas —, liga cada primitivo à sua região e desenha. Cozinhar, comprar,
negociar uma transferência e montar escalação são definições diferentes, não janelas
diferentes.

Godot 4.7 / C#. Namespace `ContextUi`. **Não depende de nada fora daqui** — nem de projeto,
nem de cena, nem de tema: a UI inteira é construída em código, então não há `.tscn` para
conflitar com os seus.

## Instalar

Copie `addons/context_panel/` para a raiz do projeto, ou adicione como submódulo git para
que uma correção no painel chegue nos dois projetos de uma vez.

Não há `plugin.cfg`: o painel não tem integração com o editor, é C# puro instanciado por
código de jogo. Um `EditorPlugin` que não faz nada seria só cerimônia.

Registre as ações de input (Project → Project Settings → Input Map). Teclado e controle são
caminhos iguais — nada aqui existe só num dos dois:

| Ação | Teclado | Controle |
|---|---|---|
| `panel_region_next` | Tab | LB |
| `panel_region_prev` | Shift+Tab | RB |
| `panel_increment` | `=` | A |
| `panel_decrement` | `-` | X |
| `panel_commit` | Enter | Y |
| `panel_close` | Esc | B |

## Usar

Uma função pura de estado para `PanelContext`, e um host que a passa como `Func<>`:

```csharp
var panel = new ContextPanel
{
    Definition = () => LojaContext.Build(session, Buy, Close),
    Skin = PanelSkin.Dark,
};
session.Changed += () => panel.Rebuild();
AddChild(panel);
```

O painel se dimensiona pela largura que a definição declara e reconstrói tudo a cada
`Rebuild()`. Não existe caminho que atualize um pedaço da tela: é isso que garante que a
tela nunca discorde do estado que a gerou.

## O que ele espera de você

- **A definição não fala de cor.** Ela diz `Selected`, `Warn`, `Locked`, `Enabled`; a
  `PanelSkin` decide como isso se parece. Uma pele nova é uma terceira entrada e nada mais.
- **O número na tela vem do avaliador do seu sistema.** A região `Readout` só lê. Conta
  dentro da definição está no lugar errado.
- **A UI desabilita exatamente o que o modelo recusaria.** Exceção chegando ao jogador é bug
  de definição, não input.

## Primitivos

`Picker` · `VerbList` · `SlotGrid` (quantidade ou escolha única) · `Checklist`, mais as três
regiões fixas de resultado: `PreviewCard` · `Readout` · `CommitAction`.

A regra que impede o catálogo de apodrecer: um primitivo precisa se justificar em pelo menos
**dois** contextos. Um primitivo novo é uma classe em `PanelContext.cs` e um `case` em
`PanelPrimitives.Build` — o shell não muda.

## O que ele não faz

- **Não guarda estado do seu modelo.** A exceção é o foco: `PanelFocus` guarda
  `(região, índice)` porque é onde o cursor do jogador está *neste* painel, e morre com ele.
- **Não abre nem fecha a si mesmo.** Quem abre a interação é o host, e é ele que sabe se o
  mundo pausa.
- **Não tem arte.** `PreviewCard.Art` e os ícones dos slots aceitam `Texture2D`; sem eles,
  desenha o `TintColor` do item.
