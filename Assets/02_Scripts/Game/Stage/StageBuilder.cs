using QL.Game.Bootstrap;
using UnityEngine;

namespace QL.Game.Stage
{
    /// <summary>
    /// 바닥 · 레인 · 칸 · 격리선(해치 턱 + 레이저) · 스폰선을 런타임에 만든다 (씬에 타일을 두지 않는다 — 수치가 바뀌면 따라온다).
    /// 크기 · 위치는 전부 GridSpace / RuleConfig. 아래 직렬화 기본값은 표시용 비율 · 연출값만.
    /// G07: 무대 모듈 프리팹(루트 + 자식 Model) 칸이 차 있으면 그것을 깔고, 비어 있으면 예전 임시 도형(바닥 · 턱 큐브)을 쓴다.
    /// 모듈 배치는 _아트납품.md A09 표 — Config(레인 · 줄 · 길이 · 칸 크기)와 아래 칸 단위 오프셋으로 계산한다. 시작 때 한 번만.
    /// G10: 복도 벽 바깥(Outer) — A11 데크 판 · 컨테이너 더미를 배치좌표.md 표(직렬화 배열) 그대로 깐다.
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

        [Header("무대 모듈 프리팹 (비우면 임시 도형 / 없음)")]
        [Tooltip("M1 격리선 턱 — 레인마다 1개. 비우면 Hatch 큐브")]
        [SerializeField] private GameObject lineSill;
        [Tooltip("M2 경고등 기둥 — 격리선 양끝")]
        [SerializeField] private GameObject linePost;
        [Tooltip("M3 벽 토막 — 좌우")]
        [SerializeField] private GameObject wallPanel;
        [Tooltip("M4 스폰 게이트 — 앞면이 점액 길 끝")]
        [SerializeField] private GameObject spawnGate;
        [Tooltip("M5 바닥 판 — 비우면 Floor 큐브")]
        [SerializeField] private GameObject floorTile;
        [Tooltip("M6 화물 상자 — crateSlots 자리")]
        [SerializeField] private GameObject propCrate;
        [Tooltip("M6 배관 토막 — 벽 안쪽 면")]
        [SerializeField] private GameObject propPipe;

        [Header("표시용 비율 (칸 단위)")]
        [Tooltip("칸 타일 한 변 = cellSize × 이 값")]
        [SerializeField] private float cellTileRatio = 0.9f;
        [Tooltip("레인 띠 폭 = 레인 하나 폭 × 이 값")]
        [SerializeField] private float laneWidthRatio = 0.9f;
        [Tooltip("임시 바닥 큐브가 레인 폭 · StageBounds 앞뒤 바깥으로 나가는 여백")]
        [SerializeField] private float floorMarginCells = 1f;
        [SerializeField] private float floorThicknessCells = 0.1f;
        [Tooltip("격리선 턱 높이 (임시 큐브)")]
        [SerializeField] private float hatchHeightCells = 0.25f;
        [Tooltip("격리선 턱 두께 (z 방향, 중심 z = 0, 임시 큐브)")]
        [SerializeField] private float hatchDepthCells = 0.2f;
        [Tooltip("레이저 막대 중심 높이 (바닥 기준) — 기둥 발사구 렌즈 중심")]
        [SerializeField] private float laserHeightCells = 0.75f;
        [Tooltip("레이저 막대 굵기 (y · z)")]
        [SerializeField] private float laserThicknessCells = 0.03f;
        [Tooltip("스폰선 띠 두께 (z 방향)")]
        [SerializeField] private float spawnLineThicknessCells = 0.1f;

        [Header("모듈 배치 (칸 단위 — A09)")]
        [Tooltip("바닥 판 한 변")]
        [SerializeField] private float floorTileCells = 2f;
        [Tooltip("바닥 판 윗면 높이 — y = −이 값에 깔아 윗면을 0 에 맞춘다")]
        [SerializeField] private float floorTileTopCells = 0.05f;
        [Tooltip("바닥 판 · 벽이 칸 마지막 줄 뒤로 나가는 길이 (z 시작 = −(rowCount + 이 값))")]
        [SerializeField] private float stageBackMarginCells = 1f;
        [Tooltip("바닥 판 · 벽이 스폰선 너머로 나가는 길이 (z 끝 = pathLength + 이 값)")]
        [SerializeField] private float stageFrontMarginCells = 2f;
        [Tooltip("바닥 판이 레인 폭 양옆으로 나가는 여백")]
        [SerializeField] private float floorSideMarginCells = 1.5f;
        [Tooltip("경고등 기둥 중심 x = 격리선 끝(레인 폭 반) + 이 값")]
        [SerializeField] private float postOffsetCells = 0.25f;
        [Tooltip("기둥 중심에서 발사구까지 — 레이저가 두 발사구 사이를 잇는다")]
        [SerializeField] private float postLaserInsetCells = 0.2f;
        [Tooltip("벽 안쪽 면 x = 레인 폭 반 + 이 값")]
        [SerializeField] private float wallOffsetCells = 1f;
        [Tooltip("벽 토막 길이 (z)")]
        [SerializeField] private float wallPanelCells = 2f;
        [Tooltip("스폰 게이트 중심 z = pathLength + 이 값 (앞면이 점액 길 끝)")]
        [SerializeField] private float gateOffsetCells = 0.4f;
        [Tooltip("배관 토막 길이 (z, 이음 간격)")]
        [SerializeField] private float pipeCells = 1f;
        [Tooltip("배관 바닥 높이 — 벽 발광 띠 · 상자와 겹치지 않게")]
        [SerializeField] private float pipeHeightCells = 1.5f;
        [Tooltip("상자 중심 x = 레인 폭 반 + 이 값")]
        [SerializeField] private float crateOffsetCells = 0.5f;
        [Tooltip("상자 높이 — 2단째 상자 바닥 높이")]
        [SerializeField] private float crateHeightCells = 0.7f;
        [Tooltip("상자 자리: x = 쪽(음수 왼쪽 · 양수 오른쪽), y = 단(0 · 1), z = 칸 단위 z")]
        [SerializeField] private Vector3[] crateSlots =
        {
            new Vector3(-1f, 0f, -2.5f), new Vector3(-1f, 1f, -2.5f), new Vector3(-1f, 0f, 6f),
            new Vector3(1f, 0f, -1.5f), new Vector3(1f, 0f, 4f), new Vector3(1f, 1f, 4f)
        };
        [Tooltip("좌우 대칭 모듈(기둥 · 벽 · 배관) 중 뒤집어 놓을 쪽이 왼쪽인가. Unity 의 OBJ import 는 x 를 반전하므로 납품 문서의 '오른쪽 뒤집기' 가 Unity 에서는 왼쪽이 된다")]
        [SerializeField] private bool mirrorLeftSide = true;
        [Tooltip("뒤집기를 scale.x = −1 대신 Y 180° 회전으로 (법선이 뒤집혀 보이면 켠다)")]
        [SerializeField] private bool mirrorByRotation;
        [Tooltip("스폰 게이트가 있으면 스폰선 띠를 끈다 (게이트 앞면과 같은 z — 판정과 무관)")]
        [SerializeField] private bool hideSpawnLineWithGate = true;

        [Header("복도 바깥 — 화물 격납고 데크 (A11 배치좌표.md, 칸 단위)")]
        [Tooltip("데크 판 4×4 — 비우면 데크 없음")]
        [SerializeField] private GameObject outerDeck;
        [Tooltip("컨테이너 더미 (높음, 경고등) — 비우면 그 더미 없음")]
        [SerializeField] private GameObject outerStackHigh;
        [Tooltip("컨테이너 더미 (낮음)")]
        [SerializeField] private GameObject outerStackLow;
        [Tooltip("색 B — 높은 더미에 바꿔 끼울 재질 (같은 메시)")]
        [SerializeField] private Material outerStackHighMaterialB;
        [Tooltip("색 B — 낮은 더미에 바꿔 끼울 재질")]
        [SerializeField] private Material outerStackLowMaterialB;
        [Tooltip("바깥 전체 높이 (복도 바닥보다 아래)")]
        [SerializeField] private float outerYCells;
        [Tooltip("데크 판 중심 |x| — 좌우 양쪽에 같은 값으로 깐다")]
        [SerializeField] private float[] outerDeckX;
        [Tooltip("데크 판 중심 z")]
        [SerializeField] private float[] outerDeckZ;
        [Tooltip("컨테이너 더미 자리 — 배치좌표.md 표 그대로 (x 는 납품 OBJ 좌표)")]
        [SerializeField] private OuterStackSlot[] outerStacks;
        [Tooltip("표의 x 를 뒤집어 놓는다. Unity 의 OBJ import 가 x 를 반전하므로, 납품 미리보기와 같은 쪽에 놓으려면 켠다 (메시도 이미 반전돼 있어 장면 전체가 미리보기와 같아진다)")]
        [SerializeField] private bool outerFlipX = true;

        /// <summary>바깥 컨테이너 더미 한 자리 (배치좌표.md 표 한 줄).</summary>
        [System.Serializable]
        public struct OuterStackSlot
        {
            [Tooltip("true = StackHigh · false = StackLow")]
            public bool high;
            [Tooltip("색 B 재질")]
            public bool colorB;
            public float x;
            public float z;
            [Tooltip("Y 회전 (도)")]
            public float yaw;
        }

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
            Quaternion flat = Quaternion.Euler(90f, 0f, 0f);

            // 무대 앞뒤 범위 (바닥 판 · 벽 · 배관 공용) — 칸 뒤 ~ 스폰선 너머
            float stageZMin = -(space.RowCount + stageBackMarginCells) * cs;
            float stageZMax = pathSpan + stageFrontMarginCells * cs;

            // 바닥 — 윗면이 y = 0
            if (floorTile != null)
            {
                Transform floor = CreateGroup("Floor", root);
                float tile = floorTileCells * cs;
                int nx = TileCount(laneSpan + 2f * floorSideMarginCells * cs, tile);
                int nz = TileCount(stageZMax - stageZMin, tile);
                float x0 = -nx * tile * 0.5f;
                for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                    PlaceModule(floorTile, floor, "FloorTile_" + ix + "_" + iz,
                        new Vector3(x0 + tile * (ix + 0.5f), -floorTileTopCells * cs, stageZMin + tile * (iz + 0.5f)), cs, false);
            }
            else
            {
                Bounds stage = space.StageBounds;
                float floorZMin = stage.min.z - floorMarginCells * cs;
                float floorZMax = stage.max.z + floorMarginCells * cs;
                float floorThickness = floorThicknessCells * cs;
                CreatePrimitive(PrimitiveType.Cube, "Floor", root, floorMaterial,
                    new Vector3(0f, -floorThickness * 0.5f, (floorZMin + floorZMax) * 0.5f),
                    Quaternion.identity,
                    new Vector3(laneSpan + 2f * floorMarginCells * cs, floorThickness, floorZMax - floorZMin));
            }

            // 레인 — 점액 길 z 0 ~ pathLength 만. 칸 영역(z < 0)은 칸이 레인을 대신 보여 준다
            Transform lanes = CreateGroup("Lanes", root);
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
            float cellTile = cs * cellTileRatio;
            for (int i = 0; i < count; i++)
            {
                MeshRenderer cell = Instantiate(cellPrefab, cells);
                cell.name = "Cell_" + i.ToString("00");
                Transform t = cell.transform;
                Vector3 pos = space.CellToWorld(i);
                pos.y = cellYOffset;
                t.localPosition = pos;
                t.localScale = new Vector3(cellTile, cellTile, 1f);
                CellRenderers[i] = cell;
            }

            // 격리선 턱 (Hatch) — 중심 z = 0 (칸과 점액 길 사이). 판정 · 위치는 그대로, 겉모습만
            if (lineSill != null)
            {
                Transform hatchGroup = CreateGroup("Hatch", root);
                for (int lane = 0; lane < space.LaneCount; lane++)
                {
                    Vector3 p = space.PathToWorld(lane, 0f);
                    p.y = 0f;
                    PlaceModule(lineSill, hatchGroup, "LineSill_" + lane, p, cs, false);
                }
            }
            else
            {
                float hatchHeight = hatchHeightCells * cs;
                float hatchDepth = hatchDepthCells * cs;
                MeshRenderer hatch = CreatePrimitive(PrimitiveType.Cube, "Hatch", root, hatchMaterial,
                    new Vector3(0f, hatchHeight * 0.5f, 0f),
                    Quaternion.identity,
                    new Vector3(laneSpan, hatchHeight, hatchDepth));
                // 첫 줄 칸이 턱 그림자로 어두워져 하이라이트 색이 달라 보이지 않게 (조명 · 그림자 전체는 U07)
                hatch.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // 경고등 기둥 — 격리선 양끝
            float postX = laneSpan * 0.5f + postOffsetCells * cs;
            if (linePost != null)
            {
                Transform posts = CreateGroup("Posts", root);
                PlaceModule(linePost, posts, "LinePost_L", new Vector3(-postX, 0f, 0f), cs, mirrorLeftSide);
                PlaceModule(linePost, posts, "LinePost_R", new Vector3(postX, 0f, 0f), cs, !mirrorLeftSide);
            }

            // 레이저 — 가로 막대. 기둥이 있으면 두 발사구 사이, 없으면 레인 폭 전체
            float laserThickness = laserThicknessCells * cs;
            float laserLength = linePost != null ? 2f * (postX - postLaserInsetCells * cs) : laneSpan;
            MeshRenderer laser = CreatePrimitive(PrimitiveType.Cube, "Laser", root, laserMaterial,
                new Vector3(0f, laserHeightCells * cs, 0f),
                Quaternion.identity,
                new Vector3(laserLength, laserThickness, laserThickness));
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;

            // 스폰선 — z = pathLength, 가는 띠
            MeshRenderer spawnLine = CreatePrimitive(PrimitiveType.Quad, "SpawnLine", root, spawnLineMaterial,
                new Vector3(0f, spawnLineYOffset, pathSpan),
                flat,
                new Vector3(laneSpan, spawnLineThicknessCells * cs, 1f));

            // 스폰 게이트 — 앞면이 점액 길 끝 (z = pathLength)
            if (spawnGate != null)
            {
                PlaceModule(spawnGate, root, "SpawnGate", new Vector3(0f, 0f, pathSpan + gateOffsetCells * cs), cs, false);
                if (hideSpawnLineWithGate) spawnLine.gameObject.SetActive(false);
            }

            // 벽 · 배관 — 좌우, 무대 앞뒤 범위를 이어 붙인다
            float wallX = laneSpan * 0.5f + wallOffsetCells * cs;
            if (wallPanel != null)
                PlaceRow(wallPanel, CreateGroup("Walls", root), "WallPanel", wallX, 0f, wallPanelCells * cs, stageZMin, stageZMax, cs);
            if (propPipe != null)
                PlaceRow(propPipe, CreateGroup("Pipes", root), "Pipe", wallX, pipeHeightCells * cs, pipeCells * cs, stageZMin, stageZMax, cs);

            // 화물 상자 — 꾸밈, crateSlots 자리
            if (propCrate != null && crateSlots != null)
            {
                Transform crates = CreateGroup("Crates", root);
                float crateX = laneSpan * 0.5f + crateOffsetCells * cs;
                for (int i = 0; i < crateSlots.Length; i++)
                {
                    Vector3 slot = crateSlots[i];
                    float side = slot.x < 0f ? -1f : 1f;
                    PlaceModule(propCrate, crates, "Crate_" + i,
                        new Vector3(side * crateX, slot.y * crateHeightCells * cs, slot.z * cs), cs, false);
                }
            }

            BuildOuter(root, cs);
        }

        /// <summary>복도 벽 바깥 — 데크 판 · 컨테이너 더미 (A11). 판정과 무관한 배경, Collider 없음.</summary>
        private void BuildOuter(Transform root, float cs)
        {
            bool hasDeck = outerDeck != null && outerDeckX != null && outerDeckZ != null;
            bool hasStacks = outerStacks != null && (outerStackHigh != null || outerStackLow != null);
            if (!hasDeck && !hasStacks) return;

            Transform outer = CreateGroup("Outer", root);
            float y = outerYCells * cs;
            float flip = outerFlipX ? -1f : 1f;

            if (hasDeck)
            {
                Transform decks = CreateGroup("Deck", outer);
                for (int iz = 0; iz < outerDeckZ.Length; iz++)
                for (int ix = 0; ix < outerDeckX.Length; ix++)
                {
                    float x = outerDeckX[ix] * cs;
                    float z = outerDeckZ[iz] * cs;
                    PlaceModule(outerDeck, decks, "Deck_L_" + ix + "_" + iz, new Vector3(-x, y, z), cs, false);
                    PlaceModule(outerDeck, decks, "Deck_R_" + ix + "_" + iz, new Vector3(x, y, z), cs, false);
                }
            }

            if (hasStacks)
            {
                Transform stacks = CreateGroup("Stacks", outer);
                for (int i = 0; i < outerStacks.Length; i++)
                {
                    OuterStackSlot slot = outerStacks[i];
                    GameObject prefab = slot.high ? outerStackHigh : outerStackLow;
                    if (prefab == null) continue;
                    GameObject go = PlaceModule(prefab, stacks, "Stack_" + i.ToString("00") + (slot.high ? "_High" : "_Low"),
                        new Vector3(flip * slot.x * cs, y, slot.z * cs), cs, false);
                    go.transform.localRotation = Quaternion.Euler(0f, flip * slot.yaw, 0f) * go.transform.localRotation;
                    Material b = slot.high ? outerStackHighMaterialB : outerStackLowMaterialB;
                    if (slot.colorB && b != null)
                    {
                        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
                        for (int r = 0; r < renderers.Length; r++) renderers[r].sharedMaterial = b;
                    }
                }
            }
        }

        /// <summary>좌우 ±x 에 z 범위를 length 간격으로 이어 붙인다 (벽 · 배관).</summary>
        private void PlaceRow(GameObject prefab, Transform parent, string baseName, float x, float y, float length,
            float zMin, float zMax, float scale)
        {
            int n = TileCount(zMax - zMin, length);
            for (int i = 0; i < n; i++)
            {
                float z = zMin + length * (i + 0.5f);
                PlaceModule(prefab, parent, baseName + "_L_" + i, new Vector3(-x, y, z), scale, mirrorLeftSide);
                PlaceModule(prefab, parent, baseName + "_R_" + i, new Vector3(x, y, z), scale, !mirrorLeftSide);
            }
        }

        /// <summary>span 을 덮는 tile 개수 (부동소수 오차로 한 장 더 생기지 않게).</summary>
        private static int TileCount(float span, float tile)
        {
            if (tile <= 0f) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(span / tile - 1e-3f));
        }

        /// <summary>모듈 프리팹 하나를 놓는다. mirror = 좌우 뒤집기 (scale.x = −1, mirrorByRotation 이면 Y 180°).</summary>
        private GameObject PlaceModule(GameObject prefab, Transform parent, string objectName, Vector3 localPosition,
            float scale, bool mirror)
        {
            GameObject go = Instantiate(prefab, parent);
            go.name = objectName;
            Transform t = go.transform;
            Vector3 s = prefab.transform.localScale * scale;
            Quaternion r = prefab.transform.localRotation;
            if (mirror)
            {
                if (mirrorByRotation) r = Quaternion.Euler(0f, 180f, 0f) * r;
                else s.x = -s.x;
            }
            t.localPosition = localPosition;
            t.localRotation = r;
            t.localScale = s;
            return go;
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
