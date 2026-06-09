using System.Collections;
using UnityEngine;

/// <summary>
/// Slime Queen 소멸 연출용 초록 사각 파티클을 재생합니다.
/// 설정 컴포넌트는 보스에 붙고, 실제 재생 오브젝트는 보스 제거 뒤에도 남도록 분리 생성합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class SlimeQueenVanishParticleEffect : MonoBehaviour
{
    private const int DefaultParticleCapacity = 8;
    private const float FullCircleRadians = Mathf.PI * 2f;

    [SerializeField, Min(1)] private int particleCount = 4;
    [SerializeField, Min(0.01f)] private float spreadSeconds = 0.35f;
    [SerializeField, Min(0f)] private float holdAfterSpreadSeconds = 1f;
    [SerializeField, Min(0f)] private float fadeOutSeconds = 0.12f;
    [SerializeField, Min(0.01f)] private float squareSize = 0.18f;
    [SerializeField, Min(0f)] private float minSpreadDistance = 0.65f;
    [SerializeField, Min(0f)] private float maxSpreadDistance = 1.15f;
    [SerializeField] private Color squareColor = new Color(0.5764705882352941f, 0.9490196078431373f, 0.6862745098039216f, 0.95f);
    [SerializeField] private string fallbackSortingLayerName = "Entity";
    [SerializeField] private int sortingOrderOffset = 3;
    [SerializeField, Min(0f)] private float destroyPaddingSeconds = 0.1f;

    private static Mesh runtimeSquareMesh;
    private static Material runtimeSquareMaterial;
    private static Texture2D runtimeSquareTexture;

    private ParticleSystem particleSystemRef;
    private ParticleSystem.Particle[] particles = new ParticleSystem.Particle[DefaultParticleCapacity];
    private Vector3[] directions = new Vector3[DefaultParticleCapacity];
    private float[] distances = new float[DefaultParticleCapacity];
    private Coroutine playbackRoutine;

    private float TotalVisibleSeconds => Mathf.Max(0.01f, spreadSeconds) + Mathf.Max(0f, holdAfterSpreadSeconds);

    public void SpawnOneShot(Vector3 worldPosition, SpriteRenderer sortingSource)
    {
        GameObject effectObject = new GameObject("SlimeQueenVanishParticleEffect");
        effectObject.hideFlags = HideFlags.DontSave;
        effectObject.transform.position = worldPosition;

        SlimeQueenVanishParticleEffect effect = effectObject.AddComponent<SlimeQueenVanishParticleEffect>();
        CopySettingsTo(effect);
        effect.PlayDetached(worldPosition, sortingSource);
    }

    private void CopySettingsTo(SlimeQueenVanishParticleEffect target)
    {
        if (target == null)
            return;

        target.particleCount = particleCount;
        target.spreadSeconds = spreadSeconds;
        target.holdAfterSpreadSeconds = holdAfterSpreadSeconds;
        target.fadeOutSeconds = fadeOutSeconds;
        target.squareSize = squareSize;
        target.minSpreadDistance = minSpreadDistance;
        target.maxSpreadDistance = maxSpreadDistance;
        target.squareColor = squareColor;
        target.fallbackSortingLayerName = fallbackSortingLayerName;
        target.sortingOrderOffset = sortingOrderOffset;
        target.destroyPaddingSeconds = destroyPaddingSeconds;
    }

    private void PlayDetached(Vector3 worldPosition, SpriteRenderer sortingSource)
    {
        EnsureRuntimeBuffers();
        ConfigureParticleSystem(sortingSource);
        EmitParticles(worldPosition);

        if (playbackRoutine != null)
            StopCoroutine(playbackRoutine);

        playbackRoutine = StartCoroutine(DriveParticles(worldPosition));
    }

    private void EnsureRuntimeBuffers()
    {
        int capacity = Mathf.Max(1, particleCount);
        if (particles == null || particles.Length < capacity)
            particles = new ParticleSystem.Particle[capacity];
        if (directions == null || directions.Length < capacity)
            directions = new Vector3[capacity];
        if (distances == null || distances.Length < capacity)
            distances = new float[capacity];
    }

    private void ConfigureParticleSystem(SpriteRenderer sortingSource)
    {
        if (particleSystemRef == null)
            particleSystemRef = GetComponent<ParticleSystem>();
        if (particleSystemRef == null)
            particleSystemRef = gameObject.AddComponent<ParticleSystem>();

        particleSystemRef.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = particleSystemRef.main;
        main.duration = TotalVisibleSeconds;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = TotalVisibleSeconds;
        main.startSpeed = 0f;
        main.startSize = squareSize;
        main.startColor = squareColor;
        main.maxParticles = Mathf.Max(1, particleCount);

        ParticleSystem.EmissionModule emission = particleSystemRef.emission;
        emission.enabled = false;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = particleSystemRef.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = particleSystemRef.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
            particleRenderer.mesh = GetRuntimeSquareMesh();
            Material material = GetRuntimeSquareMaterial();
            if (material != null)
                particleRenderer.material = material;

            if (sortingSource != null)
            {
                particleRenderer.sortingLayerID = sortingSource.sortingLayerID;
                particleRenderer.sortingOrder = sortingSource.sortingOrder + sortingOrderOffset;
            }
            else
            {
                particleRenderer.sortingLayerName = fallbackSortingLayerName;
                particleRenderer.sortingOrder = sortingOrderOffset;
            }
        }
    }

    private void EmitParticles(Vector3 worldPosition)
    {
        particleSystemRef.Play(withChildren: false);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = worldPosition,
            velocity = Vector3.zero,
            startLifetime = TotalVisibleSeconds,
            startSize = squareSize,
            startColor = squareColor
        };

        int count = Mathf.Max(1, particleCount);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, FullCircleRadians);
            directions[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            distances[i] = Random.Range(Mathf.Min(minSpreadDistance, maxSpreadDistance), Mathf.Max(minSpreadDistance, maxSpreadDistance));
            particleSystemRef.Emit(emitParams, 1);
        }
    }

    private IEnumerator DriveParticles(Vector3 origin)
    {
        float totalVisibleSeconds = TotalVisibleSeconds;
        float elapsed = 0f;

        while (elapsed < totalVisibleSeconds)
        {
            ApplyParticleFrame(origin, elapsed, totalVisibleSeconds);
            elapsed += Time.deltaTime;
            yield return null;
        }

        particleSystemRef.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        playbackRoutine = null;
        Destroy(gameObject, destroyPaddingSeconds);
    }

    private void ApplyParticleFrame(Vector3 origin, float elapsed, float totalVisibleSeconds)
    {
        int liveCount = particleSystemRef.GetParticles(particles);
        int updateCount = Mathf.Min(liveCount, Mathf.Max(1, particleCount));
        float spreadT = spreadSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / spreadSeconds);
        float easedSpreadT = 1f - (1f - spreadT) * (1f - spreadT);
        float alpha = ResolveAlpha(elapsed, totalVisibleSeconds);

        Color frameColor = squareColor;
        frameColor.a *= alpha;

        for (int i = 0; i < updateCount; i++)
        {
            particles[i].position = origin + directions[i] * distances[i] * easedSpreadT;
            particles[i].startLifetime = totalVisibleSeconds;
            particles[i].remainingLifetime = Mathf.Max(0.01f, totalVisibleSeconds - elapsed);
            particles[i].startSize = squareSize;
            particles[i].startColor = frameColor;
        }

        particleSystemRef.SetParticles(particles, liveCount);
    }

    private float ResolveAlpha(float elapsed, float totalVisibleSeconds)
    {
        float fadeSeconds = Mathf.Max(0f, fadeOutSeconds);
        if (fadeSeconds <= 0f)
            return 1f;

        float fadeStart = Mathf.Max(0f, totalVisibleSeconds - fadeSeconds);
        if (elapsed <= fadeStart)
            return 1f;

        return Mathf.Clamp01((totalVisibleSeconds - elapsed) / fadeSeconds);
    }

    private void OnDisable()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
    }

    private static Mesh GetRuntimeSquareMesh()
    {
        if (runtimeSquareMesh != null)
            return runtimeSquareMesh;

        Mesh mesh = new Mesh
        {
            name = "Runtime_SlimeQueenVanishSquare",
            hideFlags = HideFlags.DontSave
        };

        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 1f)
        };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();

        runtimeSquareMesh = mesh;
        return runtimeSquareMesh;
    }

    private static Material GetRuntimeSquareMaterial()
    {
        if (runtimeSquareMaterial != null)
            return runtimeSquareMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            name = "Runtime_SlimeQueenVanishSquareMaterial",
            hideFlags = HideFlags.DontSave,
            mainTexture = GetRuntimeSquareTexture()
        };

        runtimeSquareMaterial = material;
        return runtimeSquareMaterial;
    }

    private static Texture2D GetRuntimeSquareTexture()
    {
        if (runtimeSquareTexture != null)
            return runtimeSquareTexture;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            name = "Runtime_SlimeQueenVanishSquareTexture",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        texture.SetPixel(0, 0, Color.white);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        runtimeSquareTexture = texture;
        return runtimeSquareTexture;
    }
}
