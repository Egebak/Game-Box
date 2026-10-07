using Godot;

namespace GameBox.Games.WordMission.UI;

public partial class WordVisual : Control
{
    private string _visualId = "";
    public string VisualId
    {
        get => _visualId;
        set { _visualId = value; QueueRedraw(); }
    }

    public WordVisual() => CustomMinimumSize = new Vector2(190, 170);

    public override void _Draw()
    {
        var c = Size / 2;
        var gold = new Color("#ffd16d");
        var sky = new Color("#6bd6e9");
        var dark = new Color("#20344b");
        switch (VisualId)
        {
            case "sun":
                for (var i = 0; i < 12; i++)
                {
                    var angle = i * Mathf.Tau / 12;
                    var ray = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    DrawLine(c + ray * 50, c + ray * 70, gold, 7);
                }
                DrawCircle(c, 42, gold);
                break;
            case "icecream":
                DrawLine(c + new Vector2(-29, -12), c + new Vector2(0, 70), gold, 8);
                DrawLine(c + new Vector2(29, -12), c + new Vector2(0, 70), gold, 8);
                DrawCircle(c + new Vector2(0, -23), 35, new Color("#f3a9c8"));
                DrawCircle(c + new Vector2(0, -60), 10, new Color("#db5c68"));
                break;
            case "clock":
                DrawCircle(c, 62, gold);
                DrawCircle(c, 52, dark);
                DrawLine(c, c + new Vector2(0, -34), sky, 7);
                DrawLine(c, c + new Vector2(25, 8), sky, 7);
                DrawCircle(c, 7, sky);
                break;
            case "mouse":
                DrawCircle(c + new Vector2(-33, -36), 25, new Color("#a9bed1"));
                DrawCircle(c + new Vector2(32, -36), 25, new Color("#a9bed1"));
                DrawCircle(c, 57, new Color("#c2d4df"));
                DrawCircle(c + new Vector2(-20, -4), 6, dark);
                DrawCircle(c + new Vector2(20, -4), 6, dark);
                DrawCircle(c + new Vector2(0, 19), 8, new Color("#ed92a3"));
                DrawLine(c + new Vector2(53, 22), c + new Vector2(82, 43), sky, 5);
                break;
            case "house":
                DrawRect(new Rect2(c + new Vector2(-48, -17), new Vector2(96, 82)), sky);
                DrawLine(c + new Vector2(-58, -17), c + new Vector2(0, -76), gold, 11);
                DrawLine(c + new Vector2(0, -76), c + new Vector2(58, -17), gold, 11);
                DrawRect(new Rect2(c + new Vector2(-13, 19), new Vector2(26, 46)), dark);
                break;
        }
    }
}

public partial class GateVisual : Control
{
    private float _openAmount;

    public GateVisual() => CustomMinimumSize = new Vector2(420, 130);

    public void Open()
    {
        CreateTween().TweenMethod(Callable.From<float>(value =>
        {
            _openAmount = value;
            QueueRedraw();
        }), 0f, 1f, 0.7f);
    }

    public override void _Draw()
    {
        var c = Size / 2;
        DrawRect(new Rect2(c.X - 190, c.Y - 55, 380, 110), new Color("#8ed3a1"));
        DrawCircle(new Vector2(c.X - 145 + _openAmount * 285, c.Y + 22), 16,
            new Color("#ffd16d"));
        var offset = _openAmount * 105;
        DrawRect(new Rect2(c.X - 105 - offset, c.Y - 52, 105, 104), new Color("#6bd6e9"));
        DrawRect(new Rect2(c.X + offset, c.Y - 52, 105, 104), new Color("#6bd6e9"));
        DrawRect(new Rect2(c.X - 8, c.Y - 57, 16, 114), new Color("#20344b"));
    }
}

public partial class ToyCannonVisual : Control
{
    private float _shot;

    public ToyCannonVisual() => CustomMinimumSize = new Vector2(230, 90);

    public void Fire()
    {
        var tween = CreateTween();
        tween.TweenMethod(Callable.From<float>(value => { _shot = value; QueueRedraw(); }), 0f, 1f, 0.3f);
        tween.TweenCallback(Callable.From(() => { _shot = 0; QueueRedraw(); }));
    }

    public override void _Draw()
    {
        var c = Size / 2;
        DrawRect(new Rect2(c.X - 45, c.Y + 8, 90, 35), new Color("#6bd6e9"));
        DrawLine(c + new Vector2(0, 8), c + new Vector2(0, -39), new Color("#ffd16d"), 19);
        DrawCircle(c + new Vector2(-24, 43), 12, new Color("#20344b"));
        DrawCircle(c + new Vector2(24, 43), 12, new Color("#20344b"));
        if (_shot > 0)
            DrawCircle(c + new Vector2(0, -43 - _shot * 40), 9, new Color("#ffd16d"));
    }
}
