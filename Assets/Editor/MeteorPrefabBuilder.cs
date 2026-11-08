#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class MeteorPrefabBuilder
{
    private const string OutputFolderPath = "Assets/03.Prefabs/Combat";
    private const string PrefabName = "MeteorProjectile.prefab";

    [MenuItem("Tools/Combat/Generate MeteorProjectile Prefab")]
    public static void GenerateMeteorPrefab()
    {
        if (!Directory.Exists(OutputFolderPath))
        {
            Directory.CreateDirectory(OutputFolderPath);
        }

        string fullPath = Path.Combine(OutputFolderPath, PrefabName);

        // 1. 루트 게임오브젝트 생성
        GameObject root = new GameObject("MeteorProjectile");

        // 2. 물리 및 투사체 컴포넌트 바인딩
        MeteorProjectile projectileComp = root.AddComponent<MeteorProjectile>();
        SphereCollider col = root.AddComponent<SphereCollider>();
        col.radius = 0.6f;
        col.isTrigger = true;

        // 리플렉션/SerializedObject를 통해 기본값 안전 주입
        SerializedObject so = new SerializedObject(projectileComp);
        so.FindProperty("_fallSpeed").floatValue = 28.0f;
        so.FindProperty("_explosionRadius").floatValue = 4.5f;
        so.FindProperty("_targetLayer").intValue = LayerMask.GetMask("Player");
        so.ApplyModifiedProperties();

        // 3. 운석 코어(Core Mesh) 생성
        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "MeteorCore";
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.8f;

        // 기본 충돌체 제거 (루트의 SphereCollider 사용)
        Object.DestroyImmediate(core.GetComponent<Collider>());

        // 코어 머티리얼 (어두운 암석 + 붉은 발광)
        Renderer coreRenderer = core.GetComponent<Renderer>();
        Material coreMat = new Material(Shader.Find("Standard"));
        coreMat.color = new Color(0.15f, 0.08f, 0.05f);
        coreMat.EnableKeyword("_EMISSION");
        coreMat.SetColor("_EmissionColor", new Color(1.0f, 0.3f, 0.05f) * 2.5f);
        coreRenderer.sharedMaterial = coreMat;

        string matPath = Path.Combine(OutputFolderPath, "M_MeteorCore.mat");
        AssetDatabase.CreateAsset(coreMat, matPath);

        // 4. 화염 트레일 파티클 시스템(Fire Trail) 생성
        GameObject vfxChild = new GameObject("FireTrail_VFX");
        vfxChild.transform.SetParent(root.transform, false);

        ParticleSystem ps = vfxChild.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psRenderer = vfxChild.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 3.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 꼬리가 월드에 남도록 설정

        // 불꽃 그라데이션 (노랑 -> 주황 -> 암적색 -> 투명)
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.2f), 0.0f),
                new GradientColorKey(new Color(1f, 0.35f, 0.0f), 0.4f),
                new GradientColorKey(new Color(0.3f, 0.05f, 0.0f), 1.0f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        main.startColor = new ParticleSystem.MinMaxGradient(grad);

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.3f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 기본 파티클 머티리얼 적용
        Material particleMat = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        if (particleMat != null)
        {
            psRenderer.sharedMaterial = particleMat;
        }

        // 5. 프리팹 에셋으로 저장
        PrefabUtility.SaveAsPrefabAsset(root, fullPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=#00FF00>[MeteorPrefabBuilder]</color> 프리팹 생성 완료: {fullPath}");
        EditorUtility.DisplayDialog("완료", $"MeteorProjectile 프리팹이 성공적으로 생성되었습니다.\n경로: {fullPath}\n\nAddressables 창에서 Key를 'MeteorProjectile'로 등록하세요.", "확인");
    }
}
#endif