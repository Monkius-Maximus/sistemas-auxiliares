using System.Collections.Generic;
using Godot;

namespace ContextEngine.Demo;

// The sample world the seven demo contexts read from. In the real game these come from
// inventory, vendors, benches and the character sheet — the modules only ever see the shape,
// which is what "a source is a query, not a hard-coded list" buys you.

public sealed record Ingredient(string Id, string Name, string Tint, string Group, int Stock,
    double Hunger, double Kcal, double Prot, double Fat, double Carb, double Mood)
{
    public Color Color => new(Tint);
}

public sealed record Seasoning(string Id, string Name, string Tint, int Stock, double Power, string Profile)
{
    public Color Color => new(Tint);
}

public sealed record Vessel(string Id, string Name, string Tint, string Note)
{
    public Color Color => new(Tint);
}

public sealed record Vendor(string Id, string Name, string Tint, string Note, double Markup)
{
    public Color Color => new(Tint);
}

public sealed record StockLine(string Id, string Name, string Tint, int Price, int Stock, double Cond)
{
    public Color Color => new(Tint);
}

public sealed record Bench(string Id, string Name, string Tint, string Note)
{
    public Color Color => new(Tint);
}

public sealed record Part(string Id, string Name, string Tint, int Have)
{
    public Color Color => new(Tint);
}

public sealed record Blueprint(string Id, string Name, string Tint, Dictionary<string, int> Need,
    string Time, double Base, string Out, string Desc)
{
    public Color Color => new(Tint);
}

public sealed record Container(string Id, string Name, string Tint, string Note, double Cap, string Desc)
{
    public Color Color => new(Tint);
}

public sealed record ContainerItem(string Id, string Name, string Tint, int Qty, double Weight, bool Food)
{
    public Color Color => new(Tint);
}

public sealed record Paper(string Id, string Name, string Tint, int Secs, double Pass, string Note)
{
    public Color Color => new(Tint);
}

public sealed record AnswerData(string Text, bool Ok);

public sealed record QuestionData(string Topic, string Prompt, AnswerData[] Options);

public sealed record Lock(string Id, string Name, string Tint, int Pins, double Speed, double Win, string Note)
{
    public Color Color => new(Tint);
}

public sealed record Rig(string Id, string Name, string Tint, string Note)
{
    public Color Color => new(Tint);
}

public sealed record VehicleSystem(string Id, string Name, Rect2 Rect, double Cond, string Tool,
    string Fault, string[] Steps);

public static class GameData
{
    public static readonly Ingredient[] Ingredients =
    {
        new("egg",      "Egg",      "#e8b93f", "protein", 24, 2.1, 31, 2.8, 2.2, 0.1, 0.4),
        new("bacon",    "Bacon",    "#b8503f", "meat",    18, 1.7, 42, 3.1, 3.6, 0.0, 0.7),
        new("cheese",   "Cheese",   "#d8c078", "dairy",   14, 1.5, 38, 2.4, 3.1, 0.3, 0.6),
        new("broccoli", "Broccoli", "#4b7d46", "veg",     12, 1.2,  9, 0.8, 0.1, 1.4, -0.1),
        new("tomato",   "Tomato",   "#c14a33", "veg",     16, 1.0,  7, 0.3, 0.1, 1.2, 0.2),
        new("mushroom", "Mushroom", "#8e7565", "veg",      9, 1.1,  6, 0.6, 0.1, 0.7, 0.1),
        new("potato",   "Potato",   "#b99a5e", "starch",  20, 2.4, 34, 0.7, 0.1, 7.2, 0.2),
        new("scallion", "Scallion", "#5fa84e", "veg",     11, 0.4,  3, 0.2, 0.0, 0.5, 0.3),
    };

    public static readonly Seasoning[] Seasonings =
    {
        new("salt",    "Salt",    "#cfcbc6", 30, 1.4, "salty"),
        new("pepper",  "Pepper",  "#5e4a3c", 22, 1.1, "salty"),
        new("sugar",   "Sugar",   "#e6ddc4", 26, 1.3, "sweet"),
        new("vinegar", "Vinegar", "#b79a4e", 12, 1.0, "sour"),
        new("herbs",   "Herbs",   "#4d7a3a", 15, 0.8, "green"),
    };

    public static readonly Vessel[] Vessels =
    {
        new("skillet", "Skillet", "#7d7979", "Frying · fast, browns fat, no liquid retention."),
        new("pot",     "Pot",     "#605d5d", "Boiling · keeps liquid, softens vegetables, slower."),
        new("bowl",    "Bowl",    "#bab6b6", "Raw assembly · no heat, freshness preserved."),
    };

    public static readonly Vendor[] Vendors =
    {
        new("general",  "Grocer",   "#7d7979", "Ramos Grocery · open 08:00–19:00 · knows you.", 1.00),
        new("hardware", "Hardware", "#605d5d", "Kaster Supply · open 09:00–17:00 · fixed prices.", 1.08),
        new("pharmacy", "Pharmacy", "#bab6b6", "Night counter · open 24h · surcharge on meds.", 1.22),
    };

    public static readonly Dictionary<string, StockLine[]> Stock = new()
    {
        ["general"] = new StockLine[]
        {
            new("eggs",   "Egg Carton", "#e8b93f",  4,  6, 0.94),
            new("bacon",  "Bacon Pack", "#b8503f",  9,  3, 0.61),
            new("rice",   "Rice 1kg",   "#e6ddc4",  6,  8, 1.00),
            new("coffee", "Coffee",     "#5e4a3c", 12,  2, 0.88),
            new("beans",  "Beans Can",  "#8e7565",  3, 11, 1.00),
            new("soap",   "Soap Bar",   "#cfcbc6",  2,  9, 1.00),
        },
        ["hardware"] = new StockLine[]
        {
            new("wrench", "Wrench",    "#7d7979", 28,  2, 0.72),
            new("duct",   "Duct Tape", "#9b9797", 11,  5, 1.00),
            new("nails",  "Nails Box", "#bab6b6",  7,  7, 1.00),
            new("plank",  "Plank",     "#b99a5e",  5, 12, 0.83),
            new("paint",  "Paint Can", "#c14a33", 19,  3, 0.40),
        },
        ["pharmacy"] = new StockLine[]
        {
            new("pills", "Painkiller", "#e6e2dd", 16, 4, 0.99),
            new("gauze", "Gauze",      "#cfcbc6",  8, 6, 1.00),
            new("anti",  "Antibiotic", "#b8503f", 41, 1, 0.55),
            new("vitc",  "Vitamin C",  "#e8b93f", 13, 5, 0.90),
        },
    };

    public static readonly Bench[] Benches =
    {
        new("workbench", "Workbench",  "#b99a5e", "Carpentry Lv 4 · +6% success on wood recipes."),
        new("forge",     "Forge",      "#b8503f", "Metalwork Lv 2 · heat-only recipes unlocked."),
        new("sewing",    "Sewing Kit", "#7d7979", "Tailoring Lv 5 · repairs cost half the cloth."),
    };

    public static readonly Part[] Parts =
    {
        new("plank", "Plank",       "#b99a5e",  6),
        new("nails", "Nails",       "#bab6b6", 14),
        new("scrap", "Scrap Metal", "#7d7979",  3),
        new("cloth", "Cloth Strip", "#8e7565",  9),
        new("wire",  "Wire",        "#c9a45e",  1),
        new("glue",  "Wood Glue",   "#e6ddc4",  2),
    };

    public static readonly Dictionary<string, Blueprint[]> Blueprints = new()
    {
        ["workbench"] = new Blueprint[]
        {
            new("barricade", "Barricade", "#b99a5e", new() { ["plank"] = 4, ["nails"] = 8 },
                "45s", 0.92, "Window Barricade", "Nailed across a frame. Slows anything trying to come through."),
            new("shelf", "Shelf", "#c9a45e", new() { ["plank"] = 6, ["nails"] = 12, ["glue"] = 1 },
                "2m", 0.78, "Wall Shelf", "Wall storage, four slots. Needs a stud to bite into."),
            new("crate", "Storage Crate", "#8e7565", new() { ["plank"] = 5, ["nails"] = 6, ["wire"] = 2 },
                "3m", 0.64, "Storage Crate", "Portable when empty, 40 kg when full."),
        },
        ["forge"] = new Blueprint[]
        {
            new("blade", "Hatchet Head", "#7d7979", new() { ["scrap"] = 4, ["wire"] = 1 },
                "6m", 0.51, "Hatchet Head", "Rough forged. Still needs a handle and a grind."),
            new("hinge", "Hinges", "#9b9797", new() { ["scrap"] = 2 },
                "2m", 0.83, "Hinge ×2", "Door and lid hardware. Squeaks unless oiled."),
        },
        ["sewing"] = new Blueprint[]
        {
            new("bandage", "Bandage", "#e6e2dd", new() { ["cloth"] = 2 },
                "20s", 0.97, "Sterile Bandage ×2", "Boiled and dried. Stops bleeding, not infection."),
            new("pouch", "Belt Pouch", "#8e7565", new() { ["cloth"] = 6, ["wire"] = 1 },
                "4m", 0.72, "Belt Pouch", "+2 kg carry, one quick-slot."),
        },
    };

    public static readonly Container[] Containers =
    {
        new("footlocker", "Footlocker", "#7d7979", "Army footlocker · unlocked · 30 kg capacity.", 30, "Someone lived out of this. Latch is bent."),
        new("fridge",     "Fridge",     "#bab6b6", "Powered · slows spoilage while stored.",       22, "Still humming. Contents keep four times longer."),
        new("trunk",      "Car Trunk",  "#605d5d", "Vehicle storage · 60 kg, must be parked.",     60, "Opens only when the engine is off."),
    };

    public static readonly Dictionary<string, ContainerItem[]> ContainerItems = new()
    {
        ["footlocker"] = new ContainerItem[]
        {
            new("ammo",    "9mm Rounds",     "#c9a45e", 34, 0.01, false),
            new("canned",  "Canned Stew",    "#8e7565",  4, 0.60, true),
            new("jacket",  "Leather Jacket", "#5e4a3c",  1, 2.40, false),
            new("battery", "Battery",        "#4b7d46",  2, 0.50, false),
        },
        ["fridge"] = new ContainerItem[]
        {
            new("milk",    "Milk Carton", "#e6e2dd", 2, 1.20, true),
            new("steak",   "Steak",       "#b8503f", 3, 0.60, true),
            new("lettuce", "Lettuce",     "#4b7d46", 1, 0.30, true),
        },
        ["trunk"] = new ContainerItem[]
        {
            new("jerry",   "Jerry Can",  "#c14a33", 1,  8.0, false),
            new("spare",   "Spare Tire", "#3a3634", 1, 12.0, false),
            new("toolbox", "Toolbox",    "#7d7979", 1,  5.5, false),
        },
    };

    public static readonly Paper[] Papers =
    {
        new("math", "Algebra", "#c9a45e", 240, 0.60, "Period 3 · Vance · four minutes on the clock · pass at 60%."),
        new("bio",  "Biology", "#4b7d46", 200, 0.50, "Period 5 · Okonjo · diagrams count double · pass at 50%."),
        new("hist", "History", "#8e7565", 300, 0.55, "Period 1 · Adler · dates carry the mark · pass at 55%."),
    };

    public static readonly Dictionary<string, QuestionData[]> Questions = new()
    {
        ["math"] = new QuestionData[]
        {
            new("Linear equations", "Solve for x:  3(x − 4) = 2x + 5", new AnswerData[]
                { new("x = 17", true), new("x = 7", false), new("x = −7", false), new("x = 1", false) }),
            new("Ratio", "A pot feeds 4 on 300 g of rice. How much rice for 10?", new AnswerData[]
                { new("750 g", true), new("600 g", false), new("900 g", false), new("1.2 kg", false) }),
            new("Percentages", "A $9 pack of bacon is marked down 22%. What do you pay?", new AnswerData[]
                { new("$7.02", true), new("$6.98", false), new("$7.78", false), new("$2.00", false) }),
            new("Interest", "You owe $200 at 5% a week, simple. What is owed after three weeks?", new AnswerData[]
                { new("$230", true), new("$215", false), new("$300", false), new("$205", false) }),
        },
        ["bio"] = new QuestionData[]
        {
            new("Cells", "Which organelle releases the energy held in food?", new AnswerData[]
                { new("Mitochondrion", true), new("Ribosome", false), new("Golgi body", false), new("Vacuole", false) }),
            new("Nutrition", "Boiling vegetables in water mainly costs you which nutrient?", new AnswerData[]
                { new("Vitamin C", true), new("Iron", false), new("Protein", false), new("Fat", false) }),
            new("Spoilage", "Refrigeration slows spoilage because it…", new AnswerData[]
                { new("Slows microbial growth", true), new("Kills all bacteria", false), new("Removes water", false), new("Raises pH", false) }),
            new("Inheritance", "Two brown-eyed parents, one blue-eyed child. Blue eyes are…", new AnswerData[]
                { new("Recessive", true), new("Dominant", false), new("Sex-linked", false), new("A new mutation", false) }),
        },
        ["hist"] = new QuestionData[]
        {
            new("Rationing", "Ration books were issued in order to control…", new AnswerData[]
                { new("Scarce goods", true), new("Wages", false), new("Rail traffic", false), new("Postage", false) }),
            new("Dates", "The Marshall Plan was announced in…", new AnswerData[]
                { new("1947", true), new("1939", false), new("1952", false), new("1961", false) }),
            new("Sources", "A shop ledger is best used as evidence of…", new AnswerData[]
                { new("Everyday prices", true), new("Public opinion", false), new("Military strategy", false), new("Harvest weather", false) }),
            new("Cause", "Bread riots most often follow a sharp rise in…", new AnswerData[]
                { new("Grain prices", true), new("Literacy", false), new("Rail fares", false), new("Tax refunds", false) }),
        },
    };

    public static readonly Dictionary<string, string[]> Working = new()
    {
        ["math"] = new[] { "Read the question twice", "Write down what you know", "Do the algebra", "Substitute back to check" },
        ["bio"] = new[] { "Underline the key term", "Recall the definition", "Rule out two answers", "Sanity-check the units" },
        ["hist"] = new[] { "Date it first", "Place the actors", "Link cause to effect", "Name the source type" },
    };

    public static readonly Lock[] Locks =
    {
        new("padlock",  "Padlock",  "#7d7979", 3, 0.85, 26, "Laminated body · 3 pins · forgiving, loud."),
        new("deadbolt", "Deadbolt", "#605d5d", 5, 1.20, 17, "House door · 5 pins · spring-loaded, punishing."),
        new("ignition", "Ignition", "#bab6b6", 4, 1.65, 13, "Steering column · fast return · picks snap here."),
    };

    public static readonly Rig[] Rigs =
    {
        new("sedan",  "Sedan",     "#7d7979", "'89 estate · will not turn over · in the driveway."),
        new("pickup", "Pickup",    "#605d5d", "Flatbed · overheats after 20 km · bed still loaded."),
        new("genset", "Generator", "#bab6b6", "Portable 3 kW · runs rough · smells of fuel."),
    };

    public static readonly Dictionary<string, VehicleSystem[]> Systems = new()
    {
        ["sedan"] = new VehicleSystem[]
        {
            new("batt",  "Battery",     new Rect2(0.03f, 0.04f, 0.30f, 0.28f), 0.18, "Wire brush",
                "Terminals furred white; charge reads 18%.",
                new[] { "Disconnect the negative first", "Brush both terminals", "Reconnect and grease", "Crank to confirm" }),
            new("alt",   "Alternator",  new Rect2(0.35f, 0.04f, 0.30f, 0.28f), 0.88, "Stethoscope",
                "Bearing whines above idle, output still fine.",
                new[] { "Belt off", "Spin the pulley by hand", "Listen at the case", "Belt back on" }),
            new("rad",   "Radiator",    new Rect2(0.67f, 0.04f, 0.30f, 0.44f), 0.42, "Hose clamp",
                "Weeping at the lower hose under pressure.",
                new[] { "Let it cool", "Drain a litre", "Swap the clamp", "Top up and bleed" }),
            new("belt",  "Drive belt",  new Rect2(0.03f, 0.34f, 0.30f, 0.28f), 0.35, "Tensioner",
                "Glazed, cracking across the ribs.",
                new[] { "Mark the routing", "Release the tensioner", "Fit the new belt", "Tension to spec" }),
            new("plugs", "Plugs",       new Rect2(0.35f, 0.34f, 0.30f, 0.28f), 0.60, "Gap tool",
                "Two plugs sooted, gaps opened up.",
                new[] { "Pull one lead at a time", "Clean and gap", "Torque in sequence", "Refit the leads" }),
            new("fuel",  "Fuel filter", new Rect2(0.03f, 0.64f, 0.62f, 0.32f), 0.74, "Filter",
                "Original filter; runs rich at idle.",
                new[] { "Depressurise the line", "Catch the spill", "Fit it arrow-forward", "Prime and check for leaks" }),
            new("oil",   "Oil & sump",  new Rect2(0.67f, 0.50f, 0.30f, 0.46f), 0.93, "Drain pan",
                "Level fine, colour dark, overdue by 900 km.",
                new[] { "Warm the engine", "Drain and swap the washer", "New filter, hand tight", "Refill to the upper mark" }),
        },
        ["pickup"] = new VehicleSystem[]
        {
            new("pump",   "Water pump", new Rect2(0.03f, 0.04f, 0.46f, 0.42f), 0.30, "Gasket",
                "Play in the shaft, pink crust below it.",
                new[] { "Drain the block", "Unbolt the pulley", "Scrape the mating face", "Fit the gasket dry" }),
            new("therm",  "Thermostat", new Rect2(0.51f, 0.04f, 0.46f, 0.42f), 0.55, "Housing bolts",
                "Opens late; gauge climbs before it moves.",
                new[] { "Drain to the housing", "Note the spring direction", "Fit with a new seal", "Bleed the top hose" }),
            new("clutch", "Fan clutch", new Rect2(0.03f, 0.50f, 0.30f, 0.46f), 0.66, "Fan spanner",
                "Free-wheels when hot; no roar under load.",
                new[] { "Engine cold", "Hold the pulley", "Swap the clutch", "Spin test at idle" }),
            new("rad",    "Radiator",   new Rect2(0.36f, 0.50f, 0.30f, 0.46f), 0.48, "Fin comb",
                "Lower third blocked with chaff.",
                new[] { "Shroud off", "Comb the fins", "Flush from the back", "Refit the shroud" }),
            new("hoses",  "Hoses",      new Rect2(0.69f, 0.50f, 0.28f, 0.46f), 0.80, "Clamps",
                "Upper hose soft where it meets the neck.",
                new[] { "Cool and drain", "Cut the old clamp", "Seat the hose fully", "Torque the clamp" }),
        },
        ["genset"] = new VehicleSystem[]
        {
            new("carb", "Carburettor",    new Rect2(0.03f, 0.04f, 0.46f, 0.44f), 0.25, "Jet cleaner",
                "Main jet gummed; hunts at every load.",
                new[] { "Fuel off", "Drop the bowl", "Clean the main jet", "Set the mixture" }),
            new("line", "Fuel line",      new Rect2(0.51f, 0.04f, 0.46f, 0.44f), 0.50, "Hose cutter",
                "Perished at the tap, weeping when full.",
                new[] { "Drain the tank", "Cut back to clean rubber", "New line and clamps", "Leak-check at pressure" }),
            new("coil", "Ignition coil",  new Rect2(0.03f, 0.52f, 0.46f, 0.44f), 0.70, "Feeler gauge",
                "Air gap wide; weak spark when hot.",
                new[] { "Earth the plug lead", "Set the air gap", "Torque the mounts", "Spark test" }),
            new("pull", "Recoil starter", new Rect2(0.51f, 0.52f, 0.46f, 0.44f), 0.85, "Rope",
                "Rope frayed at the eyelet; spring fine.",
                new[] { "Release the spring tension", "Rethread the rope", "Retension two turns", "Test ten pulls" }),
        },
    };
}
