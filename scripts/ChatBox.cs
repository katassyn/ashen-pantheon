using System.Collections.Generic;
using Godot;

/// <summary>Czat co-op (Enter otwiera pole, Enter wysyła). Log zanika, gdy nie piszesz.</summary>
public partial class ChatBox : CanvasLayer
{
    private VBoxContainer _log;
    private LineEdit _input;
    private readonly List<(Label label, float age)> _lines = new();
    private bool _typing;

    public override void _Ready()
    {
        Layer = 8;
        var root = new VBoxContainer
        {
            AnchorLeft = 0f, AnchorTop = 1f, AnchorBottom = 1f,
            OffsetLeft = 12, OffsetTop = -300, OffsetRight = 460, OffsetBottom = -12,
        };
        root.AddThemeConstantOverride("separation", 2);
        AddChild(root);

        _log = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _log.AddThemeConstantOverride("separation", 1);
        root.AddChild(_log);

        _input = new LineEdit { PlaceholderText = "say something…  (/g = guild chat)  (Enter to send, Esc to cancel)", Visible = false, MaxLength = 200 };
        _input.TextSubmitted += OnSubmit;
        root.AddChild(_input);

        Net.ChatReceived += OnChat;
    }

    public override void _ExitTree() => Net.ChatReceived -= OnChat;

    // ── czat gildii (online realm): poll HTTP w tle co 8 s, "/g tekst" wysyła ──

    private float _guildPoll;
    private long _guildLastId;
    private bool _guildPrimed; // pierwszy poll tylko ustawia kursor (bez wylewania historii)
    private bool _pollBusy;

    private void PollGuildChat()
    {
        _pollBusy = true;
        long since = _guildLastId;
        System.Threading.Tasks.Task.Run(() =>
        {
            string json = AccountClient.GetJson($"/guild/chat?sinceId={since}") ?? "";
            CallDeferred(nameof(OnGuildPoll), json);
        });
    }

    private void OnGuildPoll(string json)
    {
        _pollBusy = false;
        if (json.Length == 0) return;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var m in doc.RootElement.GetProperty("Messages").EnumerateArray())
            {
                long id = m.GetProperty("Id").GetInt64();
                if (id > _guildLastId) _guildLastId = id;
                if (_guildPrimed)
                    OnChat($"[G] {m.GetProperty("From").GetString()}: {m.GetProperty("Text").GetString()}");
            }
        }
        catch { /* uszkodzona odpowiedź — następny poll */ }
        _guildPrimed = true;
    }

    private void OnChat(string line)
    {
        var lbl = new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        lbl.AddThemeFontSizeOverride("font_size", 13);
        lbl.AddThemeConstantOverride("outline_size", 3);
        lbl.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
        _log.AddChild(lbl);
        _lines.Add((lbl, 0f));
        while (_lines.Count > 12) { _lines[0].label.QueueFree(); _lines.RemoveAt(0); }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;

        if (!_typing && k.PhysicalKeycode is Key.Enter or Key.KpEnter)
        {
            _typing = true;
            _input.Visible = true;
            _input.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
        else if (_typing && k.PhysicalKeycode == Key.Escape)
        {
            CloseInput();
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnSubmit(string text)
    {
        if (text.StartsWith("/g "))
        {
            string msg = text[3..].Trim();
            if (!AccountSession.LoggedIn) OnChat("[G] Log in to the online realm to use guild chat.");
            else if (msg.Length > 0)
                System.Threading.Tasks.Task.Run(() =>
                {
                    var (ok, err) = AccountClient.Post("/guild/chat", new { Text = msg });
                    if (!ok) CallDeferred(nameof(OnChat), $"[G] {err}");
                });
            _guildPoll = 0.7f; // szybki poll — zaraz zobaczysz własną wiadomość
            CloseInput();
            return;
        }
        Net.SendChat(text);
        CloseInput();
    }

    private void CloseInput()
    {
        _input.Text = "";
        _input.Visible = false;
        _input.ReleaseFocus();
        _typing = false;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // guild chat poll (pełni też rolę heartbeatu presence — serwer robi Touch przy każdym żądaniu)
        if (AccountSession.LoggedIn)
        {
            _guildPoll -= dt;
            if (_guildPoll <= 0f && !_pollBusy)
            {
                _guildPoll = 8f;
                PollGuildChat();
            }
        }
        for (int i = 0; i < _lines.Count; i++)
        {
            var (label, age) = _lines[i];
            age += dt;
            _lines[i] = (label, age);
            // podczas pisania log w pełni widoczny; poza tym zanika po 10 s
            float alpha = _typing ? 1f : Mathf.Clamp(1f - (age - 10f) / 3f, 0f, 1f);
            label.Modulate = new Color(1f, 1f, 1f, alpha);
        }
    }
}
