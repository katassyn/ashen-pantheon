using System;
using Godot;

/// <summary>Maszyna stanów animacji wroga: idle/walk/windup/attack/hit/death.
/// Animacje generowane w kodzie na AnimationPlayer (placeholder ruchu sprite'a) —
/// podmiana na prawdziwy art = podmiana animacji, zero zmian w kodzie sterującym.</summary>
public partial class EnemyAnimator : Node
{
    private AnimationPlayer _player;
    private Sprite2D _sprite;
    private string _current = "";
    private Action _onDeathDone;

    public override void _Ready()
    {
        _sprite = GetParent().GetNodeOrNull<Sprite2D>("Sprite2D");
        _player = new AnimationPlayer();
        AddChild(_player);

        var lib = new AnimationLibrary();
        lib.AddAnimation("idle", BuildIdle());
        lib.AddAnimation("walk", BuildWalk());
        lib.AddAnimation("windup", BuildWindup());
        lib.AddAnimation("attack", BuildAttack());
        lib.AddAnimation("hit", BuildHit());
        lib.AddAnimation("death", BuildDeath());
        _player.AddAnimationLibrary("", lib);
        _player.AnimationFinished += OnFinished;
        Play("idle");
    }

    public void Play(string name)
    {
        if (_current == name || _current == "death") return;
        if (_current == "hit" && _player.IsPlaying() && name != "death") return; // hit ma priorytet do końca
        _current = name;
        _player.Play(name);
    }

    /// <summary>Jednorazowy przerywnik (hit-flash), po nim wraca do idle/walk sterowane z Behavior.</summary>
    public void Flash(string name)
    {
        if (_current == "death") return;
        _current = name;
        _player.Play(name);
    }

    public void PlayDeath(Action onDone)
    {
        _onDeathDone = onDone;
        _current = "death";
        _player.Play("death");
    }

    private void OnFinished(StringName anim)
    {
        if (anim == "death") { _onDeathDone?.Invoke(); return; }
        if (_current is "hit" or "attack" or "windup") { _current = ""; Play("idle"); }
    }

    private string SpritePath => _sprite != null ? _sprite.GetPath().ToString() : "";

    private Animation NewAnim(float length, bool loop)
    {
        var a = new Animation { Length = length, LoopMode = loop ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None };
        return a;
    }

    private void TrackVec2(Animation a, string prop, (float t, Vector2 v)[] keys)
    {
        int idx = a.AddTrack(Animation.TrackType.Value);
        a.TrackSetPath(idx, $"{SpritePath}:{prop}");
        foreach (var (t, v) in keys) a.TrackInsertKey(idx, t, v);
    }

    private void TrackColor(Animation a, (float t, Color c)[] keys)
    {
        int idx = a.AddTrack(Animation.TrackType.Value);
        a.TrackSetPath(idx, $"{SpritePath}:self_modulate");
        foreach (var (t, c) in keys) a.TrackInsertKey(idx, t, c);
    }

    private Animation BuildIdle()
    {
        var a = NewAnim(1.2f, loop: true);
        TrackVec2(a, "scale", new[] { (0f, new Vector2(0.22f, 0.22f)), (0.6f, new Vector2(0.23f, 0.21f)), (1.2f, new Vector2(0.22f, 0.22f)) });
        return a;
    }

    private Animation BuildWalk()
    {
        var a = NewAnim(0.5f, loop: true);
        int rot = a.AddTrack(Animation.TrackType.Value);
        a.TrackSetPath(rot, $"{SpritePath}:rotation");
        a.TrackInsertKey(rot, 0f, -0.12f);
        a.TrackInsertKey(rot, 0.25f, 0.12f);
        a.TrackInsertKey(rot, 0.5f, -0.12f);
        return a;
    }

    private Animation BuildWindup()
    {
        var a = NewAnim(0.35f, loop: false);
        TrackVec2(a, "scale", new[] { (0f, new Vector2(0.22f, 0.22f)), (0.35f, new Vector2(0.17f, 0.27f)) });
        TrackColor(a, new[] { (0f, Colors.White), (0.35f, new Color(1.4f, 1.1f, 0.9f)) });
        return a;
    }

    private Animation BuildAttack()
    {
        var a = NewAnim(0.2f, loop: false);
        TrackVec2(a, "scale", new[] { (0f, new Vector2(0.17f, 0.27f)), (0.08f, new Vector2(0.3f, 0.18f)), (0.2f, new Vector2(0.22f, 0.22f)) });
        TrackColor(a, new[] { (0f, new Color(1.4f, 1.1f, 0.9f)), (0.2f, Colors.White) });
        return a;
    }

    private Animation BuildHit()
    {
        var a = NewAnim(0.15f, loop: false);
        TrackColor(a, new[] { (0f, new Color(3f, 3f, 3f)), (0.15f, Colors.White) });
        return a;
    }

    private Animation BuildDeath()
    {
        var a = NewAnim(0.4f, loop: false);
        TrackVec2(a, "scale", new[] { (0f, new Vector2(0.22f, 0.22f)), (0.4f, new Vector2(0.02f, 0.02f)) });
        int mod = a.AddTrack(Animation.TrackType.Value);
        a.TrackSetPath(mod, $"{SpritePath}:modulate");
        a.TrackInsertKey(mod, 0f, Colors.White);
        a.TrackInsertKey(mod, 0.4f, new Color(1f, 1f, 1f, 0f));
        int rot = a.AddTrack(Animation.TrackType.Value);
        a.TrackSetPath(rot, $"{SpritePath}:rotation");
        a.TrackInsertKey(rot, 0f, 0f);
        a.TrackInsertKey(rot, 0.4f, 2.2f);
        return a;
    }
}
