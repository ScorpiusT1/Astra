namespace Astra.UI.Controls
{
    /// <summary>
    /// WorkflowReferenceNodeControl 宿主接口，避免通过反射调用 ViewModel 命令。
    /// </summary>
    public interface IWorkflowReferenceNodeHost
    {
        void OpenSubWorkflowEditor(string subWorkflowId);

        void ToggleRunSubWorkflowFromNode(string subWorkflowId);

        void TogglePauseResumeSubWorkflowFromNode(string subWorkflowId);
    }
}
