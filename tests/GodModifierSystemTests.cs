using AshenPantheon.Core;
using Xunit;

public class GodModifierSystemTests
{
    private static SkillDefinition Bolt() => new()
    {
        Id = "bolt", BaseDamage = 10f, Cooldown = 0.5f,
        Shape = SkillShape.Projectile,
        Tags = new() { SkillTag.Damage, SkillTag.Projectile }
    };

    [Fact]
    public void NoGod_ResolvesToBaseValues()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), god: null);
        Assert.Equal(10f, resolved.Damage);
        Assert.Equal(SkillShape.Projectile, resolved.Shape);
        Assert.Equal(StatusType.None, resolved.OnHitStatus);
        Assert.False(resolved.Explodes);
        Assert.False(resolved.Pierces);
    }

    [Fact]
    public void Pyr_MakesBoltExplodeAndBurn()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), GodCatalogForTests.Pyr);
        Assert.True(resolved.Explodes);
        Assert.Equal(StatusType.Burn, resolved.OnHitStatus);
    }

    [Fact]
    public void Vael_MakesBoltPierceAndChill()
    {
        var resolved = GodModifierSystem.Resolve(Bolt(), GodCatalogForTests.Vael);
        Assert.True(resolved.Pierces);
        Assert.Equal(StatusType.Chill, resolved.OnHitStatus);
    }
}

/// <summary>Mini-katalog bogów na potrzeby testów logiki (warstwa Godota użyje własnego, patrz GodCatalog).</summary>
internal static class GodCatalogForTests
{
    public static readonly God Pyr = new()
    {
        Name = "Pyr",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Burn; r.StatusDuration = 3f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Explodes = true; },
        }
    };

    public static readonly God Vael = new()
    {
        Name = "Vael",
        Modifiers =
        {
            (def, r) => { if (def.Tags.Contains(SkillTag.Damage)) { r.OnHitStatus = StatusType.Chill; r.StatusDuration = 2f; } },
            (def, r) => { if (def.Tags.Contains(SkillTag.Projectile)) r.Pierces = true; },
        }
    };
}
