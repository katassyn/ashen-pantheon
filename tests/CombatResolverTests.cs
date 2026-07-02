using System.Linq;
using AshenPantheon.Core;
using Xunit;

public class CombatResolverTests
{
	private static Combatant Target(float hp = 100f) => new() { MaxHealth = hp, Health = hp };

	[Fact]
	public void BasicHit_SubtractsDamage()
	{
		var target = Target();
		var skill = new ResolvedSkill { Id = "x", Damage = 30f, Shape = SkillShape.SingleTarget };
		CombatResolver.ApplyHit(skill, target);
		Assert.Equal(70f, target.Health);
	}

	[Fact]
	public void OnHitStatus_IsAppliedToTarget()
	{
		var target = Target();
		var skill = new ResolvedSkill { Id = "x", Damage = 10f, Shape = SkillShape.Projectile,
			OnHitStatus = StatusType.Burn, StatusDuration = 3f, StatusDps = 8f };
		CombatResolver.ApplyHit(skill, target);
		Assert.True(target.Has(StatusType.Burn));
		Assert.Equal(8f, target.Statuses.Single(s => s.Type == StatusType.Burn).Dps);
	}

	[Fact]
	public void MultiStatus_Coexists_BurnAndChill()
	{
		var target = Target();
		CombatResolver.ApplyHit(new ResolvedSkill { Id = "a", Damage = 1f, Shape = SkillShape.Projectile,
			OnHitStatus = StatusType.Burn, StatusDuration = 3f, StatusDps = 8f }, target);
		CombatResolver.ApplyHit(new ResolvedSkill { Id = "b", Damage = 1f, Shape = SkillShape.Nova,
			OnHitStatus = StatusType.Chill, StatusDuration = 2f }, target);

		Assert.True(target.Has(StatusType.Burn));  // chill NIE nadpisał burna
		Assert.True(target.Has(StatusType.Chill));
	}

	[Fact]
	public void SameStatus_RefreshesAndTakesStrongerDps()
	{
		var target = Target();
		target.ApplyStatus(StatusType.Poison, 1f, 4f);
		target.ApplyStatus(StatusType.Poison, 3f, 6f);
		var poison = target.Statuses.Single();
		Assert.Equal(3f, poison.TimeLeft);
		Assert.Equal(6f, poison.Dps);
	}

	[Fact]
	public void Tick_DealsDotDamage_AndExpires()
	{
		var target = Target();
		target.ApplyStatus(StatusType.Burn, 1f, 10f);
		target.Tick(0.5f);
		Assert.Equal(95f, target.Health, 1);
		bool changed = target.Tick(0.6f); // wygasa
		Assert.True(changed);
		Assert.Empty(target.Statuses);
	}

	[Fact]
	public void ChilledTarget_TakesBonusDamage()
	{
		var target = Target();
		target.ApplyStatus(StatusType.Chill, 2f, 0f);
		var skill = new ResolvedSkill { Id = "x", Damage = 100f, Shape = SkillShape.SingleTarget };
		CombatResolver.ApplyHit(skill, target);
		Assert.True(target.IsDead); // 100 × 1.25 = 125
	}

	[Fact]
	public void MarkedTarget_TakesMultipliedDamage()
	{
		var target = Target();
		target.Marked = true;
		target.MarkTimeLeft = 5f;
		var skill = new ResolvedSkill { Id = "x", Damage = 10f, Shape = SkillShape.Projectile, MarkedMultiplier = 2f };
		CombatResolver.ApplyHit(skill, target);
		Assert.Equal(80f, target.Health);
	}

	[Fact]
	public void AppliesMark_MarksTheTarget()
	{
		var target = Target();
		var skill = new ResolvedSkill { Id = "x", Damage = 5f, Shape = SkillShape.Projectile, AppliesMark = true, MarkDuration = 4f };
		CombatResolver.ApplyHit(skill, target);
		Assert.True(target.IsMarked);
	}

	[Fact]
	public void EnemyArmour_ReducesPhysicalOnly()
	{
		var armored = Target();
		armored.Armour = 500f;
		var phys = new ResolvedSkill { Id = "p", Damage = 50f, Shape = SkillShape.Projectile, DamageType = DamageType.Physical };
		CombatResolver.ApplyHit(phys, armored);
		float physTaken = 100f - armored.Health;

		var armored2 = Target();
		armored2.Armour = 500f;
		var fire = new ResolvedSkill { Id = "f", Damage = 50f, Shape = SkillShape.Projectile, DamageType = DamageType.Fire };
		CombatResolver.ApplyHit(fire, armored2);
		float fireTaken = 100f - armored2.Health;

		Assert.True(physTaken < 50f);      // armour zredukował fizyczne
		Assert.Equal(50f, fireTaken, 1);   // ogień ignoruje armour (brak resa)
	}

	[Fact]
	public void EnemyResist_ReducesElemental()
	{
		var target = Target();
		target.ResFire = 50f;
		var fire = new ResolvedSkill { Id = "f", Damage = 40f, Shape = SkillShape.Projectile, DamageType = DamageType.Fire };
		CombatResolver.ApplyHit(fire, target);
		Assert.Equal(80f, target.Health, 1); // 40 × 0.5
	}
}

public class PlayerDefenseTests
{
	private static CharacterSheet Sheet(float es = 50f, float regen = 0f) => new()
	{
		BaseLife = 100f, BaseEnergyShield = es, LifeRegen = regen,
	};

	[Fact]
	public void EnergyShield_AbsorbsBeforeHp()
	{
		var d = new PlayerDefense();
		d.ResetFull(Sheet());
		d.Absorb(30f, DamageType.Physical);
		Assert.Equal(20f, d.EnergyShield);
		Assert.Equal(100f, d.Health);
	}

	[Fact]
	public void Overflow_SpillsToHp()
	{
		var d = new PlayerDefense();
		d.ResetFull(Sheet());
		d.Absorb(70f, DamageType.Physical);
		Assert.Equal(0f, d.EnergyShield);
		Assert.Equal(80f, d.Health);
	}

	[Fact]
	public void Chaos_BypassesEnergyShield()
	{
		var d = new PlayerDefense();
		d.ResetFull(Sheet());
		d.Absorb(30f, DamageType.Chaos);
		Assert.Equal(50f, d.EnergyShield); // ES nietknięty
		Assert.Equal(70f, d.Health);
	}

	[Fact]
	public void Es_RechargesAfterDelay()
	{
		var sheet = Sheet();
		var d = new PlayerDefense();
		d.ResetFull(sheet);
		d.Absorb(30f, DamageType.Physical);
		d.Tick(2.9f, sheet);
		Assert.Equal(20f, d.EnergyShield, 1); // delay 3 s jeszcze trwa
		d.Tick(2f, sheet);                     // po delay: ~1.9 s ładowania
		Assert.True(d.EnergyShield > 20f);
	}

	[Fact]
	public void LifeRegen_HealsOverTime()
	{
		var sheet = Sheet(es: 0f, regen: 5f);
		var d = new PlayerDefense();
		d.ResetFull(sheet);
		d.Absorb(50f, DamageType.Physical);
		d.Tick(4f, sheet);
		Assert.Equal(70f, d.Health, 1); // 50 + 5×4
	}
}
