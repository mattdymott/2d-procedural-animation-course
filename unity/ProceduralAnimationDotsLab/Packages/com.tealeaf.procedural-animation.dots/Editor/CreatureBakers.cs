using System;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Tealeaf.ProceduralAnimation.Dots
{
	/// <summary>
	/// One baker per authoring component. Each owns the components its own feature needs, so the
	/// entity ends up carrying exactly the features that were composed onto the GameObject.
	/// Bakers read sibling authoring components rather than each other's baked output, which is
	/// what keeps them independent of baking order.
	///
	/// Bakers are now nested in their authoring components.
	/// </summary>

	/// <summary>Rest-pose maths the leg and gait bakers must agree on.</summary>
	internal static class CreatureBakerMath
	{
		public static int AttachmentPointIndex(VerletChainAuthoring chain, int authored) =>
			math.clamp(authored, 0, math.max(2, chain.ChainSegmentCount) - 1);

		public static float2 RestFoot(VerletChainAuthoring chain, int attachmentIndex, Vector2 homeOffset)
		{
			var root = new float2(chain.InitialRootPosition.x, chain.InitialRootPosition.y);
			var restLength = math.max(0.001f, chain.RestLength);
			return CreatureLayout.PointPosition(root, restLength, attachmentIndex)
			       + new float2(homeOffset.x, homeOffset.y);
		}
	}
}