using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Stage
{
    /// <summary>
    /// 바닥 · 레인 · 칸 · 격리선(해치 턱 + 레이저) · 스폰선을 런타임에 만든다 (씬에 타일을 두지 않는다 — 수치가 바뀌면 따라온다).
    /// 크기 · 위치는 전부 GridSpace / RuleConfig. 아래 직렬화 기본값은 표시용 비율 · 연출값만.
    /// </summary>
    public sealed class StageBuilder : MonoBehaviour
    {
        [SerializeField] private GameBootstrap bootstrap;

        [Header("프리팹 · 재질")]
        [SerializeField] private MeshRenderer cellPrefab;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material laneMaterial;
        [SerializeField] private Material hatchMaterial;
        [SerializeField] private Material spawnLineMaterial;
        [Tooltip("격리선 레이저 — URP Unlit (블룸은 U07)")]
        [SerializeField] private Material laserMaterial;

        [Header("표시용 비율 (칸 단위)")]
        [Tooltip("칸 타일 한 변 = cellSize × 이 값")]
        [SerializeField] private float cellTileRatio = 0.9f;
        [Tooltip("레인 띠 폭 = 레인 하나 폭 × 이 값")]
        [SerializeField] private float laneWidthRatio = 0.9f;
        [Tooltip("바닥이 레인 폭 · StageBounds 앞뒤 바깥으로 나가는 여백")]
        [SerializeField] private float floorMarginCells = 1f;
        [SerializeField] private float floorThicknessCells = 0.1f;
        [Tooltip("격리선 턱 높이")]
        [SerializeField] private float hatchHeightCells = 0.25f;
        [Tooltip("격리선 턱 두께 (z 방향, 중심 z = 0)")]
        [SerializeField] private float hatchDepthCells = 0.2f;
        [Tooltip("레이저 막대 중심 높이 (바닥 기준) — 턱 위에 뜬다")]
        [SerializeField] private float laserHeightCells = 0.35f;
        [Tooltip("레이저 막대 굵기 (y · z)")]
        [SerializeField] private float laserThicknessCells = 0.03f;
        [Tooltip("스폰선 띠 두께 (z 방향)")]
        [SerializeField] private float spawnLineThicknessCells = 0.1f;

        [Header("겹침 방지 y 오프셋 (월드)")]
        [SerializeField] private float laneYOffset = 0.005f;
        [SerializeField] private float cellYOffset = 0.01f;
        [SerializeField] private float spawnLineYOffset = 0.01f;

        [Header("기즈모")]
        [SerializeField] private float gizmoRadius = 0.08f;

        /// <summary>칸 인덱스 = 배열 인덱스. G02 하이라이트 · U03 가 MaterialPropertyBlock 으로 쓴다.</summary>
        public Renderer[] CellRenderers { get; private set; }

        public bool IsBuilt => CellRenderers != null;

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[StageBuilder] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            if (cellPrefab == null || floorMaterial == null || laneMaterial == null ||
                hatchMaterial == null || spawnLineMaterial == null || laserMaterial == null)
            {
                Debug.LogError("[StageBuilder] 칸 프리팹 또는 재질이 비어 있다", this);
                enabled = false;
                return;
            }

            Build(bootstrap.Space);
        }

        private void Build(GridSpace space)
        {
            float cs = space.CellSize;
            float laneSpan = space.LaneCount * cs;
            float pathSpan = space.PathLength * cs;
            Transform root = transform;

            // 바닥 — 윗면이 y = 0, StageBounds(칸 3줄 뒤 끝 ~ 스폰선) 앞뒤로 여백
            Bounds stage = space.StageBounds;
            float floorZMin = stage.min.z - floorMarginCells * cs;
            float floorZMax = stage.max.z + floorMarginCells * cs;
            float floorThickness = floorThicknessCells * cs;
            CreatePrimitive(PrimitiveType.Cube, "Floor", root, floorMaterial,
                new Vector3(0f, -floorThickness * 0.5f, (floorZMin + floorZMax) * 0.5f),
                Quaternion.identity,
                new Vector3(laneSpan + 2f * floorMarginCells * cs, floorThickness, floorZMax - floorZMin));

            // 레인 — 점액 길 z 0 ~ pathLength 만. 칸 영역(z < 0)은 칸이 레인을 대신 보여 준다
            Transform lanes = CreateGroup("Lanes", root);
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);
            for (int lane = 0; lane < space.LaneCount; lane++)
            {
                Vector3 hatchPoint = space.PathToWorld(lane, 0f);
                Vector3 spawnPoint = space.PathToWorld(lane, space.PathLength);
                Vector3 center = (hatchPoint + spawnPoint) * 0.5f;
                center.y = laneYOffset;
                CreatePrimitive(PrimitiveType.Quad, "Lane_" + lane, lanes, laneMaterial,
                    center, flat, new Vector3(cs * laneWidthRatio, pathSpan, 1f));
            }

            // 칸 — Cell.prefab (Collider 없음)
            Transform cells = CreateGroup("Cells", root);
            int count = space.CellCount;
            CellRenderers = new Renderer[count];
            float tile = cs * cellTileRatio;
            for (int i = 0; i < count; i++)
            {
                MeshRenderer cell = Instantiate(cellPrefab, cells);
                cell.name = "Cell_" + i.ToString("00");
                Transform t = cell.transform;
                Vector3 pos = space.CellToWorld(i);
                pos.y = cellYOffset;
                t.localPosition = pos;
                t.localScale = new Vector3(tile, tile, 1f);
                CellRenderers[i] = cell;
            }

            // 격리선 턱 (Hatch) — 중심 z = 0 (칸과 점액 길 사이), 레인 폭 전체
            float hatchHeight = hatchHeightCells * cs;
            float hatchDepth = hatchDepthCells * cs;
            MeshRenderer hatch = CreatePrimitive(PrimitiveType.Cube, "Hatch", root, hatchMaterial,
                new Vector3(0f, hatchHeight * 0.5f, 0f),
                Quaternion.identity,
                new Vector3(laneSpan, hatchHeight, hatchDepth));
            // 첫 줄 칸이 턱 그림자로 어두워져 하이라이트 색이 달라 보이지 않게 (조명 · 그림자 전체는 U07)
            hatch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // 레이저 — 턱 위에 뜬 가로 막대, 레인 폭 전체
            float laserThickness = laserThicknessCells * cs;
            MeshRenderer laser = CreatePrimitive(PrimitiveType.Cube, "Laser", root, laserMaterial,
                new Vector3(0f, laserHeightCells * cs, 0f),
                Quaternion.identity,
                new Vector3(laneSpan, laserThickness, laserThickness));
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;

            // 스폰선 — z = pathLength, 가는 띠
            CreatePrimitive(PrimitiveType.Quad, "SpawnLine", root, spawnLineMaterial,
                new Vector3(0f, spawnLineYOffset, pathSpan),
                flat,
                new Vector3(laneSpan, spawnLineThicknessCells * cs, 1f));
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            return t;
        }

        private static MeshRenderer CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>()); // 칸 판정은 GridSpace — Collider 안 씀 (시작 시 1회)
            go.name = name;
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            t.localScale = localScale;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void OnDrawGizmos()
        {
            if (bootstrap == null) return;
            GridSpace space = bootstrap.Space;
            if (space == null)
            {
                // 편집 모드 — Awake 전이라 데이터에서 임시로 만든다 (에디터 전용 경로)
                if (bootstrap.Data == null || bootstrap.Data.Config == null) return;
                space = new GridSpace(bootstrap.Data.Config.ToRuleConfig());
            }
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < space.CellCount; i++)
                Gizmos.DrawSphere(space.CellToWorld(i), gizmoRadius * space.CellSize);
        }
    }
}
