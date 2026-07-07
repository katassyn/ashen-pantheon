using Godot;
using AshenPantheon.Core;

/// <summary>Baza wrogów. Multiplayer: prawdziwa logika (AI/HP/DoT) żyje TYLKO u hosta;
/// na klientach wróg jest puppetem (pozycja/HP/status z sieci). Loot rolowany per-gracz.</summary>
public abstract partial class EnemyBase : CharacterBody2D, IHittable
{
    [Export] public float MaxHealth = 60f;
    [Export] public float ContactDamage = 12f;
    [Export] public float ContactCooldown = 0.8f;
    [Export] public float ContactRange = 42f;
    [Export] public float HpBarWidth = 40f;
    [Export] public float HpBarY = -34f;

    public float HpMult = 1f;
    public float DmgMult = 1f;
    public float XpMult = 1f;
    public long XpReward = 12;
    /// <summary>Endgame: wymuszony poziom itemów dropu (0 = poziom potwora).</summary>
    public int LootIlvlOverride;

    public long NetId;
    public bool Puppet;

    /// <summary>Mapy świata: zasięg aggro (arena = praktycznie nieskończony) i punkt domowy packa.</summary>
    public float AggroRange = 100000f;
    public Vector2 HomePos;

    /// <summary>Id do replikacji (klient odtwarza puppet z bestiariusza).</summary>
    public virtual string ReplicationId => "husk";
    /// <summary>Nazwa do UI (death recap, boss bar) — Monster nadpisuje nazwą z bestiariusza.</summary>
    public virtual string DisplayName => ReplicationId;
    /// <summary>Tabela lootu (data-driven, patrz data/loot/).</summary>
    protected virtual string LootTableId => "common";
    /// <summary>Przedmiot questowy (cel Collect) + szansa — z definicji potwora.</summary>
    protected virtual string QuestItemId => "";
    protected virtual float QuestItemChance => 0f;
    /// <summary>Pity: gwarantowany drop przy N-tym zabiciu (0 = tylko szansa).</summary>
    protected virtual int QuestItemPityAt => 0;
    /// <summary>Legendary essence upgrade (mini-bossy/boss Q). Pusty = brak.</summary>
    protected virtual string LegendaryEssenceId => "";
    protected virtual float LegendaryEssenceChance => 0f;
    protected virtual int LegendaryEssenceCount => 1;
    /// <summary>Poziom potwora (= poziom dropu / skala affixów).</summary>
    protected virtual int MonsterLevel => 1;

    protected Combatant Combatant;
    protected Sprite2D Sprite;
    protected EnemyAnimator Animator;
    protected PlayerController CurrentTarget;

    private float _contactCd;
    private bool _dying;
    private Vector2 _netPos;
    private bool _netMoving;

    protected bool IsChilled => Combatant.IsChilled;
    public bool IsMarked => Combatant != null && Combatant.IsMarked;
    public float HpFrac => Combatant == null ? 1f : Mathf.Clamp(Combatant.Health / Combatant.MaxHealth, 0f, 1f);
    public int StatusMask => Combatant?.StatusMask() ?? 0;
    public bool Moving => Velocity.LengthSquared() > 1f || _netMoving;
    protected abstract Color BaseTint { get; }

    private static readonly LootGenerator Loot = new();
    private static readonly System.Random LootRng = new();

    public override void _Ready()
    {
        Combatant = new Combatant { MaxHealth = MaxHealth * HpMult, Health = MaxHealth * HpMult };
        ContactDamage *= DmgMult;
        Sprite = GetNodeOrNull<Sprite2D>("Sprite2D");
        Animator = GetNodeOrNull<EnemyAnimator>("Animator");
        AddToGroup("hittable");
        AddToGroup("enemies");
        _netPos = GlobalPosition;
        if (HomePos == Vector2.Zero) HomePos = GlobalPosition;
        UpdateTint();
        QueueRedraw();

        if (!Puppet && Net.Online) Net.RegisterEnemy(this);
    }

    /// <summary>Najbliższy żywy gracz (co-op: wrogowie wybierają cel spośród wszystkich).</summary>
    protected PlayerController NearestPlayer()
    {
        PlayerController best = null;
        float bd = float.MaxValue;
        foreach (Node n in GetTree().GetNodesInGroup("players"))
            if (n is PlayerController p && !p.Dead)
            {
                float d = GlobalPosition.DistanceTo(p.GlobalPosition);
                if (d < bd) { bd = d; best = p; }
            }
        return best;
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        if (_dying) { Velocity = Vector2.Zero; return; }

        if (Puppet)
        {
            GlobalPosition = GlobalPosition.Lerp(_netPos, Mathf.Min(1f, 14f * dt));
            Animator?.Play(_netMoving ? "walk" : "idle");
            return;
        }

        if (_contactCd > 0f) _contactCd -= dt;

        // multi-status: tyknięcie w core (DoT-y, mark, stun) — koegzystencja Burn+Chill+Bleed
        bool statusChanged = Combatant.Tick(dt);
        if (statusChanged) UpdateTint();
        if (Combatant.Statuses.Count > 0 || statusChanged) QueueRedraw();
        if (Combatant.IsDead) { Die(); return; }

        if (Combatant.IsStunned)
        {
            Velocity = Vector2.Zero;
            Animator?.Play("idle");
            if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0) Net.SyncEnemy(this);
            return;
        }

        CurrentTarget = NearestPlayer();

        // leash pack: bez gracza w zasięgu aggro → wracaj do domu / stój
        if (CurrentTarget != null && GlobalPosition.DistanceTo(CurrentTarget.GlobalPosition) > AggroRange)
            CurrentTarget = null;
        if (CurrentTarget == null)
        {
            Vector2 toHome = HomePos - GlobalPosition;
            if (toHome.Length() > 30f)
            {
                Velocity = toHome.Normalized() * 60f;
                MoveAndSlide();
                Animator?.Play("walk");
            }
            else
            {
                Velocity = Vector2.Zero;
                Animator?.Play("idle");
            }
            if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0) Net.SyncEnemy(this);
            return;
        }

        {
            Vector2 toPlayer = CurrentTarget.GlobalPosition - GlobalPosition;
            Behavior(dt, toPlayer, toPlayer.Length());

            if (ContactRange > 0f && toPlayer.Length() <= ContactRange && _contactCd <= 0f)
            {
                _contactCd = ContactCooldown;
                Net.DamagePlayer(CurrentTarget, ContactDamage, DamageType.Physical, DisplayName);
            }
        }

        if (Net.Online && Engine.GetPhysicsFrames() % 6 == 0) Net.SyncEnemy(this);
    }

    protected abstract void Behavior(float dt, Vector2 toPlayer, float dist);

    public static System.Collections.Generic.IEnumerable<EnemyBase> All(SceneTree tree)
    {
        foreach (Node n in tree.GetNodesInGroup("enemies"))
            if (n is EnemyBase e && !e._dying) yield return e;
    }

    /// <summary>Damage-gate (questy Q): "questId/objectiveId" — mob odporny, dopóki gracz nie zaliczy celu.</summary>
    protected virtual string GateObjective => "";

    private bool GateBlocked()
    {
        string gate = GateObjective;
        if (gate.Length == 0) return false;
        var parts = gate.Split('/');
        if (parts.Length != 2) return false;
        var q = QuestCatalog.Find(parts[0]);
        if (q == null) return false;
        foreach (var o in q.Objectives)
            if (o.Id == parts[1])
                return !GameState.Quests.IsActive(q.Id) || !GameState.Quests.ObjectiveDone(q, o);
        return false;
    }

    public void ReceiveHit(ResolvedSkill skill)
    {
        if (_dying) return;
        if (GateBlocked())
        {
            FloatingText.Spawn(GetParent(), GlobalPosition, "IMMUNE", new Color(0.75f, 0.7f, 1f), 16);
            return; // warunek questa niespełniony — boss nietykalny (kanon Q3/Q9/Q10)
        }
        if (Puppet)
        {
            // klient: tylko wizualny feedback — HP/statusy przyjdą z serwera
            Animator?.Flash("hit");
            return;
        }

        // pipeline v2: rzut na trafienie (celność gracza vs unik celu)
        float before = Combatant.Health;
        bool hit = CombatResolver.ApplyHitRolled(skill, Combatant, GD.Randf());
        if (!hit)
        {
            FloatingText.Spawn(GetParent(), GlobalPosition, "dodge", new Color(0.7f, 0.7f, 0.75f), 12);
            return; // unik — brak obrażeń i lifestealu
        }

        float dealt = before - Combatant.Health;
        if (dealt > 0.5f)
            FloatingText.Spawn(GetParent(), GlobalPosition, $"{dealt:0}",
                skill.IsCrit ? new Color(1f, 0.85f, 0.2f) : Colors.White, skill.IsCrit ? 20 : 14);

        if (skill.HealOnHit > 0f)
            Net.HealCaster(skill.CasterPeer, skill.HealOnHit);
        UpdateTint();
        QueueRedraw();
        if (Combatant.IsDead) Die();
        else Animator?.Flash("hit");
    }

    /// <summary>Puppet: stan z serwera (statusy jako bitmaska — multi-status).</summary>
    public void ApplyNetState(Vector2 pos, float hpFrac, int statusMask, bool marked, bool moving)
    {
        _netPos = pos;
        _netMoving = moving;
        // klient: przybliżone liczby obrażeń z delty HP (dokładne żyją u hosta)
        float delta = (Combatant.Health / Combatant.MaxHealth - hpFrac) * Combatant.MaxHealth;
        if (delta > 1f)
            FloatingText.Spawn(GetParent(), GlobalPosition, $"{delta:0}", Colors.White, 14);
        Combatant.Health = hpFrac * Combatant.MaxHealth;

        if (statusMask != Combatant.StatusMask())
        {
            Combatant.Statuses.Clear();
            foreach (StatusType t in System.Enum.GetValues<StatusType>())
                if (t != StatusType.None && (statusMask & (1 << (int)t)) != 0)
                    Combatant.ApplyStatus(t, 1f, 0f); // wizualnie; realne czasy żyją u hosta
            UpdateTint();
        }

        Combatant.Marked = marked;
        Combatant.MarkTimeLeft = marked ? 1f : 0f;
        QueueRedraw();
    }

    protected virtual bool DropsLoot => true;

    protected void Die()
    {
        if (_dying) return;
        _dying = true;
        RemoveFromGroup("enemies");
        RemoveFromGroup("hittable");

        Net.GrantXpAll(XpReward);
        Net.BroadcastQuestKill(ReplicationId); // cele Kill u wszystkich graczy (party-share)

        // cel Collect: przedmiot questowy z szansą + PITY (gwarancja najpóźniej przy N-tym zabiciu)
        if (QuestItemId.Length > 0)
        {
            GameState.QuestItemPity.TryGetValue(QuestItemId, out int misses);
            bool drop = GD.Randf() < QuestItemChance || (QuestItemPityAt > 0 && misses + 1 >= QuestItemPityAt);
            if (drop)
            {
                GameState.QuestItemPity[QuestItemId] = 0;
                Net.BroadcastQuestCollect(QuestItemId);
            }
            else GameState.QuestItemPity[QuestItemId] = misses + 1;
        }

        // loot z TABELI (data/loot/*.json), rolowany OSOBNO per gracz (instancjonowany)
        if (DropsLoot)
            foreach (int peer in Net.AllPeers())
            {
                var drops = LootTables.Roll(LootTableId, LootRng, Loot, LootIlvlOverride > 0 ? LootIlvlOverride : MonsterLevel);
                int i = 0;
                foreach (var drop in drops)
                {
                    var pos = GlobalPosition + new Vector2(26f * i, 14f * (i % 2));
                    i++;
                    if (drop.Item != null) Net.GivePickup(peer, drop.Item, pos);
                    else if (drop.Gold > 0) Net.GiveGold(peer, drop.Gold, pos);
                    else if (drop.Ingredient.Length > 0) Net.GiveIngredient(peer, drop.Ingredient, drop.IngredientCount, pos);
                }
                // legendary essence upgrade (kanon: mini-bossy + boss Q, każdy Q swoje) — poza tabelą, per gracz
                if (LegendaryEssenceId.Length > 0 && GD.Randf() < LegendaryEssenceChance)
                    Net.GiveIngredient(peer, LegendaryEssenceId, LegendaryEssenceCount,
                        GlobalPosition + new Vector2(0, -30));
            }

        if (Net.Online) Net.DespawnEnemy(this, died: true);
        PlayDeathAndFree();
    }

    /// <summary>Puppet: śmierć zreplikowana z serwera.</summary>
    public void RemoteDie()
    {
        if (_dying) return;
        _dying = true;
        RemoveFromGroup("enemies");
        RemoveFromGroup("hittable");
        PlayDeathAndFree();
    }

    private void PlayDeathAndFree()
    {
        var shape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        if (shape != null) shape.SetDeferred("disabled", true);
        if (Animator != null) Animator.PlayDeath(() => QueueFree());
        else QueueFree();
    }

    protected void UpdateTint()
    {
        if (Sprite == null) return;
        // multi-status: tint od najbardziej "widocznego" aktywnego statusu
        Sprite.Modulate =
            Combatant.Has(StatusType.Bleed) ? new Color(0.85f, 0.15f, 0.25f) :
            Combatant.Has(StatusType.Burn) ? new Color(1f, 0.4f, 0.2f) :
            Combatant.Has(StatusType.Poison) ? new Color(0.5f, 0.9f, 0.35f) :
            Combatant.Has(StatusType.Chill) ? new Color(0.4f, 0.7f, 1f) :
            BaseTint;
    }

    // ikony debuffów: kolor kropki = kolor tinta danego statusu (spójna legenda)
    private static readonly (StatusType Type, Color Col)[] StatusDots =
    {
        (StatusType.Burn, new Color(1f, 0.4f, 0.2f)),
        (StatusType.Chill, new Color(0.4f, 0.7f, 1f)),
        (StatusType.Poison, new Color(0.5f, 0.9f, 0.35f)),
        (StatusType.Bleed, new Color(0.85f, 0.15f, 0.25f)),
    };

    public override void _Draw()
    {
        if (Combatant == null) return;
        float frac = HpFrac;
        var size = new Vector2(HpBarWidth, 6f);
        var pos = new Vector2(-HpBarWidth / 2f, HpBarY);
        DrawRect(new Rect2(pos, size), new Color(0f, 0f, 0f, 0.7f));
        DrawRect(new Rect2(pos, new Vector2(size.X * frac, size.Y)), new Color(0.9f, 0.2f, 0.2f));

        // kropki aktywnych debuffów nad paskiem (host: realne statusy, puppet: bitmaska z sync)
        float dotX = -HpBarWidth / 2f + 4f;
        float dotY = HpBarY - 13f;
        foreach (var (type, col) in StatusDots)
        {
            if (!Combatant.Has(type)) continue;
            DrawCircle(new Vector2(dotX, dotY), 3.5f, col);
            dotX += 9f;
        }

        if (Combatant.IsMarked)
        {
            float y = HpBarY - 8f;
            var tri = new Vector2[] { new(-6f, y), new(6f, y), new(0f, y + 8f) };
            DrawColoredPolygon(tri, new Color(1f, 0.85f, 0.2f));
        }
    }
}
