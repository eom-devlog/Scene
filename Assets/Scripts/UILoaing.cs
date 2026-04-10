using UnityEngine;
using UnityEngine.UI;
using System;

public class UILoading : MonoBehaviour
{
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text text;
    [SerializeField] private GameObject root;
    [SerializeField] private Button cancelButton;

    public event Action OnCancelClicked;

    private void Awake()
    {
        DontDestroyOnLoad(this);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(() => OnCancelClicked?.Invoke());
    }

    public void Show() => root.SetActive(true);
    public void Hide() => root.SetActive(false);

    public void SetProgress(float progress)
    {
        text.text = $"{progress * 100}%";

        if (progressBar != null) progressBar.value = Mathf.Clamp01(progress);
    }

    public void SetMessage(string msg)
    {
        
    }
}
