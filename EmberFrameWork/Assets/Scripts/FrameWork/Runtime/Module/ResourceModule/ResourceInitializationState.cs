namespace EmberFrameWork
{
    public enum ResourceInitializationState
    {
        NotStarted,
        InitializingPackage,
        RequestingVersion,
        LoadingManifest,
        Ready,
        Failed,
        Shutdown
    }
}
