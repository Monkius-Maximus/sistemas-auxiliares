using System;
using System.Collections.Generic;
using Godot;

namespace LifeSim.Ui;

/// <summary>
/// A definição de uma interação: o que o painel mostra quando o jogador clica no fogão,
/// na bancada, no balcão ou no baú. O shell (<see cref="ContextPanel"/>) não conhece
/// nenhum desses sistemas — ele lê uma definição destas, liga cada primitivo à sua região
/// e desenha. Um sistema novo é uma definição nova e, no máximo, um primitivo novo;
/// nunca uma janela nova.
///
/// Sete regiões, na ordem de leitura: em que você age (<c>Subject</c>), o que você faz
/// (<c>Actions</c>), o corpo da interação (<c>Primary</c>, <c>Secondary</c>) e o resultado
/// (<c>Preview</c>, <c>Readout</c>, <c>Commit</c>).
///
/// A definição é descartável e reconstruída inteira a cada mudança. Ela não guarda estado:
/// quem guarda é o dono do contexto — a <c>CookingSession</c>, no caso da cozinha.
/// </summary>
public sealed class PanelContext
{
    public required string Title { get; init; }

    /// <summary>Linha fina ao lado do título: onde a interação está acontecendo.</summary>
    public string Crumb { get; init; } = "";

    /// <summary>
    /// Largura declarada pelo contexto. Uma tela de transferência precisa de mais espaço
    /// que um menu de pratos, e é o contexto que sabe disso — não o shell.
    /// </summary>
    public required float Width { get; init; }

    /// <summary>Fecha a interação. O ✕ do cabeçalho só existe quando isto existe.</summary>
    public Action OnClose { get; init; }

    /// <summary>Em que se age: recipiente, vendedor, bancada, contêiner. Nulo quando não há escolha.</summary>
    public PanelRegion Subject { get; init; }

    /// <summary>Verbos e perícias que mudam o resultado. Nulo quando o contexto não tem nenhum.</summary>
    public PanelRegion Actions { get; init; }

    public required PanelRegion Primary { get; init; }

    public PanelRegion Secondary { get; init; }

    public required PreviewCard Preview { get; init; }
    public required Readout Readout { get; init; }
    public required CommitAction Commit { get; init; }
}

/// <summary>Uma região preenchível: cabeçalho com título e contador, e um corpo primitivo.</summary>
public sealed class PanelRegion
{
    public required string Title { get; init; }

    /// <summary>Contador no canto do cabeçalho ("3 / 4", "6 conhecidos"). Vazio não desenha.</summary>
    public string Count { get; init; } = "";

    public required RegionBody Body { get; init; }
}

/// <summary>
/// Um primitivo de corpo de região. O conjunto é fechado de propósito: cada tipo novo aqui
/// é uma forma nova de interagir no jogo inteiro, não um enfeite de uma tela só.
/// </summary>
public abstract class RegionBody
{
}

/// <summary>Escolha única em ladrilhos. O que está sendo usado: recipiente, bancada, vendedor.</summary>
public sealed class Picker : RegionBody
{
    public required IReadOnlyList<PickerOption> Options { get; init; }

    /// <summary>Uma linha sobre a opção selecionada. É onde o contexto explica a escolha.</summary>
    public string Note { get; init; } = "";
}

public sealed class PickerOption
{
    public required string Name { get; init; }
    public Texture2D Icon { get; init; }
    public Color Tint { get; init; } = Colors.White;
    public bool Selected { get; init; }
    public required Action OnPick { get; init; }
}

/// <summary>
/// Verbos do contexto: alternâncias que mudam o resultado ("provar enquanto cozinha") e
/// comandos diretos ("esvaziar"). Entradas trancadas continuam visíveis — é assim que o
/// jogador descobre que a perícia existe antes de ter a perícia.
/// </summary>
public sealed class VerbList : RegionBody
{
    public required IReadOnlyList<Verb> Verbs { get; init; }
}

public sealed class Verb
{
    public required string Name { get; init; }

    /// <summary>Perícia exigida, como o jogador lê ("CULIN 6"). "—" quando não há.</summary>
    public string Skill { get; init; } = "—";

    public string Note { get; init; } = "";

    /// <summary>Ligado. Só faz sentido para verbos que alternam; comandos ficam sempre falso.</summary>
    public bool Active { get; init; }

    /// <summary>Aparece apagado e não responde ao clique.</summary>
    public bool Locked { get; init; }

    public required Action OnUse { get; init; }
}

/// <summary>
/// <see cref="SlotGridMode.Quantity"/> dá steppers ± a cada ladrilho — é a grade da cozinha
/// e da prateleira da loja. <see cref="SlotGridMode.Select"/> usa o mesmo ladrilho como
/// escolha única — receitas, plantas de construção, opções de diálogo.
/// </summary>
public enum SlotGridMode
{
    Quantity,
    Select,
}

public sealed class SlotGrid : RegionBody
{
    public required SlotGridMode Mode { get; init; }
    public required IReadOnlyList<PanelSlot> Slots { get; init; }
    public int Columns { get; init; } = 4;
}

/// <summary>
/// Um ladrilho. Os dois modos compartilham o mesmo tipo porque compartilham a mesma
/// aparência: o que muda é o ladrilho ganhar steppers ou virar botão de escolha.
/// </summary>
public sealed class PanelSlot
{
    public required string Name { get; init; }
    public Texture2D Icon { get; init; }
    public Color Tint { get; init; } = Colors.White;

    /// <summary>Linha de apoio: estoque, preço, tempo. É onde o ladrilho declara seu custo.</summary>
    public string Sub { get; init; } = "";

    /// <summary>Modo Quantity: quanto já entrou. Zero desenha "–".</summary>
    public int Quantity { get; init; }

    /// <summary>Modo Select: o ladrilho escolhido.</summary>
    public bool Selected { get; init; }

    /// <summary>Pinta <see cref="Sub"/> com a cor de alarme. Para "falta", "condição ruim".</summary>
    public bool Warn { get; init; }

    /// <summary>Modo Quantity: o + responde. Modo Select: o ladrilho responde.</summary>
    public bool Enabled { get; init; } = true;

    public Action OnAdd { get; init; }
    public Action OnRemove { get; init; }
    public Action OnPick { get; init; }
}

/// <summary>Tenho versus preciso, linha a linha. Requisitos de construção, porções, recibo.</summary>
public sealed class Checklist : RegionBody
{
    public required IReadOnlyList<ChecklistRow> Rows { get; init; }
}

public sealed class ChecklistRow
{
    public required string Name { get; init; }
    public Texture2D Icon { get; init; }
    public Color Tint { get; init; } = Colors.White;
    public string Note { get; init; } = "";

    /// <summary>Coluna da direita: "6 / 4", "2 × $9". O número que decide a linha.</summary>
    public string Tally { get; init; } = "";

    /// <summary>Falso pinta a linha de alarme — é o "falta isto".</summary>
    public bool Met { get; init; } = true;
}

/// <summary>
/// O que vai sair da interação, com espaço de arte. É a única região que um artista
/// possui inteira: sprite, nome, uma linha e etiquetas.
/// </summary>
public sealed class PreviewCard
{
    public required string Title { get; init; }
    public Texture2D Art { get; init; }
    public Color Tint { get; init; } = Colors.White;
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// O número que decide. Valor grande com barra, decomposição opcional em fatores 0..1 e
/// uma pilha de estatísticas. Quem calcula é o avaliador do sistema — esta região só lê.
/// </summary>
public sealed class Readout
{
    public required string Title { get; init; }
    public required string Headline { get; init; }

    /// <summary>Já formatado pelo contexto: "81%", "$212", "12.4 kg".</summary>
    public required string Value { get; init; }

    /// <summary>0..1. Preenche a barra e escolhe a cor do valor.</summary>
    public required float Fill { get; init; }

    /// <summary>Uma linha dizendo o que fazer com o número.</summary>
    public string Caption { get; init; } = "";

    /// <summary>Vazio quando o sistema não decompõe o resultado.</summary>
    public IReadOnlyList<ReadoutFactor> Factors { get; init; } = Array.Empty<ReadoutFactor>();

    public IReadOnlyList<ReadoutStat> Stats { get; init; } = Array.Empty<ReadoutStat>();
}

public sealed class ReadoutFactor
{
    public required string Name { get; init; }

    /// <summary>0..1. A barra, a porcentagem e a cor saem daqui.</summary>
    public required float Value { get; init; }
}

/// <summary>
/// Papel do número, não sua cor. Nesta pele só o alarme tem cor própria: o bom não se
/// enfeita, o ruim grita.
/// </summary>
public enum StatTone
{
    Neutral,
    Alert,
    Muted,
}

public sealed class ReadoutStat
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public StatTone Tone { get; init; } = StatTone.Neutral;
}

/// <summary>Uma ação por contexto. O rótulo e o bloqueio são declarados pela definição.</summary>
public sealed class CommitAction
{
    public required string Label { get; init; }

    /// <summary>Custo ou escopo da ação, ao lado do rótulo: "$212", "45s", "Frigideira".</summary>
    public string Meta { get; init; } = "";

    /// <summary>Sob o botão: o que vai acontecer, ou por que não dá.</summary>
    public string Hint { get; init; } = "";

    public bool Enabled { get; init; } = true;

    public required Action OnRun { get; init; }
}
