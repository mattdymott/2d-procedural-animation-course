using System.Runtime.CompilerServices;

// GaitStepper is the gait decision policy, not part of the published surface. The package's
// own tests drive it directly because it is where the stepping rules are worth pinning down.
[assembly: InternalsVisibleTo("Tealeaf.ProceduralAnimation.Dots.Tests.Editor")]
