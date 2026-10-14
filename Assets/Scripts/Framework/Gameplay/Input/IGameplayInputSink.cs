namespace Spotlight
{
    public interface IGameplayInputSink
    {
        bool CanHandleInput { get; }
        bool TryHandle(GameplayInputCommand command);
    }
}
