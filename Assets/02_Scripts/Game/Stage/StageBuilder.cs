using System;
using System.Collections.Generic;
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
    /// G12: U10 카메라에서 비던 바닥 — 데크 격자 한 줄씩 더 (직렬화 값) + 격자 밖 빈 곳 판(outerDeckFill).
    /// G09: 레인 경계마다 가는 발광 선(LaneLines) — 레인 바닥은 어둡게(Mat_Lane), 구분은 선으로 (시안 ingame_reference).
    /// G15: 에디터 메뉴로 「구운」 맵(씬 오브젝트)이 있으면 생성하지 않고 그것을 쓴다. 굽기는 Build 와 같은 코드 —
    /// 결과를 <see cref="LockedGroupName"/> (판정과 묶인 위치) · <see cref="EditableGroupName"/> (장식) 두 묶음으로 나눈다.
    /// 시작 때 두 묶음의 자식을 루트 바로 아래로 꺼내 (월드 위치 유지) 생성 때와 같은 계층으로 맞춘다 — 이름으로 찾는 쪽(HatchAlertFx · GateSignFx · 테스트)이 그대로 돈다.
    /// </summary>
    public sealed class StageBuilder : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>구운 맵 — 판정(GridSpace)과 묶인 묶음. 옮기면 드래그 · 점액 경로와 겉모습이 어긋난다.</summary>
        public const string LockedGroupName = "_Locked (수정 금지)";
        /// <summary>구운 맵 — 사람이 Scene 뷰에서 자유롭게 옮기는 장식 묶음.</summary>
        public const string EditableGroupName = "Editable";

        /// <summary>Build 가 루트 바로 아래 만드는 것 중 수정 금지 묶음으로 가는 이름 (나머지는 Editable).</summary>
        private static readonly string[] LockedParts =
            { "Floor", "Lanes", "LaneLines", "Cells", "Hatch", "Posts", "Laser", "SpawnLine", "SpawnGate" };

        [SerializeField] private GameBootstrap bootstrap;

        [Header("프리팹 · 재질")]
        [SerializeField] private MeshRenderer cellPrefab;
        [SerializeField] private Material floorMaterial;
        [SerializeField] private Material laneMaterial;
        [SerializeField] private Material hatchMaterial;
        [SerializeField] private Material spawnLineMaterial;
        [Tooltip("격리선 레이저 — URP Unlit (블룸은 U07)")]
        [SerializeField] private Material laserMaterial;
        [Tooltip("레인 구분 발광 선 — _EMISSION 켠 재질 (Mat_LaneLine). 비우면 선 없음")]
        [SerializeField] private Material laneLineMaterial;

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
        [Tooltip("격자 밖에 더 까는 데크 판 (G12) — x = 중심 |x| (좌우 양쪽에 깐다, 0 보다 크게), y = 중심 z. 화면에 바닥이 비는 곳(해치 앞 가운데 · 21:9 위 모서리)만")]
        [SerializeField] private Vector2[] outerDeckFill;
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

        [Header("레인 구분 발광 선 (G09 — 점액 길 z 0 ~ pathLength)")]
        [Tooltip("선 굵기 (칸 단위)")]
        [SerializeField] private float laneLineWidthCells = 0.03f;
        [Tooltip("발광 색 (세기를 곱해 _EmissionColor 로)")]
        [SerializeField] private Color laneLineColor = new Color(0.1f, 0.85f, 0.9f, 1f);
        [Tooltip("발광 세기 — 블룸 문턱(U07 Volume 0.9)을 살짝 넘게. 점액 발광보다 어둡게 (G09 p1 측정: 2 = 점액만큼 밝음, 1.2 채택)")]
        [SerializeField] private float laneLineIntensity = 1.2f;
        [Tooltip("레인 폭 양끝(바깥 테두리) 선도 긋는다 — 끄면 레인 사이 선만")]
        [SerializeField] private bool laneLineEdges = true;
        [Tooltip("시작 때 선들을 정적 배치로 묶는다 (런타임 생성이라 Static 플래그 대신)")]
        [SerializeField] private bool laneLineStaticBatch = true;

        [Header("겹침 방지 y 오프셋 (월드)")]
        [SerializeField] private float laneYOffset = 0.005f;
        [Tooltip("레인 선 — 레인 띠 위 · 칸 · 사거리 띠 아래")]
        [SerializeField] private float laneLineYOffset = 0.008f;
        [SerializeField] private float cellYOffset = 0.01f;
        [SerializeField] private float spawnLineYOffset = 0.01f;

        [Header("기즈모")]
        [SerializeField] private float gizmoRadius = 0.08f;

        [Header("구운 맵 (G15 — 메뉴 Quarantine Line/전투맵 굽기 · 직접 넣지 않는다)")]
        [Tooltip("구운 수정 금지 묶음. 둘 다 차 있으면 시작 때 생성하지 않고 씬 것을 쓴다")]
        [SerializeField] private Transform bakedLocked;
        [Tooltip("구운 자유 편집 묶음")]
        [SerializeField] private Transform bakedEditable;

        /// <summary>null = Instantiate. 굽기 때만 에디터가 넘긴 함수 (PrefabUtility.InstantiatePrefab).</summary>
        private Func<GameObject, Transform, GameObject> _spawn;
        private bool _baking;

        /// <summary>칸 인덱스 = 배열 인덱스. G02 하이라이트 · U03 가 MaterialPropertyBlock 으로 쓴다.</summary>
        public Renderer[] CellRenderers { get; private set; }

        public bool IsBuilt => CellRenderers != null;

        /// <summary>구운 맵이 있다 (두 묶음 다).</summary>
        public bool IsBaked => bakedLocked != null && bakedEditable != null;
        public Transform BakedLocked => bakedLocked;
        public Transform BakedEditable => bakedEditable;

        private void Start()
        {
            if (bootstrap == null || !bootstrap.IsReady)
            {
                Debug.LogError("[StageBuilder] GameBootstrap 이 없거나 준비되지 않았다", this);
                enabled = false;
                return;
            }

            if (!HasRequiredAssets())
            {
                Debug.LogError("[StageBuilder] 칸 프리팹 또는 재질이 비어 있다", this);
                enabled = false;
                return;
            }

            // 남의 묶음을 가리키는 참조 (직렬화 값을 복사한 StageBuilder 등) 는 없는 것으로 — 남의 것을 꺼내거나 지우지 않는다
            if (bakedLocked != null && bakedLocked.parent != transform) bakedLocked = null;
            if (bakedEditable != null && bakedEditable.parent != transform) bakedEditable = null;

            if (bakedLocked != null || bakedEditable != null)
            {
                if (UseBaked(bootstrap.Space)) return;
                DiscardBaked(); // 반쪽 · 어긋난 구운 맵 — 이번 실행만 생성으로 대신 (씬 파일은 그대로)
            }

            Build(bootstrap.Space);
        }

        private bool HasRequiredAssets()
        {
            return cellPrefab != null && floorMaterial != null && laneMaterial != null &&
                   hatchMaterial != null && spawnLineMaterial != null && laserMaterial != null;
        }

        // ── 구운 맵 (G15) ──────────────────────────────────────

        /// <summary>
        /// 구운 맵을 쓴다: 칸 렌더러를 이름(Cell_00…)으로 모으고, 두 묶음의 자식을 루트 바로 아래로 꺼낸다 (월드 위치 유지 —
        /// Editable 안에서 옮긴 것은 옮긴 자리 그대로). 레인 선의 발광 블록 · 정적 배치는 저장되지 않으니 여기서 다시 한다.
        /// 칸 수가 데이터와 다르면 false (다시 굽기 필요).
        /// </summary>
        private bool UseBaked(GridSpace space)
        {
            if (!IsBaked)
            {
                Debug.LogError("[StageBuilder] 구운 맵 묶음이 하나만 있다 ('" + LockedGroupName + "' · '" + EditableGroupName +
                               "') — 메뉴 「전투맵 굽기」 로 다시 굽는다. 이번 실행은 생성으로 대신한다", this);
                return false;
            }

            Transform cells = bakedLocked.Find("Cells");
            int count = space.CellCount;
            var renderers = new Renderer[count];
            bool ok = cells != null && cells.childCount == count;
            for (int i = 0; ok && i < count; i++)
            {
                Transform c = cells.Find("Cell_" + i.ToString("00"));
                renderers[i] = c != null ? c.GetComponent<Renderer>() : null;
                ok = renderers[i] != null;
            }
            if (!ok)
            {
                Debug.LogError("[StageBuilder] 구운 맵의 칸이 데이터(칸 " + count + ")와 맞지 않는다 — 메뉴 「전투맵 굽기」 로 다시 굽는다. " +
                               "이번 실행은 생성으로 대신한다", this);
                return false;
            }

            if (bakedLocked.localPosition != Vector3.zero || bakedLocked.localRotation != Quaternion.identity ||
                bakedLocked.localScale != Vector3.one)
                Debug.LogWarning("[StageBuilder] '" + LockedGroupName + "' 묶음이 옮겨졌다 — 칸 · 레인 겉모습이 판정(GridSpace)과 어긋난다", this);

            MoveChildrenToRoot(bakedLocked);
            MoveChildrenToRoot(bakedEditable);

            Transform lines = transform.Find("LaneLines");
            if (lines != null) FinishLaneLines(lines);

            CellRenderers = renderers;
            return true;
        }

        private void MoveChildrenToRoot(Transform group)
        {
            while (group.childCount > 0) group.GetChild(0).SetParent(transform, true);
        }

        /// <summary>실행 중 쓰지 못한 구운 묶음을 치운다 (Play 중만 — 씬 파일은 바뀌지 않는다).</summary>
        private void DiscardBaked()
        {
            if (bakedLocked != null) Destroy(bakedLocked.gameObject);
            if (bakedEditable != null) Destroy(bakedEditable.gameObject);
            bakedLocked = null;
            bakedEditable = null;
        }

        /// <summary>
        /// 에디터 굽기 (메뉴 「전투맵 굽기」). 시작 때 생성과 같은 Build 로 만든 뒤 루트 아래 두 묶음으로 나눈다.
        /// spawn = 프리팹 인스턴스 만들기 (에디터 = PrefabUtility.InstantiatePrefab, null 이면 Instantiate).
        /// 이미 구운 것이 있으면 false — 먼저 <see cref="UnbakeForEditor"/>. Collider 는 즉시 지우고, 레인 선 발광 · 정적 배치는 실행 때 한다.
        /// </summary>
        public bool BakeForEditor(Func<GameObject, Transform, GameObject> spawn, out string error)
        {
            error = null;
            if (bakedLocked != null || bakedEditable != null) { error = "이미 구운 맵이 있다"; return false; }
            if (bootstrap == null) { error = "bootstrap 이 비어 있다"; return false; }
            if (!HasRequiredAssets()) { error = "칸 프리팹 또는 재질이 비어 있다"; return false; }
            GridSpace space = bootstrap.Space;
            if (space == null)
            {
                if (bootstrap.Data == null || bootstrap.Data.Config == null) { error = "GameDataSet · Config 가 비어 있다"; return false; }
                space = new GridSpace(bootstrap.Data.Config.ToRuleConfig()); // 편집 모드 — 기즈모와 같은 경로
            }

            // 루트에 이미 있는 자식 (생성 결과 · 사람이 둔 것) 은 건드리지 않는다
            var before = new HashSet<Transform>();
            foreach (Transform c in transform) before.Add(c);

            Renderer[] keepCells = CellRenderers;
            _spawn = spawn;
            _baking = true;
            try
            {
                Build(space);
            }
            finally
            {
                _spawn = null;
                _baking = false;
                CellRenderers = keepCells; // IsBuilt = 실행 중 준비됨 — 굽기는 바꾸지 않는다
            }

            var built = new List<Transform>();
            foreach (Transform c in transform)
                if (!before.Contains(c)) built.Add(c);

            bakedLocked = CreateGroup(LockedGroupName, transform);
            bakedEditable = CreateGroup(EditableGroupName, transform);
            for (int i = 0; i < built.Count; i++)
                built[i].SetParent(Array.IndexOf(LockedParts, built[i].name) >= 0 ? bakedLocked : bakedEditable, false);
            return true;
        }

        /// <summary>
        /// 굽기 되돌리기 — 두 묶음을 지우고 참조를 비운다 (다음 시작부터 생성). destroy = 지우는 함수
        /// (에디터 = Undo.DestroyObjectImmediate, null 이면 편집 모드 DestroyImmediate · 실행 중 Destroy).
        /// </summary>
        public void UnbakeForEditor(Action<GameObject> destroy)
        {
            if (destroy == null) destroy = go => { if (Application.isPlaying) Destroy(go); else DestroyImmediate(go); };
            if (bakedLocked != null) destroy(bakedLocked.gameObject);
            if (bakedEditable != null) destroy(bakedEditable.gameObject);
            bakedLocked = null;
            bakedEditable = null;
        }

        private GameObject Spawn(GameObject prefab, Transform parent)
        {
            return _spawn != null ? _spawn(prefab, parent) : Instantiate(prefab, parent);
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

            BuildLaneLines(space, root, flat);

            // 칸 — Cell.prefab (Collider 없음)
            Transform cells = CreateGroup("Cells", root);
            int count = space.CellCount;
            CellRenderers = new Renderer[count];
            float cellTile = cs * cellTileRatio;
            for (int i = 0; i < count; i++)
            {
                MeshRenderer cell = Spawn(cellPrefab.gameObject, cells).GetComponent<MeshRenderer>();
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

        /// <summary>
        /// 레인 구분 발광 선 (보류함 29) — 레인 경계 x 마다 가는 쿼드 하나, 점액 길 z 0 ~ pathLength.
        /// Collider 없음 · 그림자 없음. 색 × 세기는 MaterialPropertyBlock 의 _EmissionColor (공유 재질 그대로). 시작 때 1회.
        /// </summary>
        private void BuildLaneLines(GridSpace space, Transform root, Quaternion flat)
        {
            if (laneLineMaterial == null)
            {
                Debug.LogWarning("[StageBuilder] laneLineMaterial 이 비어 레인 구분 선을 그리지 않는다", this);
                return;
            }

            float cs = space.CellSize;
            float pathSpan = space.PathLength * cs;
            float x0 = -space.LaneCount * cs * 0.5f;
            int first = laneLineEdges ? 0 : 1;
            int last = laneLineEdges ? space.LaneCount : space.LaneCount - 1;

            Transform group = CreateGroup("LaneLines", root);
            for (int i = first; i <= last; i++)
            {
                MeshRenderer line = CreatePrimitive(PrimitiveType.Quad, "LaneLine_" + i, group, laneLineMaterial,
                    new Vector3(x0 + i * cs, laneLineYOffset, pathSpan * 0.5f), flat,
                    new Vector3(laneLineWidthCells * cs, pathSpan, 1f), true);
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
            }

            // 굽기 땐 하지 않는다 — 블록은 씬에 저장되지 않고, 정적 배치는 실행 때만 (구운 맵은 UseBaked 가 한다)
            if (!_baking) FinishLaneLines(group);
        }

        /// <summary>레인 선 발광 (MaterialPropertyBlock 의 _EmissionColor) · 정적 배치. 생성 · 구운 맵 공용, 시작 때 1회.</summary>
        private void FinishLaneLines(Transform group)
        {
            var block = new MaterialPropertyBlock();
            Color emission = laneLineColor * laneLineIntensity;
            emission.a = 1f;
            block.SetColor(EmissionColorId, emission);
            MeshRenderer[] lines = group.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < lines.Length; i++) lines[i].SetPropertyBlock(block);

            if (laneLineStaticBatch) StaticBatchingUtility.Combine(group.gameObject);
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

                // G12: 격자 밖 빈 곳 — 같은 판을 좌우 대칭으로 (outerDeckFill 표 순서)
                if (outerDeckFill != null)
                {
                    for (int i = 0; i < outerDeckFill.Length; i++)
                    {
                        float x = outerDeckFill[i].x * cs;
                        float z = outerDeckFill[i].y * cs;
                        PlaceModule(outerDeck, decks, "Deck_FL_" + i, new Vector3(-x, y, z), cs, false);
                        PlaceModule(outerDeck, decks, "Deck_FR_" + i, new Vector3(x, y, z), cs, false);
                    }
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
            GameObject go = Spawn(prefab, parent);
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

        /// <param name="removeColliderNow">true 면 Collider 를 즉시 지운다 (같은 프레임에 Collider 0 을 보장 — 레인 선). 굽기 땐 항상 즉시</param>
        private MeshRenderer CreatePrimitive(PrimitiveType type, string name, Transform parent, Material material,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, bool removeColliderNow = false)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            // 칸 판정은 GridSpace — Collider 안 씀 (시작 시 1회). 편집 모드 굽기는 Destroy 를 못 쓴다
            if (removeColliderNow || _baking || !Application.isPlaying) DestroyImmediate(go.GetComponent<Collider>());
            else Destroy(go.GetComponent<Collider>());
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
