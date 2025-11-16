namespace NavStack.Core
{
    /// <summary>
    /// 导航结果
    /// </summary>
    public class NavigationResult
    {
        public bool Success { get; set; }
        public Exception Exception { get; set; }
        public string Message { get; set; }

        public static NavigationResult Succeeded() => new NavigationResult { Success = true };
        public static NavigationResult Failed(string message, Exception ex = null) =>
            new NavigationResult { Success = false, Message = message, Exception = ex };
    }
}
