using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LaneRushGame : MonoBehaviour
{
    private const int RoadCount = 16;
    private const int TrafficCount = 14;
    private const int TreeCount = 28;
    private const int BuildingCount = 18;
    private const int ShrubCount = 20;
    private static readonly float[] Lanes = { -5.1f, 0f, 5.1f };

    private readonly List<Transform> roads = new();
    private readonly List<Transform> laneMarks = new();
    private readonly List<Transform> trees = new();
    private readonly List<Transform> buildings = new();
    private readonly List<Transform> verges = new();
    private readonly List<Transform> shrubs = new();
    private readonly List<TrafficCar> traffic = new();
    private readonly string[] trafficNames = {
        "traffic-normal-car-1190", "traffic-suv-1402", "traffic-taxi-1413",
        "traffic-sports-car-1370", "traffic-normal-car-1191", "traffic-sports-car-1371"
    };
    private readonly string[] treeNames = {
        "tree-palm-1203", "tree-common-836", "tree-willow-1481", "tree-willow-1483",
        "tree-common-835", "tree-common-838", "tree-blendertimer-11", "tree-common-839", "tree-common-837"
    };
    private readonly string[] buildingNames = {
        "building-large-741", "building-large-743", "building-big-745",
        "building-small-746", "building-small-742", "building-747"
    };

    private Transform player;
    private Camera gameCamera;
    private AudioSource engineAudio;
    private AudioSource effectsAudio;
    private AudioSource boostAudio;
    private AudioClip hornClip;
    private AudioClip crashClip;
    private AudioClip boostClip;
    private readonly Transform[] boostFlames = new Transform[2];
    private Material roadMaterial;
    private Material grassMaterial;
    private Material whiteMaterial;
    private Material curbMaterial;
    private Material amberMaterial;
    private Material grassLightMaterial;
    private Material grassDarkMaterial;
    private Material contactShadowMaterial;
    private Material boostMaterial;
    private float targetX;
    private float lateralVelocity;
    private float elapsed;
    private float distance;
    private float speed = 120f;
    private float nitro = 100f;
    private float combo = 1f;
    private float spawnAccumulator;
    private int closeCalls;
    private int best;
    private bool running;
    private bool paused;
    private bool crashed;
    private bool boosting;
    private bool wasBoosting;
    private int steerPointer = -1;
    private int boostPointer = -1;
    private float keyboardSteer;
    private float guiSteer;
    private bool guiBoostHeld;
    private bool enginePlaybackConfirmed;
    private bool boostPlaybackConfirmed;
    private float frameSampleTime;
    private int frameSampleCount;
    private GUIStyle titleStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle buttonStyle;
    private GUIStyle smallStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<LaneRushGame>() == null)
            new GameObject("Lane Rush Game").AddComponent<LaneRushGame>();
    }

    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        QualitySettings.shadowDistance = 0f;
        QualitySettings.shadowCascades = 1;
        QualitySettings.lodBias = 1f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
        Application.targetFrameRate = 60;
#if UNITY_ANDROID && !UNITY_EDITOR
        ScalableBufferManager.ResizeBuffers(0.78f, 0.78f);
#endif
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.multiTouchEnabled = true;
        best = PlayerPrefs.GetInt("lane-rush-best", 0);
        CreateMaterials();
        CreateWorld();
        CreatePlayer();
        CreateCameraAndLighting();
        CreateTrafficPool();
        CreateAudio();
        ResetRun(false);
    }

    private void CreateMaterials()
    {
        roadMaterial = new Material(Resources.Load<Material>("Materials/Road"));
        roadMaterial.mainTexture = CreateSurfaceTexture(128, false);
        roadMaterial.mainTextureScale = new Vector2(3f, 3f);
        grassMaterial = new Material(Resources.Load<Material>("Materials/Grass"));
        Texture2D grassTexture = CreateSurfaceTexture(128, true);
        grassMaterial.mainTexture = grassTexture;
        grassMaterial.mainTextureScale = new Vector2(24f, 34f);
        whiteMaterial = Resources.Load<Material>("Materials/White");
        curbMaterial = Resources.Load<Material>("Materials/Curb");
        amberMaterial = Resources.Load<Material>("Materials/Amber");
        grassLightMaterial = new Material(Resources.Load<Material>("Materials/GrassLight"));
        grassDarkMaterial = new Material(Resources.Load<Material>("Materials/GrassDark"));
        grassLightMaterial.mainTexture = grassTexture;
        grassDarkMaterial.mainTexture = grassTexture;
        grassLightMaterial.mainTextureScale = grassDarkMaterial.mainTextureScale = new Vector2(2f, 3f);
        contactShadowMaterial = CreateContactShadowMaterial();
        boostMaterial = Resources.Load<Material>("Materials/BoostGlow");
    }

    private static Texture2D CreateSurfaceTexture(int size, bool lawn)
    {
        var random = new System.Random(lawn ? 7348 : 5162);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float grain = (float)random.NextDouble();
            float broad = Mathf.Sin(x * 0.12f) * Mathf.Cos(y * 0.09f) * 0.035f;
            float value = Mathf.Clamp(lawn ? 0.83f + grain * 0.17f + broad : 0.76f + grain * 0.2f + broad, 0.55f, 1f);
            pixels[y * size + x] = lawn ? new Color(value * 0.91f, value, value * 0.86f) : new Color(value, value, value);
        }
        Texture2D texture = new(size, size, TextureFormat.RGB24, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void CreateWorld()
    {
        CreateCube("Ground", new Vector3(0, -0.32f, 105), new Vector3(180, 0.5f, 280), grassMaterial);
        for (int i = 0; i < RoadCount; i++)
        {
            float z = -18f + i * 18f;
            roads.Add(CreateCube("Road", new Vector3(0, 0, z), new Vector3(19, 0.18f, 18.2f), roadMaterial).transform);
            verges.Add(CreateCube("Verge L", new Vector3(-15.1f, -0.045f, z), new Vector3(10.2f, 0.08f, 18.2f), i % 2 == 0 ? grassLightMaterial : grassDarkMaterial).transform);
            verges.Add(CreateCube("Verge R", new Vector3(15.1f, -0.045f, z), new Vector3(10.2f, 0.08f, 18.2f), i % 2 == 0 ? grassDarkMaterial : grassLightMaterial).transform);
            roads.Add(CreateCube("Curb L", new Vector3(-9.8f, 0.14f, z), new Vector3(0.35f, 0.26f, 18.2f), curbMaterial).transform);
            roads.Add(CreateCube("Curb R", new Vector3(9.8f, 0.14f, z), new Vector3(0.35f, 0.26f, 18.2f), curbMaterial).transform);
            foreach (float x in new[] { -2.55f, 2.55f })
            {
                Transform mark = CreateCube("Lane Mark", new Vector3(x, 0.13f, z), new Vector3(0.18f, 0.04f, 5.2f), whiteMaterial).transform;
                laneMarks.Add(mark);
            }
        }

        for (int i = 0; i < TreeCount; i++)
        {
            float height = UnityEngine.Random.Range(8.5f, 11.5f);
            Transform t = CreateModel(treeNames[i % treeNames.Length], new Vector3(height * 0.62f, height, height * 0.62f), i < 12);
            t.position = new Vector3((i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(12.5f, 18f), 0, 8 + i * 8f + UnityEngine.Random.Range(-2.5f, 2.5f));
            t.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            trees.Add(t);
        }
        for (int i = 0; i < ShrubCount; i++)
        {
            float height = UnityEngine.Random.Range(2.2f, 3.8f);
            Transform shrub = CreateModel(treeNames[(i + 3) % treeNames.Length], new Vector3(height * 1.15f, height, height * 1.15f), false);
            shrub.position = new Vector3((i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(10.8f, 14.5f), 0, 14 + i * 11f + UnityEngine.Random.Range(-2f, 2f));
            shrub.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
            shrubs.Add(shrub);
        }
        for (int i = 0; i < BuildingCount; i++)
        {
            Transform b = CreateModel(buildingNames[i % buildingNames.Length], new Vector3(9f, 17f, 9f), false);
            b.position = new Vector3((i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(20f, 34f), 0, 20 + i * 13f + UnityEngine.Random.Range(-3f, 3f));
            b.rotation = Quaternion.Euler(0, i % 2 == 0 ? 90 : -90, 0);
            buildings.Add(b);
        }
    }

    private void CreatePlayer()
    {
        player = new GameObject("Player").transform;
        // The old player mesh had over one million vertices and dominated mobile frame time.
        Transform visual = CreateModel("traffic-sports-car-1371", new Vector3(2.9f, 1.6f, 5.3f), false);
        visual.SetParent(player, false);
        CreateContactShadow(player);
        for (int i = 0; i < boostFlames.Length; i++)
        {
            float x = i == 0 ? -0.65f : 0.65f;
            Transform flame = CreateCube("Nitro Flame", new Vector3(x, 0.42f, -2.8f), new Vector3(0.22f, 0.18f, 1.45f), boostMaterial).transform;
            flame.SetParent(player, false);
            flame.gameObject.SetActive(false);
            boostFlames[i] = flame;
        }
        player.position = new Vector3(0, 0.1f, 4f);
    }

    private void CreateCameraAndLighting()
    {
        gameCamera = new GameObject("Main Camera").AddComponent<Camera>();
        gameCamera.tag = "MainCamera";
        gameCamera.gameObject.AddComponent<AudioListener>();
        gameCamera.allowDynamicResolution = true;
        gameCamera.fieldOfView = 53;
        gameCamera.nearClipPlane = 0.2f;
        gameCamera.farClipPlane = 290f;
        gameCamera.backgroundColor = new Color(0.43f, 0.67f, 0.79f);
        gameCamera.clearFlags = CameraClearFlags.Skybox;
        gameCamera.transform.position = new Vector3(0, 4.45f, -6.4f);
        gameCamera.transform.LookAt(new Vector3(0, 1.2f, 18f));
        RenderSettings.skybox = Resources.Load<Material>("Materials/Sky");

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.72f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.34f, 0.43f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.18f, 0.12f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.40f, 0.57f, 0.75f);
        RenderSettings.fogStartDistance = 95f;
        RenderSettings.fogEndDistance = 270f;

        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.91f, 0.75f);
        sun.intensity = 1.35f;
        sun.shadows = LightShadows.None;
        sun.transform.rotation = Quaternion.Euler(38, -28, 0);
    }

    private void CreateTrafficPool()
    {
        for (int i = 0; i < TrafficCount; i++)
        {
            Transform root = new GameObject($"Traffic {i}").transform;
            Transform visual = CreateModel(trafficNames[i % trafficNames.Length], new Vector3(2.55f, 1.6f, 4.8f), true);
            visual.SetParent(root, false);
            visual.localRotation = Quaternion.identity;
            var car = new TrafficCar { root = root, visual = visual };
            CreateIndicators(car);
            CreateContactShadow(root);
            traffic.Add(car);
            root.gameObject.SetActive(false);
        }
    }

    private void CreateIndicators(TrafficCar car)
    {
        car.leftIndicator = CreateCube("Left Indicator", new Vector3(-0.98f, 0.65f, -2.52f), new Vector3(0.34f, 0.22f, 0.16f), amberMaterial).transform;
        car.rightIndicator = CreateCube("Right Indicator", new Vector3(0.98f, 0.65f, -2.52f), new Vector3(0.34f, 0.22f, 0.16f), amberMaterial).transform;
        car.leftIndicator.SetParent(car.root, false);
        car.rightIndicator.SetParent(car.root, false);
        car.leftIndicator.gameObject.SetActive(false);
        car.rightIndicator.gameObject.SetActive(false);
    }

    private void CreateAudio()
    {
        engineAudio = gameObject.AddComponent<AudioSource>();
        engineAudio.clip = Resources.Load<AudioClip>("Audio/engine-loop");
        engineAudio.loop = true;
        engineAudio.playOnAwake = false;
        engineAudio.volume = 0.9f;
        engineAudio.spatialBlend = 0;
        engineAudio.priority = 0;
        if (engineAudio.clip != null) engineAudio.clip.LoadAudioData();
        effectsAudio = gameObject.AddComponent<AudioSource>();
        effectsAudio.spatialBlend = 0;
        effectsAudio.volume = 1f;
        effectsAudio.priority = 0;
        boostAudio = gameObject.AddComponent<AudioSource>();
        boostClip = CreateBoostClip();
        boostAudio.clip = boostClip;
        boostAudio.loop = true;
        boostAudio.playOnAwake = false;
        boostAudio.spatialBlend = 0;
        boostAudio.volume = 0.8f;
        AudioListener.volume = 1f;
        hornClip = CreateHornClip();
        crashClip = CreateNoiseClip();
        Debug.Log($"LANE_RUSH_AUDIO_READY listener={gameCamera.GetComponent<AudioListener>() != null} engine={engineAudio.clip != null} horn={hornClip != null} boost={boostClip != null}");
    }

    private void Update()
    {
        SamplePerformance();
        HandleInput();
        if (!running || paused)
        {
            if (wasBoosting) SetBoostEffects(false);
            if (paused && engineAudio.isPlaying) engineAudio.Pause();
            return;
        }

        float dt = Mathf.Min(Time.deltaTime, 0.04f);
        elapsed += dt;
        float difficulty = Mathf.Min(2.2f, distance / 2200f);
        boosting = boostPointer >= 0 || guiBoostHeld || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.B);
        boosting &= nitro > 1f;
        if (boosting != wasBoosting) SetBoostEffects(boosting);
        if (boosting && !boostPlaybackConfirmed)
        {
            boostPlaybackConfirmed = true;
            Debug.Log("LANE_RUSH_BOOST_ACTIVE");
        }
        nitro = Mathf.Clamp(nitro + (boosting ? -32f : 13f) * dt, 0, 100);
        speed = Mathf.Min(320f, 120f + elapsed * 4f + (boosting ? 70f : 0f));
        distance += speed / 3.6f * dt;
        combo = Mathf.Max(1f, combo - dt * 0.08f);
        float worldStep = speed / 3.6f * dt;

        float steer = Mathf.Abs(keyboardSteer) > 0.1f ? keyboardSteer : Mathf.Abs(guiSteer) > 0.1f ? guiSteer : TouchSteer();
        lateralVelocity += steer * (boosting ? 38f : 32f) * dt;
        lateralVelocity *= Mathf.Pow(boosting ? 0.05f : 0.075f, dt);
        targetX = Mathf.Clamp(targetX + lateralVelocity * dt, -5.8f, 5.8f);
        player.position = new Vector3(Mathf.Lerp(player.position.x, targetX, 0.12f), 0.1f + Mathf.Sin(elapsed * 11f) * 0.018f, player.position.z);
        player.rotation = Quaternion.Euler(0, 0, Mathf.LerpAngle(player.eulerAngles.z, -lateralVelocity * 0.7f, 0.12f));
        gameCamera.transform.position = new Vector3(Mathf.Lerp(gameCamera.transform.position.x, player.position.x * 0.34f, 0.07f), 4.45f, -6.4f);
        gameCamera.fieldOfView = Mathf.Lerp(gameCamera.fieldOfView, boosting ? 61f : 53f, Mathf.Clamp01(dt * 7f));
        if (boosting)
            for (int i = 0; i < boostFlames.Length; i++)
                boostFlames[i].localScale = new Vector3(0.22f, 0.18f, 1.25f + Mathf.Sin(elapsed * 34f + i) * 0.28f);

        MoveLooped(roads, worldStep, RoadCount * 18f, -27f);
        MoveLooped(laneMarks, worldStep, RoadCount * 18f, -27f);
        MoveLooped(verges, worldStep, RoadCount * 18f, -27f);
        MoveLooped(trees, worldStep, TreeCount / 2 * 16f, -10f);
        MoveLooped(shrubs, worldStep, ShrubCount / 2 * 22f, -12f);
        MoveLooped(buildings, worldStep * 0.35f, BuildingCount / 2 * 26f, -20f);
        UpdateTraffic(dt, worldStep, difficulty);
        if (engineAudio.clip != null && engineAudio.clip.loadState == AudioDataLoadState.Loaded && !engineAudio.isPlaying)
            engineAudio.Play();
        if (engineAudio.isPlaying && !enginePlaybackConfirmed)
        {
            enginePlaybackConfirmed = true;
            Debug.Log($"LANE_RUSH_ENGINE_PLAYING clip={engineAudio.clip.name} samples={engineAudio.clip.samples}");
        }
        engineAudio.pitch = 0.82f + Mathf.InverseLerp(120, 320, speed) * 0.5f + (boosting ? 0.12f : 0);
    }

    private void SetBoostEffects(bool active)
    {
        wasBoosting = active;
        foreach (Transform flame in boostFlames) flame.gameObject.SetActive(active);
        if (active) boostAudio.Play(); else boostAudio.Stop();
        Debug.Log($"LANE_RUSH_BOOST_EFFECT active={active} sound={boostAudio.isPlaying}");
    }

    private void SamplePerformance()
    {
        frameSampleTime += Time.unscaledDeltaTime;
        frameSampleCount++;
        if (frameSampleTime < 10f) return;
        Debug.Log($"LANE_RUSH_PERF fps={frameSampleCount / frameSampleTime:0.0} frameMs={frameSampleTime * 1000f / frameSampleCount:0.0}");
        frameSampleTime = 0;
        frameSampleCount = 0;
    }

    private void HandleInput()
    {
        if (!running && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) ResetRun(true);
        keyboardSteer = Input.GetAxisRaw("Horizontal");
        if (Input.GetKeyDown(KeyCode.H)) Horn();
        if (Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.Escape)) paused = !paused;
        foreach (Touch touch in Input.touches)
        {
            Vector2 p = touch.position;
            if (touch.phase == TouchPhase.Began)
            {
                if (p.y < Screen.height * 0.27f && p.x > Screen.width * 0.57f && p.x < Screen.width * 0.72f) { Horn(); continue; }
                if (p.y < Screen.height * 0.27f && p.x > Screen.width * 0.41f && p.x < Screen.width * 0.56f) { boostPointer = touch.fingerId; continue; }
                steerPointer = touch.fingerId;
            }
            if (touch.phase is TouchPhase.Ended or TouchPhase.Canceled)
            {
                if (touch.fingerId == steerPointer) steerPointer = -1;
                if (touch.fingerId == boostPointer) boostPointer = -1;
            }
        }
    }

    private float TouchSteer()
    {
        foreach (Touch touch in Input.touches)
            if (touch.fingerId == steerPointer) return touch.position.x < Screen.width * 0.5f ? -1 : 1;
        return 0;
    }

    private void UpdateTraffic(float dt, float worldStep, float difficulty)
    {
        spawnAccumulator -= dt;
        if (spawnAccumulator <= 0)
        {
            ActivateTraffic(difficulty);
            spawnAccumulator = Mathf.Max(0.42f, 1.15f - difficulty * 0.22f) + UnityEngine.Random.Range(0.1f, 0.3f);
        }
        foreach (TrafficCar car in traffic)
        {
            if (!car.root.gameObject.activeSelf) continue;
            Vector3 p = car.root.position;
            p.z -= worldStep - car.speed * dt;
            if (difficulty > 0.10f) car.decision -= dt;
            if (!car.changing && difficulty > 0.10f && car.decision <= 0 && p.z > 24f)
                BeginLaneChange(car, difficulty);
            if (car.changing)
            {
                car.signalTime += dt;
                bool lit = Mathf.FloorToInt(car.signalTime * 4f) % 2 == 0;
                car.leftIndicator.gameObject.SetActive(lit && car.targetLane < car.lane);
                car.rightIndicator.gameObject.SetActive(lit && car.targetLane > car.lane);
                if (car.signalTime > 0.75f)
                {
                    float goal = Lanes[car.targetLane];
                    p.x = Mathf.MoveTowards(p.x, goal, (3.2f + difficulty * 0.25f) * dt);
                    car.root.rotation = Quaternion.Euler(0, Mathf.Clamp((goal - p.x) * 1.4f, -6f, 6f), 0);
                    if (Mathf.Abs(p.x - goal) < 0.01f)
                    {
                        car.lane = car.targetLane;
                        car.changing = false;
                        car.root.rotation = Quaternion.identity;
                        car.leftIndicator.gameObject.SetActive(false);
                        car.rightIndicator.gameObject.SetActive(false);
                        car.decision = UnityEngine.Random.Range(2.4f, 4.2f);
                        Debug.Log($"LANE_RUSH_LANE_CHANGE_DONE lane={car.lane}");
                    }
                }
            }
            car.root.position = p;
            float dx = Mathf.Abs(p.x - player.position.x);
            float dz = Mathf.Abs(p.z - player.position.z);
            if (!car.passed && p.z < player.position.z)
            {
                car.passed = true;
                if (dx < 2.5f) { closeCalls++; combo = Mathf.Min(5, combo + 0.45f); distance += 18 * combo; Beep(720, 0.07f); }
            }
            if (dx < 1.55f && dz < 2.65f) Crash();
            if (p.z < -16f) car.root.gameObject.SetActive(false);
        }
    }

    private void ActivateTraffic(float difficulty)
    {
        TrafficCar car = traffic.Find(c => !c.root.gameObject.activeSelf);
        if (car == null) return;
        int lane = UnityEngine.Random.Range(0, Lanes.Length);
        car.lane = car.targetLane = lane;
        car.speed = UnityEngine.Random.Range(13f, 21f + difficulty * 3f);
        car.decision = UnityEngine.Random.Range(1.2f, 2.8f);
        car.signalTime = 0;
        car.changing = car.passed = false;
        car.root.SetPositionAndRotation(new Vector3(Lanes[lane], 0.08f, UnityEngine.Random.Range(120f, 175f)), Quaternion.identity);
        car.leftIndicator.gameObject.SetActive(false);
        car.rightIndicator.gameObject.SetActive(false);
        car.root.gameObject.SetActive(true);
    }

    private void BeginLaneChange(TrafficCar car, float difficulty)
    {
        int direction = car.lane == 0 ? 1 : car.lane == Lanes.Length - 1 ? -1 : UnityEngine.Random.value > 0.5f ? 1 : -1;
        int target = car.lane + direction;
        if (!LaneClear(car, target) && car.lane == 1) target = car.lane - direction;
        if (!LaneClear(car, target)) { car.decision = UnityEngine.Random.Range(0.7f, 1.4f); return; }
        car.targetLane = target;
        car.signalTime = 0;
        car.changing = true;
        Debug.Log($"LANE_RUSH_LANE_CHANGE_START from={car.lane} to={target} z={car.root.position.z:0}");
    }

    private bool LaneClear(TrafficCar source, int target)
    {
        foreach (TrafficCar other in traffic)
            if (other != source && other.root.gameObject.activeSelf && other.targetLane == target && Mathf.Abs(other.root.position.z - source.root.position.z) < 22f)
                return false;
        return true;
    }

    private void ResetRun(bool start)
    {
        foreach (TrafficCar car in traffic) car.root.gameObject.SetActive(false);
        targetX = lateralVelocity = elapsed = distance = 0;
        speed = 120; nitro = 100; combo = 1; closeCalls = 0; spawnAccumulator = 0;
        steerPointer = boostPointer = -1;
        guiBoostHeld = false;
        player.position = new Vector3(0, 0.1f, 4f);
        player.rotation = Quaternion.identity;
        if (wasBoosting) SetBoostEffects(false);
        gameCamera.fieldOfView = 53f;
        crashed = paused = false;
        enginePlaybackConfirmed = false;
        boostPlaybackConfirmed = false;
        running = start;
        if (start && engineAudio.clip != null)
        {
            if (engineAudio.clip.loadState == AudioDataLoadState.Unloaded) engineAudio.clip.LoadAudioData();
            if (engineAudio.clip.loadState == AudioDataLoadState.Loaded) engineAudio.Play();
        }
    }

    private void Crash()
    {
        if (crashed) return;
        crashed = true; running = false; boosting = false;
        if (wasBoosting) SetBoostEffects(false);
        engineAudio.Stop();
        effectsAudio.PlayOneShot(crashClip, 1f);
        best = Mathf.Max(best, Mathf.FloorToInt(distance));
        PlayerPrefs.SetInt("lane-rush-best", best);
    }

    private void Horn()
    {
        effectsAudio.PlayOneShot(hornClip, 1f);
        Debug.Log($"LANE_RUSH_HORN_PLAYED listener={gameCamera.GetComponent<AudioListener>() != null} source={effectsAudio.isActiveAndEnabled}");
    }

    private static AudioClip CreateHornClip()
    {
        const int rate = 44100;
        int count = Mathf.CeilToInt(rate * 0.38f);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / rate;
            float envelope = Mathf.Clamp01(t / 0.018f) * Mathf.Clamp01((0.38f - t) / 0.07f);
            float low = Mathf.Sin(2 * Mathf.PI * 385f * t);
            float high = Mathf.Sin(2 * Mathf.PI * 485f * t);
            samples[i] = (low * 0.55f + high * 0.45f) * envelope * 0.75f;
        }
        AudioClip clip = AudioClip.Create("Car Horn", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateBoostClip()
    {
        const int rate = 22050;
        int count = rate / 2;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            float envelope = Mathf.Min(1f, t * 18f, (1f - t) * 18f);
            float sweep = Mathf.Sin(2f * Mathf.PI * (130f * t + 175f * t * t));
            float rumble = Mathf.Sin(2f * Mathf.PI * 73f * t);
            samples[i] = (sweep * 0.55f + rumble * 0.3f + UnityEngine.Random.Range(-0.12f, 0.12f)) * envelope;
        }
        AudioClip clip = AudioClip.Create("Nitro Boost", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static AudioClip CreateNoiseClip()
    {
        const int rate = 22050;
        int count = Mathf.CeilToInt(rate * 0.3f);
        float[] samples = new float[count];
        float previous = 0;
        for (int i = 0; i < count; i++)
        {
            previous = Mathf.Lerp(previous, UnityEngine.Random.Range(-1f, 1f), 0.18f);
            samples[i] = previous * (1f - (float)i / count) * 0.7f;
        }
        AudioClip clip = AudioClip.Create("Crash", count, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void Beep(float frequency, float duration)
    {
        int rate = 22050, count = Mathf.CeilToInt(rate * duration);
        float[] samples = new float[count];
        for (int i = 0; i < count; i++) samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * i / rate) * Mathf.Clamp01((count - i) / (rate * 0.04f)) * 0.32f;
        AudioClip clip = AudioClip.Create("Effect", count, 1, rate, false);
        clip.SetData(samples, 0);
        effectsAudio.PlayOneShot(clip);
        Destroy(clip, duration + 0.2f);
    }

    private static void MoveLooped(List<Transform> items, float step, float span, float threshold)
    {
        foreach (Transform item in items)
        {
            Vector3 p = item.position;
            p.z -= step;
            if (p.z < threshold) p.z += span;
            item.position = p;
        }
    }

    private Transform CreateModel(string resourceName, Vector3 targetSize, bool shadows, Vector3 orientation = default)
    {
        GameObject prefab = Resources.Load<GameObject>("Models/" + resourceName);
        GameObject instance = prefab != null ? Instantiate(prefab) : CreateFallbackCar();
        instance.name = resourceName;
        instance.transform.rotation = Quaternion.Euler(orientation);
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) { bounds.Encapsulate(renderer.bounds); renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = shadows; }
            Vector3 size = bounds.size;
            float scale = Mathf.Min(targetSize.x / Mathf.Max(size.x, 0.01f), Mathf.Min(targetSize.y / Mathf.Max(size.y, 0.01f), targetSize.z / Mathf.Max(size.z, 0.01f)));
            instance.transform.localScale = Vector3.one * scale;
            Bounds scaled = CalculateBounds(instance);
            instance.transform.position -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
        }
        return instance.transform;
    }

    private void CreateContactShadow(Transform parent)
    {
        GameObject shadow = new("Player Contact Shadow");
        Mesh mesh = new()
        {
            name = "Contact Shadow Mesh",
            vertices = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(-1, 0, 1), new Vector3(1, 0, 1) },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        mesh.RecalculateBounds();
        shadow.AddComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = shadow.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = contactShadowMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        shadow.transform.SetParent(parent, false);
        shadow.transform.localPosition = new Vector3(0, 0.015f, 0.2f);
        shadow.transform.localScale = new Vector3(1.05f, 1f, 1.9f);
    }

    private static Material CreateContactShadowMaterial()
    {
        const int size = 64;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false) { name = "Contact Shadow Texture" };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = (x + 0.5f) / size * 2f - 1f;
            float ny = (y + 0.5f) / size * 2f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - nx * nx - ny * ny), 2f) * 0.48f;
            pixels[y * size + x] = new Color(0.01f, 0.015f, 0.012f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Material baseMaterial = Resources.Load<Material>("Materials/ContactShadow");
        if (baseMaterial == null) throw new InvalidOperationException("Contact shadow material is missing from Resources.");
        Material material = new(baseMaterial) { name = "Contact Shadow", mainTexture = texture };
        material.renderQueue = 2990;
        return material;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    private GameObject CreateFallbackCar()
    {
        GameObject fallback = CreateCube("Fallback", Vector3.zero, new Vector3(2, 0.8f, 4), whiteMaterial);
        return fallback;
    }

    private static GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = new GameObject(name);
        MeshFilter filter = cube.AddComponent<MeshFilter>();
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        MeshRenderer renderer = cube.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        cube.transform.SetPositionAndRotation(position, Quaternion.identity);
        cube.transform.localScale = scale;
        return cube;
    }

    private void InitStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.075f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        hudLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.018f), alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.65f, 0.72f, 0.77f) } };
        hudValueStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.038f), alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(Screen.height * 0.034f), fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.78f, 1f, 0.13f) } };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height * 0.022f), alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
    }

    private void OnGUI()
    {
        InitStyles();
        float w = Screen.width, h = Screen.height;
        GUI.color = new Color(0.02f, 0.06f, 0.09f, 0.88f);
        GUI.Box(new Rect(w * 0.62f, h * 0.02f, w * 0.36f, h * 0.14f), GUIContent.none);
        GUI.color = Color.white;
        DrawHudCell("DISTANCE", $"{Mathf.FloorToInt(distance):0000} M", w * 0.63f, h);
        DrawHudCell("SPEED", $"{Mathf.RoundToInt(speed)} KM/H", w * 0.72f, h, new Color(0.78f, 1f, 0.13f));
        DrawHudCell("COMBO", $"{combo:0.0}X", w * 0.81f, h, new Color(0.78f, 1f, 0.13f));
        DrawHudCell("BEST", $"{best:0000} M", w * 0.90f, h);

        if (boosting && running && !paused)
        {
            GUI.color = new Color(0.08f, 0.8f, 1f, 0.22f);
            GUI.DrawTexture(new Rect(0, 0, w * 0.025f, h), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(w * 0.975f, 0, w * 0.025f, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.96f, 1f);
            GUI.Label(new Rect(w * 0.39f, h * 0.05f, w * 0.22f, h * 0.065f), "NITRO BOOST", smallStyle);
            GUI.color = Color.white;
        }

        GUI.Label(new Rect(w * 0.01f, h * 0.02f, w * 0.2f, h * 0.08f), "LR   LANE RUSH", smallStyle);
        Rect nitroTrack = new(w * 0.78f, h * 0.73f, w * 0.18f, Mathf.Max(12f, h * 0.022f));
        GUI.Label(new Rect(w * 0.78f, h * 0.655f, w * 0.18f, h * 0.07f), boosting ? "BOOST" : nitro >= 99 ? "NITRO READY" : $"NITRO {nitro:0}%", smallStyle);
        GUI.color = new Color(0.02f, 0.05f, 0.06f, 0.9f);
        GUI.DrawTexture(nitroTrack, Texture2D.whiteTexture);
        GUI.color = boosting ? new Color(0.15f, 0.92f, 1f) : new Color(0.78f, 1f, 0.13f);
        GUI.DrawTexture(new Rect(nitroTrack.x + 3f, nitroTrack.y + 3f, (nitroTrack.width - 6f) * nitro / 100f, nitroTrack.height - 6f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        if (running && !paused)
        {
            bool leftHeld = GUI.RepeatButton(new Rect(w * 0.28f, h * 0.82f, w * 0.12f, h * 0.15f), "←", buttonStyle);
            guiBoostHeld = GUI.RepeatButton(new Rect(w * 0.41f, h * 0.82f, w * 0.15f, h * 0.15f), "BOOST", buttonStyle);
            if (GUI.Button(new Rect(w * 0.57f, h * 0.82f, w * 0.15f, h * 0.15f), "HORN", buttonStyle) && Input.touchCount == 0) Horn();
            bool rightHeld = GUI.RepeatButton(new Rect(w * 0.73f, h * 0.82f, w * 0.12f, h * 0.15f), "→", buttonStyle);
            guiSteer = leftHeld ? -1f : rightHeld ? 1f : 0f;
        }
        else
        {
            guiSteer = 0;
            guiBoostHeld = false;
            GUI.color = new Color(0.02f, 0.06f, 0.09f, 0.93f);
            GUI.Box(new Rect(w * 0.27f, h * 0.19f, w * 0.46f, h * 0.56f), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(w * 0.3f, h * 0.25f, w * 0.4f, h * 0.18f), crashed ? $"{distance:0} METRES\nRUN IT BACK?" : paused ? "PAUSED" : "LANE\nRUSH.", titleStyle);
            GUI.Label(new Rect(w * 0.32f, h * 0.45f, w * 0.36f, h * 0.08f), crashed ? $"Close calls: {closeCalls}   Best: {best} metres" : "Thread traffic. Chain close calls. Own the road.", smallStyle);
            if (GUI.Button(new Rect(w * 0.32f, h * 0.57f, w * 0.36f, h * 0.11f), paused ? "RESUME" : crashed ? "RESTART ENGINE" : "START ENGINE", buttonStyle))
            {
                if (paused) paused = false; else ResetRun(true);
            }
        }
    }

    private void DrawHudCell(string label, string value, float x, float h, Color? color = null)
    {
        float w = Screen.width * 0.085f;
        GUI.Label(new Rect(x, h * 0.035f, w, h * 0.03f), label, hudLabelStyle);
        Color old = hudValueStyle.normal.textColor;
        hudValueStyle.normal.textColor = color ?? Color.white;
        GUI.Label(new Rect(x, h * 0.068f, w, h * 0.065f), value, hudValueStyle);
        hudValueStyle.normal.textColor = old;
    }

    private sealed class TrafficCar
    {
        public Transform root;
        public Transform visual;
        public Transform leftIndicator;
        public Transform rightIndicator;
        public int lane;
        public int targetLane;
        public float speed;
        public float decision;
        public float signalTime;
        public bool changing;
        public bool passed;
    }
}
