using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;


public enum SceneState { Idle, Loading, Loaded, Activating, Active, Cancelled, Failed }

public class SceneStateMachine
{
    public SceneState State { get; private set; } = SceneState.Idle;
    public event Action<SceneState> OnStateChanged;

    public void SetState(SceneState next)
    {
        if (State == next) return;
        State = next;
        OnStateChanged?.Invoke(next);
    }
}


public class SceneLoader
{
    private readonly SceneStateMachine _stateMachine;
    private readonly UILoading _loadingUI;

    public SceneLoader(SceneStateMachine stateMachine, UILoading loadingUI)
    {
        _stateMachine = stateMachine;
        _loadingUI = loadingUI;
    }

    // Public entry. timeoutMs = 0 => 무제한
    public async Task<bool> LoadSceneAsync(string sceneName, int timeoutMs, CancellationToken externalToken)
    {
        using (var ctsTimeout = timeoutMs > 0 ? new CancellationTokenSource(timeoutMs) : null)
        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken, ctsTimeout?.Token ?? CancellationToken.None))
        {
            var ct = linkedCts.Token;
            _stateMachine.SetState(SceneState.Loading);
            _loadingUI?.Show();
            _loadingUI?.SetProgress(0f);

            // Hook cancel button if UI exists
            void OnUICancel() => linkedCts.Cancel();
            if (_loadingUI != null) _loadingUI.OnCancelClicked += OnUICancel;

            try
            {
                var asyncOp = SceneManager.LoadSceneAsync(sceneName);
                if (asyncOp == null)
                    throw new Exception($"Failed to start loading scene '{sceneName}'");

                asyncOp.allowSceneActivation = false; // 활성화 타이밍 제어
                _stateMachine.SetState(SceneState.Loading);

                // Progress loop: Unity reports 0..0.9 while loading, then 0.9->1.0 when allowSceneActivation=true and activation completes
                while (!asyncOp.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _stateMachine.SetState(SceneState.Cancelled);
                        await HandleCancelCleanup(sceneName);
                        return false;
                    }

                    float progress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                    _loadingUI?.SetProgress(progress);
                    await Task.Yield(); // allow frame to progress
                }

            }
            catch (OperationCanceledException)
            {
                _stateMachine.SetState(SceneState.Cancelled);
                await HandleCancelCleanup(sceneName);
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Scene load failed: {ex}");
                _stateMachine.SetState(SceneState.Failed);
                _loadingUI?.SetMessage("Load failed");
                return false;
            }
            finally
            {
                if (_loadingUI != null) _loadingUI.OnCancelClicked -= OnUICancel;
            }

            // Second phase: ensure progress reached 0.9 then activate
            var op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;
            try
            {
                // Wait until internal loading reached 0.9
                while (op.progress < 0.9f)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _stateMachine.SetState(SceneState.Cancelled);
                        await HandleCancelCleanup(sceneName);
                        return false;
                    }
                    _loadingUI?.SetProgress(op.progress / 0.9f);
                    await Task.Yield();
                }

                _stateMachine.SetState(SceneState.Activating);
                _loadingUI?.SetProgress(1f);

                if (ct.IsCancellationRequested)
                {
                    _stateMachine.SetState(SceneState.Cancelled);
                    await HandleCancelCleanup(sceneName);
                    return false;
                }

                // Activate scene
                op.allowSceneActivation = true;

                // Wait until activation completes
                while (!op.isDone)
                {
                    if (ct.IsCancellationRequested)
                    {
                        _stateMachine.SetState(SceneState.Cancelled);
                        await HandleCancelCleanup(sceneName);
                        return false;
                    }
                    await Task.Yield();
                }

                _stateMachine.SetState(SceneState.Active);
                _loadingUI?.Hide();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Activation failed: {ex}");
                _stateMachine.SetState(SceneState.Failed);
                _loadingUI?.SetMessage("Activation failed");
                _loadingUI?.Hide();
                return false;
            }
        }
    }

    // 취소 시 정리: 예시는 간단히 GC/언로드 시나리오
    private async Task HandleCancelCleanup(string sceneName)
    {
        // 예: 로드 도중 부분 자원이 올라왔다면 언로드 시도
        // SceneManager로는 부분적 언로드가 복잡할 수 있음. 여기서는 커스텀 정리 훅 호출 구조 권장.
        _loadingUI?.SetMessage("Cancelling...");
        // 필요한 동기화/정리 수행 (예: Addressables.Release, 중간에 생성된 싱글톤 리셋 등)
        await Task.Delay(100); // 짧은 대기(프레임 허용)
        _loadingUI?.Hide();
    }
}
