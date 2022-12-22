using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

namespace AIPets.unityenv;

public class utils
{
    public static grpc.Vector3 ConvertUnitVec3(Vector3 vec3)
    {
        return new grpc.Vector3
        {
            X = vec3.x,
            Y = vec3.y,
            Z = vec3.z,
        };
    }

    public static Vector3 ConvertGrpcVec3(grpc.Vector3 vec3)
    {
        return new Vector3
        {
            x = vec3.X,
            y = vec3.Y,
            z = vec3.Z,
        };
    }

    public static grpc.Vector2 ConvertUnitVec2(Vector2 vec3)
    {
        return new grpc.Vector2
        {
            X = vec3.x,
            Y = vec3.y,
        };
    }

    public static Vector2 ConvertGrpcVec2(grpc.Vector2 vec3)
    {
        return new Vector2
        {
            x = vec3.X,
            y = vec3.Y,
        };
    }
}