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
			OnHitStatus = StatusType.Burn, StatusDuration = 3f };

		CombatResolver.ApplyHit(skill, target);

		Assert.Equal(StatusType.Burn, target.ActiveStatus);
		Assert.Equal(3f, target.StatusTimeLeft);
	}

	[Fact]
	public void ChilledTarget_TakesBonusDamage()
	{
		var target = Target();
		target.ActiveStatus = StatusType.Chill;
		target.StatusTimeLeft = 2f;
		var skill = new ResolvedSkill { Id = "x", Damage = 100f, Shape = SkillShape.SingleTarget };

		CombatResolver.ApplyHit(skill, target);

		// 100 dmg + 25% bonus za chill = 125 → HP 100 - 125 = dead (<=0)
		Assert.True(target.IsDead);
	}

	[Fact]
	public void LethalDamage_MarksDead()
	{
		var target = Target(20f);
		var skill = new ResolvedSkill { Id = "x", Damage = 25f, Shape = SkillShape.SingleTarget };

		CombatResolver.ApplyHit(skill, target);

		Assert.True(target.IsDead);
	}

	[Fact]
	public void MarkedTarget_TakesMultipliedDamage()
	{
		var target = Target();
		target.Marked = true;
		target.MarkTimeLeft = 5f;
		var skill = new ResolvedSkill { Id = "x", Damage = 10f, Shape = SkillShape.Projectile, MarkedMultiplier = 2f };

		CombatResolver.ApplyHit(skill, target);

		// 10 dmg × 2 (marked) = 20 → 100 - 20 = 80
		Assert.Equal(80f, target.Health);
	}

	[Fact]
	public void AppliesMark_MarksTheTarget()
	{
		var target = Target();
		var skill = new ResolvedSkill { Id = "x", Damage = 5f, Shape = SkillShape.Projectile, AppliesMark = true, MarkDuration = 4f };

		CombatResolver.ApplyHit(skill, target);

		Assert.True(target.IsMarked);
		Assert.Equal(4f, target.MarkTimeLeft);
	}
}
