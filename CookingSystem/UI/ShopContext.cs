using System;
using System.Collections.Generic;
using System.Linq;
using ContextUi;
using LifeSim.Household;

namespace LifeSim.Cooking;

/// <summary>
/// A loja escrita como definição de contexto: vendedor, prateleira com steppers, recibo e o saldo
/// depois da compra. É o layout de loja do mock original, que agora tem um sistema por trás.
///
/// Função pura de casa e carrinho para definição. Não guarda nada.
/// </summary>
public static class ShopContext
{
    private const float PanelWidth = 1120f;

    public static PanelContext Build(
        Household.Household home,
        ShoppingTrip trip,
        IReadOnlyList<VendorDef> vendors,
        IReadOnlyList<RecipeAnchor> anchors,
        Action onBuy,
        Action onClose)
    {
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(trip);

        var vendor = trip.Vendor;
        var pantry = home.Kitchen.Pantry;
        bool open = vendor.IsOpenAt(home.HourOfDay);
        int total = trip.Total;
        int after = home.Funds - total;
        var shelf = pantry.Defs.Where(vendor.Sells).OrderBy(d => d.DisplayName).ToList();

        return new PanelContext
        {
            Title = "Mercearia",
            Crumb = $"{vendor.DisplayName} · dia {home.Day}, {home.HourOfDay:00}h · saldo ${home.Funds}",
            Width = PanelWidth,
            OnClose = onClose,
            FocusEntry = PanelRegionId.Primary,

            Subject = new PanelRegion
            {
                Title = "Onde comprar",
                Body = new Picker
                {
                    Note = VendorNote(vendor, open),
                    Options = vendors.Select(v => new PickerOption
                    {
                        Name = v.DisplayName,
                        Icon = v.Icon,
                        Tint = v.TintColor,
                        Selected = v == vendor,
                        OnPick = () => trip.SetVendor(v),
                    }).ToList(),
                },
            },

            Actions = new PanelRegion
            {
                Title = "Completar receita",
                Body = new VerbList { Verbs = RecipeVerbs(trip, pantry, anchors, open).ToList() },
            },

            Primary = new PanelRegion
            {
                Title = "Na prateleira",
                // O rótulo vai no cabeçalho para o ladrilho caber os dois números inteiros.
                Count = open ? "preço · bom em casa" : "fechado",
                Body = new SlotGrid
                {
                    Mode = SlotGridMode.Quantity,
                    Slots = shelf.Select(def => Slot(home, trip, def, open, after)).ToList(),
                },
            },

            Secondary = new PanelRegion
            {
                Title = "Carrinho",
                Count = trip.Items == 0 ? "vazio" : $"{trip.Items} itens",
                Body = new Checklist { Rows = Receipt(trip) },
            },

            Preview = new PreviewCard
            {
                Title = "Vendedor",
                Art = vendor.Icon,
                Tint = vendor.TintColor,
                Name = vendor.DisplayName,
                Description = vendor.Description,
                Tags = new[]
                {
                    open ? "aberto" : "fechado",
                    vendor.Markup > 1f ? $"+{(vendor.Markup - 1f) * 100f:0}% no preço" : "preço de tabela",
                    vendor.StockAgeDays > 0f ? $"prateleira {vendor.StockAgeDays:0} d" : "produto do dia",
                },
            },

            Readout = new Readout
            {
                Title = "Resultado",
                Headline = "Saldo depois da compra",
                Value = $"${after}",
                Fill = home.Funds <= 0 ? 0f : Math.Clamp(after / (float)home.Funds, 0f, 1f),
                Caption = after < 0
                    ? $"Faltam ${-after}. Tire algo do carrinho."
                    : vendor.StockAgeDays > 0f && trip.Items > 0
                        ? $"Chega com {vendor.StockAgeDays:0} dias de prateleira: estraga antes do que o do mercado."
                        : $"Salário de ${Household.Household.DailyIncome} todo dia às {Household.Household.PaydayHour}h.",
                Stats = new[]
                {
                    new ReadoutStat { Key = "Total", Value = $"${total}" },
                    new ReadoutStat { Key = "Itens", Value = $"{trip.Items}" },
                    new ReadoutStat { Key = "Saldo", Value = $"${home.Funds}", Tone = StatTone.Muted },
                    new ReadoutStat
                    {
                        Key = "Preço", Value = vendor.Markup > 1f ? $"+{(vendor.Markup - 1f) * 100f:0}%" : "tabela",
                        Tone = vendor.Markup > 1f ? StatTone.Alert : StatTone.Neutral,
                    },
                    new ReadoutStat
                    {
                        Key = "Horário", Value = vendor.AlwaysOpen ? "24 h" : $"{vendor.OpensAt}–{vendor.ClosesAt}h",
                        Tone = open ? StatTone.Neutral : StatTone.Alert,
                    },
                    new ReadoutStat
                    {
                        Key = "Prateleira", Value = vendor.StockAgeDays > 0f ? $"{vendor.StockAgeDays:0} d" : "do dia",
                        Tone = vendor.StockAgeDays > 0f ? StatTone.Alert : StatTone.Neutral,
                    },
                },
            },

            Commit = new CommitAction
            {
                Label = !open ? "Fechado" : after < 0 ? "Dinheiro curto" : "Comprar",
                Meta = total > 0 ? $"${total}" : "",
                Enabled = open && trip.Items > 0 && after >= 0,
                // O botão explica a própria recusa — nunca um cinza mudo.
                Hint = !open ? $"{vendor.DisplayName} abre às {vendor.OpensAt}h. A conveniência abre sempre."
                    : trip.Items == 0 ? "Carrinho vazio."
                    : after < 0 ? $"Faltam ${-after}."
                    : "Vai direto para a despensa.",
                OnRun = onBuy,
            },
        };
    }

    private static string VendorNote(VendorDef v, bool open) =>
        open ? v.Description
        : $"Fechado agora. Abre às {v.OpensAt}h.";

    /// <summary>
    /// Um verbo por receita conhecida, que põe no carrinho exatamente o que falta. Liga o
    /// "Falta: Batata" do menu rápido à loja sem o jogador fazer a conta de cabeça.
    /// </summary>
    private static IEnumerable<Verb> RecipeVerbs(ShoppingTrip trip, Pantry pantry,
                                                 IReadOnlyList<RecipeAnchor> anchors, bool open)
    {
        foreach (var anchor in anchors)
        {
            var missing = trip.MissingFor(anchor, pantry);
            bool impossible = trip.CannotCompleteHere(anchor, pantry);
            int cost = missing.Sum(m => trip.Vendor.PriceOf(m.Def) * m.Missing);

            yield return new Verb
            {
                Name = anchor.DishName,
                Locked = !open || missing.Count == 0 || impossible,
                Note = impossible ? $"{trip.Vendor.DisplayName} não vende tudo o que falta."
                     : missing.Count == 0 ? "Já tem tudo — na despensa ou no carrinho."
                     : $"Falta {string.Join(", ", missing.Select(m => $"{m.Def.DisplayName.ToLowerInvariant()} {m.Missing}"))} · ${cost}",
                OnUse = () => trip.AddMissingFor(anchor, pantry),
            };
        }
    }

    private static PanelSlot Slot(Household.Household home, ShoppingTrip trip, IngredientDef def, bool open, int after)
    {
        int price = trip.Vendor.PriceOf(def);
        int inCart = trip.UnitsOf(def);
        int have = ShoppingTrip.Usable(home.Kitchen.Pantry, def);
        return new PanelSlot
        {
            Name = def.DisplayName,
            Icon = def.Icon,
            Tint = def.TintColor,
            // O que já tem em casa e ainda presta, ao lado do preço: comprar sem saber o estoque
            // é o erro mais comum, e contar o podre como estoque é o segundo.
            Sub = $"${price} · {have}",
            Quantity = inCart,
            Enabled = open && after >= price,
            OnAdd = () => trip.Add(def),
            OnRemove = () => trip.Remove(def),
        };
    }

    private static IReadOnlyList<ChecklistRow> Receipt(ShoppingTrip trip) =>
        trip.Items == 0
            ? new[] { new ChecklistRow { Name = "Nada no carrinho", Note = "+ na prateleira", Tint = Godot.Colors.Transparent } }
            : trip.Cart.OrderBy(kv => kv.Key.DisplayName).Select(kv => new ChecklistRow
            {
                Name = kv.Key.DisplayName,
                Icon = kv.Key.Icon,
                Tint = kv.Key.TintColor,
                Note = $"{kv.Value} × ${trip.Vendor.PriceOf(kv.Key)}",
                Tally = $"${trip.Vendor.PriceOf(kv.Key) * kv.Value}",
            }).ToList();
}
