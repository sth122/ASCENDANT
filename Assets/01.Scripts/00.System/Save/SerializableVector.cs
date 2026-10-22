/// <summary>
/// Unity Vector3를 JSON으로 안전하게 직렬화하기 위한 래퍼 구조체
/// </summary>
[System.Serializable]
public struct SerializableVector3
{
    public float X;
    public float Y;
    public float Z;

    public SerializableVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public UnityEngine.Vector3 ToVector3() => new UnityEngine.Vector3(X, Y, Z);
    public static SerializableVector3 FromVector3(UnityEngine.Vector3 vector) => new SerializableVector3(vector.x, vector.y, vector.z);
}