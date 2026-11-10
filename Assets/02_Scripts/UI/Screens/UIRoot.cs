using QL.Game.Flow;
using UnityEngine;

namespace QL.UI.Screens
{
    /// <summary>
    /// UIRoot 프리팹 맨 위. 화면들이 읽고 부를 GameLoop 하나를 들고 있다 (프리팹은 씬 오브젝트를 직렬화할 수 없어 씬 인스턴스에서 지정,
    /// 비어 있으면 처음 한 번 찾는다). 상태를 바꾸는 호출은 GameLoop 공개 메서드(HandleEscape · Restart)뿐 (S3 규칙 1).
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField] private GameLoop loop;

        public GameLoop Loop
        {
            get
            {
                if (loop == null) loop = FindAnyObjectByType<GameLoop>();
                return loop;
            }
        }
    }
}
