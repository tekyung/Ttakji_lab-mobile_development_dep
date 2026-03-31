using System;
using System.Threading.Tasks;
using TCG_Project.Scripts.Managers;

namespace TCG_Project.Scripts.Utils
{
    /// <summary>
    /// 유니티 환경(Coroutine)에 종속되지 않고, 순수 C# Task를 이용해
    /// 비동기적으로 유저의 입력을 대기하고 타임아웃을 처리하는 범용 헬퍼 클래스입니다.
    /// </summary>
    public static class AsyncTimeoutHelper
    {
        public static async Task<T> WaitForChoiceWithTimeout<T>(
            Action<Action<T>> requestAction, 
            Func<T> defaultFallback, 
            int timeoutMillis)
        {
            var tcs = new TaskCompletionSource<T>();
            
            // 1. 유저 입력 요청 (UI 또는 임시 응답기가 콜백을 호출하면 tcs에 결과가 세팅됨)
            requestAction.Invoke(result => tcs.TrySetResult(result));

            // 2. 타임아웃 Task 생성 (지정된 시간만큼 대기)
            var timeoutTask = Task.Delay(timeoutMillis);

            // 3. 둘 중 먼저 끝나는 것을 대기 (경합 조건 방지)
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            // 4. 결과 처리
            if (completedTask == timeoutTask)
            {
                // 시간 초과 시: 기본 로직(defaultFallback) 수행
                EventManager.OnLogMessage?.Invoke($"<color=red>⏳ 제한 시간({timeoutMillis / 1000f}초) 초과! 시스템이 강제로 기본 로직을 수행합니다.</color>");
                return defaultFallback.Invoke();
            }
            else
            {
                // 유저가 제때 입력했을 때: 입력받은 결과 반환
                return await tcs.Task;
            }
        }
    }
}