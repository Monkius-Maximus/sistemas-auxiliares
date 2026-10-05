# Protótipo "Vida em Casa": resumo para outras ferramentas

> Contexto completo do protótipo, pensado para ser colado em outra IA antes de pedir uma
> proposta. No fim há um **pedido padrão** e um **formato de resposta**. Use os dois para que as
> propostas de ferramentas diferentes saiam comparáveis lado a lado.

---

## 1. O que é

O protótipo é um simulador de vida no estilo The Sims 4 / inZOI. Hoje existe **um sistema
completo e jogável: a cozinha**, com tudo o que a cerca: comprar, guardar, cozinhar, comer,
estragar e passar fome. O resto da vida (dormir, trabalhar, socializar, construir) ainda não
existe.

O objetivo da fase atual é **ter uma ferramenta completa e funcional antes de otimizar**.
Primeiro prova-se o que diverte; depois corta-se o que o jogador não usaria.

A interface (o "painel de contexto") foi feita para ser **reaproveitada em outro jogo do
autor, de futebol**. Ela não sabe nada de cozinha.

**Tecnologia:** Godot 4.7 com C# (.NET 8). Conteúdo em arquivos de dados (`.tres`) no estilo
Paradox: item novo é arquivo novo, sem mexer em código. O alvo inclui rodar bem em
dispositivos mais fracos.

---

## 2. O que já funciona

### O loop do jogador

```
Menu principal → Novo jogo → a casa (dia 1, 8h)
  relógio corre (1 h de jogo = 4 s; velocidades 1×, 3×, 10×; pausa)
  Ana (a Sim) perde fome e sede por hora; humor = soma de "moodlets"
  clique num objeto da casa:
    Fogão     → menu rápido de pratos (1 clique) ou preparo manual (ingrediente a ingrediente)
    Geladeira → comer uma porção das refeições prontas
    Pia       → beber água (de graça)
    Porta     → mercearia (2 lojas com preço, horário e frescor diferentes)
  painel aberto = tempo parado; Esc volta uma tela
  livre-arbítrio ligado: Ana bebe, come e cozinha sozinha quando precisa
  salário fixo de $120 às 9h (substituto de um emprego)
```

### Os sistemas

| Sistema | Como funciona | Números-chave |
|---|---|---|
| **Cozinha emergente** | Sem receitas fixas. O jogador escolhe recipiente e ingredientes em quantidade; o jogo deriva nome, nutrição e qualidade | Qualidade = **produto** de 5 fatores (tempero, equilíbrio, harmonia, variedade, frescor), com teto por perícia `0,50 + 0,05 × nível` |
| **Receitas-âncora** | Poucas combinações conhecidas ganham nome próprio e bônus. Não travam nada | 3 hoje: Omelete de Queijo, Caldo Verde da Casa, Salada Caprese |
| **Recipientes** | Frigideira, panela e tigela mudam o prato via modificadores | Frigideira: sabor ×1,08, gordura ×1,25. Panela: choques ×0,75, sede +16. Tigela: choques ×1,3 |
| **Menu rápido** | Lista estilo The Sims derivada das âncoras: custo, qualidade prevista, o que falta | Mesma função de avaliação do preparo manual: o número previsto é o número recebido |
| **Perecíveis** | A despensa guarda **lotes** com idade. Cada ingrediente tem curva própria (fresco → passado → estragado) | Passado < 0,65; estragado < 0,30. Intoxicação só vem de massa estragada |
| **Refeições e sobras** | O prato vira refeição com porções derivadas do peso; a sobra estraga | 1 porção a cada 100 g (1–8); fresca 2 dias, estragada no 5º |
| **Necessidades** | Fome e sede de 0 a 100; necessidade baixa pesa no humor; zerar desmaia | −4 fome/h, −5 sede/h; < 30 "com fome" (−10), < 15 "faminto" (−25); desmaio −40 por 8 h |
| **Comer** | O único sorteio do sistema: intoxicação | "Enjoado" −35 por 6 h; devolve metade do que comeu |
| **Autonomia** | Ana decide sozinha: água primeiro, depois a melhor refeição, e cozinha se nada serve | Só aceita risco de intoxicação > 10% se faminta; não come porção que rende < +10 |
| **Dinheiro e lojas** | Saldo da casa; mercado barato que fecha às 20h × conveniência 24h, +40% e produto com 2 dias de prateleira | Começa com $150; comer custa $41–78/dia no mercado |
| **Carrinho** | É do modelo, não da tela. "Completar receita" põe só o que falta | Nada é cobrado antes de "Comprar" |
| **Relógio e diário** | Tudo envelhece hora a hora; o diário registra o que aconteceu (parede de notificações) | Últimas 30 entradas |

### A interface

- **Painel de contexto:** uma janela com 7 regiões fixas (assunto, ações, primária, secundária,
  prévia, resultado, confirmar). Cozinhar, comprar e comer são **o mesmo painel com definições
  diferentes**, não janelas novas.
- **Navegador:** as telas formam uma pilha. Abrir empilha; Esc/B/✕ volta uma. O cabeçalho
  mostra a trilha ("Fogão › Mercearia"), o cursor volta ao lugar de onde saiu, e toda troca
  tem um fade curto.
- **Controle, teclado e mouse são caminhos iguais.** A navegação é por região (Tab, LB/RB) e o
  rodapé mostra as teclas da região focada.
- **Arte por convenção de nome:** `Art/Ingredients/bacon.png` aparece sozinha onde o bacon
  aparece. Sem arte, cada item mostra sua cor com duas iniciais. 39 imagens estão listadas;
  nenhuma foi feita ainda.
- **Bancada de testes:** o fogão em tela cheia, com relógio manual e troca de pele (clara ou
  escura). É uma ferramenta de desenvolvimento.

---

## 3. Regras de design que valem para qualquer proposta

1. **Nada de lista escrita à mão do que pode ser derivado.** Nome, preço e qualidade de prato
   saem do avaliador. Uma proposta que pede "uma tabela de 200 pratos" contraria o sistema.
2. **O número mostrado é o número recebido.** A prévia e a ação chamam a mesma função.
3. **Um jeito de fazer cada coisa.** Sem atalhos duplicados ou configurações redundantes.
4. **A profundidade é opcional.** O caminho de 1 clique existe sempre; o sistema profundo dá
   peso à escolha, mas não é obrigatório.
5. **Toda interação nova é uma tela do mesmo painel**, empilhada no navegador. Nada de janela
   própria.
6. **Conteúdo é arquivo.** Um sistema novo define os "substantivos" em C#; a variedade vem de
   `.tres`.
7. **Os efeitos se explicam antes da escolha.** Efeito que o jogador não lê antes de escolher é
   efeito que ele não usa.
8. **O modelo é testável fora do Godot.** As regras ficam em C# puro, verificadas por um script
   de balanceamento (`BalanceCheck`) que simula 3 dias da casa.

---

## 4. O que ainda não existe (candidatos)

Esta é a lista sobre a qual pedir propostas. A avaliação é **preliminar e minha** (do
desenvolvedor do protótipo), para ser confirmada ou contestada.

| # | Candidato | Por que entraria | Esforço | Avaliação preliminar |
|---|---|---|---|---|
| 1 | **Salvar / carregar** | Sem isso, voltar ao menu perde a casa | M | Necessário |
| 2 | **Sono e energia** | Segunda necessidade com ciclo diário; dá sentido à noite | M | Necessário |
| 3 | **Emprego de verdade** | Troca o salário fixo; cria conflito entre horário e cozinhar | M–G | Necessário |
| 4 | **Perícia de culinária que sobe** | O nível hoje é fixo (6); cozinhar deveria ensinar | P | Útil, barato |
| 5 | **Descobrir receitas** | O Sim conhece todas as âncoras; descobrir dá progressão | P–M | Útil |
| 6 | **Higiene e diversão** | Completa o quadro clássico de necessidades | M | Bem-vindo, depois do sono |
| 7 | **Mais cômodos e objetos como conteúdo** | Hoje são 4 objetos fixos numa cozinha | M | Necessário para crescer |
| 8 | **Modo construir / comprar móveis** | Pilar do gênero | G | Bem-vindo, caro |
| 9 | **Mais de um Sim e refeição em grupo** | Família e porções divididas | G | Bem-vindo, caro |
| 10 | **Social e relacionamentos** | Pilar do gênero | G | Bem-vindo, caro |
| 11 | **Traços de personalidade** | Reações diferentes à mesma comida | M | Útil |
| 12 | **Armazenamento (geladeira × bancada)** | Onde a comida fica muda o quanto estraga | P | Útil, pequeno |
| 13 | **Deslocamento ou entrega das compras** | Hoje a compra chega na hora | P–M | Discutível |
| 14 | **Tempo correndo com painel aberto** | Como no The Sims | P (técnico) | Discutível: pode atropelar o jogador |
| 15 | **Morte por fome** | Hoje só desmaia | P | Decisão de tom |
| 16 | **Arte, som e música** | Hoje tudo é marcador colorido e silêncio | G (produção) | Necessário para mostrar |
| 17 | **Opções e acessibilidade** | Volume, tamanho de texto, remapear teclas | P–M | Bem-vindo |
| 18 | **Versão mobile e toque** | Alvo de dispositivos mais fracos | M | Avaliar cedo |
| 19 | **Reuso no jogo de futebol** | Painel e navegador já são genéricos | — | Validar com 1 tela de futebol |

Esforço: **P** até 1 dia · **M** alguns dias · **G** uma semana ou mais, no ritmo atual.

---

## 5. Pedido padrão (colar depois do contexto)

> Com base no contexto acima, proponha um protótipo para **[CANDIDATO]**.
>
> Respeite as regras de design da seção 3. Responda **exatamente** no formato abaixo, sem seções
> extras. Use números concretos (valores, tempos, custos) em vez de "ajustar depois".

### Formato de resposta

```
## [Candidato]

### Resumo
Duas frases: o que o jogador ganha.

### Fluxo do jogador
Passo a passo em lista numerada, do clique ao resultado, citando qual tela do painel abre
(assunto / ações / primária / secundária / prévia / resultado / confirmar).

### Regras e números
Tabela: regra | valor | por quê.

### O que muda no que já existe
Lista: arquivo ou sistema existente → mudança.

### Conteúdo (.tres)
Que novos tipos de arquivo e quantos exemplos.

### Arte necessária
Lista no padrão Art/<Categoria>/<id>.png com tamanho.

### Riscos
O que pode virar complicação sem o jogador perceber valor.

### Avaliação (1 a 5)
| Critério | Nota | Justificativa em uma linha |
| Viável (custo de fazer) | | |
| Necessário (o jogo é incompleto sem) | | |
| Útil (o jogador usa de fato) | | |
| Bem-vindo (o jogador gosta) | | |
| Desprezível (5 = pode cortar sem perda) | | |

### Menor versão que já prova a ideia
Uma frase.
```

---

## 6. Onde está cada coisa (para quem for ler o código)

```
CookingSystem/
  Cooking/    regras da cozinha (C# puro)        Household/  casa, Sim, autonomia, loja (C# puro)
  Data/       tipos de conteúdo                  Content/    conteúdo em .tres + carregadores
  addons/context_panel/   painel, navegador, transições (reutilizável)
  UI/         telas do painel e interações       UI/Game/    menu, casa, relógio
  Art/        arte por convenção                 Tools/      BalanceCheck, planilha de arte
  docs/       ARTE-E-FLUXO.md, arte-e-fluxo.xlsx, context-panel.md, este resumo
```

Repositório: `github.com/Monkius-Maximus/sistemas-auxiliares`, branch `claude/context-engine-panel`.
