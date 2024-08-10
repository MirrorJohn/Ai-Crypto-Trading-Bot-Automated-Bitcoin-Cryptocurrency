namespace TraderTools.Basics
{
    public interface IDataDirectoryService
    {
        void SetApplicationName(string applicationName);
        string MainDirectory { get; }
        string MainDirectoryWithApplicationName { get; }
    }
}