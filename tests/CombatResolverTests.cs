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
}
