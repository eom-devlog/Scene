using UnityEngine;
using System.Threading;
using System.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    [SerializeField] UILoading loadingUI;
    private SceneStateMachine stateMachine;
    private SceneLoader loader;
    private CancellationTokenSource playCts;

    private void Awake()
    {
        stateMachine = new SceneStateMachine();
        loader = new SceneLoader(stateMachine, loadingUI);
        stateMachine.OnStateChanged += s => Debug.Log("SceneState: " + s);
        loadingUI.OnCancelClicked += () => Debug.Log("UI cancel clicked");
    }

    public async void StartLoadButtonPressed(string sceneName)
    {
        // 예: 호출마다 새로운 CTS를 만들어 외부에서 취소하거나 타임아웃 지정
        playCts?.Cancel();
        playCts?.Dispose();
        playCts = new CancellationTokenSource();

        // 10초 타임아웃 예시
        bool ok = await loader.LoadSceneAsync(sceneName, timeoutMs: 10000, externalToken: playCts.Token);

        if (ok) Debug.Log("Scene loaded successfully");
        else Debug.Log("Scene load cancelled or failed");
    }

    private void OnDestroy()
    {
        playCts?.Cancel();
        playCts?.Dispose();
    }


    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartLoadButtonPressed("Battle");
        }
        if(Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartLoadButtonPressed("Lobby");
        }
    }
}
