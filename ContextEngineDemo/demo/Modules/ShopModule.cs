using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Storefront. Same shell, different definition: the picker lists vendors instead of vessels,
/// the receipt is a checklist instead of a slot grid, and the evaluator counts money instead
/// of quality. Nothing in the panel had to change to make that true.
/// </summary>
public sealed class ShopModule : IContextModule
{
    private const int Wallet = 240;

    private readonly ActionSet _acts = new(("haggle", false), ("inspect", false));
    private readonly Dictionary<string, int> _cart = new();
    private string _vendor = "general";

    public ContextDefinition Definition { get; } = new()
    {
        Id = "shop",
        Title = "Storefront",
        Crumb = "counter",
        Width = 1120,
        PausesWorld = true,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "vendors@location"),
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "haggle", "inspect", "credit" }),
            ("gate", new Godot.Collections.Dictionary { { "haggle", "barter>=4" }, { "credit", "rep>=8" } })),
        Primary = new RegionSpec("grid.qty", "vendor.stock",
            ("price", "item.price * vendor.markup * (1 - discount)")),
        Secondary = new RegionSpec("checklist", "cart", ("mode", "receipt")),
        Preview = new RegionSpec("preview", "cart.last", ("art", "item.sprite")),
        Readout = new RegionSpec("readout", "", ("evaluator", "WalletEvaluator"), ("factors", 0)),
        CommitAction = "purchase",
        CommitLabel = "Buy",
        CommitBlockedWhen = "total > wallet",
    };

    public void Enter()
    {
        _cart.Clear();
        _cart["eggs"] = 2;
        _vendor = "general";
    }

    public void Apply(Intent intent)
    {
        switch (intent)
        {
            case SubjectChanged s:
                _vendor = s.Id;
                _cart.Clear();
                break;
            case QtyChanged q:
                var line = GameData.Stock[_vendor].FirstOrDefault(i => i.Id == q.Id);
                if (line != null) ModuleSupport.Bump(_cart, q.Id, q.Delta, line.Stock);
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
            case Committed:
                _cart.Clear();
                break;
        }
    }

    public ContextViewModel Build()
    {
        var vendor = GameData.Vendors.First(v => v.Id == _vendor);
        var items = GameData.Stock[_vendor];
        var haggle = _acts["haggle"] && WalletEvaluator.HaggleAllowed(_vendor);
        var inspect = _acts["inspect"];

        var wallet = WalletEvaluator.Instance.Evaluate(new WalletState(_vendor, _cart, _acts["haggle"], Wallet));
        var lines = items.Where(i => _cart.ContainsKey(i.Id)).ToList();
        var last = lines.LastOrDefault();

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = vendor.Name + " · counter",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Vendor", GameData.Vendors, _vendor, vendor.Note,
                    v => (v.Id, v.Name, v.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("haggle", "Haggle", "BART 4",
                        !WalletEvaluator.HaggleAllowed(_vendor) ? "Kaster does not negotiate."
                        : haggle ? "−8% on every line. Rep cost if you push again."
                        : "Try for 8% off the whole basket."),
                    new Verb("inspect", "Inspect goods", "PERC 5",
                        inspect ? "Condition shown on each shelf tile." : "Reveal condition before you pay for junk."),
                    // Visible and disabled, which is the point.
                    new Verb("credit", "Ask for credit", "REP 8",
                        "Locked — needs standing 8 with this vendor.", Locked: true)),

                [RegionId.Primary] = new TileGridVm
                {
                    Title = "On the shelf",
                    Count = items.Length + " lines",
                    Tiles = items.Select(i => Tile(i, vendor, haggle, inspect)).ToList(),
                },

                [RegionId.Secondary] = new ChecklistVm
                {
                    Title = "Receipt",
                    Count = lines.Count > 0 ? lines.Count + " items" : "empty",
                    Rows = lines.Count > 0
                        ? lines.Select(i => new ChecklistRow
                        {
                            Name = i.Name,
                            Tint = i.Color,
                            MarkAlert = true,
                            Note = $"{_cart[i.Id]} × ${WalletEvaluator.Price(i, vendor, haggle)}" + (haggle ? " · haggled" : ""),
                            Tally = "$" + WalletEvaluator.Price(i, vendor, haggle) * _cart[i.Id],
                        }).ToList()
                        : new List<ChecklistRow>
                        {
                            new() { Name = "Nothing selected", MarkOff = true, Note = "press + on a shelf item",
                                    Tally = "$0", TallyTone = Tone.Mute },
                        },
                },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "Last picked",
                    SlotLabel = "sprite 64²",
                    Name = last?.Name ?? vendor.Name,
                    Desc = last != null
                        ? inspect
                            ? $"Condition {Mathf.RoundToInt((float)(last.Cond * 100))}% — affects how long it lasts in your bag."
                            : "Condition unknown. Inspect before buying perishables."
                        : "Nothing in the basket. Pick something off the shelf.",
                    Tags = last != null
                        ? new List<string>
                        {
                            "$" + WalletEvaluator.Price(last, vendor, haggle),
                            _cart[last.Id] + " in basket",
                            haggle ? "haggled" : "list price",
                        }
                        : new List<string> { vendor.Name, Mathf.RoundToInt((float)((vendor.Markup - 1) * 100)) + "% markup" },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = "Cash after purchase",
                    Value = "$" + (Wallet - wallet.Total),
                    ValueTone = wallet.Over ? Tone.Alert : Tone.Ink,
                    BarFraction = wallet.Load,
                    Caption = wallet.Over
                        ? "Over budget — remove a line or haggle."
                        : $"Spending {Mathf.RoundToInt((float)(wallet.Load * 100))}% of what you carry.",
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Subtotal", Value = "$" + wallet.Total },
                        new() { Key = "Saved", Value = "$" + wallet.Saved, Tone = haggle ? Tone.Alert : Tone.Mute },
                        new() { Key = "Markup", Value = Mathf.RoundToInt((float)((vendor.Markup - 1) * 100)) + "%",
                                Tone = vendor.Markup > 1 ? Tone.Alert : Tone.Mute },
                        new() { Key = "Items", Value = wallet.Items.ToString() },
                        new() { Key = "Wallet", Value = "$" + Wallet, Tone = Tone.Mute },
                        new() { Key = "Rep", Value = haggle ? "−1" : "+" + lines.Count, Tone = haggle ? Tone.Alert : Tone.Ink },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = wallet.Over ? "Not enough cash" : "Buy",
                    Meta = wallet.Total > 0 ? "$" + wallet.Total : "",
                    Disabled = wallet.Over || wallet.Total == 0,
                    // The button explains its own refusal.
                    Hint = wallet.Over
                        ? $"You are ${wallet.Short} short."
                        : "Goods go straight to your bag; weight applies.",
                },
            },
        };
    }

    private TileVm Tile(StockLine line, Vendor vendor, bool haggle, bool inspect)
    {
        var inCart = _cart.TryGetValue(line.Id, out var n) ? n : 0;
        var price = WalletEvaluator.Price(line, vendor, haggle);
        return new TileVm
        {
            Id = line.Id,
            Name = line.Name,
            Tint = line.Color,
            Sub = inspect
                ? $"${price} · cond {Mathf.RoundToInt((float)(line.Cond * 100))}%"
                : $"${price} · {line.Stock - inCart} left",
            SubAlert = inspect && line.Cond < 0.7,
            Qty = inCart > 0 ? inCart.ToString() : "–",
            Active = inCart > 0,
            CanAdd = inCart < line.Stock,
            CanSub = inCart > 0,
        };
    }
}
