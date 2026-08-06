using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class SupportPoseMath
    {
        public static float2 TransformPoint(in SupportPose pose, float2 localPoint)
        {
            var sine = math.sin(pose.Rotation);
            var cosine = math.cos(pose.Rotation);
            return pose.Position + new float2(
                cosine * localPoint.x - sine * localPoint.y,
                sine * localPoint.x + cosine * localPoint.y);
        }

        public static float2 InverseTransformPoint(in SupportPose pose, float2 worldPoint)
        {
            var offset = worldPoint - pose.Position;
            var sine = math.sin(pose.Rotation);
            var cosine = math.cos(pose.Rotation);
            return new float2(
                cosine * offset.x + sine * offset.y,
                -sine * offset.x + cosine * offset.y);
        }

        public static float2 TransformDirection(in SupportPose pose, float2 localDirection)
        {
            var sine = math.sin(pose.Rotation);
            var cosine = math.cos(pose.Rotation);
            return new float2(
                cosine * localDirection.x - sine * localDirection.y,
                sine * localDirection.x + cosine * localDirection.y);
        }
    }
}
