/// <summary>
/// 비동기 UniTask 및 CancellationTokenSource의 라이프사이클을 안전하게 보조하는 정적 유틸리티 클래스
/// </summary>
public static class AsyncUtil
{
    /// <summary>
    /// 전달받은 CancellationTokenSource가 유효할 경우 작업을 취소하고 메모리를 해제한 뒤 null로 초기화
    /// </summary>
    /// <param name="cts">정리할 CancellationTokenSource 참조</param>
    public static void CancelAndDispose(ref System.Threading.CancellationTokenSource cts)
    {
        if(cts !=null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
}
