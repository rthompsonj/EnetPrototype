namespace SoL.Networking.Managers
{
    public interface INetworkManager
    {
        void AddCommandToQueue(GameCommand command);
    }
}