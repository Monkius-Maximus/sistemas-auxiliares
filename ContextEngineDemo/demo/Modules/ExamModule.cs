using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ContextEngine.Demo;

/// <summary>
/// Examination. Two new primitives earn their place here: the question stack in primary, and
/// the context clock in the readout. The clock is not wall time — pausing is legal and the
/// evaluator stays pure, which is exactly why a timer belongs in the readout rather than
/// inside the maths.
/// </summary>
public sealed class ExamModule : IContextModule
{
    private readonly ActionSet _acts = new(("work", true), ("elim", false), ("check", false));
    private readonly Dictionary<int, int> _answers = new();
    private readonly List<int> _working = new();

    private string _paper = "math";
    private int _index;
    private double _time = 240;
    private bool _running = true;

    public ContextDefinition Definition { get; } = new()
    {
        Id = "exam",
        Title = "Examination",
        Crumb = "desk 14 · Northfield High",
        Width = 1200,
        // A repair under pressure does not pause the world; an exam very much does.
        PausesWorld = true,
        FocusEntry = RegionId.Primary,
        Subject = new RegionSpec("picker", "papers@timetable"),
        Actions = new RegionSpec("verbs", "",
            ("items", new Godot.Collections.Array { "work", "elim", "check", "cheat" }),
            ("gate", new Godot.Collections.Dictionary { { "cheat", "sneak>=7 && !invigilator.watching" } })),
        Primary = new RegionSpec("question", "paper.questions", ("mode", "single"), ("pager", true)),
        Secondary = new RegionSpec("sequence", "paper.method", ("scored", "order"), ("gatedBy", "work")),
        Preview = new RegionSpec("preview", "paper", ("art", "paper.sprite")),
        Readout = new RegionSpec("readout", "",
            ("evaluator", "ExamEvaluator"),
            ("timer", new Godot.Collections.Dictionary
            {
                { "secs", "paper.secs" }, { "pausable", true }, { "rate", "check ? 2 : 1" },
            }),
            ("forecast", "grade@question")),
        CommitAction = "handIn",
        CommitLabel = "Hand in",
        CommitBlockedWhen = "answered == 0 || time == 0",
    };

    public void Enter()
    {
        _paper = "math";
        _answers.Clear();
        _working.Clear();
        _index = 0;
        _time = GameData.Papers.First(p => p.Id == _paper).Secs;
        _running = true;
    }

    public bool Tick(double delta)
    {
        if (!_running || _time <= 0) return false;

        // Double-checking the paper spends two seconds a second — a modifier verb with a real cost.
        var before = Mathf.FloorToInt(_time);
        _time = Mathf.Max(0, _time - delta * (_acts["check"] ? 2 : 1));
        return Mathf.FloorToInt(_time) != before;
    }

    public void Apply(Intent intent)
    {
        var questions = GameData.Questions[_paper];

        switch (intent)
        {
            case SubjectChanged s:
                _paper = s.Id;
                _answers.Clear();
                _working.Clear();
                _index = 0;
                _time = GameData.Papers.First(p => p.Id == _paper).Secs;
                _running = true;
                break;
            case PagerChanged p:
                _index = Mathf.Clamp(p.Index, 0, questions.Length - 1);
                break;
            case Answered a:
                if (_time <= 0) break;
                _answers[a.QuestionIndex] = a.OptionIndex;
                _index = Mathf.Min(questions.Length - 1, a.QuestionIndex + 1);
                break;
            case StepTaken s:
                if (!_working.Contains(s.Index)) _working.Add(s.Index);
                break;
            case SequenceReset:
                _working.Clear();
                break;
            case TimerToggled:
                _running = !_running;
                break;
            case ActionToggled a:
                _acts.Toggle(a.Id);
                break;
            case Committed:
                _running = false;
                break;
        }
    }

    public ContextViewModel Build()
    {
        var paper = GameData.Papers.First(p => p.Id == _paper);
        var questions = GameData.Questions[_paper];
        var steps = GameData.Working[_paper];
        var index = Mathf.Min(_index, questions.Length - 1);
        var question = questions[index];

        var work = _acts["work"];
        var elim = _acts["elim"];
        var timeLeft = Mathf.FloorToInt(_time);
        var timeUp = timeLeft <= 0;

        var judgement = ExamEvaluator.Instance.Evaluate(new ExamState(
            _paper, _answers, _working, timeLeft, work, elim, _acts["check"]));

        var sequence = ModuleSupport.Sequence(steps, _working, "Working",
            "Tap the steps in the order you would actually work them.");

        // Eliminate crosses out the first wrong answer — an information verb that lifts a blind guess.
        var struck = elim ? System.Array.FindIndex(question.Options, o => !o.Ok) : -1;
        var picked = _answers.TryGetValue(index, out var pick) ? pick : -1;
        var timeFraction = Mathf.Clamp((double)timeLeft / paper.Secs, 0, 1);

        return new ContextViewModel
        {
            Title = Definition.Title,
            Crumb = $"{paper.Name} · desk 14 · Northfield High",
            Width = Definition.Width,
            Regions = new Dictionary<RegionId, IRegionVm>
            {
                [RegionId.Subject] = ModuleSupport.Picker("Paper", GameData.Papers, _paper, paper.Note,
                    p => (p.Id, p.Name, p.Color)),

                [RegionId.Actions] = ModuleSupport.Verbs(_acts,
                    new Verb("work", "Show your working", "STUDY 4",
                        work ? (sequence.InOrder ? "Method credit is accruing." : "Wrong order costs 7 points of the mark.")
                             : "Unlocks the working panel; method carries part of the mark."),
                    new Verb("elim", "Eliminate an option", "LOGIC 5",
                        elim ? "One wrong answer crossed out per question."
                             : "Crosses out one wrong answer, lifting a blind guess to 34%."),
                    new Verb("check", "Double-check the paper", "CARE 3",
                        _acts["check"] ? "+4 points, and the clock burns twice as fast."
                                       : "+4 points on the mark, at double time cost."),
                    new Verb("cheat", "Glance at Nina's sheet", "SNEAK 7",
                        "Locked — she sits two rows forward and Vance is watching.", Locked: true)),

                [RegionId.Primary] = new QuestionVm
                {
                    Title = "Question stack",
                    Count = $"Q{index + 1}/{questions.Length}",
                    Index = index,
                    Topic = question.Topic,
                    Prompt = question.Prompt,
                    Frozen = timeUp,
                    Pager = questions.Select((_, i) => new PagerCell
                    {
                        Index = i,
                        Label = (i + 1).ToString(),
                        Current = i == index,
                        Answered = _answers.ContainsKey(i),
                    }).ToList(),
                    Options = question.Options.Select((o, i) => new AnswerOption
                    {
                        Index = i,
                        Letter = "ABCD"[i].ToString(),
                        Text = o.Text,
                        Selected = picked == i,
                        StruckOut = i == struck,
                        Tag = i == struck ? "crossed out" : picked == i ? "your answer" : "",
                    }).ToList(),
                },

                [RegionId.Secondary] = work
                    ? sequence.Vm
                    : new TextVm
                    {
                        Title = "Working",
                        Count = "hidden",
                        Body = "Turn on Show your working and the sequence primitive appears here: the four " +
                               "steps of the method, tapped in the order you would actually work them. Order " +
                               "is scored — the same primitive drives repair procedures and any recipe with a method.",
                    },

                [RegionId.Preview] = new PreviewVm
                {
                    Title = "On the desk",
                    SlotLabel = "sprite 96²",
                    Name = paper.Name + " paper",
                    Desc = "Answer sheet, HB pencil, no calculator. Vance walks the aisle every ninety seconds.",
                    Tags = new List<string>
                    {
                        paper.Name,
                        $"{judgement.Answered} of {questions.Length} answered",
                        ModuleSupport.Clock(timeLeft) + " left",
                    },
                },

                [RegionId.Readout] = new ReadoutVm
                {
                    Headline = timeUp ? "Paper collected" : "Projected mark",
                    Value = Mathf.RoundToInt((float)(judgement.Projected * 100)) + "%",
                    ValueTone = judgement.Passing ? Tone.Ink : Tone.Alert,
                    BarFraction = Mathf.Max(judgement.Projected, 0.02),
                    Caption = timeUp
                        ? "Time ran out — unanswered questions score zero."
                        : judgement.Passing
                            ? $"Above the {Mathf.RoundToInt((float)(paper.Pass * 100))}% line, on current accuracy."
                            : $"Below the {Mathf.RoundToInt((float)(paper.Pass * 100))}% line. Answer more, or show the working.",
                    Timer = new TimerVm
                    {
                        Label = _running ? "Time remaining" : "Clock paused",
                        Clock = ModuleSupport.Clock(timeLeft),
                        Fraction = timeFraction,
                        Alert = timeFraction < 0.22,
                        ButtonLabel = _running ? "Pause" : "Resume",
                        Caption = _acts["check"]
                            ? "Double-checking: the clock spends two seconds a second."
                            : "A context clock, not wall time — pausing is legal and changes nothing else.",
                    },
                    Forecast = new ForecastVm
                    {
                        Title = "Mark trajectory",
                        Bars = judgement.Trajectory.ToList(),
                        Threshold = paper.Pass,
                        From = "Q1",
                        To = "Q" + questions.Length,
                        Delta = $"pass line {Mathf.RoundToInt((float)(paper.Pass * 100))}%",
                        DeltaAlert = !judgement.Passing,
                    },
                    Rows = new List<StatVm>
                    {
                        new() { Key = "Answered", Value = $"{judgement.Answered} / {questions.Length}" },
                        new() { Key = "Sure of", Value = work ? judgement.Correct.ToString() : "??",
                                Tone = work ? Tone.Ink : Tone.Mute },
                        new() { Key = "Method", Value = work ? (sequence.InOrder ? "in order" : "slipped") : "off",
                                Tone = work ? (sequence.InOrder ? Tone.Ink : Tone.Alert) : Tone.Mute },
                        new() { Key = "Nerve", Value = judgement.Rushed ? "shaky" : "steady",
                                Tone = judgement.Rushed ? Tone.Alert : Tone.Ink },
                        new() { Key = "Pass at", Value = Mathf.RoundToInt((float)(paper.Pass * 100)) + "%", Tone = Tone.Dim },
                        new() { Key = "Sec / Q", Value = judgement.Answered > 0
                                ? Mathf.RoundToInt((float)(paper.Secs - timeLeft) / judgement.Answered).ToString() : "—",
                                Tone = Tone.Dim },
                    },
                },

                [RegionId.Commit] = new CommitVm
                {
                    Label = timeUp ? "Time up" : judgement.Answered > 0 ? "Hand in" : "Nothing answered",
                    Meta = judgement.Answered > 0 ? Mathf.RoundToInt((float)(judgement.Projected * 100)) + "%" : "",
                    Disabled = timeUp || judgement.Answered == 0,
                    Hint = timeUp
                        ? "Vance already took the sheet."
                        : judgement.Answered > 0
                            ? "Handing in early banks the mark and the remaining minutes."
                            : "Answer at least one question first.",
                },
            },
        };
    }
}
