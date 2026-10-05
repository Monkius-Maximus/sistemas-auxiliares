"""
Gera docs/arte-e-fluxo.xlsx: a lista de arte que o jogo procura, as partes da tela que já são
desenhadas por código, como inserir arte, e o mapa de cliques e cenas.

A lista de arte sai dos próprios .tres de Content/: ingrediente novo aparece na planilha
sem ninguém editar este script. Rodar de novo sempre que o conteúdo mudar:

    pip install openpyxl
    python3 Tools/gerar_planilha_arte.py

O status ("feito" / "falta") é lido de Art/: arte salva no caminho certo já sai marcada.
"""

import os
import re
import sys

from openpyxl import Workbook
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "arte-e-fluxo.xlsx")


# ----------------------------------------------------------------------
# Leitura do conteúdo
# ----------------------------------------------------------------------

def read_tres(path):
    """Os campos simples do bloco [resource] de um .tres: o que a planilha precisa."""
    text = open(path, encoding="utf-8").read()
    body = text[text.index("[resource]"):]
    fields = {}
    for key, value in re.findall(r'^(\w+) = (.+)$', body, re.M):
        fields[key] = value.strip().strip('"')
    return fields


def color_hex(value):
    """Color(r, g, b, a) do .tres em #rrggbb."""
    if not value:
        return "#ffffff"
    parts = [float(p) for p in re.findall(r'[\d.]+', value)[:3]]
    return "#" + "".join(f"{round(p * 255):02x}" for p in parts)


def monogram(name):
    """A mesma regra de PanelPrimitives.Monogram: o que o marcador mostra hoje."""
    words = [w for w in name.split() if w[0].isalpha() and len(w) > 2]
    if not words:
        return ""
    if len(words) > 1:
        return (words[0][0] + words[1][0]).upper()
    word = words[0]
    second = next((c for c in word[1:] if c.isalpha() and c.lower() not in "aeiouáéíóúâêôãõà"), None)
    if second is None:
        second = word[1] if len(word) > 1 else ""
    return (word[0] + second).upper()


def folder(name):
    path = os.path.join(ROOT, "Content", name)
    return [os.path.join(path, f) for f in sorted(os.listdir(path)) if f.endswith(".tres")]


def has_art(category, art_id):
    return os.path.exists(os.path.join(ROOT, "Art", category, art_id + ".png"))


# ----------------------------------------------------------------------
# A lista de arte
# ----------------------------------------------------------------------

ART_HEADERS = ["Prioridade", "Categoria", "ID (nome do arquivo)", "Nome no jogo", "Caminho exato no projeto",
               "Tamanho do arquivo (px)", "Exibido em (px)", "Fundo", "Onde aparece", "Marcador atual (cor)",
               "Iniciais do marcador", "Status"]


def art_rows():
    rows = []

    def add(priority, category, art_id, name, size, shown, background, where, tint="", initials=None):
        rows.append([priority, category, art_id, name, f"res://Art/{category}/{art_id}.png", size, shown,
                     background, where, tint, monogram(name) if initials is None else initials,
                     "feito" if has_art(category, art_id) else "falta"])

    for path in folder("Ingredients"):
        f = read_tres(path)
        kind = "tempero" if f.get("IsSeasoning") == "true" else "ingrediente"
        add("A", "Ingredients", f["Id"], f["DisplayName"], "128×128", "22–30",
            "transparente",
            f"Ladrilho da despensa (fogão › preparar manualmente), prateleira e carrinho da loja, "
            f"porções do menu rápido. É um {kind}.",
            color_hex(f.get("TintColor")))

    bases = [read_tres(p) for p in folder("Bases")]
    for f in bases:
        add("B", "Vessels", f["Id"], f["DisplayName"], "128×128", "22 e 96",
            "transparente",
            "Escolha do recipiente no painel manual; prévia do prato enquanto o recipiente está vazio.",
            color_hex(f.get("TintColor")))

    for path in folder("Anchors"):
        f = read_tres(path)
        art_id = os.path.splitext(os.path.basename(path))[0]
        tint = next((color_hex(b.get("TintColor")) for b in bases if b["Id"] == f.get("BaseItemId")), "")
        add("A", "Dishes", art_id, f["DishName"], "256×256", "30 e 96",
            "transparente",
            "Prato de receita conhecida: linha do menu rápido, prato previsto, geladeira (lista e prévia). "
            "O nome do arquivo é o nome do .tres em Content/Anchors/.",
            tint)

    for f in bases:
        add("A", "Dishes", "generico-" + f["Id"], f"Prato improvisado na {f['DisplayName'].lower()} ({f.get('FormName', 'Prato')} de …)",
            "256×256", "30 e 96", "transparente",
            f"Qualquer prato improvisado na {f['DisplayName'].lower()} (\"{f.get('FormName', 'Prato')} de …\"): "
            "prato previsto do painel manual e geladeira.",
            color_hex(f.get("TintColor")), initials="(nome do prato)")

    for path in folder("Vendors"):
        f = read_tres(path)
        add("B", "Vendors", f["Id"], f["DisplayName"], "256×256", "22 e 96", "transparente",
            "Loja: escolha do vendedor e cartão do vendedor.", color_hex(f.get("TintColor")))

    house = [
        ("geladeira", "Geladeira", "#8fb3c9", "Objeto da casa. Clique abre a geladeira (comer)."),
        ("fogao", "Fogão", "#c96f4a", "Objeto da casa. Clique abre o menu rápido do fogão."),
        ("pia", "Pia", "#5b8fc7", "Objeto da casa. Clique: o Sim bebe água."),
        ("porta", "Porta", "#6f9a5a", "Objeto da casa. Clique abre a mercearia."),
    ]
    for art_id, name, tint, where in house:
        add("A", "House", art_id, name, "360×360", "180", "transparente", where, tint)
    add("C", "House", "fundo-cozinha", "Fundo da casa", "1920×1080", "tela inteira", "opaco",
        "Atrás do HUD e dos objetos. Sem ele, cor lisa da pele.", "", initials="—")

    hud = [
        ("fome", "Fome", "#d9a24a", "16 e 22", "HUD (barra de fome) e geladeira (linha Fome)."),
        ("sede", "Sede", "#5b8fc7", "16 e 22", "HUD (barra de sede) e geladeira (linha Sede)."),
        ("humor", "Humor", "#b07cc6", "22", "Geladeira (linha Humor)."),
        ("retrato-sim", "Ana", "#b07cc6", "28", "HUD: retrato do Sim ao lado do nome."),
        ("pausa", "Pausa", "", "≈24", "HUD: botão de pausa. Substitui o texto “II”."),
        ("velocidade-1", "Velocidade 1", "", "≈24", "HUD: botão > (normal)."),
        ("velocidade-2", "Velocidade 2", "", "≈24", "HUD: botão >> (rápido)."),
        ("velocidade-3", "Velocidade 3", "", "≈24", "HUD: botão >>> (ultra)."),
    ]
    for art_id, name, tint, shown, where in hud:
        add("B" if art_id in ("fome", "sede", "humor", "retrato-sim") else "C", "Hud", art_id, name,
            "128×128" if art_id == "retrato-sim" else "64×64", shown, "transparente", where, tint,
            initials="texto" if not tint else None)

    add("C", "Menu", "logo", "Logotipo", "760×320", "380×160", "transparente",
        "Menu principal, no lugar do título “Vida em Casa”.", "", initials="—")
    add("C", "Menu", "fundo", "Fundo do menu", "1920×1080", "tela inteira", "opaco",
        "Menu principal. Sem ele, cor lisa da pele.", "", initials="—")

    order = {"A": 0, "B": 1, "C": 2}
    rows.sort(key=lambda r: (order[r[0]], r[1]))
    return rows


# ----------------------------------------------------------------------
# As outras abas
# ----------------------------------------------------------------------

PROCEDURAL = [
    ["Elemento", "Desenhado por", "Cor vem de", "Precisa de arte?", "Observação"],
    ["Moldura, cabeçalho e colunas do painel", "addons/context_panel/ContextPanel.cs", "PanelSkin (Panel, PanelAlt, Line)", "Não",
     "Retângulos e divisórias de 2 px. Trocar a aparência = trocar as cores da pele."],
    ["Células, ladrilhos e botões (fundo e borda)", "PanelPrimitives.FlatButton / Box", "PanelSkin (Cell, LineSoft, Accent)", "Não",
     "Hover e pressionado também são cor."],
    ["Anel de foco (contorno laranja)", "PanelPrimitives.FocusRing", "PanelSkin.Accent", "Não", "Aparece com teclado e controle."],
    ["Barras (qualidade, fatores, fome, sede)", "PanelPrimitives.Bar · GameScreen.Need", "PanelSkin.Scale · NeedArt", "Não", ""],
    ["Etiquetas (tags) e chips", "PanelPrimitives.BuildPreview", "PanelSkin", "Não", ""],
    ["Marcador de item sem arte (cor + iniciais)", "PanelPrimitives.Swatch", "TintColor do .tres", "Não — é o substituto",
     "Some sozinho quando a arte existe. As iniciais separam itens de cor parecida (bacon × tomate)."],
    ["Pontos de estado (verbo ativo, porção cumprida)", "PanelPrimitives.Dot", "PanelSkin", "Não", ""],
    ["Teclas do rodapé (Enter, Tab, Esc…)", "addons/context_panel/PromptBar.cs", "PanelSkin", "Opcional, futuro",
     "Hoje é texto. Ícones de botão de controle entram aqui quando houver arte."],
    ["Fundo escurecido atrás do painel e da pausa", "ContextNavigator · GameScreen.Dimmer", "preto 60 %", "Não", ""],
    ["Fades entre telas e entre cenas", "ContextNavigator · SceneTransition", "preto", "Não", "0,12 s no painel, 0,18 s entre cenas."],
    ["HUD (barra superior)", "GameScreen.RefreshHud", "PanelSkin", "Só os ícones (aba Arte)", ""],
    ["Fonte", "padrão do Godot", "—", "Opcional, futuro",
     "Para trocar: Projeto › Configurações › GUI › Tema › Fonte personalizada."],
]

HOW_TO = [
    ["Passo", "O que fazer", "Detalhe"],
    ["1", "Escolha a linha na aba “Arte necessária”.", "A coluna “Caminho exato” é o arquivo que o jogo procura."],
    ["2", "Desenhe ou gere a imagem no tamanho da coluna “Tamanho”.",
     "PNG com fundo transparente (exceto fundos de tela). Objeto centralizado, sem margem grande, mesma luz e ângulo em toda a série."],
    ["3", "Salve com o nome exato, em minúsculas, sem acento e sem espaço.", "Ex.: Art/Ingredients/bacon.png. Crie a pasta se ela não existir."],
    ["4", "Abra (ou volte para) o editor do Godot.",
     "O editor importa o PNG sozinho e cria o .import do lado. Sem esse passo o jogo não enxerga o arquivo."],
    ["5", "Rode o jogo (F5).", "O marcador colorido some e a arte aparece em todas as telas que usam aquele item."],
    ["6", "Rode este script de novo.", "python3 Tools/gerar_planilha_arte.py — a coluna Status passa a “feito”."],
    ["Alternativa", "Arquivo com outro nome ou em outra pasta.",
     "Abra o .tres do item no Inspetor e arraste o PNG para o campo Icon (DishIcon no recipiente). O campo ganha da convenção."],
    ["Item novo", "Ingrediente, recipiente, receita ou loja novos.",
     "Crie o .tres em Content/<pasta>/ com um Id novo e salve a arte com o mesmo Id. Rode o script: a linha nova aparece aqui."],
    ["Pixel art", "Arte pequena e serrilhada de propósito.",
     "Projeto › Configurações › Renderização › Texturas › Filtro padrão = Nearest, senão o Godot borra os pixels."],
]

INTERACTIONS = [
    ["Tela", "Elemento", "Mouse", "Teclado", "Controle", "O que acontece", "Código"],
    ["Menu principal", "Novo jogo", "clique", "Enter", "A", "Abre a casa (Scenes/Game.tscn), dia 1 às 8h.", "MainMenu.cs"],
    ["Menu principal", "Bancada de testes", "clique", "Enter", "A", "Abre o fogão em tela cheia com pele, regiões e relógio na mão.", "MainMenu.cs → CookingDemo.cs"],
    ["Menu principal", "Sair", "clique", "Enter", "A", "Fecha o jogo.", "MainMenu.cs"],
    ["Casa", "Fogão", "clique", "Enter / Espaço com foco", "A", "Abre o menu rápido (O que preparar?). Tempo para.", "GameScreen → HouseInteractions.OpenStove"],
    ["Casa", "Geladeira", "clique", "Enter / Espaço com foco", "A", "Abre a geladeira (comer uma porção). Tempo para.", "HouseInteractions.OpenFridge"],
    ["Casa", "Pia", "clique", "Enter / Espaço com foco", "A", "Ana bebe água (+45 de sede). Desabilitada com sede cheia.", "Household.DrinkWater"],
    ["Casa", "Porta", "clique", "Enter / Espaço com foco", "A", "Abre a mercearia. Tempo para.", "HouseInteractions.OpenShop"],
    ["Casa", "II · > · >> · >>>", "clique", "P (pausa) · 1 · 2 · 3", "Select (pausa)", "Velocidade do relógio: 1 h de jogo = 4 s · 1,3 s · 0,4 s (um dia = 96 s na normal).", "GameClock"],
    ["Casa", "Livre-arbítrio", "clique", "—", "—", "Liga/desliga a autonomia: Ana come, bebe e cozinha sozinha.", "Household.Autonomous"],
    ["Casa", "Menu (Esc)", "clique", "Esc", "Start", "Menu de pausa: Continuar · Menu principal · Sair.", "GameScreen.ShowPauseMenu"],
    ["Painel (qualquer)", "✕ no canto", "clique", "Esc", "B", "Volta uma tela (Mercearia › Fogão › casa). O cursor volta ao ladrilho de onde saiu.", "ContextNavigator.Back"],
    ["Painel (qualquer)", "Trocar de região", "—", "Tab / Shift+Tab", "RB / LB", "Pula o foco entre os blocos do painel.", "ContextPanel.JumpRegion"],
    ["Painel (qualquer)", "Botão grande laranja", "clique", "Enter", "Y", "Confirma: Preparar, Cozinhar, Comer, Comprar.", "PanelContext.Commit"],
    ["Fogão › menu rápido", "Prato da lista", "clique", "setas + Enter", "direcional + A", "Marca o prato; o lado direito mostra a previsão.", "QuickMealContext"],
    ["Fogão › menu rápido", "Preparar", "clique", "Enter", "Y", "Cozinha o prato marcado; as porções vão para a geladeira.", "QuickMealPlanner.Prepare"],
    ["Fogão › menu rápido", "Preparar manualmente…", "clique", "Enter", "A", "Empilha o painel manual sobre o fogão (título “Fogão › Cozinha”).", "HouseInteractions.OpenManual"],
    ["Fogão › manual", "Ladrilho de ingrediente", "clique em + / −", "= / −", "A / X (segurar repete)", "Põe ou tira uma unidade do recipiente.", "CookingSession.AddUnit / RemoveUnit"],
    ["Fogão › manual", "Recipiente", "clique", "Enter", "A", "Troca frigideira/panela/tigela.", "CookingSession.SetBase"],
    ["Fogão › manual", "Cozinhar", "clique", "Enter", "Y", "Cozinha o que está no recipiente.", "CookingSession.Cook"],
    ["Geladeira", "Refeição", "clique", "Enter", "A", "Marca a refeição; mostra fome/sede/humor depois de comer.", "FridgeContext"],
    ["Geladeira", "Comer uma porção", "clique", "Enter", "Y", "Ana come; pode passar mal se estiver estragada.", "Household.Eat"],
    ["Loja", "Vendedor", "clique", "Enter", "A", "Troca mercado/conveniência (preço, horário, frescor).", "ShopContext"],
    ["Loja", "+ / − na prateleira", "clique", "= / −", "A / X", "Põe ou tira do carrinho.", "ShoppingTrip"],
    ["Loja", "Completar receita", "clique", "Enter", "A", "Põe no carrinho só o que falta para a receita.", "ShopContext"],
    ["Loja", "Comprar", "clique", "Enter", "Y", "Paga e leva para a despensa.", "Household.Buy"],
]

FLOW = [
    ["De", "Evento", "Para", "Tempo do jogo"],
    ["(abrir o jogo)", "cena principal do project.godot", "Menu principal", "não existe ainda"],
    ["Menu principal", "Novo jogo", "Casa", "começa a correr (1×)"],
    ["Menu principal", "Bancada de testes", "Bancada (CookingDemo)", "só pelos botões +4 h / +1 dia"],
    ["Bancada", "← Menu", "Menu principal", "a casa da bancada é descartada"],
    ["Casa", "clique num objeto (fogão, geladeira, porta)", "Casa + painel aberto", "parado"],
    ["Casa + painel aberto", "verbo que abre outra tela (ex.: Comprar mantimentos…)", "Casa + 2 telas na pilha", "parado"],
    ["Casa + 2 telas na pilha", "✕ / Esc / B", "Casa + painel aberto (a de baixo)", "parado"],
    ["Casa + painel aberto", "✕ / Esc / B na última tela", "Casa", "volta a correr"],
    ["Casa", "Esc / Start / botão Menu", "Pausa", "parado"],
    ["Pausa", "Continuar / Esc", "Casa", "volta a correr"],
    ["Pausa", "Menu principal", "Menu principal", "a casa é descartada (ainda não há save)"],
    ["Pausa", "Sair do jogo", "(fecha)", "—"],
]


# ----------------------------------------------------------------------
# Planilha
# ----------------------------------------------------------------------

HEAD_FILL = PatternFill("solid", fgColor="2B2B2B")
HEAD_FONT = Font(bold=True, color="FFFFFF")
THIN = Side(style="thin", color="BBBBBB")
GRID = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
STATUS_FILL = {"feito": PatternFill("solid", fgColor="CDEBC0"), "falta": PatternFill("solid", fgColor="F6D2C8")}
PRIORITY_FILL = {"A": PatternFill("solid", fgColor="FFE3B3"), "B": PatternFill("solid", fgColor="FFF4D6"),
                 "C": PatternFill("solid", fgColor="F2F2F2")}


def sheet(wb, title, rows, widths, first=False):
    ws = wb.active if first else wb.create_sheet()
    ws.title = title
    for row in rows:
        ws.append(row)
    for cell in ws[1]:
        cell.fill = HEAD_FILL
        cell.font = HEAD_FONT
    for row in ws.iter_rows():
        for cell in row:
            cell.border = GRID
            cell.alignment = Alignment(wrap_text=True, vertical="top")
    for i, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = width
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = ws.dimensions
    return ws


def main():
    wb = Workbook()

    rows = art_rows()
    ws = sheet(wb, "Arte necessária", [ART_HEADERS] + rows,
               [10, 12, 24, 30, 42, 14, 12, 13, 60, 14, 12, 9], first=True)
    for r in range(2, ws.max_row + 1):
        ws.cell(r, 1).fill = PRIORITY_FILL[ws.cell(r, 1).value]
        tint = ws.cell(r, 10).value
        if tint and tint.startswith("#"):
            ws.cell(r, 10).fill = PatternFill("solid", fgColor=tint[1:].upper())
        status = ws.cell(r, 12)
        status.fill = STATUS_FILL[status.value]

    legend = ws.max_row + 2
    ws.cell(legend, 1, "Prioridade: A = o jogo parece um jogo com isso · B = acabamento das telas · C = cosmético.")
    ws.cell(legend + 1, 1, "Tamanho do arquivo é o dobro do exibido ou mais: a tela escala para baixo sem perder nitidez, "
                           "e a mesma arte serve para telas maiores depois.")
    ws.cell(legend + 2, 1, f"Total: {len(rows)} imagens · faltam {sum(1 for r in rows if r[-1] == 'falta')}.")

    sheet(wb, "Já feito em código", PROCEDURAL, [42, 38, 34, 20, 70])
    sheet(wb, "Como inserir", HOW_TO, [12, 50, 100])
    sheet(wb, "Cliques e teclas", INTERACTIONS, [20, 26, 16, 22, 22, 60, 40])
    sheet(wb, "Fluxo de telas", FLOW, [26, 44, 26, 36])

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    wb.save(OUT)
    print(f"{OUT}: {len(rows)} imagens, {sum(1 for r in rows if r[-1] == 'falta')} faltando")


if __name__ == "__main__":
    sys.exit(main())
